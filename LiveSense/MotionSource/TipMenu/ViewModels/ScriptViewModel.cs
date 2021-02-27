using LiveSense.Common;
using Stylet;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    public interface IScript
    {
        float? Evaluate(float time, DeviceAxis axis);
        void OnBegin() { }
        void OnEnd() { }
    }

    public class ScriptViewModel : PropertyChangedBase
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public IScript Instance { get; set; }

        public ScriptViewModel(string name)
            : this(name, string.Empty) { }

        public ScriptViewModel(string name, string source)
        {
            Name = name;
            Source = source;
        }
    }
}