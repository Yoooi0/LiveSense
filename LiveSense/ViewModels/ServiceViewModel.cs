using LiveSense.Common;
using LiveSense.Common.Settings;
using LiveSense.Service;
using Newtonsoft.Json.Linq;
using Stylet;
using System.Collections.Generic;
using System.Linq;

namespace LiveSense.ViewModels
{
    public class ServiceViewModel : Conductor<IService>.Collection.OneActive, IHandle<AppSettingsEvent>
    {
        public ServiceViewModel(IEventAggregator eventAggregator, IEnumerable<IService> services)
        {
            eventAggregator.Subscribe(this);

            Items.AddRange(services);
            ScreenExtensions.TryActivate(this);
        }

        public void Handle(AppSettingsEvent message)
        {
            if (message.Status == AppSettingsStatus.Saving)
            {
                message.Settings.Add("Service", JObject.FromObject(new
                {
                    ActiveItem = ActiveItem.ServiceName,
                    Items = Items.Select(i => JObject.FromObject(i))
                }));
            }
            else if(message.Status == AppSettingsStatus.Loading)
            {
                if (!message.Settings.ContainsKey("Service"))
                    return;

                var serviceSettings = message.Settings["Service"];
                foreach (var itemSettings in (serviceSettings["Items"] as JArray))
                {
                    var item = Items.FirstOrDefault(i => i.ServiceName == itemSettings["ServiceName"].ToString());
                    if (item == null)
                        continue;

                    itemSettings.Populate(item);
                }

                ActiveItem = Items.FirstOrDefault(i => i.ServiceName == serviceSettings["ActiveItem"].ToString());
            }
        }

        protected override void OnInitialActivate()
        {
            base.OnInitialActivate();
            ActiveItem = null;
        }
    }
}
