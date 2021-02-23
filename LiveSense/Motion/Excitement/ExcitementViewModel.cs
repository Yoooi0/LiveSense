using LiveSense.Common;
using Newtonsoft.Json;
using Stylet;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LiveSense.Motion.Excitement
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ExcitementViewModel : Screen, IMotionSource
    {
        private readonly ITipQueue _queue;
        private readonly Dictionary<DeviceAxis, float> _axisValues;

        private CancellationTokenSource _cancellationSource;
        private Thread _thread;
        private float _excitement;
        private DateTime _lastTipTime;
        private float _motionDt;
        private float _motionTime;

        public BindableCollection<IExcitementGraphItem> LogItems { get; set; }

        [JsonProperty] public ValueRange<int> TipRange { get; set; } = (0, 500);
        [JsonProperty] public ValueRange<float> TimeChangeRange { get; set; } = (0.005f, 0.05f);
        [JsonProperty] public ValueRange<float> SpreadRange { get; set; } = (0.25f, 1f);
        [JsonProperty] public ValueRange<float> TimeSinceLastTipRange { get; set; } = (5f, 20f);
        [JsonProperty] public ValueRange<float> DecayRange { get; set; } = (0f, 0.125f);
        [JsonProperty] public ValueRange<float> DecayStrengthRange { get; set; } = (0, 1.5f);

        [JsonProperty] public string Name => "Excitement";

        public ExcitementViewModel(ITipQueue queue)
        {
            _queue = queue;

            _axisValues = new Dictionary<DeviceAxis, float>()
            {
                [DeviceAxis.L0] = 0.5f,
                [DeviceAxis.L1] = 0.5f,
                [DeviceAxis.L2] = 0.5f,
                [DeviceAxis.R0] = 0.5f,
                [DeviceAxis.R1] = 0.5f,
                [DeviceAxis.R2] = 0.5f,
                [DeviceAxis.V0] = 0.0f,
                [DeviceAxis.V1] = 0.0f
            };

            LogItems = new BindableCollection<IExcitementGraphItem>();
        }

        public float GetValue(DeviceAxis axis) => _axisValues[axis];

        private void Process(object state)
        {
            var token = (CancellationToken)state;

            const int updateRate = 16;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    while (_queue.Count > 0)
                    {
                        var tip = _queue.Dequeue(token);
                        if (tip.Amount < TipRange.Minimum)
                            return;

                        _lastTipTime = DateTime.UtcNow;
                        var lerpT = MathUtils.UnLerp(TipRange.Minimum, TipRange.Maximum, tip.Amount);
                        _excitement = MathUtils.Lerp(_excitement, 1, lerpT);
                    }

                    var now = DateTime.UtcNow;
                    var secondsSinceLastTip = (float)(now - _lastTipTime).TotalSeconds;

                    var alpha = MathUtils.Lerp(DecayStrengthRange.Minimum, DecayStrengthRange.Maximum, (secondsSinceLastTip - TimeSinceLastTipRange.Minimum) / TimeSinceLastTipRange.Maximum);
                    var strength = (float)Math.Pow(Math.E, _excitement * alpha) - 1;
                    var decay = MathUtils.Lerp(DecayRange.Minimum, DecayRange.Maximum, strength);
                    if (secondsSinceLastTip >= TimeSinceLastTipRange.Minimum)
                        _excitement = MathUtils.Clamp01(_excitement - decay * updateRate / 1000);

                    var targetDt = MathUtils.Lerp(TimeChangeRange.Minimum, TimeChangeRange.Maximum, _excitement);
                    var targetSpread = MathUtils.Lerp(SpreadRange.Minimum, SpreadRange.Maximum, _excitement);

                    _motionDt = MathUtils.Lerp(_motionDt, targetDt, 0.05f);
                    _motionTime += _motionDt;

                    _ = Execute.OnUIThreadAsync(() => {
                        LogItems.Add(new LogItem()
                        {
                            Timestamp = now,
                            Excitement = (_excitement, 0, 1),
                            Decay = (decay, 0, DecayRange.Maximum),
                            LastTip = (secondsSinceLastTip, 0, TimeSinceLastTipRange.Maximum)
                        });

                        while (LogItems.Count * updateRate > 10000)
                            LogItems.RemoveAt(0);
                    });

                    var motionValue = (float)(Math.Sin(_motionTime) + 1) / 2;
                    _axisValues[DeviceAxis.L0] = MathUtils.Lerp(0.5f - targetSpread / 2, 0.5f + targetSpread / 2, motionValue); //TODO: per axis?
                    Thread.Sleep(updateRate);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            _cancellationSource = new CancellationTokenSource();
            _thread = new Thread(Process)
            {
                IsBackground = true
            };
            _thread.Start(_cancellationSource.Token);
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();

            _cancellationSource?.Cancel();
            _thread?.Join();
            _cancellationSource?.Dispose();

            _cancellationSource = null;
            _thread = null;
        }
    }

    public class LogItem : IExcitementGraphItem
    {
        public DateTime Timestamp { get; set; }
        public ExcitementGraphProperty Excitement { get; set; }
        public ExcitementGraphProperty Decay { get; set; }
        public ExcitementGraphProperty LastTip { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ValueRange<T> : PropertyChangedBase
    {
        [JsonProperty] public T Minimum { get; set; }
        [JsonProperty] public T Maximum { get; set; }

        public static implicit operator ValueRange<T>((T Minimum, T Maximum) tuple) => new ValueRange<T>()
        {
            Minimum = tuple.Minimum,
            Maximum = tuple.Maximum
        };
    }
}
