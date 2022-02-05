using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace LiveSense.Common;

public static class Extensions
{
    public static bool TryToObject<T>(this JToken token, out T value)
    {
        value = default;

        try
        {
            if (token.Type == JTokenType.Null)
                return false;

            value = token.ToObject<T>();
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        static async Task DoWaitAsync(Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource();
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken), useSynchronizationContext: false);
            await await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
        }

        if (!cancellationToken.CanBeCanceled)
            return task;
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);
        return DoWaitAsync(task, cancellationToken);
    }

    public static Task<TResult> WithCancellation<TResult>(this Task<TResult> task, CancellationToken cancellationToken)
    {
        static async Task<T> DoWaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken), useSynchronizationContext: false);
            return await await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
        }

        if (!cancellationToken.CanBeCanceled)
            return task;
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<TResult>(cancellationToken);
        return DoWaitAsync(task, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ValidateIndex<T>(this ICollection<T> collection, int index)
        => index >= 0 && index < collection.Count;

    public static bool TryGet<T>(this IList list, int index, out T value)
    {
        value = default;
        if (index < 0 || index >= list.Count)
            return false;

        var o = list[index];
        if (o == null)
            return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;

        if (o is not T)
            return false;

        value = (T)o;
        return true;
    }

    public static bool TryGet<T>(this IList<T> list, int index, out T value)
    {
        value = default;
        if (!list.ValidateIndex(index))
            return false;

        value = list[index];
        return true;
    }

    public static bool EnsureContainsObjects(this JToken token, params string[] propertyNames)
    {
        if (token is not JObject o)
            return false;

        foreach (var propertyName in propertyNames)
        {
            if (!o.ContainsKey(propertyName))
                o[propertyName] = new JObject();

            if (o[propertyName] is JObject child)
                o = child;
            else
                return false;
        }

        return true;
    }

    public static bool TryGetObject(this JToken token, out JObject result, params string[] propertyNames)
    {
        result = null;
        if (token is not JObject o)
            return false;

        foreach (var propertyName in propertyNames)
        {
            if (!o.ContainsKey(propertyName) || o[propertyName] is not JObject child)
                return false;

            o = child;
        }

        result = o;
        return true;
    }

    public static void Populate(this JToken token, object target)
    {
        using var reader = token.CreateReader();
        JsonSerializer.CreateDefault().Populate(reader, target);
    }

    public static async Task<string> ReceiveStringAsync(this ClientWebSocket socket, CancellationToken token)
    {
        var result = default(WebSocketReceiveResult);
        var buffer = new ArraySegment<byte>(new byte[1024]);
        using var stream = new MemoryStream();
        do
        {
            result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
            stream.Write(buffer.Array, buffer.Offset, result.Count);
        }
        while (!result.EndOfMessage);

        stream.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static async Task SendStringAsync(this ClientWebSocket socket, string data, CancellationToken token)
        => await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
}

public static class EnumUtils
{
    public static T[] GetValues<T>() where T : Enum
        => (T[])Enum.GetValues(typeof(T));
}
