using LiveSense.Common;
using LiveSense.Common.Settings;
using LiveSense.Motion;
using Newtonsoft.Json.Linq;
using Stylet;
using System.Collections.Generic;
using System.Linq;

namespace LiveSense.ViewModels
{
    public class MotionSourceChangedEvent
    {
        public IMotionSource MotionSource { get; }

        public MotionSourceChangedEvent(IMotionSource motionSource)
        {
            MotionSource = motionSource;
        }
    }

    public class MotionSourceViewModel : Conductor<IMotionSource>.Collection.OneActive, IHandle<AppSettingsEvent>
    {
        private readonly IEventAggregator _eventAggregator;

        public MotionSourceValuesViewModel MotionValues { get; private set; }

        public MotionSourceViewModel(MotionSourceValuesViewModel motionValues, IEventAggregator eventAggregator, IEnumerable<IMotionSource> motionSources)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            MotionValues = motionValues;

            Items.AddRange(motionSources);
            MotionValues.ConductWith(this);
            ScreenExtensions.TryActivate(this);
        }

        protected override void OnInitialActivate()
        {
            base.OnInitialActivate();
            _eventAggregator.Publish(new MotionSourceChangedEvent(ActiveItem));
        }

        public override void ActivateItem(IMotionSource item)
        {
            base.ActivateItem(item);
            _eventAggregator.Publish(new MotionSourceChangedEvent(item));
        }

        public void Handle(AppSettingsEvent message)
        {
            if(message.Status == AppSettingsStatus.Saving)
            {
                message.Settings.Add("MotionSource", JObject.FromObject(new
                {
                    ActiveItem = ActiveItem.MotionName,
                    Items = Items.Select(i => JObject.FromObject(i))
                }));
            }
            else if (message.Status == AppSettingsStatus.Loading)
            {
                if (!message.Settings.ContainsKey("MotionSource"))
                    return;

                var motionSettings = message.Settings["MotionSource"];
                foreach (var itemSettings in (motionSettings["Items"] as JArray))
                {
                    var item = Items.FirstOrDefault(i => i.MotionName == itemSettings["MotionName"].ToString());
                    if (item == null)
                        continue;

                    itemSettings.Populate(item);
                }

                ActiveItem = Items.FirstOrDefault(i => i.MotionName == motionSettings["ActiveItem"].ToString());
            }
        }
    }
}
