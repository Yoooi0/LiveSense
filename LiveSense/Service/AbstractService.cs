using LiveSense.Common;
using LiveSense.Common.Messages;
using LiveSense.Service;
using Newtonsoft.Json.Linq;
using PropertyChanged;
using Stylet;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.OutputTarget
{
    public abstract class AbstractService : Screen, IHandle<AppSettingsMessage>, IDisposable, IService
    {
        private CancellationTokenSource _cancellationSource;
        private Task _task;

        public abstract string Name { get; }
        [SuppressPropertyChangedWarnings] public abstract ServiceStatus Status { get; protected set; }
        public bool ContentVisible { get; set; }
        protected ITipQueue Queue { get; }

        protected AbstractService(IEventAggregator eventAggregator, ITipQueue queue)
        {
            eventAggregator.Subscribe(this);
            Queue = queue;
        }

        public async Task ToggleConnectAsync()
        {
            if (Status == ServiceStatus.Connected || Status == ServiceStatus.Connecting)
                await DisconnectAsync().ConfigureAwait(true);
            else
                await ConnectAsync().ConfigureAwait(true);
        }

        protected abstract Task RunAsync(CancellationToken token);
        protected async Task ConnectAsync()
        {
            if (Status != ServiceStatus.Disconnected)
                return;

            Status = ServiceStatus.Connecting;
            await Task.Delay(1000).ConfigureAwait(true);

            _cancellationSource = new CancellationTokenSource();
            _task = Task.Factory.StartNew(() => RunAsync(_cancellationSource.Token),
                _cancellationSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
                .Unwrap();
            _ = _task.ContinueWith(_ => Execute.OnUIThreadAsync(async () => await DisconnectAsync().ConfigureAwait(true))).Unwrap();
        }

        protected virtual async Task DisconnectAsync()
        {
            if (Status == ServiceStatus.Disconnected || Status == ServiceStatus.Disconnecting)
                return;

            Status = ServiceStatus.Disconnecting;
            Dispose(disposing: false);
            await Task.Delay(1000).ConfigureAwait(false);
            Status = ServiceStatus.Disconnected;
        }

        protected abstract void HandleSettings(JObject settings, AppSettingsMessageType type);
        public void Handle(AppSettingsMessage message)
        {
            if (message.Type == AppSettingsMessageType.Saving)
            {
                if (!message.Settings.EnsureContainsObjects("Service", Name)
                 || !message.Settings.TryGetObject(out var settings, "Service", Name))
                    return;

                HandleSettings(settings, message.Type);
            }
            else if (message.Type == AppSettingsMessageType.Loading)
            {
                if (!message.Settings.TryGetObject(out var settings, "Service", Name))
                    return;

                HandleSettings(settings, message.Type);
            }
        }

        protected async void Dispose(bool disposing)
        {
            _cancellationSource?.Cancel();

            if (_task != null)
                await _task.ConfigureAwait(false);

            _cancellationSource?.Dispose();

            _cancellationSource = null;
            _task = null;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}