using LiveSense.Common;

namespace LiveSense.OutputTarget;

public interface IDeviceAxisValueProvider
{
    public float GetValue(DeviceAxis axis);
}
