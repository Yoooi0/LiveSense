using LiveSense.Common;
using Newtonsoft.Json;
using Stylet;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TipMenuAction : PropertyChangedBase
    {
        [JsonProperty] public BindableCollection<DeviceAxis> Axes { get; set; }
        [JsonProperty] public string ScriptName { get; set; }
        [JsonProperty] public float Minimum { get; set; }
        [JsonProperty] public float Maximum { get; set; }

        public TipMenuAction()
        {
            Axes = new BindableCollection<DeviceAxis>();
            ScriptName = null;
            Minimum = 0;
            Maximum = 100;
        }
    }
}
