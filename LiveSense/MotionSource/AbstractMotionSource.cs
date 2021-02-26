using LiveSense.Common;
using LiveSense.Common.Messages;
using Newtonsoft.Json.Linq;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSense.MotionSource
{
    public abstract class AbstractMotionSource : Screen, IMotionSource, IDisposable, IHandle<AppSettingsMessage>
    {
        public abstract string Name { get; }

        protected AbstractMotionSource(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);
        }

        public abstract float GetValue(DeviceAxis axis);

        protected abstract void HandleSettings(JObject settings, AppSettingsMessageType type);
        public void Handle(AppSettingsMessage message)
        {
            if (message.Type == AppSettingsMessageType.Saving)
            {
                if (!message.Settings.EnsureContainsObjects("MotionSource", Name)
                 || !message.Settings.TryGetObject(out var settings, "MotionSource", Name))
                    return;

                HandleSettings(settings, message.Type);
            }
            else if (message.Type == AppSettingsMessageType.Loading)
            {
                if (!message.Settings.TryGetObject(out var settings, "MotionSource", Name))
                    return;

                HandleSettings(settings, message.Type);
            }
        }

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
