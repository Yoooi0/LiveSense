using Newtonsoft.Json;
using Stylet;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TipMenuItem : PropertyChangedBase
    {
        [JsonProperty] public int AmountFrom { get; set; }
        [JsonProperty] public int AmountTo { get; set; }
        [JsonProperty] public float Duration { get; set; }
        [JsonProperty] public BindableCollection<TipMenuAction> Actions { get; set; }

        public TipMenuItem()
        {
            AmountFrom = 0;
            AmountTo = 100;
            Duration = 5;
            Actions = new BindableCollection<TipMenuAction>();
        }
    }
}
