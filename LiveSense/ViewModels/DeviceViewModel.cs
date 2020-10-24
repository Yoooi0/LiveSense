using LiveSense.Common;
using LiveSense.Common.Settings;
using LiveSense.Device;
using Newtonsoft.Json.Linq;
using Stylet;
using System.Collections.Generic;
using System.Linq;

namespace LiveSense.ViewModels
{
    public class DeviceViewModel : Conductor<IDevice>.Collection.OneActive, IHandle<AppSettingsEvent>
    {
        public DeviceViewModel(IEventAggregator eventAggregator, IEnumerable<IDevice> devices)
        {
            eventAggregator.Subscribe(this);

            Items.AddRange(devices);
            ScreenExtensions.TryActivate(this);
        }

        public void Handle(AppSettingsEvent message)
        {
            if (message.Status == AppSettingsStatus.Saving)
            {
                message.Settings.Add("Device", JObject.FromObject(new
                {
                    ActiveItem = ActiveItem.DeviceName,
                    Items = Items.Select(i => JObject.FromObject(i))
                }));
            }
            else if (message.Status == AppSettingsStatus.Loading)
            {
                if (!message.Settings.ContainsKey("Device"))
                    return;

                var deviceSettings = message.Settings["Device"];
                foreach (var itemSettings in (deviceSettings["Items"] as JArray))
                {
                    var item = Items.FirstOrDefault(i => i.DeviceName == itemSettings["DeviceName"].ToString());
                    if (item == null)
                        continue;

                    itemSettings.Populate(item);
                }

                ActiveItem = Items.FirstOrDefault(i => i.DeviceName == deviceSettings["ActiveItem"].ToString());
            }
        }

        protected override void OnInitialActivate()
        {
            base.OnInitialActivate();
            ActiveItem = null;
        }
    }
}
