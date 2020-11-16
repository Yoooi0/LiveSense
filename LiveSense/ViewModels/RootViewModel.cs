using LiveSense.Common.Settings;
using MaterialDesignExtensions.Controls;
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
using System.Threading.Tasks;

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

    public class RootViewModel : PropertyChangedBase
    {
        private readonly IEventAggregator _eventAggregator;

        [Inject] public SettingsViewModel Settings { get; internal set; }
        [Inject] public MotionSourceViewModel MotionSource { get; internal set; }
        [Inject] public TipQueueViewModel TipQueue { get; internal set; }

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

        public async Task SaveSettings()
        {
            var dialogArgs = new SaveFileDialogArguments()
            {
                Width = 900,
                Height = 700,
                Filters = "All files|*.*|JSON files|*.json",
                CreateNewDirectoryEnabled = true,
                CurrentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
            };

            var result = await SaveFileDialog.ShowDialogAsync("RootDialog", dialogArgs);
            if (!result.Confirmed)
                return;

            var settings = new JObject();
            _eventAggregator.Publish(new AppSettingsEvent(settings, AppSettingsStatus.Saving));

            File.WriteAllText(result.File, settings.ToString());
        }

        public async Task LoadSettings()
        {
            var dialogArgs = new OpenFileDialogArguments()
            {
                Width = 900,
                Height = 700,
                Filters = "All files|*.*|JSON files|*.json",
                CreateNewDirectoryEnabled = true,
                CurrentDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
            };

            var result = await OpenFileDialog.ShowDialogAsync("RootDialog", dialogArgs);
            if (!result.Confirmed)
                return;

            var serializer = JsonSerializer.CreateDefault();
            serializer.Converters.Add(new StringEnumConverter());

            var settings = JObject.Parse(File.ReadAllText(result.File));
            _eventAggregator.Publish(new AppSettingsEvent(settings, AppSettingsStatus.Loading));
        }
    }
}
