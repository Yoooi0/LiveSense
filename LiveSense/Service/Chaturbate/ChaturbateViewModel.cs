using LiveSense.Common;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylet;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.Service.Chaturbate
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ChaturbateViewModel : Screen, IService
    {
        private readonly ITipQueue _queue;

        private ChaturbateRoomAuth _auth;
        private Task _chatTask;
        private CancellationTokenSource _cancellationSource;
        private ClientWebSocket _socket;

        [JsonProperty] public string ServiceName => "Chaturbate";
        [JsonProperty] public string RoomName { get; set; }
        [JsonProperty] public float RoomDelay { get; set; }

        public ChaturbateViewModel(ITipQueue queue)
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
                var uri = new Uri($"https://chaturbate.com/{RoomName.ToLower()}/");
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");

                using var response = (HttpWebResponse)await request.GetResponseAsync();
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);

                var content = await reader.ReadToEndAsync();
                var dossier = Regex.Unescape(Regex.Match(content, @"window\.initialRoomDossier\s?=\s?\""(.+?)"";").Groups[1].Value);
                if (string.IsNullOrWhiteSpace(dossier))
                {
                    _ = DialogHost.Show(new ErrorMessageDialog("Room does not exist!"));
                    return false;
                }

                var document = JObject.Parse(dossier);
                var roomStatus = document["room_status"].ToString();
                if (roomStatus == "offline")
                {
                    _ = DialogHost.Show(new ErrorMessageDialog("Room is offline!"));
                    return false;
                }

                var random = new Random();
                var id0 = random.Next(0, 1000);
                var id1 = string.Join("", Enumerable.Range(0, 8).Select(_ => "abcdefghijklmnopqrstuvwxyz"[random.Next(26)]));

                _auth = new ChaturbateRoomAuth()
                {
                    Host = $"{document["wschat_host"].ToString().Replace("https://", "wss://")}/{id0}/{id1}/websocket",
                    Username = document["chat_username"].ToString(),
                    Password = document["chat_password"].ToString(),
                    RoomPassword = document["room_pass"].ToString()
                };

                _cancellationSource = new CancellationTokenSource();
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(new Uri(_auth.Host), _cancellationSource.Token);

                _chatTask = Task.Factory.StartNew(ReadMessagesAsync, _cancellationSource.Token, _cancellationSource.Token);

                return true;
            }
            catch(Exception e)
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
            _auth = null;
        }

        private async Task ReadMessagesAsync(object state)
        {
            var token = (CancellationToken)state;

            try
            {
                while (_socket.State == WebSocketState.Open && !_cancellationSource.IsCancellationRequested)
                {
                    var message = await ReadStringAsync(_socket, token);
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    if (message[0] == 'o')
                        await WriteStringAsync(_socket, CreateConnectMessage(), token);

                    if (message[0] == 'a')
                    {
                        var document = JToken.Parse(message[1..]);
                        document = JToken.Parse(document.First().ToString());

                        var method = document["method"].ToString();
                        if (method == "onAuthResponse")
                        {
                            var args = document["args"].First().Value<int>();
                            if (args != 1)
                            {
                                _ = Execute.OnUIThreadAsync(() => DialogHost.Show(new ErrorMessageDialog("Failed to authenticate!")));
                                _ = ToggleConnect();
                                break;
                            }

                            await WriteStringAsync(_socket, CreateAuthResponseMessage(), token);
                        }
                        else if (method == "onNotify")
                        {
                            var args = JObject.Parse(document["args"].First().ToString());
                            if (args["type"].ToString() == "tip_alert")
                            {
                                var username = args["from_username"].ToString();
                                var amount = args["amount"].Value<int>();

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

        private string CreateConnectMessage()
            => CreateMessage("connect", new
            {
                user = _auth.Username,
                password = _auth.Password,
                room = RoomName.ToLower(),
                room_password = _auth.RoomPassword
            });

        private string CreateAuthResponseMessage()
            => CreateMessage("joinRoom", new
            {
                room = RoomName.ToLower()
            });

        private string CreateMessage(string method, object data)
        {
            var result = JsonConvert.SerializeObject(new { method, data });
            return JsonConvert.SerializeObject(new[] { result });
        }

        private async Task<string> ReadStringAsync(ClientWebSocket socket, CancellationToken token)
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

        private async Task WriteStringAsync(ClientWebSocket socket, string data, CancellationToken token)
            => await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, token);

        protected override async void OnDeactivate()
        {
            await Disconnect();
            IsConnected = false;
            IsBusy = false;

            base.OnDeactivate();
        }

        private class ChaturbateRoomAuth
        {
            public string Host { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string RoomPassword { get; set; }
        }
    }
}
