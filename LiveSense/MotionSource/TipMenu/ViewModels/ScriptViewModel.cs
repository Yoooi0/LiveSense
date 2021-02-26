using LiveSense.Common;
using Stylet;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    public interface IScript
    {
        float? Evaluate(float time, DeviceAxis axis);
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