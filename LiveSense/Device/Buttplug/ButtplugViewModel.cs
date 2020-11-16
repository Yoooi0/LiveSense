using Buttplug.Client;
using Buttplug.Client.Connectors;
using Buttplug.Client.Connectors.WebsocketConnector;
using Buttplug.Core.Messages;
using LiveSense.Common;
using LiveSense.Motion;
using LiveSense.ViewModels;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using PropertyChanged;
using Stylet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace LiveSense.Device.Buttplug
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ButtplugViewModel : Screen, IDevice
    {
        private IMotionSource _motionSource;
        private CancellationTokenSource _cancellationSource;
        private Thread _deviceThread;
        private ButtplugClient _client;

        [JsonProperty] public int UpdateRate { get; set; }
        [JsonProperty] public string ServerAddress { get; set; }
        [JsonProperty] public int ServerPort { get; set; }
        [JsonProperty] public string DeviceName => "Buttplug.io";

        public ButtplugViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);

            UpdateRate = 10;
            ServerAddress = "localhost";
            ServerPort = 12345;
        }

        public bool IsConnected { get; set; }
        public bool IsBusy { get; set; }
        public bool CanToggleConnect => !IsBusy && !string.IsNullOrWhiteSpace(ServerAddress) && ServerPort >= 1024 && ServerPort <= 65535;
        public async Task ToggleConnect()
        {
            IsBusy = true;

            if (IsConnected)
            {
                await Disconnect();
            }
            else
            {
                IsConnected = await Connect();
            }

            IsBusy = false;
        }

        public async Task<bool> Connect()
        {
            if (string.IsNullOrWhiteSpace(ServerAddress) || ServerPort < 1024 || ServerPort > 65535)
                return false;

            await Task.Delay(1000);
            _cancellationSource = new CancellationTokenSource();
            _client = new ButtplugClient("LiveSense Client", new ButtplugWebsocketConnector(new Uri($"ws://{ServerAddress}:{ServerPort}/buttplug")));
            try
            {
                await _client.ConnectAsync(_cancellationSource.Token);
                _client.ServerDisconnect += async (s, e) =>
                {
                    IsBusy = true;
                    await Disconnect();
                    IsBusy = false;
                };

                _ = Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        await _client.StartScanningAsync(_cancellationSource.Token);
                        while (_client.Devices.Length == 0)
                            await Task.Delay(200);

                        await Task.Delay(5000);
                        await _client.StopScanningAsync(_cancellationSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        await _client.StopScanningAsync(CancellationToken.None);
                    }
                }, _cancellationSource.Token);
            }
            catch (Exception e)
            {
                _ = Execute.OnUIThreadAsync(() => DialogHost.Show(new ErrorMessageDialog($"Can't connect to Buttplug Server:\n\n{e}")));
                return false;
            }

            _deviceThread = new Thread(UpdateDevice)
            {
                IsBackground = true
            };
            _deviceThread.Start(_cancellationSource.Token);

            return true;
        }

        public async Task Disconnect()
        {
            _cancellationSource?.Cancel();

            await Task.Delay(1000);

            _deviceThread?.Join();
            if(_client != null)
                await _client.DisconnectAsync();

            _cancellationSource?.Dispose();

            _cancellationSource = null;
            _client = null;
            _deviceThread = null;

            IsConnected = false;
        }

        private void UpdateDevice(object state)
        {
            var token = (CancellationToken)state;

            var interval = (uint)Math.Round(1000.0f / UpdateRate);
            var cache = new Dictionary<ButtplugClientDevice, Dictionary<DeviceAxis, float>>();
            while (!token.IsCancellationRequested && _client.Connected)
            {
                if (_client.Connected && _motionSource != null)
                {
                    var values = ((DeviceAxis[])Enum.GetValues(typeof(DeviceAxis))).ToDictionary(a => a, a => _motionSource.GetValue(a));
                    var tasks = new List<Task>();
                    foreach (var device in _client.Devices)
                    {
                        if (!cache.ContainsKey(device))
                            cache.Add(device, new Dictionary<DeviceAxis, float>());

                        var cachedValues = cache[device];
                        bool IsAxisDirty(DeviceAxis axis)
                            => !cachedValues.ContainsKey(axis) || Math.Abs(cachedValues[axis] - values[axis]) != 0;

                        if (device.AllowedMessages.TryGetValue(typeof(LinearCmd), out var linearAttributes))
                        {
                            var cmds = new List<(uint, double)>();
                            if (linearAttributes.FeatureCount >= 1 && IsAxisDirty(DeviceAxis.L0)) cmds.Add((interval, values[DeviceAxis.L0]));
                            if (linearAttributes.FeatureCount >= 2 && IsAxisDirty(DeviceAxis.L1)) cmds.Add((interval, values[DeviceAxis.L1]));
                            if (linearAttributes.FeatureCount >= 3 && IsAxisDirty(DeviceAxis.L2)) cmds.Add((interval, values[DeviceAxis.L2]));
                            if (cmds.Count > 0) tasks.Add(device.SendLinearCmd(cmds));
                        }

                        if (device.AllowedMessages.TryGetValue(typeof(RotateCmd), out var rotateAttributes))
                        {
                            var cmds = new List<(double, bool)>();
                            if (rotateAttributes.FeatureCount >= 1 && IsAxisDirty(DeviceAxis.R0)) cmds.Add((values[DeviceAxis.R0] - 0.5, values[DeviceAxis.R0] > 0.5));
                            if (rotateAttributes.FeatureCount >= 2 && IsAxisDirty(DeviceAxis.R1)) cmds.Add((values[DeviceAxis.R1] - 0.5, values[DeviceAxis.R1] > 0.5));
                            if (rotateAttributes.FeatureCount >= 3 && IsAxisDirty(DeviceAxis.R2)) cmds.Add((values[DeviceAxis.R2] - 0.5, values[DeviceAxis.R2] > 0.5));
                            if (cmds.Count > 0) tasks.Add(device.SendRotateCmd(cmds));
                        }

                        if (device.AllowedMessages.TryGetValue(typeof(VibrateCmd), out var vibrateAttributes) && vibrateAttributes.FeatureCount != 0)
                        {
                            var cmds = new List<double>();
                            if (vibrateAttributes.FeatureCount >= 1 && IsAxisDirty(DeviceAxis.V0)) cmds.Add(values[DeviceAxis.V0]);
                            if (vibrateAttributes.FeatureCount >= 2 && IsAxisDirty(DeviceAxis.V1)) cmds.Add(values[DeviceAxis.V1]);
                            if (cmds.Count > 0) tasks.Add(device.SendVibrateCmd(cmds));
                        }

                        foreach (var axis in (DeviceAxis[])Enum.GetValues(typeof(DeviceAxis)))
                            cachedValues[axis] = values[axis];
                    }

                    Task.WaitAll(tasks.ToArray());
                }

                Thread.Sleep((int)interval);
            }
        }

        protected override async void OnDeactivate()
        {
            IsBusy = true;
            await Disconnect();
            IsBusy = false;
        }

        public void Handle(MotionSourceChangedEvent message)
            => _motionSource = message.MotionSource;
    }
}
