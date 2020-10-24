using LiveSense.Common;
using LiveSense.Device;
using LiveSense.Motion;
using LiveSense.Service;
using LiveSense.ViewModels;
using Stylet;
using StyletIoC;

namespace LiveSense
{
    public class Bootstrapper : Bootstrapper<RootViewModel>
    {
        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            builder.Bind<IDevice>().ToAllImplementations();
            builder.Bind<IService>().ToAllImplementations();
            builder.Bind<IMotionSource>().ToAllImplementations();
            builder.Bind<ITipQueue>().ToInstance(new ObservableTipQueue());
        }
    }
}
