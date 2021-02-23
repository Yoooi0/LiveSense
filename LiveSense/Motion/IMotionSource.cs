using LiveSense.OutputTarget;

namespace LiveSense.Motion
{
    public interface IMotionSource : IDeviceAxisValueProvider
    {
        string Name { get; }
    }
}
