using LiveSense.Common;
using LiveSense.MotionSource.TipMenu.ViewModels;
using Newtonsoft.Json;
using Stylet;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TipMenuAction : PropertyChangedBase
    {
        [JsonProperty] public BindableCollection<DeviceAxis> Axes { get; set; }
        [JsonProperty] public string ScriptName { get; set; }

        public TipMenuAction()
        {
            Axes = new BindableCollection<DeviceAxis>();
            ScriptName = null;
        }
    }
}
