using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;

namespace ClickyKeys
{
    /// <summary>
    /// A rising-edge ("just pressed") button event from a game controller.
    /// </summary>
    public sealed class GamepadButtonEventArgs : EventArgs
    {
        /// <summary>Stable per-device id (RawGameController.NonRoamableId).</summary>
        public string ControllerId { get; }

        /// <summary>Human-readable controller name (may be generic).</summary>
        public string ControllerName { get; }

        /// <summary>
        /// Stable button code used as the binding key. For controllers Windows
        /// exposes as a standard gamepad this is a <see cref="GamepadButtons"/>
        /// bit value (layout-normalized: "A" is always the bottom face button,
        /// etc.). For unrecognized HID controllers it is
        /// <c>RawButtonCodeBase + rawIndex</c>. See
        /// <see cref="GamepadInputService.FriendlyName"/>.
        /// </summary>
        public int ButtonCode { get; }

        /// <summary>Friendly label for <see cref="ButtonCode"/> (e.g. "A", "LB").</summary>
        public string ButtonName { get; }

        public GamepadButtonEventArgs(string controllerId, string controllerName, int buttonCode, string buttonName)
        {
            ControllerId = controllerId;
            ControllerName = controllerName;
            ButtonCode = buttonCode;
            ButtonName = buttonName;
        }
    }

    /// <summary>
    /// Gamepad input source built on <c>Windows.Gaming.Input</c>.
    ///
    /// <para>
    /// Controllers Windows recognizes as a standard gamepad (Xbox, PS4/PS5 and
    /// most Xbox-layout pads) are read through the <see cref="Gamepad"/> facade,
    /// which normalizes the physical layout: the same <see cref="GamepadButtons"/>
    /// value always means the same position regardless of brand. That makes the
    /// button names universal (Xbox-style labels; the position is the same on a
    /// PlayStation pad even though it's physically ✕/○/□/△ there).
    /// </para>
    ///
    /// <para>
    /// Controllers NOT exposed as a standard gamepad (exotic HID devices) fall
    /// back to <see cref="RawGameController"/> raw button indices, labelled
    /// "Button N" — there is no universal naming for arbitrary HID layouts.
    /// </para>
    ///
    /// <para>
    /// Buttons are not delivered through the keyboard/mouse hooks; a single
    /// background thread polls at ~60 Hz and raises <see cref="ButtonPressed"/>
    /// on each rising edge. Cost is negligible while connected and effectively
    /// zero when nothing is plugged in (the loop idles at 4 Hz). Reads work
    /// regardless of window focus, so background counting keeps working while
    /// minimized. The event is raised on the polling thread — marshal to the UI
    /// thread before touching UI.
    /// </para>
    /// </summary>
    public sealed class GamepadInputService : IDisposable
    {
        /// <summary>
        /// Process-wide instance, mirroring <see cref="GlobalInputHook.Instance"/>.
        /// </summary>
        public static GamepadInputService Instance { get; } = new();

        private GamepadInputService() { }

        /// <summary>Raised on the polling thread when a button is first pressed.</summary>
        public event EventHandler<GamepadButtonEventArgs>? ButtonPressed;

        /// <summary>
        /// Raw (fallback) button codes start here so they never collide with the
        /// small <see cref="GamepadButtons"/> bit values (max 0x20000).
        /// </summary>
        public const int RawButtonCodeBase = 1_000_000;

        // Standard, layout-normalized buttons we surface from the Gamepad facade.
        // Triggers are analog and intentionally excluded (a press threshold could
        // be added later).
        private static readonly GamepadButtons[] StdButtons =
        {
            GamepadButtons.A, GamepadButtons.B, GamepadButtons.X, GamepadButtons.Y,
            GamepadButtons.DPadUp, GamepadButtons.DPadDown,
            GamepadButtons.DPadLeft, GamepadButtons.DPadRight,
            GamepadButtons.LeftShoulder, GamepadButtons.RightShoulder,
            GamepadButtons.LeftThumbstick, GamepadButtons.RightThumbstick,
            GamepadButtons.Menu, GamepadButtons.View,
            GamepadButtons.Paddle1, GamepadButtons.Paddle2,
            GamepadButtons.Paddle3, GamepadButtons.Paddle4,
        };

        /// <summary>Friendly label for a button code.</summary>
        public static string FriendlyName(int code)
        {
            if (code >= RawButtonCodeBase)
                return $"Button {code - RawButtonCodeBase}";

            return (GamepadButtons)code switch
            {
                GamepadButtons.A => "A",
                GamepadButtons.B => "B",
                GamepadButtons.X => "X",
                GamepadButtons.Y => "Y",
                GamepadButtons.DPadUp => "D-Pad Up",
                GamepadButtons.DPadDown => "D-Pad Down",
                GamepadButtons.DPadLeft => "D-Pad Left",
                GamepadButtons.DPadRight => "D-Pad Right",
                GamepadButtons.LeftShoulder => "LB",
                GamepadButtons.RightShoulder => "RB",
                GamepadButtons.LeftThumbstick => "LS (Click)",
                GamepadButtons.RightThumbstick => "RS (Click)",
                GamepadButtons.Menu => "Menu",
                GamepadButtons.View => "View",
                GamepadButtons.Paddle1 => "Paddle 1",
                GamepadButtons.Paddle2 => "Paddle 2",
                GamepadButtons.Paddle3 => "Paddle 3",
                GamepadButtons.Paddle4 => "Paddle 4",
                _ => $"Button {code}"
            };
        }

