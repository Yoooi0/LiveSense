using LiveSense.Common;
using LiveSense.Common.Controls;
using LiveSense.Common.Messages;
using LiveSense.OutputTarget;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylet;
using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.Service.Stripchat
{
    public class StripchatViewModel : AbstractService
    {
        public override string Name => "Stripchat";
        public override ServiceStatus Status { get; protected set; }

        public string RoomName { get; set; }
        public float RoomDelay { get; set; }

        public StripchatViewModel(IEventAggregator eventAggregator, ITipQueue queue)
            : base(eventAggregator, queue) { }

        public bool IsConnected => Status == ServiceStatus.Connected;
        public bool IsConnectBusy => Status == ServiceStatus.Connecting || Status == ServiceStatus.Disconnecting;
        public bool CanToggleConnect => !string.IsNullOrWhiteSpace(RoomName) && !IsConnectBusy;

        protected override async Task RunAsync(CancellationToken token)
        {
            using var socket = new ClientWebSocket();

            try
            {
                var uri = new Uri($"https://stripchat.com/{RoomName}/");
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
                request.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

                using var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);

                var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                var state = Regex.Match(content, @"<script>[\n\r\s]*?window\.__PRELOADED_STATE__\s?=\s?({.+?})</script>").Groups[1].Value;
                var stateDocument = JObject.Parse(state);

                var roomOnline = stateDocument["viewCam"]["isCamAvailable"].ToObject<bool>();
                if (!roomOnline)
                    throw new Exception("Room is offline!");

                var host = stateDocument["config"]["data"]["websocketUrl"].ToString();
                var channelData = stateDocument["viewCam"];

                await socket.ConnectAsync(new Uri(host), token).ConfigureAwait(false);
                Status = ServiceStatus.Connected;

                while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var message = await socket.ReceiveStringAsync(token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    var document = JObject.Parse(message);
                    if (document.ContainsKey("subscriptionKey"))
                    {
                        var subscriptionKey = document["subscriptionKey"].ToString();
                        if (subscriptionKey == "connected")
                        {
                            var epoch = (long)Math.Round(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
                            await socket.SendStringAsync(JsonConvert.SerializeObject(new {
                                id = $"{epoch}-sub-newChatMessage:{channelData["streamName"]}",
                                method = "PUT",
                                url = $"/front/clients/{document["params"]["clientId"]}/subscriptions/newChatMessage:{channelData["streamName"]}"
                            }), token).ConfigureAwait(false);
                        }
                        else if(subscriptionKey == $"newChatMessage:{channelData["streamName"]}")
                        {
                            var messageDocument = document["params"]["message"];
                            var messageType = messageDocument["type"].ToString();
                            if (messageType == "tip")
                            {
                                var username = messageDocument["userData"]["username"].ToString();
                                var amount = messageDocument["details"]["amount"].ToObject<int>();

                                _ = Task.Delay((int)(RoomDelay * 1000), token)
                                        .ContinueWith(_ => Queue.Enqueue(new ServiceTip(Name, username, amount)), token)
                                        .ConfigureAwait(false);
                            }
                            else if(messageType == "lovense")
                            {
                                var lovense = messageDocument["details"]["lovenseDetails"];
                                if (lovense["type"].ToString() != "tip")
                                    continue;

                                var username = lovense["detail"]["name"].ToString();
                                var amount = lovense["detail"]["amount"].ToObject<int>();

                                _ = Task.Delay((int)(RoomDelay * 1000), token)
                                        .ContinueWith(_ => Queue.Enqueue(new ServiceTip(Name, username, amount)), token)
                                        .ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _ = Execute.OnUIThreadAsync(() => _ = DialogHost.Show(new ErrorMessageDialog($"Unhandled error:\n\n{e}"), "RootDialog"));
            }

            if (socket?.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
        }

        protected override void HandleSettings(JObject settings, AppSettingsMessageType type) { }
    }
}
