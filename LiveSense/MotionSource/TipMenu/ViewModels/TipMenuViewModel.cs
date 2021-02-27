using ICSharpCode.AvalonEdit.Document;
using LiveSense.Common;
using LiveSense.Common.Messages;
using LiveSense.Service;
using Newtonsoft.Json.Linq;
using PropertyChanged;
using Stylet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace LiveSense.MotionSource.TipMenu.ViewModels
{
    public class TipMenuViewModel : AbstractMotionSource
    {
        private readonly ITipQueue _queue;
        private readonly IScriptCompiler _compiler;
        private readonly ConcurrentDictionary<DeviceAxis, float> _devicePositions;
        private Thread _thread;
        private CancellationTokenSource _cancellationSource;

        public override string Name => "Tip Menu";

        public BindableCollection<TipMenuItem> TipMenuItems { get; set; }
        public TipMenuItem SelectedTipMenuItem { get; set; }
        public TipMenuAction SelectedAction { get; set; }

        public bool IsEditorBusy { get; set; }
        public BindableCollection<ScriptViewModel> Scripts { get; set; }
        public ScriptViewModel SelectedScript { get; set; }
        public TextDocument EditorDocument { get; set; }
        public string ScriptName { get; set; }
        public string CompilationOutput { get; set; }

        public TipMenuViewModel(IEventAggregator eventAggregator, IScriptCompiler compiler, ITipQueue queue)
            : base(eventAggregator)
        {
            _compiler = compiler;
            _queue = queue;

            TipMenuItems = new BindableCollection<TipMenuItem>();
            Scripts = new BindableCollection<ScriptViewModel>();
            EditorDocument = new TextDocument();

            _devicePositions = new ConcurrentDictionary<DeviceAxis, float>(EnumUtils.GetValues<DeviceAxis>().ToDictionary(a => a, a => a.DefaultValue()));
        }

        private void Process(object state)
        {
            var token = (CancellationToken)state;

            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                while (!token.IsCancellationRequested)
                {
                    var tip = _queue.Peek(token);
                    ExecuteTip(stopwatch, tip, token);

                    if(_queue.FirstOrDefault() == tip)
                        tip = _queue.Dequeue(token);

                    if (_queue.Count == 0)
                        ExecuteReset(stopwatch, 500, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ExecuteTip(Stopwatch stopwatch, ServiceTip tip, CancellationToken token)
        {
            TipMenuItem FindItem(int amount) => TipMenuItems.FirstOrDefault(i => amount >= i.AmountFrom && amount <= i.AmountTo);
            IScript FindScriptByName(string scriptName) => Scripts.FirstOrDefault(s => string.Equals(s.Name, scriptName))?.Instance;

            float CalculateValue(IEnumerable<TipMenuAction> actions, DeviceAxis axis)
            {
                var scriptValues = actions.Select(action =>
                {
                    if (!action.Axes.Contains(axis))
                        return null;

                    return FindScriptByName(action.ScriptName)?.Evaluate((float)stopwatch.Elapsed.TotalSeconds, axis);
                });

                return scriptValues.Where(x => x != null && float.IsFinite(x.Value))
                                   .Select(x => x.Value)
                                   .DefaultIfEmpty(float.NaN)
                                   .Average();
            }

            void UpdatePositions(IEnumerable<TipMenuAction> actions)
            {
                foreach (var axis in EnumUtils.GetValues<DeviceAxis>())
                {
                    var value = CalculateValue(actions, axis);
                    if (!float.IsFinite(value))
                        continue;

                    _devicePositions[axis] = value;
                }
            }

            void UpdateResetPositions(IEnumerable<DeviceAxis> idleAxes, Dictionary<DeviceAxis, float> startPositions, float duration)
            {
                var resetTime = MathUtils.Clamp01((float)stopwatch.Elapsed.TotalSeconds / duration);
                foreach (var axis in idleAxes)
                    _devicePositions[axis] = MathUtils.Lerp(startPositions[axis], axis.DefaultValue(), resetTime);
            }

            var item = FindItem(tip.Amount);
            if (item == null)
                return;

            if (item.Duration == 0)
                return;

            var idleAxes = EnumUtils.GetValues<DeviceAxis>().Except(item.Actions.SelectMany(a => a.Axes).Distinct());
            var devicePositionsCopy = new Dictionary<DeviceAxis, float>(_devicePositions);

            const float uiUpdateInterval = 1f / 30f;
            var uiUpdateTick = 0f;

            foreach(var action in item.Actions)
                FindScriptByName(action.ScriptName)?.OnBegin();

            stopwatch.Restart();
            while(!token.IsCancellationRequested && stopwatch.ElapsedMilliseconds <= item.Duration)
            {
                if (_queue.FirstOrDefault() != tip)
                    break;

                var updateTick = (int)Math.Floor(stopwatch.Elapsed.TotalSeconds / uiUpdateInterval);
                if (updateTick > uiUpdateTick)
                {
                    uiUpdateTick = updateTick;
                    Execute.OnUIThread(() => tip.Progress = MathUtils.Clamp01((float)stopwatch.ElapsedMilliseconds / item.Duration) * 100);
                }

                UpdatePositions(item.Actions);
                UpdateResetPositions(idleAxes, devicePositionsCopy, Math.Min(item.Duration, 1000) / 1000f);

                Thread.Sleep(2);
            }

            foreach (var action in item.Actions)
                FindScriptByName(action.ScriptName)?.OnEnd();
        }

        private void ExecuteReset(Stopwatch stopwatch, int duration, CancellationToken token)
        {
            var devicePositionsCopy = new Dictionary<DeviceAxis, float>(_devicePositions);

            stopwatch.Restart();
            while (!token.IsCancellationRequested && stopwatch.ElapsedMilliseconds <= duration)
            {
                var resetTime = MathUtils.Clamp01((float)stopwatch.ElapsedMilliseconds / duration);
                foreach (var axis in EnumUtils.GetValues<DeviceAxis>())
                    _devicePositions[axis] = MathUtils.Lerp(devicePositionsCopy[axis], axis.DefaultValue(), resetTime);

                Thread.Sleep(2);
            }

            foreach (var axis in EnumUtils.GetValues<DeviceAxis>())
                _devicePositions[axis] = axis.DefaultValue();
        }

        public override float GetValue(DeviceAxis axis) => _devicePositions[axis];

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

        protected override void HandleSettings(JObject settings, AppSettingsMessageType type)
        {
            if (type == AppSettingsMessageType.Saving)
            {
                settings[nameof(TipMenuItems)] = JArray.FromObject(TipMenuItems);

                var scriptsToken = new JObject();
                foreach (var script in Scripts)
                    scriptsToken.Add(script.Name, Convert.ToBase64String(Encoding.ASCII.GetBytes(script.Source)));

                settings[nameof(Scripts)] = scriptsToken;
            }
            else if (type == AppSettingsMessageType.Loading)
            {
                if (settings.TryGetValue(nameof(TipMenuItems), out var tipMenuItemsToken) && tipMenuItemsToken is JArray tipMenuItems)
                    TipMenuItems.AddRange(tipMenuItems.ToObject<List<TipMenuItem>>());

                if(settings.TryGetObject(out var scriptsToken, nameof(Scripts)))
                {
                    Task.Run(() =>
                    {
                        Execute.OnUIThread(() => IsEditorBusy = true);
                        foreach (var property in scriptsToken.Properties())
                        {
                            var source = Encoding.ASCII.GetString(Convert.FromBase64String(property.Value.ToObject<string>()));
                            var script = new ScriptViewModel(property.Name, source);

                            _compiler.Compile(source, out var instance);
                            script.Instance = instance;

                            Scripts.Add(script);
                        }
                        Execute.OnUIThread(() => IsEditorBusy = false);
                    }).ConfigureAwait(false);
                }
            }
        }

        protected override void Dispose(bool disposing) { }

        #region Script
        public bool CanAddScript => !string.IsNullOrWhiteSpace(ScriptName) && !string.Equals(SelectedScript?.Name, ScriptName);
        public void AddScript()
        {
            if (Scripts.Any(s => string.Equals(s.Name, ScriptName, StringComparison.OrdinalIgnoreCase)))
                return;

            var script = new ScriptViewModel(ScriptName, EditorDocument.Text);
            CompilationOutput = _compiler.Compile(script.Source, out var instance);

            Scripts.Add(script);
            SelectedScript = script;
        }

        public bool CanDeleteScript => SelectedScript != null;
        public void DeleteScript()
        {
            if (SelectedScript == null || !Scripts.Contains(SelectedScript))
                return;

            var script = SelectedScript;
            var index = Scripts.IndexOf(script);
            Scripts.Remove(script);
            _compiler.Dispose(script.Instance);

            SelectedScript = Scripts.Count > 0 ? Scripts[Math.Min(index, Scripts.Count - 1)] : null;
        }

        public bool CanSaveScript => SelectedScript != null && !string.IsNullOrWhiteSpace(ScriptName);
        public void SaveScript()
        {
            if (SelectedScript == null)
                return;

            SelectedScript.Name = ScriptName;
            SelectedScript.Source = EditorDocument.Text;

            Task.Run(async () =>
            {
                Execute.OnUIThread(() => IsEditorBusy = true);
                await Task.Delay(500).ConfigureAwait(true);
                _compiler.Dispose(SelectedScript.Instance);
                CompilationOutput = _compiler.Compile(SelectedScript.Source, out var instance);
                SelectedScript.Instance = instance;
                Execute.OnUIThread(() => IsEditorBusy = false);
            }).ConfigureAwait(true);
        }

        [SuppressPropertyChangedWarnings]
        public void OnSelectedScriptChanged(object s, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
            {
                ScriptName = null;
                EditorDocument.Text = string.Empty;
                return;
            }

            if (e.AddedItems[0] is not ScriptViewModel script)
                return;

            ScriptName = script.Name;
            EditorDocument.Text = script.Source;
            if (script.Instance == null)
            {
                CompilationOutput = _compiler.Compile(EditorDocument.Text, out var instance);
                script.Instance = instance;
            }
        }
        #endregion

        #region Menu
        public void AddItem() => TipMenuItems.Add(new TipMenuItem());
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

        public bool CanMoveItemUp => SelectedTipMenuItem != null;
        public void MoveItemUp()
        {
            var index = TipMenuItems.IndexOf(SelectedTipMenuItem);
            if (index == 0)
                return;

            TipMenuItems.Move(index, index - 1);
        }

        public bool CanMoveItemDown => SelectedTipMenuItem != null;
        public void MoveItemDown()
        {
            var index = TipMenuItems.IndexOf(SelectedTipMenuItem);
            if (index == TipMenuItems.Count - 1)
                return;

            TipMenuItems.Move(index, index + 1);
        }
        #endregion

        #region Actions
        public bool CanAddAction => SelectedTipMenuItem != null;
        public void AddAction() => SelectedTipMenuItem.Actions.Add(new TipMenuAction());
        public bool CanRemoveAction => SelectedTipMenuItem != null && SelectedAction != null;
        public void RemoveAction()
        {
            var index = SelectedTipMenuItem.Actions.IndexOf(SelectedAction);
            SelectedTipMenuItem.Actions.Remove(SelectedAction);

            if (SelectedTipMenuItem.Actions.Count == 0)
                SelectedAction = null;
            else
                SelectedAction = SelectedTipMenuItem.Actions[Math.Min(index, SelectedTipMenuItem.Actions.Count - 1)];
        }
        #endregion
    }
}
