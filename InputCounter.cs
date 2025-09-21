using Gma.System.MouseKeyHook;
using System.Collections.Concurrent;
using System.Windows.Forms;

namespace ClickyKeys
{

    public class InputCounter(OverlayForm _overlay) : IDisposable
    {
        private IKeyboardMouseEvents? _globalHook;
        private Thread? _hookThread;
        private readonly ManualResetEvent _started = new(false);

        private readonly ConcurrentDictionary<int, int> _panelCounts = new();
        private int _leftClicks;
        private int _rightClicks;

        private int? _mouseLeftPanelIndex;
        private int? _mouseRightPanelIndex;

        private readonly HashSet<Keys> _pressed = new();
        private readonly object _pressedLock = new();

        private readonly Dictionary<Keys, int> _keyToPanel = new();
        private readonly HashSet<Keys> _trackedKeys = new();

        private PanelState _panelsState = new();


        public void LoadPanels(PanelState state)
        {
            _panelsState = state ?? new PanelState();


            _keyToPanel.Clear();
            _trackedKeys.Clear();
            _mouseLeftPanelIndex = null;
            _mouseRightPanelIndex = null;

            foreach (var (panel, idx) in _panelsState.Panels.Select((p, i) => (p, i)))
            {
                bool duplicate = false;
                if (panel.Input == InputType.Key && panel.KeyCode != Keys.None)
                {
                    
                    foreach (var (name, _) in _keyToPanel)
                    {
                        if (name == panel.KeyCode)
                            duplicate = true;
                    }
                    if (duplicate == false)
                    {
                        _keyToPanel[panel.KeyCode] = idx;
                        _trackedKeys.Add(panel.KeyCode);
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
            }
        }

        private static Keys NormalizeKey(Keys k)
        {
            k &= Keys.KeyCode;

            return k switch
            {
                Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey or Keys.Shift => Keys.ShiftKey,
                Keys.LControlKey or Keys.RControlKey or Keys.ControlKey or Keys.Control => Keys.ControlKey,
                Keys.LMenu or Keys.RMenu or Keys.Menu or Keys.Alt => Keys.Menu,
                _ => k
            };
        }

        public void Start()
        {
            if (_globalHook != null || _hookThread != null) return;

            _hookThread = new Thread(() =>
            {
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                    _globalHook = Hook.GlobalEvents();
                    _globalHook.MouseDownExt += OnMouseDown;
                    _globalHook.KeyDown += OnKeyDown;
                    _globalHook.KeyUp += OnKeyUp;

                    _started.Set();
                    Application.Run();
                }
                finally
                {
                    if (_globalHook != null)
                    {
                        _globalHook.MouseDownExt -= OnMouseDown;
                        _globalHook.KeyDown -= OnKeyDown;
                        _globalHook.KeyUp -= OnKeyUp;
                        _globalHook.Dispose();
                        _globalHook = null;
                    }
                }
            });

            _hookThread.IsBackground = true;
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();

            _started.WaitOne(TimeSpan.FromSeconds(5));
        }

        private void OnMouseDown(object? sender, MouseEventExtArgs e)
        {
            if (e.Button == MouseButtons.Left && _mouseLeftPanelIndex.HasValue)
            {
                _panelCounts.AddOrUpdate(_mouseLeftPanelIndex.Value, 1, (_, v) => v + 1);
            }
            else if (e.Button == MouseButtons.Right && _mouseRightPanelIndex.HasValue)
            {
                _panelCounts.AddOrUpdate(_mouseRightPanelIndex.Value, 1, (_, v) => v + 1);
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            lock (_pressedLock)
            {
                if (_pressed.Contains(e.KeyCode)) return; // anti-repeat
                _pressed.Add(e.KeyCode);
            }

            // Counting keys defined in file
            if (_trackedKeys.Contains(NormalizeKey(e.KeyCode)) 
                && _keyToPanel.TryGetValue(NormalizeKey(e.KeyCode), out var panelIndex))
            {
                _panelCounts.AddOrUpdate(panelIndex, 1, (_, v) => v + 1);
            }

            // Special keys
            if (e.KeyCode == Keys.F12) Reset();
            else if (e.KeyCode == Keys.F11) _overlay.ToggleToolStrip();

        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            lock (_pressedLock) { _pressed.Remove(e.KeyCode); }
        }

        public void Reset()
        {
            Interlocked.Exchange(ref _leftClicks, 0);
            Interlocked.Exchange(ref _rightClicks, 0);

            foreach (var key in _panelCounts.Keys.ToList())
                _panelCounts[key] = 0;
        }

        public IReadOnlyList<(string Name, int Value)> GetStats()
        {
            var list = new List<(string, int)>();

            for (int i = 0; i < _panelsState.Panels.Count; i++)
            {
                var p = _panelsState.Panels[i];

                bool isKey = p.Input == InputType.Key && p.KeyCode != Keys.None;
                bool isMouse = p.Input == InputType.MouseLeft || p.Input == InputType.MouseRight;
                if (!isKey && !isMouse) continue;

                var label = !string.IsNullOrWhiteSpace(p.Description)
                    ? p.Description
                    : (isMouse
                        ? (p.Input == InputType.MouseLeft ? "LMB" : "RMB")
                        : p.KeyCode.ToString().ToUpperInvariant());

                var val = _panelCounts.TryGetValue(i, out var v) ? v : 0;
                list.Add((label, val));
            }

            return list;
        }

        public void Dispose()
        {
            try
            {
                try { Application.ExitThread(); } catch { }
                _hookThread?.Join(1000);
            }
            catch { }
        }

    }
}