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
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.Service.Chaturbate
{
    public class ChaturbateViewModel : AbstractService
    {
        public override string Name => "Chaturbate";
        public override ServiceStatus Status { get; protected set; }

        public string RoomName { get; set; }
        public float RoomDelay { get; set; }

        public ChaturbateViewModel(IEventAggregator eventAggregator, ITipQueue queue)
            : base(eventAggregator, queue) { }

        public bool IsConnected => Status == ServiceStatus.Connected;
        public bool IsConnectBusy => Status == ServiceStatus.Connecting || Status == ServiceStatus.Disconnecting;
        public bool CanToggleConnect => !string.IsNullOrWhiteSpace(RoomName) && !IsConnectBusy;

        protected override async Task RunAsync(CancellationToken token)
        {
            using var socket = new ClientWebSocket();

            try
            {
                var uri = new Uri($"https://chaturbate.com/{RoomName.ToLower()}/");
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");

                using var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false);
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);

                var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                var dossier = Regex.Unescape(Regex.Match(content, @"window\.initialRoomDossier\s?=\s?\""(.+?)"";").Groups[1].Value);
                if (string.IsNullOrWhiteSpace(dossier))
                    throw new Exception("Room does not exist!");

                var dossierDocument = JObject.Parse(dossier);
                var roomStatus = dossierDocument["room_status"].ToString();
                if (roomStatus == "offline")
                    throw new Exception("Room is offline!");

                var random = new Random();
                var id0 = random.Next(0, 1000);
                var id1 = string.Join("", Enumerable.Range(0, 8).Select(_ => "abcdefghijklmnopqrstuvwxyz"[random.Next(26)]));

                var authHost = $"{dossierDocument["wschat_host"].ToString().Replace("https://", "wss://")}/{id0}/{id1}/websocket";
                var authUsername = dossierDocument["chat_username"].ToString();
                var authPassword = dossierDocument["chat_password"].ToString();
                var authRoomPassword = dossierDocument["room_pass"].ToString();

                await socket.ConnectAsync(new Uri(authHost), token).ConfigureAwait(false);
                Status = ServiceStatus.Connected;

                while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var message = await socket.ReceiveStringAsync(token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    if (message[0] == 'o')
                        await socket.SendStringAsync(CreateConnectMessage(authUsername, authPassword, authRoomPassword), token).ConfigureAwait(false);

                    if (message[0] == 'a')
                    {
                        var document = JToken.Parse(message[1..]);
                        document = JToken.Parse(document.First().ToString());

                        var method = document["method"].ToString();
                        if (method == "onAuthResponse")
                        {
                            var args = document["args"].First().Value<int>();
                            if (args != 1)
                                throw new Exception("Failed to authenticate!");

                            await socket.SendStringAsync(CreateAuthResponseMessage(), token).ConfigureAwait(false);
                        }
                        else if (method == "onNotify")
                        {
                            var args = JObject.Parse(document["args"].First().ToString());
                            if (args["type"].ToString() == "tip_alert")
                            {
                                var username = args["from_username"].ToString();
                                var amount = args["amount"].Value<int>();

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

        private string CreateConnectMessage(string username, string password, string roomPassword)
            => CreateMessage("connect", new
            {
                user = username,
                password = password,
                room = RoomName.ToLower(),
                room_password = roomPassword
            });

        private string CreateAuthResponseMessage()
            => CreateMessage("joinRoom", new
            {
                room = RoomName.ToLower()
            });

        private static string CreateMessage(string method, object data)
        {
            var result = JsonConvert.SerializeObject(new { method, data });
            return JsonConvert.SerializeObject(new[] { result });
        }
    }
}
