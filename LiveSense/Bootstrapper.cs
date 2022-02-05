using LiveSense.Common;
using LiveSense.OutputTarget;
using LiveSense.MotionSource;
using LiveSense.Service;
using LiveSense.ViewModels;
using Stylet;
using StyletIoC;
using LiveSense.MotionSource.TipMenu.ViewModels;

namespace LiveSense;

public class Bootstrapper : Bootstrapper<RootViewModel>
{
    protected override void ConfigureIoC(IStyletIoCBuilder builder)
    {
        builder.Bind<MotionSourceViewModel>().And<IDeviceAxisValueProvider>().To<MotionSourceViewModel>().InSingletonScope();

        builder.Bind<IOutputTarget>().ToAllImplementations();
        builder.Bind<IService>().ToAllImplementations();
        builder.Bind<IMotionSource>().ToAllImplementations();
        builder.Bind<ITipQueue>().ToInstance(new TipQueue());
        builder.Bind<IScriptCompiler>().ToInstance(new ScriptCompiler());
    }
}
