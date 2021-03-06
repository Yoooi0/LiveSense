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
using System.IO;
using System.IO.Compression;
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
        private readonly TextDocument _fallbackDocument;
        private Thread _thread;
        private CancellationTokenSource _cancellationSource;

        public override string Name => "Tip Menu";

        public BindableCollection<TipMenuItem> Items { get; set; }
        public TipMenuItem SelectedItem { get; set; }
        public TipMenuAction SelectedAction { get; set; }

        public bool IsEditorBusy { get; set; }
        public BindableCollection<ScriptViewModel> Scripts { get; set; }
        public ScriptViewModel SelectedScript { get; set; }
        public string ScriptName { get; set; }
        public TextDocument CurrentDocument => SelectedScript?.Document ?? _fallbackDocument;
        public string CompilationOutput => SelectedScript?.CompilationOutput ?? string.Empty;

        public TipMenuViewModel(IEventAggregator eventAggregator, IScriptCompiler compiler, ITipQueue queue)
            : base(eventAggregator)
        {
            _compiler = compiler;
            _queue = queue;

            Items = new BindableCollection<TipMenuItem>();
            Scripts = new BindableCollection<ScriptViewModel>();

            _fallbackDocument = new TextDocument();
            _devicePositions = new ConcurrentDictionary<DeviceAxis, float>(EnumUtils.GetValues<DeviceAxis>().ToDictionary(a => a, a => a.DefaultValue()));
        }

        private void Run(CancellationToken token)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                while (!token.IsCancellationRequested)
                {
                    var tip = _queue.Peek(token);
                    ExecuteTip(stopwatch, tip, token);

                    if (_queue.FirstOrDefault() == tip)
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
            TipMenuItem FindItem(int amount) => Items.FirstOrDefault(i => amount >= i.AmountFrom && amount <= i.AmountTo);
            IScript FindScriptByName(string scriptName) => Scripts.FirstOrDefault(s => string.Equals(s.Name, scriptName))?.Instance;

            float CalculateValue(TipMenuItem item, DeviceAxis axis)
            {
                var scriptValues = item.Actions.Select(action =>
                {
                    if (!action.Axes.Contains(axis))
                        return float.NaN;

                    var script = FindScriptByName(action.ScriptName);
                    var result = script?.Evaluate((float)stopwatch.Elapsed.TotalSeconds, axis, item.Duration);
                    if (result == null)
                        return float.NaN;

                    return MathUtils.Lerp(action.Minimum / 100f, action.Maximum / 100f, result.Value);
                });

                return scriptValues.Where(x => float.IsFinite(x))
                                   .DefaultIfEmpty(float.NaN)
                                   .Average();
            }

            void UpdatePositions(TipMenuItem item, float smoothingDuration)
            {
                foreach (var axis in EnumUtils.GetValues<DeviceAxis>())
                {
                    var value = CalculateValue(item, axis);
                    if (!float.IsFinite(value))
                        continue;

                    var smoothing = MathUtils.Clamp01((float)stopwatch.Elapsed.TotalSeconds / smoothingDuration);
                    _devicePositions[axis] = MathUtils.Lerp(_devicePositions[axis], value, smoothing * smoothing * smoothing);
                }
            }

            void UpdateResetPositions(IEnumerable<DeviceAxis> idleAxes, Dictionary<DeviceAxis, float> startPositions, float resetDuration)
            {
                var resetTime = MathUtils.Clamp01((float)stopwatch.Elapsed.TotalSeconds / resetDuration);
                foreach (var axis in idleAxes)
                    _devicePositions[axis] = MathUtils.Lerp(startPositions[axis], axis.DefaultValue(), resetTime);
            }

            var item = FindItem(tip.Amount);
            if (item == null)
                return;

            if (item.Duration < 1)
                return;

            var idleAxes = EnumUtils.GetValues<DeviceAxis>().Except(item.Actions.SelectMany(a => a.Axes).Distinct());
            var devicePositionsCopy = new Dictionary<DeviceAxis, float>(_devicePositions);

            const float uiUpdateInterval = 1f / 30f;
            var smoothingDuration = Math.Min(item.Duration, 1);
            var resetDuration = Math.Min(item.Duration, 1);
            var uiUpdateTick = 0f;

            foreach (var action in item.Actions)
                FindScriptByName(action.ScriptName)?.OnBegin();

            stopwatch.Restart();
            while (!token.IsCancellationRequested && stopwatch.Elapsed.TotalSeconds <= item.Duration)
            {
                if (_queue.FirstOrDefault() != tip)
                    break;

                var updateTick = (int)Math.Floor(stopwatch.Elapsed.TotalSeconds / uiUpdateInterval);
                if (updateTick > uiUpdateTick)
                {
                    uiUpdateTick = updateTick;
                    Execute.OnUIThread(() => tip.Progress = MathUtils.Clamp01((float)stopwatch.Elapsed.TotalSeconds / item.Duration) * 100);
                }

                UpdatePositions(item, smoothingDuration);
                UpdateResetPositions(idleAxes, devicePositionsCopy, resetDuration);

                Thread.Sleep(2);
            }
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
            _thread = new Thread(() => Run(_cancellationSource.Token))
            {
                IsBackground = true
            };
            _thread.Start();
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
            static string Compress(string s)
            {
                using var compressed = new MemoryStream();
                using var brotli = new BrotliStream(compressed, CompressionLevel.Optimal);

                brotli.Write(Encoding.UTF8.GetBytes(s));
                brotli.Flush();

                return Convert.ToBase64String(compressed.ToArray());
            }

            static string Decompress(string s)
            {
                using var compressed = new MemoryStream();
                compressed.Write(Convert.FromBase64String(s));
                compressed.Seek(0, SeekOrigin.Begin);

                using var brotli = new BrotliStream(compressed, CompressionMode.Decompress);
                using var decompressed = new MemoryStream();
                brotli.CopyTo(decompressed);

                return Encoding.UTF8.GetString(decompressed.ToArray());
            }

            if (type == AppSettingsMessageType.Saving)
            {
                settings[nameof(Items)] = JArray.FromObject(Items);

                var scriptsToken = new JObject();
                foreach (var script in Scripts)
                    scriptsToken.Add(script.Name, Compress(script.Document.Text));

                settings[nameof(Scripts)] = scriptsToken;
            }
            else if (type == AppSettingsMessageType.Loading)
            {
                if (settings.TryGetValue(nameof(Items), out var tipMenuItemsToken) && tipMenuItemsToken is JArray tipMenuItems)
                    Items.AddRange(tipMenuItems.ToObject<List<TipMenuItem>>());

                if (settings.TryGetObject(out var scriptsToken, nameof(Scripts)))
                {
                    var uiThread = Thread.CurrentThread;
                    Task.Run(() =>
                    {
                        Execute.OnUIThread(() => IsEditorBusy = true);
                        foreach (var property in scriptsToken.Properties())
                        {
                            var source = Decompress(property.Value.ToObject<string>());
                            var script = new ScriptViewModel(property.Name, source)
                            {
                                CompilationOutput = _compiler.Compile(source, out var instance),
                                Instance = instance
                            };

                            script.Document.SetOwnerThread(uiThread);
                            Scripts.Add(script);
                        }
                        Execute.OnUIThread(() => IsEditorBusy = false);
                    }).ConfigureAwait(true);
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

            var uiThread = Thread.CurrentThread;
            var source = CurrentDocument.Text;
            Task.Run(async () =>
            {
                Execute.OnUIThread(() => IsEditorBusy = true);
                await Task.Delay(500).ConfigureAwait(true);

                var script = new ScriptViewModel(ScriptName, source)
                {
                    CompilationOutput = _compiler.Compile(source, out var instance),
                    Instance = instance
                };

                script.Document.SetOwnerThread(uiThread);

                Scripts.Add(script);
                SelectedScript = script;

                NotifyOfPropertyChange(nameof(CompilationOutput));

                Execute.OnUIThread(() => IsEditorBusy = false);
            });
        }

        public bool CanDeleteScript => SelectedScript != null;
        public void DeleteScript()
        {
            if (SelectedScript == null || !Scripts.Contains(SelectedScript))
                return;

            var script = SelectedScript;
            var index = Scripts.IndexOf(script);
            Scripts.Remove(script);
            SelectedScript = Scripts.Count > 0 ? Scripts[Math.Min(index, Scripts.Count - 1)] : null;

            Task.Run(() => _compiler.Dispose(script.Instance)).ConfigureAwait(true);
        }

        public bool CanSaveScript => SelectedScript != null && !string.IsNullOrWhiteSpace(ScriptName);
        public void SaveScript()
        {
            if (SelectedScript == null)
                return;

            SelectedScript.Name = ScriptName;
            var source = SelectedScript.Document.Text;
            Task.Run(async () =>
            {
                Execute.OnUIThread(() => IsEditorBusy = true);

                await Task.Delay(500).ConfigureAwait(true);
                _compiler.Dispose(SelectedScript.Instance);

                SelectedScript.CompilationOutput = _compiler.Compile(source, out var instance);
                SelectedScript.Instance = instance;

                NotifyOfPropertyChange(nameof(CompilationOutput));

                Execute.OnUIThread(() => IsEditorBusy = false);
            }).ConfigureAwait(true);
        }

        [SuppressPropertyChangedWarnings]
        public void OnSelectedScriptChanged(object s, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
            {
                ScriptName = null;
                return;
            }

            if (e.AddedItems[0] is not ScriptViewModel script)
                return;

            ScriptName = script.Name;
        }
        #endregion

        #region Menu
        public void AddItem()
        {
            if (SelectedItem != null)
            {
                var item = new TipMenuItem()
                {
                    AmountFrom = SelectedItem.AmountTo + 1,
                    AmountTo = SelectedItem.AmountTo + 100,
                    Duration = SelectedItem.Duration + 1
                };

                var index = Items.IndexOf(SelectedItem);
                Items.Insert(index + 1, item);
                SelectedItem = item;
            }
            else
            {
                var item = new TipMenuItem();
                Items.Add(item);
                SelectedItem = item;
            }

            NotifyOfPropertyChange(nameof(CanMoveItemDown));
            NotifyOfPropertyChange(nameof(CanMoveItemUp));
        }

        public bool CanRemoveItem => SelectedItem != null;
        public void RemoveItem()
        {
            var index = Items.IndexOf(SelectedItem);
            Items.Remove(SelectedItem);

            if (Items.Count == 0)
                SelectedItem = null;
            else
                SelectedItem = Items[Math.Min(index, Items.Count - 1)];
        }

        public bool CanMoveItemUp => SelectedItem != null && Items.IndexOf(SelectedItem) > 0;
        public void MoveItemUp()
        {
            var index = Items.IndexOf(SelectedItem);
            if (index == 0)
                return;

            Items.Move(index, index - 1);

            NotifyOfPropertyChange(nameof(CanMoveItemDown));
            NotifyOfPropertyChange(nameof(CanMoveItemUp));
        }

        public bool CanMoveItemDown => SelectedItem != null && Items.IndexOf(SelectedItem) < Items.Count - 1;
        public void MoveItemDown()
        {
            var index = Items.IndexOf(SelectedItem);
            if (index == Items.Count - 1)
                return;

            Items.Move(index, index + 1);

            NotifyOfPropertyChange(nameof(CanMoveItemDown));
            NotifyOfPropertyChange(nameof(CanMoveItemUp));
        }
        #endregion

        #region Actions
        public bool CanAddAction => SelectedItem != null;
        public void AddAction() => SelectedItem.Actions.Add(new TipMenuAction());
        public bool CanRemoveAction => SelectedItem != null && SelectedAction != null;
        public void RemoveAction()
        {
            var index = SelectedItem.Actions.IndexOf(SelectedAction);
            SelectedItem.Actions.Remove(SelectedAction);

            if (SelectedItem.Actions.Count == 0)
                SelectedAction = null;
            else
                SelectedAction = SelectedItem.Actions[Math.Min(index, SelectedItem.Actions.Count - 1)];
        }
        #endregion
    }
}
