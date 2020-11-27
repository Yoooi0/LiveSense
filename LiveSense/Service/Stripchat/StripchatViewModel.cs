using LiveSense.Common;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.Service.Stripchat
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StripchatViewModel : Screen, IService
    {
        private readonly ITipQueue _queue;

        private Task _chatTask;
        private JToken _channelData;
        private CancellationTokenSource _cancellationSource;
        private ClientWebSocket _socket;

        [JsonProperty] public string ServiceName => "Stripchat";
        [JsonProperty] public string RoomName { get; set; }
        [JsonProperty] public float RoomDelay { get; set; }

        public StripchatViewModel(ITipQueue queue)
        {
            _queue = queue;
        }

        public bool IsConnected { get; set; }
        public bool IsBusy { get; set; }
        public bool CanToggleConnect => !IsBusy && !string.IsNullOrWhiteSpace(RoomName);
        public async Task ToggleConnect()
        {
            IsBusy = true;

            if (IsConnected)
            {
                await Disconnect();
                IsConnected = false;
            }
            else
            {
                IsConnected = await Connect();
            }

            IsBusy = false;
        }

        public async Task<bool> Connect()
        {
            await Task.Delay(1000);

            try
            {
                var uri = new Uri($"https://stripchat.com/{RoomName}/");
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
                request.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

                using var response = (HttpWebResponse)await request.GetResponseAsync();
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);

                var content = await reader.ReadToEndAsync();
                var state = Regex.Match(content, @"<script>[\n\r\s]*?window\.__PRELOADED_STATE__\s?=\s?({.+?})</script>").Groups[1].Value;
                var document = JObject.Parse(state);

                var roomOnline = document["viewCam"]["isCamAvailable"].ToObject<bool>();
                if (!roomOnline)
                {
                    _ = DialogHost.Show(new ErrorMessageDialog("Room is offline!"));
                    return false;
                }

                var host = document["config"]["data"]["websocketUrl"].ToString();
                _cancellationSource = new CancellationTokenSource();
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(new Uri(host), _cancellationSource.Token);

                _channelData = document["viewCam"];
                _chatTask = Task.Factory.StartNew(ReadMessagesAsync, _cancellationSource.Token, _cancellationSource.Token);

                return true;
            }
            catch (Exception e)
            {
                var message = $"Error when connecting to room:\n\n{e}";
                if (e is WebException we)
                {
                    if (((HttpWebResponse)we.Response).StatusCode == HttpStatusCode.NotFound)
                        message = "Room does not exist!";
                }

                _ = DialogHost.Show(new ErrorMessageDialog(message));
                await Disconnect();
                return false;
            }
        }

        public async Task Disconnect()
        {
            _cancellationSource?.Cancel();

            await Task.Delay(1000);

            if (_chatTask != null)
                await _chatTask;

            if (_socket?.State == WebSocketState.Open)
                await _socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            _socket?.Dispose();

            _cancellationSource?.Dispose();
            _cancellationSource = null;
            _chatTask = null;
            _socket = null;
            _channelData = null;
        }

        private async Task ReadMessagesAsync(object state)
        {
            static async Task<string> ReadStringAsync(ClientWebSocket socket, CancellationToken token)
            {
                WebSocketReceiveResult result;
                var buffer = new ArraySegment<byte>(new byte[1024]);
                using var stream = new MemoryStream();
                do
                {
                    result = await socket.ReceiveAsync(buffer, token);
                    stream.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                stream.Seek(0, SeekOrigin.Begin);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }

            static async Task WriteStringAsync(ClientWebSocket socket, string data, CancellationToken token)
                => await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, token);

            var token = (CancellationToken)state;

            try
            {
                while (_socket.State == WebSocketState.Open && !_cancellationSource.IsCancellationRequested)
                {
                    var message = await ReadStringAsync(_socket, token);
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    Debug.WriteLine(message);
                    var document = JObject.Parse(message);
                    if (document.ContainsKey("subscriptionKey"))
                    {
                        var subscriptionKey = document["subscriptionKey"].ToString();
                        if (subscriptionKey == "connected")
                        {
                            var epoch = (long)Math.Round(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
                            await WriteStringAsync(_socket, JsonConvert.SerializeObject(new {
                                id = $"{epoch}-sub-newChatMessage:{_channelData["streamName"]}",
                                method = "PUT",
                                url = $"/front/clients/{document["params"]["clientId"]}/subscriptions/newChatMessage:{_channelData["streamName"]}"
                            }), token);
                        }
                        else if(subscriptionKey == $"newChatMessage:{_channelData["streamName"]}")
                        {
                            var messageDocument = document["params"]["message"];
                            var messageType = messageDocument["type"].ToString();
                            if (messageType == "tip")
                            {
                                var username = messageDocument["userData"]["username"].ToString();
                                var amount = messageDocument["details"]["amount"].ToObject<int>();

                                _ = Task.Delay((int)(RoomDelay * 1000), token)
                                        .ContinueWith(_ => _queue.Enqueue(new ServiceTip(username, amount)));
                            }
                            else if(messageType == "lovense")
                            {
                                var lovense = messageDocument["details"]["lovenseDetails"];
                                if (lovense["type"].ToString() != "tip")
                                    continue;

                                var username = lovense["detail"]["name"].ToString();
                                var amount = lovense["detail"]["amount"].ToObject<int>();

                                _ = Task.Delay((int)(RoomDelay * 1000), token)
                                        .ContinueWith(_ => _queue.Enqueue(new ServiceTip(username, amount)));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override async void OnDeactivate()
        {
            await Disconnect();
            IsConnected = false;
            IsBusy = false;

            base.OnDeactivate();
        }
    }
}
