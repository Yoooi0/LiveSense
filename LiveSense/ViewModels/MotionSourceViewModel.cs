using LiveSense.Common;
using LiveSense.Common.Messages;
using LiveSense.MotionSource;
using LiveSense.OutputTarget;
using Stylet;

namespace LiveSense.ViewModels;

public class MotionSourceViewModel : Conductor<IMotionSource>.Collection.OneActive, IHandle<AppSettingsMessage>, IDeviceAxisValueProvider, IDisposable
{
    private CancellationTokenSource _cancellationSource;

    public ObservableConcurrentDictionary<DeviceAxis, float> Values { get; set; }
    public bool IsValuesPanelExpanded { get; set; }

    public MotionSourceViewModel(IEventAggregator eventAggregator, IEnumerable<IMotionSource> motionSources)
    {
        eventAggregator.Subscribe(this);

        Items.AddRange(motionSources);

        Values = new ObservableConcurrentDictionary<DeviceAxis, float>(EnumUtils.GetValues<DeviceAxis>().ToDictionary(a => a, _ => 0f));

        _cancellationSource = new CancellationTokenSource();
        _ = Task.Factory.StartNew(() => UpdateValuesAsync(_cancellationSource.Token),
            _cancellationSource.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)
            .Unwrap();
    }

    private async Task UpdateValuesAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (ActiveItem != null && IsValuesPanelExpanded)
                {
                    await Execute.OnUIThreadAsync(() =>
                    {
                        foreach (var axis in EnumUtils.GetValues<DeviceAxis>())
                        {
                            var value = GetValue(axis) * 100;
                            if (!float.IsFinite(value))
                                value = 0;

                            Values[axis] = value;
                        }
                    }).ConfigureAwait(false);
                }

                await Task.Delay(32, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Handle(AppSettingsMessage message)
    {
        if (message.Type == AppSettingsMessageType.Saving)
        {
            if (!message.Settings.EnsureContainsObjects("MotionSource")
             || !message.Settings.TryGetObject(out var settings, "MotionSource"))
                return;

            if (ActiveItem != null)
                settings[nameof(ActiveItem)] = ActiveItem.Name;
        }
        else if (message.Type == AppSettingsMessageType.Loading)
        {
            if (!message.Settings.TryGetObject(out var settings, "MotionSource"))
                return;

            if (settings.TryGetValue(nameof(ActiveItem), out var selectedItemToken))
                ChangeActiveItem(Items.FirstOrDefault(x => string.Equals(x.Name, selectedItemToken.ToObject<string>())) ?? Items[0], closePrevious: false);
        }
    }

    public float GetValue(DeviceAxis axis) => ActiveItem?.GetValue(axis) ?? float.NaN;

    protected virtual void Dispose(bool disposing)
    {
        _cancellationSource?.Cancel();
        _cancellationSource?.Dispose();
        _cancellationSource = null;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
