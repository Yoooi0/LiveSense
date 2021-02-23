using LiveSense.Common;
using LiveSense.Common.Messages;
using LiveSense.Motion;
using LiveSense.OutputTarget;
using Newtonsoft.Json.Linq;
using Stylet;
using System.Collections.Generic;
using System.Linq;

namespace LiveSense.ViewModels
{
    public class MotionSourceViewModel : Conductor<IMotionSource>.Collection.OneActive, IHandle<AppSettingsMessage>, IDeviceAxisValueProvider
    {
        private readonly IEventAggregator _eventAggregator;

        public MotionSourceValuesViewModel MotionValues { get; internal set; }

        public MotionSourceViewModel(MotionSourceValuesViewModel motionValues, IEventAggregator eventAggregator, IEnumerable<IMotionSource> motionSources)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            MotionValues = motionValues;

            Items.AddRange(motionSources);
            MotionValues.ConductWith(this);
        }

        public void Handle(AppSettingsMessage message)
        {
            if(message.Type == AppSettingsMessageType.Saving)
            {
                message.Settings.Add("MotionSource", JObject.FromObject(new
                {
                    ActiveItem = ActiveItem.Name,
                    Items = Items.Select(i => JObject.FromObject(i))
                }));
            }
            else if (message.Type == AppSettingsMessageType.Loading)
            {
                if (!message.Settings.ContainsKey("MotionSource"))
                    return;

                var motionSettings = message.Settings["MotionSource"];
                foreach (var itemSettings in (motionSettings["Items"] as JArray))
                {
                    var item = Items.FirstOrDefault(i => i.Name == itemSettings["MotionName"].ToString());
                    if (item == null)
                        continue;

                    itemSettings.Populate(item);
                }

                ActiveItem = Items.FirstOrDefault(i => i.Name == motionSettings["ActiveItem"].ToString());
            }
        }

        public float GetValue(DeviceAxis axis) => ActiveItem?.GetValue(axis) ?? float.NaN;
    }
}
