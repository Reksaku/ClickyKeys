using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ClickyKeys
{
    public sealed class InputCounter : IDisposable
    {
        private readonly IOverlay _overlay;

        private readonly ConcurrentDictionary<int, int> _panelCounts = new();
        private readonly Dictionary<Key, int> _keyToPanel = new();
        private readonly HashSet<Key> _pressed = new();
        private readonly object _pressedLock = new();

        private int? _mouseLeftPanelIndex;
        private int? _mouseRightPanelIndex;
        private PanelState _panelsState = new();

        private int? _mouseMiddlePanelIndex;
        private int? _mouseX1PanelIndex;
        private int? _mouseX2PanelIndex;

        // Global shortcut keys. Defaults match the historical hardcoded values
        // (F12 = reset, F11 = toggle toolbar) and are overridden from config via
        // SetShortcuts. Compared against the raw pressed key in OnKeyDown.
        private Key _resetKey = Key.F12;
        private Key _toggleToolbarKey = Key.F11;

        // Fired on UI thread (low-level hooks pump on the installing thread)
        // whenever a tracked panel's counter changes.
        public event Action<int, int>? PanelValueChanged;

        // Fired when all counters are reset (F12).
        public event Action? CountersReset;

        public InputCounter(IOverlay overlay) => _overlay = overlay;

        /// <summary>
        /// Sets the global shortcut keys (reset / toggle toolbar). Called at
        /// startup from the persisted <see cref="Configuration"/> and live
        /// whenever the user reassigns them in the Settings window. A
        /// <see cref="Key.None"/> value disables that shortcut.
        /// </summary>
        public void SetShortcuts(Key resetKey, Key toggleToolbarKey)
        {
            _resetKey = resetKey;
            _toggleToolbarKey = toggleToolbarKey;
        }

        public void LoadPanels(PanelState state)
        {
            _panelsState = state ?? new PanelState();

            _keyToPanel.Clear();
            _mouseLeftPanelIndex = null;
            _mouseRightPanelIndex = null;
            _mouseMiddlePanelIndex = null;
            _mouseX1PanelIndex = null;
            _mouseX2PanelIndex = null;

            foreach (var (panel, idx) in _panelsState.Panels.Select((p, i) => (p, i)))
            {
                if (panel.Input == InputType.Key && panel.KeyCode != Key.None)
                {
                    var norm = NormalizeKey(panel.KeyCode);
                    if (!_keyToPanel.ContainsKey(norm))
                    {
                        _keyToPanel[norm] = idx;
                        _panelCounts.TryAdd(idx, 0);
                    }
                }
                else if (panel.Input == InputType.MouseLeft)
                {
                    _mouseLeftPanelIndex = idx;
                    _panelCounts.TryAdd(idx, 0);
                }
                else if (panel.Input == InputType.MouseRight)
                {
                    _mouseRightPanelIndex = idx;
                    _panelCounts.TryAdd(idx, 0);
                }
                else if (panel.Input == InputType.MouseMiddle) 
                { 
                    _mouseMiddlePanelIndex = idx; 
                    _panelCounts.TryAdd(idx, 0); 
                }
                else if (panel.Input == InputType.MouseXButton1) 
                { 
                    _mouseX1PanelIndex = idx; 
                    _panelCounts.TryAdd(idx, 0); 
                }
                else if (panel.Input == InputType.MouseXButton2) 
                { 
                    _mouseX2PanelIndex = idx; 
                    _panelCounts.TryAdd(idx, 0); 
                }
            }
        }

        private static Key NormalizeKey(Key key) => key switch
        {
            Key.LeftShift or Key.RightShift => Key.LeftShift,
            Key.LeftCtrl or Key.RightCtrl => Key.LeftCtrl,
            Key.LeftAlt or Key.RightAlt or Key.System => Key.System,
            _ => key
        };

        public void Start()
        {
            if (!GlobalInputHook.Instance.IsRunning)
                GlobalInputHook.Instance.Start();

            GlobalInputHook.Instance.KeyDown += OnKeyDown;
            GlobalInputHook.Instance.KeyUp += OnKeyUp;
            GlobalInputHook.Instance.MouseDown += OnMouseDown;
        }

        public void Reset()
        {
            foreach (var k in _panelCounts.Keys.ToList())
                _panelCounts[k] = 0;
            CountersReset?.Invoke();
        }

        public int GetPanelCount(int panelIndex) =>
            _panelCounts.TryGetValue(panelIndex, out var v) ? v : 0;

        /// <summary>
        /// Zeroes the counter for a specific panel. Previously this method
        /// took a <see cref="Key"/> and iterated <c>_keyToPanel.Keys</c> with
        /// an enumeration index, which — because <c>Dictionary</c> iteration
        /// order is undefined — zeroed the wrong counter. Callers already
        /// know the panel index, so we take it directly.
        /// </summary>
        public void ResetSingle(int panelIndex)
        {
            if (!_panelCounts.ContainsKey(panelIndex))
                return;

            _panelCounts[panelIndex] = 0;
            PanelValueChanged?.Invoke(panelIndex, 0);
        }

        public IReadOnlyList<(Key Code, InputType Input, string Name, int Value)> GetStats()
        {
            var list = new List<(Key, InputType, string, int)>();
            for (int i = 0; i < _panelsState.Panels.Count; i++)
            {
                var p = _panelsState.Panels[i];
                bool isKey = p.Input == InputType.Key && p.KeyCode != Key.None;
                bool isMouse = p.Input == InputType.MouseLeft || p.Input == InputType.MouseRight;

                if (!isKey && !isMouse) continue;

                var label = p.Input switch
                {
                    InputType.MouseLeft => "LMB",
                    InputType.MouseRight => "RMB",
                    InputType.MouseMiddle => "MMB",
                    InputType.MouseXButton1 => "X1",
                    InputType.MouseXButton2 => "X2",
                    _ => string.IsNullOrWhiteSpace(p.Description) ? p.KeyCode.ToString() : p.Description
                };


                var val = _panelCounts.TryGetValue(i, out var v) ? v : 0;
                list.Add((p.KeyCode, p.Input, label, val));
            }
            return list;
        }

        public void Dispose()
        {
            GlobalInputHook.Instance.KeyDown -= OnKeyDown;
            GlobalInputHook.Instance.KeyUp -= OnKeyUp;
            GlobalInputHook.Instance.MouseDown -= OnMouseDown;
        }

        // === Event handlers ===

        private void OnKeyDown(Key wpfKey)
        {
            var norm = NormalizeKey(wpfKey);

            lock (_pressedLock)
            {
                if (_pressed.Contains(norm)) return;
                _pressed.Add(norm);
            }

            if (_keyToPanel.TryGetValue(norm, out var panelIndex))
            {
                var newValue = _panelCounts.AddOrUpdate(panelIndex, 1, (_, v) => v + 1);
                PanelValueChanged?.Invoke(panelIndex, newValue);
            }

            if (_resetKey != Key.None && wpfKey == _resetKey) Reset();
            else if (_toggleToolbarKey != Key.None && wpfKey == _toggleToolbarKey) _overlay.ToggleToolStrip();
        }

        private void OnKeyUp(Key wpfKey)
        {
            var norm = NormalizeKey(wpfKey);
            lock (_pressedLock) { _pressed.Remove(norm); }
        }

        private void OnMouseDown(MouseButton btn)
        {
            int? idx = btn switch
            {
                MouseButton.Left => _mouseLeftPanelIndex,
                MouseButton.Right => _mouseRightPanelIndex,
                MouseButton.Middle => _mouseMiddlePanelIndex,
                MouseButton.XButton1 => _mouseX1PanelIndex,
                MouseButton.XButton2 => _mouseX2PanelIndex,
                _ => null
            };

            if (!idx.HasValue) return;

            var newValue = _panelCounts.AddOrUpdate(idx.Value, 1, (_, v) => v + 1);
            PanelValueChanged?.Invoke(idx.Value, newValue);
        }
    }
}
