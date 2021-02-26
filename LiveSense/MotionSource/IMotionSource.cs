using LiveSense.OutputTarget;

namespace LiveSense.MotionSource
{
    public interface IMotionSource : IDeviceAxisValueProvider
    {
        string Name { get; }
    }
}
