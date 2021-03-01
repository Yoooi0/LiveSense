using ICSharpCode.AvalonEdit.Document;
using LiveSense.Common;
using Stylet;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    public interface IScript
    {
        float? Evaluate(float time, DeviceAxis axis, float duration);
        void OnBegin() { }
    }

    public class ScriptViewModel : PropertyChangedBase
    {
        public TextDocument Document { get; set; }
        public string Name { get; set; }
        public IScript Instance { get; set; }
        public string CompilationOutput { get; set; }

        public ScriptViewModel(string name)
            : this(name, string.Empty) { }

        public ScriptViewModel(string name, string source)
        {
            Name = name;
            Document = new TextDocument()
            {
                Text = source
            };
        }
    }
}