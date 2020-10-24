using LiveSense.Common;
using LiveSense.Device;
using LiveSense.Service;
using Newtonsoft.Json;
using Stylet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LiveSense.Motion.TipMenu
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TipMenuViewModel : Screen, IMotionSource
    {
        private readonly ITipQueue _queue;
        private readonly ConcurrentDictionary<DeviceAxis, float> _devicePositions;
        private readonly IReadOnlyDictionary<DeviceAxis, float> _defaultPositions;
        private Thread _thread;
        private CancellationTokenSource _cancellationSource;

        [JsonProperty] public string MotionName => "Tip Menu";
        [JsonProperty] public BindableCollection<TipMenuItem> TipMenuItems { get; set; }

        public TipMenuItem SelectedTipMenuItem { get; set; }
        public TipMenuAction SelectedAction { get; set; }

        public TipMenuViewModel(ITipQueue queue)
        {
            _queue = queue;

            TipMenuItems = new BindableCollection<TipMenuItem>
            {
                new TipMenuItem()
                {
                    AmountFrom = 1,
                    AmountTo = 999,
                    Duration = 5000,
                    Actions = new BindableCollection<TipMenuAction>()
                    {
                        new TipMenuAction()
                        {
                            Axis = DeviceAxis.L0,
                            Function = MotionFunction.Sine
                        }
                    }
                }
            };

            _defaultPositions = new Dictionary<DeviceAxis, float>()
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

            _devicePositions = new ConcurrentDictionary<DeviceAxis, float>(_defaultPositions);
        }

        private void Process(object state)
        {
            var token = (CancellationToken)state;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var tip = _queue.Peek(token);
                    ExecuteTip(tip, token);

                    if(_queue.FirstOrDefault() == tip)
                        tip = _queue.Dequeue(token);

                    if (_queue.Count == 0)
                        ExecuteReset(500);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ExecuteTip(ServiceTip tip, CancellationToken token)
        {
            var item = FindItem(tip.Amount);
            if (item == null)
                return;

            if (item.Duration == 0)
                return;

            var idleAxes = ((DeviceAxis[])Enum.GetValues(typeof(DeviceAxis)))
                                         .Except(item.Actions.Select(a => a.Axis));
            var devicePositionsCopy = new Dictionary<DeviceAxis, float>(_devicePositions);

            var time = 0L;
            var startTime = DateTime.UtcNow.Ticks;
            while((time = (DateTime.UtcNow.Ticks - startTime) / TimeSpan.TicksPerMillisecond) <= item.Duration)
            {
                if (token.IsCancellationRequested)
                    return;
                if (_queue.FirstOrDefault() != tip)
                    break;

                Execute.OnUIThread(() => tip.Progress = MathUtils.Clamp01((float)time / item.Duration) * 100);

                foreach (var action in item.Actions)
                {
                    var value = action.NormalizeAndCalculate(time);
                    if(float.IsFinite(value))
                        _devicePositions[action.Axis] = value;
                }

                var resetTime = MathUtils.Clamp01((float)time / Math.Min(item.Duration, 1000));
                foreach (var axis in idleAxes)
                    _devicePositions[axis] = MathUtils.Lerp(devicePositionsCopy[axis], _defaultPositions[axis], resetTime);

                Thread.Sleep(3);
            }

            foreach (var action in item.Actions)
            {
                var value = action.NormalizeAndCalculate(item.Duration);
                if (float.IsFinite(value))
                    _devicePositions[action.Axis] = value;
            }
            foreach (var axis in idleAxes)
                _devicePositions[axis] = _defaultPositions[axis];
        }

        private void ExecuteReset(int duration)
        {
            var devicePositionsCopy = new Dictionary<DeviceAxis, float>(_devicePositions);

            var time = 0L;
            var startTime = DateTime.UtcNow.Ticks;
            while ((time = (DateTime.UtcNow.Ticks - startTime) / TimeSpan.TicksPerMillisecond) <= duration)
            {
                var resetTime = MathUtils.Clamp01((float)time / duration);
                foreach (var axis in (DeviceAxis[])Enum.GetValues(typeof(DeviceAxis)))
                    _devicePositions[axis] = MathUtils.Lerp(devicePositionsCopy[axis], _defaultPositions[axis], resetTime);

                Thread.Sleep(3);
            }

            foreach (var axis in (DeviceAxis[])Enum.GetValues(typeof(DeviceAxis)))
                _devicePositions[axis] = _defaultPositions[axis];
        }

        public float GetValue(DeviceAxis axis)
            => _devicePositions[axis];

        private TipMenuItem FindItem(int amount)
            => TipMenuItems.FirstOrDefault(i => amount >= i.AmountFrom && amount <= i.AmountTo);

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

        #region Button actions
        public bool CanRemoveItem => SelectedTipMenuItem != null;
        public void RemoveItem()
        {
            var index = TipMenuItems.IndexOf(SelectedTipMenuItem);
            TipMenuItems.Remove(SelectedTipMenuItem);

            if (TipMenuItems.Count == 0)
                SelectedTipMenuItem = null;
            else
                SelectedTipMenuItem = TipMenuItems[Math.Min(index, TipMenuItems.Count - 1)];
        }

        public void AddItem() => TipMenuItems.Add(new TipMenuItem());

        public bool CanRemoveAction => SelectedTipMenuItem != null && SelectedAction != null;
        public bool CanAddAction => SelectedTipMenuItem != null;

        public void RemoveAction()
        {
            var index = SelectedTipMenuItem.Actions.IndexOf(SelectedAction);
            SelectedTipMenuItem.Actions.Remove(SelectedAction);

            if (SelectedTipMenuItem.Actions.Count == 0)
                SelectedAction = null;
            else
                SelectedAction = SelectedTipMenuItem.Actions[Math.Min(index, SelectedTipMenuItem.Actions.Count - 1)];
        }

        public void AddAction() => SelectedTipMenuItem.Actions.Add(new TipMenuAction());

        public bool CanMoveActionUp => SelectedTipMenuItem != null && SelectedAction != null;
        public bool CanMoveActionDown => SelectedTipMenuItem != null && SelectedAction != null;

        public void MoveActionUp()
        {
            var actions = SelectedTipMenuItem.Actions;
            var index = actions.IndexOf(SelectedAction);
            if (index == 0)
                return;

            actions.Move(index, index - 1);
        }

        public void MoveActionDown()
        {
            var actions = SelectedTipMenuItem.Actions;
            var index = actions.IndexOf(SelectedAction);
            if (index == actions.Count - 1)
                return;

            actions.Move(index, index + 1);
        }

        public bool CanMoveItemUp => SelectedTipMenuItem != null;
        public bool CanMoveItemDown => SelectedTipMenuItem != null;

        public void MoveItemUp()
        {
            var index = TipMenuItems.IndexOf(SelectedTipMenuItem);
            if (index == 0)
                return;

            TipMenuItems.Move(index, index - 1);
        }

        public void MoveItemDown()
        {
            var index = TipMenuItems.IndexOf(SelectedTipMenuItem);
            if (index == TipMenuItems.Count - 1)
                return;

            TipMenuItems.Move(index, index + 1);
        }
        #endregion
    }
}
