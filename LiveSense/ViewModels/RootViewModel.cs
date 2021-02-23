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
    public class CustomContractResolver : DefaultContractResolver
    {
        private static void ClearGenericCollectionCallback<T>(object o, StreamingContext _)
        {
            if (o is ICollection<T> collection && !(collection is Array) && !collection.IsReadOnly)
                collection.Clear();
        }

        private static IEnumerable<Type> GetCollectionGenericTypes(Type type)
        {
            var interfaces = type.GetInterfaces();
            //TODO:
            //if(type.IsInterface)
            //    interfaces.Append(type);

            foreach (var intType in interfaces)
                if (intType.IsGenericType && intType.GetGenericTypeDefinition() == typeof(ICollection<>))
                    yield return intType.GetGenericArguments()[0];
        }

        protected override JsonArrayContract CreateArrayContract(Type objectType)
        {
            var contract = base.CreateArrayContract(objectType);
            if (!objectType.IsArray)
            {
                if (typeof(IList).IsAssignableFrom(objectType))
                {
                    contract.OnDeserializingCallbacks.Add((o, _) =>
                    {
                        if (o is IList list && !(list is Array) && !list.IsReadOnly)
                            list.Clear();
                    });
                }
                else if (GetCollectionGenericTypes(objectType).Count() == 1)
                {
                    var method = typeof(CustomContractResolver).GetMethod(nameof(ClearGenericCollectionCallback), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    var generic = method.MakeGenericMethod(contract.CollectionItemType);
                    contract.OnDeserializingCallbacks.Add((SerializationCallback)Delegate.CreateDelegate(typeof(SerializationCallback), generic));
                }
            }

            return contract;
        }
    }

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
                    Formatting = Formatting.Indented,
                    ContractResolver = new CustomContractResolver()
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
