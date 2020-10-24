using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.Common
{
    public static class Extensions
    {
        public static void Populate<T>(this JToken value, T target, JsonSerializerSettings settings = null) where T : class
        {
            using var sr = value.CreateReader();
            JsonSerializer.CreateDefault(settings).Populate(sr, target);
        }
    }
}
