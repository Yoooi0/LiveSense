﻿using MaterialDesignThemes.Wpf;
using LiveSense.Common.Controls;
using LiveSense.Common.Messages;
using Newtonsoft.Json.Linq;
using Stylet;
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveSense.Common;

namespace LiveSense.OutputTarget.ViewModels
{
    public class SerialOutputTargetViewModel : ThreadAbstractOutputTarget
    {
        public override string Name => "Serial";
        public override OutputTargetStatus Status { get; protected set; }

        public BindableCollection<string> ComPorts { get; set; }
        public string SelectedComPort { get; set; }

        public SerialOutputTargetViewModel(IEventAggregator eventAggregator, IDeviceAxisValueProvider valueProvider)
            : base(eventAggregator, valueProvider)
        {
            ComPorts = new BindableCollection<string>(SerialPort.GetPortNames());
        }

        public bool CanChangePort => !IsRefreshBusy && !IsConnectBusy && !IsConnected;
        public bool IsRefreshBusy { get; set; }
        public bool CanRefreshPorts => !IsRefreshBusy && !IsConnectBusy && !IsConnected;
        public async Task RefreshPorts()
        {
            IsRefreshBusy = true;
            await Task.Delay(750).ConfigureAwait(true);

            var lastSelected = SelectedComPort;
            ComPorts.Clear();
            try
            {
                ComPorts.AddRange(SerialPort.GetPortNames());
            }
            catch { }
            SelectedComPort = lastSelected;

            await Task.Delay(250).ConfigureAwait(true);
            IsRefreshBusy = false;
        }

        public bool IsConnected => Status == OutputTargetStatus.Connected;
        public bool IsConnectBusy => Status == OutputTargetStatus.Connecting || Status == OutputTargetStatus.Disconnecting;
        public bool CanToggleConnect => !IsConnectBusy && SelectedComPort != null;

        protected override void Run(CancellationToken token)
        {
            var serialPort = default(SerialPort);

            try
            {
                serialPort = new SerialPort(SelectedComPort, 115200)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    DtrEnable = true,
                    RtsEnable = true
                };

                serialPort.Open();
                serialPort.ReadExisting();
                Status = OutputTargetStatus.Connected;
            }
            catch (Exception e)
            {
                try
                {
                    if (serialPort?.IsOpen == true)
                        serialPort.Close();
                }
                catch (IOException) { }

                _ = Execute.OnUIThreadAsync(async () =>
                {
                    _ = DialogHost.Show(new ErrorMessageDialog($"Error when opening serial port:\n\n{e}"), "RootDialog");
                    await RefreshPorts().ConfigureAwait(true);
                });

                return;
            }

            try
            {
                var sb = new StringBuilder(256);
                var lastSentValues = EnumUtils.GetValues<DeviceAxis>().ToDictionary(a => a, _ => float.NaN);
                while (!token.IsCancellationRequested && serialPort?.IsOpen == true)
                {
                    var interval = MathF.Max(1, 1000.0f / UpdateRate);
                    UpdateValues();

                    sb.Clear();
                    foreach (var (axis, value) in Values)
                    {
                        if (float.IsFinite(lastSentValues[axis]) && MathF.Abs(lastSentValues[axis] - value) * 999 < 1)
                            continue;

                        lastSentValues[axis] = value;
                        sb.Append(axis)
                          .AppendFormat("{0:000}", value * 999)
                          .AppendFormat("I{0}", (int)interval)
                          .Append(' ');
                    }

                    var commands = sb.ToString().Trim();
                    if (serialPort?.IsOpen == true && !string.IsNullOrWhiteSpace(commands))
                        serialPort?.WriteLine(commands);

                    Thread.Sleep((int)interval);
                }
            }
            catch (Exception e) when (e is TimeoutException || e is IOException)
            {
                _ = Execute.OnUIThreadAsync(async () =>
                {
                    _ = DialogHost.Show(new ErrorMessageDialog($"Unhandled error:\n\n{e}"), "RootDialog");
                    await RefreshPorts().ConfigureAwait(true);
                });
            }
            catch (Exception) { }

            try
            {
                if (serialPort?.IsOpen == true)
                    serialPort?.Close();
            }
            catch (IOException) { }
        }

        protected override void HandleSettings(JObject settings, AppSettingsMessageType type)
        {
            if (type == AppSettingsMessageType.Saving)
            {
                if(SelectedComPort != null)
                    settings[nameof(SelectedComPort)] = new JValue(SelectedComPort);
            }
            else if (type == AppSettingsMessageType.Loading)
            {
                if (settings.TryGetValue(nameof(SelectedComPort), out var selectedComPortToken))
                    SelectedComPort = selectedComPortToken.ToObject<string>();
            }
        }
    }
}
