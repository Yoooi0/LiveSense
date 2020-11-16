using LiveSense.Device;
using LiveSense.ViewModels;
using Stylet;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.Motion
{
    public class MotionSourceValuesViewModel : Screen, IHandle<MotionSourceChangedEvent>, IDisposable
    {
        public class ValueItemModel : PropertyChangedBase
        {
            public DeviceAxis Axis { get; set; }
            public float Value { get; set; }

            public ValueItemModel(DeviceAxis axis, float value)
            {
                Axis = axis;
                Value = value;
            }
        }

        private IMotionSource _motionSource;
        private CancellationTokenSource _cancellationSource;
        private Task _updateTask;

        public BindableCollection<ValueItemModel> Values { get; set; }

        public MotionSourceValuesViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);

            Values = new BindableCollection<ValueItemModel>();
            foreach (var axis in (DeviceAxis[])Enum.GetValues(typeof(DeviceAxis)))
                Values.Add(new ValueItemModel(axis, 0));
        }

        //TODO: thread?
        private async Task UpdateValuesAsync(object state)
        {
            var token = (CancellationToken)state;
            while (!token.IsCancellationRequested)
            {
                if (_motionSource != null)
                {
                    await Execute.OnUIThreadAsync(() =>
                    {
                        foreach (var item in Values)
                        {
                            var value = _motionSource?.GetValue(item.Axis) * 100 ?? 0;
                            if (float.IsNaN(value))
                                value = 0;

                            item.Value = value;
                        }
                    });
                }

                await Task.Delay(32);
            }
        }

        public void Handle(MotionSourceChangedEvent message)
            => _motionSource = message.MotionSource;

        protected override void OnActivate()
        {
            _cancellationSource = new CancellationTokenSource();
            _updateTask = Task.Factory.StartNew(UpdateValuesAsync, _cancellationSource.Token, _cancellationSource.Token);
        }

        protected virtual void Dispose(bool disposing)
        {
            _cancellationSource.Cancel();
            _updateTask.Wait();
            _cancellationSource.Dispose();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
