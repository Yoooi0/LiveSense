using LiveSense.Common;
using LiveSense.Device;
using Newtonsoft.Json;
using Stylet;

namespace LiveSense.Motion.TipMenu
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TipMenuAction : PropertyChangedBase
    {
        [JsonProperty] public DeviceAxis Axis { get; set; }
        [JsonProperty] public MotionFunction Function { get; set; }
        [JsonProperty] public int Period { get; set; }
        [JsonProperty] public float RangeFrom { get; set; }
        [JsonProperty] public float RangeTo { get; set; }
        [JsonProperty] public int Delay { get; set; }
        [JsonProperty] public int Offset { get; set; }

        public TipMenuAction()
        {
            Axis = DeviceAxis.L0;
            Function = MotionFunction.Sine;
            Period = 1000;
            RangeFrom = 0;
            RangeTo = 100;
            Delay = 0;
            Offset = 0;
        }

        public float Normalize(float time)
        {
            if (Period == 0)
                return float.PositiveInfinity;
            if (Delay > 0 && time <= Delay)
                return float.NaN;

            return MathUtils.Clamp01(((time - Offset) % Period) / Period);
        }
        public float Calculate(float normalizedTime)
            => MathUtils.Map(MathUtils.Clamp01(Function.Calculate(normalizedTime)), 0, 1, RangeFrom / 100.0f, RangeTo / 100.0f);

        public float NormalizeAndCalculate(float time)
        {
            var normalizedTime = Normalize(time);
            if (!float.IsFinite(normalizedTime))
                return float.NaN;

            return Calculate(normalizedTime);
        }
    }
}
