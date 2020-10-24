using LiveSense.Device;
using LiveSense.Service;
using Stylet;

namespace LiveSense.Motion
{
    public interface IMotionSource
    {
        string MotionName { get; }
        public float GetValue(DeviceAxis axis);
    }
}
