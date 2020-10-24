using LiveSense.ViewModels;
using Stylet;

namespace LiveSense.Device
{
    public interface IDevice : IHandle<MotionSourceChangedEvent>
    {
        string DeviceName { get; }
    }
}
