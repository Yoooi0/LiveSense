using LiveSense.Common.Controls;
using LiveSense.Common.Messages;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Stylet;
using StyletIoC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Input;

namespace LiveSense.ViewModels
{
    public class RootViewModel : Conductor<IScreen>.Collection.AllActive
    {
        private readonly IEventAggregator _eventAggregator;

        [Inject] public MotionSourceViewModel MotionSource { get; internal set; }
        [Inject] public TipQueueViewModel TipQueue { get; internal set; }
        [Inject] public OutputTargetViewModel OutputTarget { get; set; }
        [Inject] public ServiceViewModel Service { get; set; }

        public RootViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            JsonConvert.DefaultSettings = () =>
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };

                settings.Converters.Add(new StringEnumConverter());
                return settings;
            };
        }

        protected override void OnActivate()
        {
            Items.Add(MotionSource);
            Items.Add(TipQueue);
            Items.Add(OutputTarget);
            Items.Add(Service);

            ActivateAndSetParent(Items);
            base.OnActivate();
        }

        public void OnLoaded(object sender, EventArgs e)
        {
            Execute.PostToUIThread(async () =>
            {
                var settings = ReadSettings();
                _eventAggregator.Publish(new AppSettingsMessage(settings, AppSettingsMessageType.Loading));

                if (!settings.TryGetValue("DisablePopup", out var disablePopupToken) || !disablePopupToken.Value<bool>())
                {
                    var result = await DialogHost.Show(new InformationMessageDialog(showCheckbox: true)).ConfigureAwait(true);
                    if (result is not bool disablePopup || !disablePopup)
                        return;

                    settings["DisablePopup"] = true;
                    WriteSettings(settings);
                }
            });
        }

        public void OnClosing(object sender, EventArgs e)
        {
            var settings = ReadSettings();
            _eventAggregator.Publish(new AppSettingsMessage(settings, AppSettingsMessageType.Saving));
            WriteSettings(settings);
        }

        private JObject ReadSettings()
        {
            var path = Path.Join(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "LiveSense.config.json");
            if (!File.Exists(path))
                return new JObject();

            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return new JObject();
            }
        }

        private void WriteSettings(JObject settings)
        {
            var path = Path.Join(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "LiveSense.config.json");
            File.WriteAllText(path, settings.ToString());
        }

        public void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Window window)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            window.DragMove();
        }
    }
}
