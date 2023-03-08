using LiveSense.Common;
using LiveSense.Common.Controls;
using LiveSense.Common.Messages;
using LiveSense.OutputTarget;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylet;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;

namespace LiveSense.Service.Chaturbate;

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

        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler() { CookieContainer = cookies };
        using var client = new HttpClient(handler);

        try
        {
            var csrf = await GetClientCsrf(client, cookies, token).ConfigureAwait(false);
            var context = await GetRoomContext(client, RoomName.ToLower(), token).ConfigureAwait(false);
            var roomId = context["room_uid"].ToString();
            var accessToken = await GetAuthToken(client, csrf, roomId, token).ConfigureAwait(false);

            var wssUri = new Uri($"wss://realtime.pa.highwebmedia.com/?access_token={accessToken}&format=json&heartbeats=true&v=1.2&agent=ably-js%2F1.2.13%20browser&remainPresentFor=0");
            await socket.ConnectAsync(wssUri, token).ConfigureAwait(false);

            await SubscribeRoomChannels(socket, roomId, token);

            Status = ServiceStatus.Connected;
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var content = await socket.ReceiveStringAsync(token).ConfigureAwait(false);

                var document = JObject.Parse(content);
                if (document["action"].ToObject<int>() != 15)
                    continue;

                var channel = document["channel"].ToString();
                if (!channel.StartsWith("room:tip_alert:"))
                    continue;

                var messages = document["messages"] as JArray;
                foreach (var message in messages.OfType<JObject>())
                {
                    var data = JObject.Parse(message["data"].ToString());
                    var username = data["from_username"].ToString();
                    var amount = data["amount"].ToObject<int>();

                    _ = Task.Delay((int)(RoomDelay * 1000), token)
                            .ContinueWith(_ => Queue.Enqueue(new ServiceTip(Name, username, amount)), token)
                            .ConfigureAwait(false);
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

        static async Task<string> GetClientCsrf(HttpClient client, CookieContainer cookies, CancellationToken token)
        {
            var uri = new Uri("https://chaturbate.com/");
            var result = await client.GetAsync(uri, token).ConfigureAwait(false);
            var content = await result.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            return cookies.GetAllCookies().First(c => string.Equals(c.Name, "csrftoken", StringComparison.OrdinalIgnoreCase)).Value;
        }

        static async Task<JObject> GetRoomContext(HttpClient client, string roomName, CancellationToken token)
        {
            var result = await client.GetAsync(new Uri($"https://chaturbate.com/api/chatvideocontext/{roomName}/"), token).ConfigureAwait(false);
            var content = await result.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            return JObject.Parse(content);
        }

        static async Task SubscribeRoomChannels(ClientWebSocket socket, string roomId, CancellationToken token)
        {
            foreach (var subscribeMessage in GetSubscribeMessages(roomId))
                await socket.SendStringAsync(subscribeMessage, token).ConfigureAwait(false);

            static IEnumerable<string> GetSubscribeMessages(string roomId)
            {
                yield return CreateMessage($"room:tip_alert:{roomId}");
                yield return CreateMessage($"room:purchase:{roomId}");
                yield return CreateMessage($"room:funclub:{roomId}");
                yield return CreateMessage($"room:message:{roomId}:0");
                yield return CreateMessage("global:push_service");
                yield return CreateMessage($"room_annon:presence:{roomId}");
                yield return CreateMessage($"room:quality_update:{roomId}");
                yield return CreateMessage($"room:notice:{roomId}");
                yield return CreateMessage($"room:enter_leave:{roomId}");
                yield return CreateMessage($"room:password_protected:{roomId}");
                yield return CreateMessage($"room:mod_promoted:{roomId}");
                yield return CreateMessage($"room:mod_revoked:{roomId}");
                yield return CreateMessage($"room:status:{roomId}");
                yield return CreateMessage($"room:title_change:{roomId}");
                yield return CreateMessage($"room:silence:{roomId}");
                yield return CreateMessage($"room:kick:{roomId}");
                yield return CreateMessage($"room:update:{roomId}");
                yield return CreateMessage($"room:settings:{roomId}");

                static string CreateMessage(string channel)
                    => $@"{{""action"":10,""flags"":327680,""channel"":""{channel}"",""params"":{{}}}}";
            }
        }

        static async Task<string> GetAuthToken(HttpClient client, string csrf, string roomId, CancellationToken token)
        {
            var broadcaster = new JObject() { ["broadcaster_uid"] = roomId };
            var topics = new JObject()
            {
                [$"RoomTipAlertTopic#RoomTipAlertTopic:{roomId}"] = broadcaster,
                [$"RoomPurchaseTopic#RoomPurchaseTopic:{roomId}"] = broadcaster,
                [$"RoomFanClubJoinedTopic#RoomFanClubJoinedTopic:{roomId}"] = broadcaster,
                [$"RoomMessageTopic#RoomMessageTopic:{roomId}"] = broadcaster,
                ["GlobalPushServiceBackendChangeTopic#GlobalPushServiceBackendChangeTopic"] = new JObject(),
                [$"RoomAnonPresenceTopic#RoomAnonPresenceTopic:{roomId}"] = broadcaster,
                [$"QualityUpdateTopic#QualityUpdateTopic:{roomId}"] = broadcaster,
                [$"RoomNoticeTopic#RoomNoticeTopic:{roomId}"] = broadcaster,
                [$"RoomEnterLeaveTopic#RoomEnterLeaveTopic:{roomId}"] = broadcaster,
                [$"RoomPasswordProtectedTopic#RoomPasswordProtectedTopic:{roomId}"] = broadcaster,
                [$"RoomModeratorPromotedTopic#RoomModeratorPromotedTopic:{roomId}"] = broadcaster,
                [$"RoomModeratorRevokedTopic#RoomModeratorRevokedTopic:{roomId}"] = broadcaster,
                [$"RoomStatusTopic#RoomStatusTopic:{roomId}"] = broadcaster,
                [$"RoomTitleChangeTopic#RoomTitleChangeTopic:{roomId}"] = broadcaster,
                [$"RoomSilenceTopic#RoomSilenceTopic:{roomId}"] = broadcaster,
                [$"RoomKickTopic#RoomKickTopic:{roomId}"] = broadcaster,
                [$"RoomUpdateTopic#RoomUpdateTopic:{roomId}"] = broadcaster,
                [$"RoomSettingsTopic#RoomSettingsTopic:{roomId}"] = broadcaster
            };

            var authContent = $$"""
                ---
                Content-Disposition: form-data; name="topics"

                {{topics.ToString(Formatting.None)}}
                ---
                Content-Disposition: form-data; name="csrfmiddlewaretoken"

                {{csrf}}
                -----
                """;

            var requestContent = new StringContent(authContent, Encoding.UTF8, MediaTypeHeaderValue.Parse("multipart/form-data; boundary=-"));
            requestContent.Headers.Add("X-Requested-With", "XMLHttpRequest");

            var response = await client.PostAsync(new Uri("https://chaturbate.com/push_service/auth/"), requestContent, token).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            var document = JObject.Parse(responseContent);
            return document["token"].ToString();
        }
    }

    protected override void HandleSettings(JObject settings, AppSettingsMessageType type) { }
}
