using LiveSense.Common;
using LiveSense.Motion;
using LiveSense.ViewModels;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Stylet;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSense.Device.TCode
{

    [JsonObject(MemberSerialization.OptIn)]
    public class TCodeViewModel : Screen, IDevice
    {
        private IMotionSource _motionSource;
        private CancellationTokenSource _cancellationSource;
        private Thread _deviceThread;
        private SerialPort _serialPort;

        public BindableCollection<ComPortModel> ComPorts { get; set; }

        [JsonProperty] public ObservableDictionary<DeviceAxis, AxisSettingsModel> AxisSettings { get; set; }
        [JsonProperty] public ComPortModel SelectedComPort { get; set; }
        [JsonProperty] public int UpdateRate { get; set; }
        [JsonProperty] public string DeviceName => "TCode";

        public TCodeViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);

            ComPorts = new BindableCollection<ComPortModel>(SerialPort.GetPortNames().Select(p => new ComPortModel(p)));
            AxisSettings = new ObservableDictionary<DeviceAxis, AxisSettingsModel>();
            foreach (var axis in (DeviceAxis[])Enum.GetValues(typeof(DeviceAxis)))
                AxisSettings.Add(axis, new AxisSettingsModel());

            UpdateRate = 60;
        }

        public bool IsConnected { get; set; }
        public bool IsBusy { get; set; }
        public bool CanToggleConnect => !IsBusy && SelectedComPort != null;
        public async Task ToggleConnect()
        {
            IsBusy = true;

            if (IsConnected)
            {
                await Disconnect();
                IsConnected = false;
            }
            else
            {
                IsConnected = await Connect();
            }

            IsBusy = false;
        }

        public async Task<bool> Connect()
        {
            if (SelectedComPort == null)
                return false;

            await Task.Delay(1000);

            try
            {
                _serialPort = new SerialPort(SelectedComPort.Name, 115200)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.Open();
                _serialPort.ReadExisting();

                //TODO:
                //_serialPort.WriteLine("D1");
                //var response = _serialPort.ReadLine();
                //if (!response.Contains("tcode", StringComparison.OrdinalIgnoreCase))
                //{
                //    _ = Execute.OnUIThreadAsync(() => DialogHost.Show(new ErrorMessageDialog("This is not a TCode device!")));
                //    return false;
                //}
            }
            catch(Exception e)
            {
                if (_serialPort?.IsOpen == true)
                    _serialPort.Close();

                _ = Execute.OnUIThreadAsync(() => DialogHost.Show(new ErrorMessageDialog($"Error when opening serial port:\n\n{e}")));
                return false;
            }

            _cancellationSource = new CancellationTokenSource();
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
            _deviceThread?.Join();

            if(_serialPort?.IsOpen == true)
                _serialPort?.Close();
            _cancellationSource?.Dispose();

            await Task.Delay(1000);

            _cancellationSource = null;
            _deviceThread = null;
            _serialPort = null;
        }

        private void UpdateDevice(object state)
        {
            var token = (CancellationToken)state;
            var sb = new StringBuilder(256);

            var interval = (int)Math.Round(1000.0f / UpdateRate);
            while (!token.IsCancellationRequested)
            {
                sb.Clear();
                foreach(var axis in (DeviceAxis[])Enum.GetValues(typeof(DeviceAxis)))
                {
                    var value = _motionSource?.GetValue(axis) ?? float.NaN;
                    if (float.IsNaN(value))
                        continue;

                    if (AxisSettings.TryGetValue(axis, out var axisSettings))
                        value = MathUtils.Lerp(axisSettings.Minimum / 100.0f, axisSettings.Maximum / 100.0f, value);

                    sb.Append(axis)
                      .AppendFormat("{0:000}", value * 999)
                      .Append(' ');
                }

                var commands = sb.ToString().Trim();
                if (_serialPort.IsOpen && !string.IsNullOrWhiteSpace(commands))
                    _serialPort?.WriteLine(commands);

                Thread.Sleep(interval);
            }
        }

        protected override async void OnDeactivate()
        {
            await Disconnect();
            IsConnected = false;
            IsBusy = false;
        }

        public void Handle(MotionSourceChangedEvent message)
            => _motionSource = message.MotionSource;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class AxisSettingsModel
    {
        [JsonProperty] public int Minimum { get; set; }
        [JsonProperty] public int Maximum { get; set; }

        public AxisSettingsModel()
        {
            Minimum = 0;
            Maximum = 100;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ComPortModel
    {
        [JsonProperty] public string Name { get; }
        [JsonProperty] public string Description { get; }

        public ComPortModel(string name) : this(name, null) { }

        public ComPortModel(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public override string ToString() => Description != null ? $"{Name} ({Description})" : Name;
    }
}