        /// <summary>
        /// Resolves with the next pressed button's code. Used by the panel
        /// editor to capture a binding. Ensures polling is running. Honors an
        /// optional timeout / cancellation.
        /// </summary>
        public Task<int> CaptureNextButtonAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<GamepadButtonEventArgs>? handler = null;
            handler = (_, e) =>
            {
                ButtonPressed -= handler;
                tcs.TrySetResult(e.ButtonCode);
            };
            ButtonPressed += handler;

            Start();

            CancellationToken token = cancellationToken;
            CancellationTokenSource? linked = null;
            if (timeout is TimeSpan to)
            {
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(to);
                token = linked.Token;
            }
            if (token.CanBeCanceled)
            {
                token.Register(() =>
                {
                    ButtonPressed -= handler;
                    tcs.TrySetCanceled(token);
                    linked?.Dispose();
                });
            }

            return tcs.Task;
        }

        // ~60 Hz while a controller is connected; lazy 4 Hz heartbeat otherwise.
        private static readonly TimeSpan ActiveInterval = TimeSpan.FromMilliseconds(16);
        private static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(250);

        // Previous per-controller state for edge detection, keyed by id. A given
        // controller lives in exactly one of these depending on whether it's a
        // standard gamepad (button bitmask) or a raw HID device (per-index bools).
        private readonly Dictionary<string, uint> _prevStd = new();
        private readonly Dictionary<string, bool[]> _prevRaw = new();

        private Thread? _thread;
        private volatile bool _running;

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "GamepadPoll"
            };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _thread?.Join(TimeSpan.FromMilliseconds(500)); }
            catch { /* ignore */ }
            _thread = null;
            _prevStd.Clear();
            _prevRaw.Clear();
        }

        public void Dispose() => Stop();

        private void PollLoop()
        {
            // Scratch buffers for the raw path (resized per controller).
            GameControllerSwitchPosition[] switches = Array.Empty<GameControllerSwitchPosition>();
            double[] axes = Array.Empty<double>();

            while (_running)
            {
                IReadOnlyList<RawGameController> controllers;
                try { controllers = RawGameController.RawGameControllers; }
                catch { controllers = Array.Empty<RawGameController>(); }

                if (controllers.Count == 0)
                {
                    if (_prevStd.Count > 0) _prevStd.Clear();
                    if (_prevRaw.Count > 0) _prevRaw.Clear();
                    Thread.Sleep(IdleInterval);
                    continue;
                }

                var seen = new HashSet<string>();

                foreach (var controller in controllers)
                {
                    string id;
                    try { id = controller.NonRoamableId ?? string.Empty; }
                    catch { continue; }
                    if (id.Length == 0) continue;
                    seen.Add(id);

                    string name = SafeName(controller);

                    // Prefer the normalized Gamepad facade when available.
                    Gamepad? gamepad = null;
                    try { gamepad = Gamepad.FromGameController(controller); }
                    catch { gamepad = null; }

                    if (gamepad != null)
                        PollStandard(id, name, gamepad);
                    else
                        PollRaw(id, name, controller, ref switches, ref axes);
                }

                PruneUnseen(_prevStd, seen);
                PruneUnseen(_prevRaw, seen);

                Thread.Sleep(ActiveInterval);
            }
        }

        private void PollStandard(string id, string name, Gamepad gamepad)
        {
            uint cur;
            try { cur = (uint)gamepad.GetCurrentReading().Buttons; }
            catch { return; }

            // A device can switch representation; drop any stale raw state.
            _prevRaw.Remove(id);

            if (!_prevStd.TryGetValue(id, out var prev))
            {
                // Seed without firing so a button already held at connect time
                // isn't treated as a press.
                _prevStd[id] = cur;
                return;
            }

            foreach (var button in StdButtons)
            {
                uint bit = (uint)button;
                if ((cur & bit) != 0 && (prev & bit) == 0)
                {
                    int code = (int)bit;
                    ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(id, name, code, FriendlyName(code)));
                }
            }

            _prevStd[id] = cur;
        }

        private void PollRaw(string id, string name, RawGameController controller,
            ref GameControllerSwitchPosition[] switches, ref double[] axes)
        {
            int buttonCount = controller.ButtonCount;
            if (buttonCount <= 0) return;

            var buttons = new bool[buttonCount];
            if (switches.Length != controller.SwitchCount)
                switches = new GameControllerSwitchPosition[controller.SwitchCount];
            if (axes.Length != controller.AxisCount)
                axes = new double[controller.AxisCount];

            try { controller.GetCurrentReading(buttons, switches, axes); }
            catch { return; }

            _prevStd.Remove(id);

            if (!_prevRaw.TryGetValue(id, out var prev) || prev.Length != buttonCount)
            {
                _prevRaw[id] = (bool[])buttons.Clone();
                return;
            }

            for (int b = 0; b < buttonCount; b++)
            {
                if (buttons[b] && !prev[b])
                {
                    int code = RawButtonCodeBase + b;
                    ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(id, name, code, FriendlyName(code)));
                }
            }

            Array.Copy(buttons, prev, buttonCount);
        }

        private static void PruneUnseen<TValue>(Dictionary<string, TValue> map, HashSet<string> seen)
        {
            if (map.Count == 0) return;
            List<string>? stale = null;
            foreach (var key in map.Keys)
                if (!seen.Contains(key)) (stale ??= new List<string>()).Add(key);
            if (stale != null)
                foreach (var key in stale) map.Remove(key);
        }

        private static string SafeName(RawGameController controller)
        {
            try
            {
                return string.IsNullOrWhiteSpace(controller.DisplayName)
                    ? "Controller"
                    : controller.DisplayName;
            }
            catch { return "Controller"; }
        }
    }
}
