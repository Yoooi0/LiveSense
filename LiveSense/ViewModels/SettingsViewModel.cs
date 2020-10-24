using Stylet;
using StyletIoC;

namespace LiveSense.ViewModels
{
    public class SettingsViewModel : PropertyChangedBase
    {
        [Inject] public DeviceViewModel Device { get; set; }
        [Inject] public ServiceViewModel Service { get; set; }
    }
}
