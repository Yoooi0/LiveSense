using Newtonsoft.Json;
using Stylet;

namespace LiveSense.Motion.TipMenu
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TipMenuItem : PropertyChangedBase
    {
        [JsonProperty] public int AmountFrom { get; set; }
        [JsonProperty] public int AmountTo { get; set; }
        [JsonProperty] public int Duration { get; set; }
        [JsonProperty] public BindableCollection<TipMenuAction> Actions { get; set; }

        public TipMenuItem()
        {
            AmountFrom = 0;
            AmountTo = 0;
            Duration = 1000;
            Actions = new BindableCollection<TipMenuAction>();
        }
    }
}
