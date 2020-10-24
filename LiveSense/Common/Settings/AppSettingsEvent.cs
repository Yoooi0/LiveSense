using Newtonsoft.Json.Linq;

namespace LiveSense.Common.Settings
{
    public enum AppSettingsStatus
    {
        Saving,
        Loading
    }

    public class AppSettingsEvent
    {
        public AppSettingsStatus Status { get; }
        public JObject Settings { get; }

        public AppSettingsEvent(JObject settings, AppSettingsStatus status)
        {
            Settings = settings;
            Status = status;
        }
    }
}
