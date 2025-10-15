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
        private readonly HashSet<Key> _trackedKeys = new();
        private readonly HashSet<Key> _pressed = new();
        private readonly object _pressedLock = new();

        private int? _mouseLeftPanelIndex;
        private int? _mouseRightPanelIndex;
        private PanelState _panelsState = new();

        private int? _mouseMiddlePanelIndex;
        private int? _mouseX1PanelIndex;
        private int? _mouseX2PanelIndex;

        public InputCounter(IOverlay overlay) => _overlay = overlay;

        public void LoadPanels(PanelState state)
        {
            _panelsState = state ?? new PanelState();

            _keyToPanel.Clear();
            _trackedKeys.Clear();
            _mouseLeftPanelIndex = null;
            _mouseRightPanelIndex = null;

            foreach (var (panel, idx) in _panelsState.Panels.Select((p, i) => (p, i)))
            {
                if (panel.Input == InputType.Key && panel.KeyCode != Key.None)
                {
                    var norm = NormalizeKey(panel.KeyCode);
                    if (!_keyToPanel.ContainsKey(norm))
                    {
                        _keyToPanel[norm] = idx;
                        _trackedKeys.Add(norm);
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
        }

        public void ResetSingle(Key KeyCode)
        {
            int i = 0;
            foreach (var k in _keyToPanel.Keys)
            {
                if (k == KeyCode)
                {
                    _panelCounts[i] = 0;
                }
                i++;
            }
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

            if (_trackedKeys.Contains(norm) && _keyToPanel.TryGetValue(norm, out var panelIndex))
                _panelCounts.AddOrUpdate(panelIndex, 1, (_, v) => v + 1);

            if (wpfKey == Key.F12) Reset();
            else if (wpfKey == Key.F11) _overlay.ToggleToolStrip();
        }

        private void OnKeyUp(Key wpfKey)
        {
            var norm = NormalizeKey(wpfKey);
            lock (_pressedLock) { _pressed.Remove(norm); }
        }

        private void OnMouseDown(MouseButton btn)
        {
            switch (btn)
            {
                case MouseButton.Left when _mouseLeftPanelIndex.HasValue: _panelCounts.AddOrUpdate(_mouseLeftPanelIndex.Value, 1, (_, v) => v + 1); break;
                case MouseButton.Right when _mouseRightPanelIndex.HasValue: _panelCounts.AddOrUpdate(_mouseRightPanelIndex.Value, 1, (_, v) => v + 1); break;
                case MouseButton.Middle when _mouseMiddlePanelIndex.HasValue: _panelCounts.AddOrUpdate(_mouseMiddlePanelIndex.Value, 1, (_, v) => v + 1); break;
                case MouseButton.XButton1 when _mouseX1PanelIndex.HasValue: _panelCounts.AddOrUpdate(_mouseX1PanelIndex.Value, 1, (_, v) => v + 1); break;
                case MouseButton.XButton2 when _mouseX2PanelIndex.HasValue: _panelCounts.AddOrUpdate(_mouseX2PanelIndex.Value, 1, (_, v) => v + 1); break;
            }
        }
    }
}
