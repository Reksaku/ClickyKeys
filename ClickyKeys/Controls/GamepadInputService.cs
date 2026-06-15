using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ClickyKeys
{
    /// <summary>
    /// A rising-edge ("just pressed") button event from a game controller.
    /// </summary>
    public sealed class GamepadButtonEventArgs : EventArgs
    {
        /// <summary>Stable device id (SDL joystick instance id) as text.</summary>
        public string ControllerId { get; }

        /// <summary>Human-readable controller name (from SDL, may be generic).</summary>
        public string ControllerName { get; }

        /// <summary>
        /// Stable button code used as the binding key. Recognized controllers map
        /// to the historical layout-normalized values so existing panel bindings
        /// and statistics keep working (A=4, B=8, X=16, Y=32, D-Pad 64..512,
        /// LB=1024, RB=2048, LS=4096, RS=8192, Menu=1, View=2, Guide=16384).
        /// Unrecognized HID devices use <c>RawButtonCodeBase + rawIndex</c>.
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
    /// Gamepad input source built on <b>SDL2</b> (SDL2.dll via P/Invoke).
    ///
    /// <para>
    /// SDL is used for the widest device coverage: recognized controllers
    /// (Xbox, PlayStation, Nintendo and many third-party pads, via SDL's
    /// game-controller mapping database) report layout-normalized, named
    /// buttons through the <c>SDL_GameController</c> API; anything not in the
    /// database falls back to the raw <c>SDL_Joystick</c> API with
    /// index-numbered buttons ("Button N").
    /// </para>
    ///
    /// <para>
    /// Background input: the <c>SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS</c> hint is
    /// set before init, so input is read regardless of which window has focus —
    /// the whole point of the counter (counting while playing in another app).
    /// </para>
    ///
    /// <para>
    /// A single background thread owns SDL, pumps its events (for hotplug),
    /// polls open devices at ~60 Hz and raises <see cref="ButtonPressed"/> on
    /// each rising edge; it re-enumerates devices about once a second. If
    /// SDL2.dll is missing the service simply disables itself (no crash).
    /// The event is raised on the polling thread — marshal to the UI thread
    /// before touching UI.
    /// </para>
    /// </summary>
    public sealed class GamepadInputService : IDisposable
    {
        public static GamepadInputService Instance { get; } = new();

        private GamepadInputService() { }

        // ---- Embedded native SDL2.dll: extract to the settings folder and
        // load it from there ----

        // Where the native library is unpacked — the same data folder the rest
        // of the app uses for settings (%AppData%\ClickyKeys).
        private static readonly string NativeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickyKeys");

        private static string SdlPath => Path.Combine(NativeDir, "SDL2.dll");

        // Runs once on first access to the type, before any SDL P/Invoke: unpack
        // the embedded SDL2.dll and route "SDL2" imports to the unpacked copy.
        static GamepadInputService()
        {
            try
            {
                ExtractEmbeddedSdl();
                NativeLibrary.SetDllImportResolver(
                    typeof(GamepadInputService).Assembly, ResolveNativeLibrary);
            }
            catch
            {
                // If anything fails the service just won't find SDL2 and
                // disables itself gracefully (see PollLoop init).
            }
        }

        // Writes the embedded SDL2.dll to <see cref="SdlPath"/>, but only when
        // it's missing or a different size (cheap version check), so we don't
        // rewrite a file that may be loaded by this very process.
        private static void ExtractEmbeddedSdl()
        {
            var asm = typeof(GamepadInputService).Assembly;

            // LogicalName makes this "SDL2.dll"; fall back to the default
            // "<namespace>.SDL2.dll" name just in case.
            string? resName = "SDL2.dll";
            if (asm.GetManifestResourceInfo(resName) == null)
                resName = Array.Find(asm.GetManifestResourceNames(),
                    n => n.EndsWith("SDL2.dll", StringComparison.OrdinalIgnoreCase));
            if (resName == null) return; // not embedded — nothing to do

            using var res = asm.GetManifestResourceStream(resName);
            if (res == null) return;

            Directory.CreateDirectory(NativeDir);

            if (File.Exists(SdlPath))
            {
                try { if (new FileInfo(SdlPath).Length == res.Length) return; }
                catch { /* fall through and rewrite */ }
            }

            var tmp = SdlPath + ".tmp";
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                    res.CopyTo(fs);
                File.Copy(tmp, SdlPath, overwrite: true);
            }
            catch
            {
                // Target may be locked by a running instance — keep the existing
                // copy; it's good enough.
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        private static IntPtr ResolveNativeLibrary(
            string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName is "SDL2" or "SDL2.dll")
            {
                try
                {
                    if (File.Exists(SdlPath) && NativeLibrary.TryLoad(SdlPath, out var handle))
                        return handle;
                }
                catch { /* bad image / arch mismatch → fall back */ }
            }
            return IntPtr.Zero; // default resolution for everything else
        }

        /// <summary>Raised on the polling thread when a button is first pressed.</summary>
        public event EventHandler<GamepadButtonEventArgs>? ButtonPressed;

        /// <summary>Base offset for raw (unmapped HID) button codes.</summary>
        public const int RawButtonCodeBase = 1_000_000;

        // Stable button codes (kept equal to the previous values so saved
        // bindings/stats still match).
        private const int CodeMenu = 1, CodeView = 2, CodeA = 4, CodeB = 8,
            CodeX = 16, CodeY = 32, CodeDPadUp = 64, CodeDPadDown = 128,
            CodeDPadLeft = 256, CodeDPadRight = 512, CodeLB = 1024, CodeRB = 2048,
            CodeLS = 4096, CodeRS = 8192, CodeGuide = 16384,
            CodeLT = 32768, CodeRT = 65536; // analog triggers (threshold-based)

        // SDL_GameControllerButton value (0..14, +guide) -> our stable code.
        private static readonly (int SdlButton, int Code)[] GcButtonMap =
        {
            (0, CodeA), (1, CodeB), (2, CodeX), (3, CodeY),
            (4, CodeView),   // BACK
            (5, CodeGuide),  // GUIDE
            (6, CodeMenu),   // START
            (7, CodeLS),     // LEFTSTICK
            (8, CodeRS),     // RIGHTSTICK
            (9, CodeLB),     // LEFTSHOULDER
            (10, CodeRB),    // RIGHTSHOULDER
            (11, CodeDPadUp), (12, CodeDPadDown), (13, CodeDPadLeft), (14, CodeDPadRight),
        };

        private const int GcButtonCount = 15; // SDL buttons 0..14 we track

        private const int SdlTriggerMax = 32767; // SDL trigger axis full-pull value

        // LT/RT activation threshold in SDL axis units (0..32767). volatile so
        // the UI thread can update it while the poll thread reads it.
        private volatile int _triggerThresholdAxis = SdlTriggerMax / 2; // ~50%

        /// <summary>
        /// Sets the LT/RT "counts as a press" threshold as a percentage
        /// (1..100) of a full trigger pull. Live-adjustable from Settings.
        /// </summary>
        public void SetTriggerThresholdPercent(int percent)
        {
            percent = Math.Clamp(percent, 1, 100);
            _triggerThresholdAxis = (int)((long)percent * SdlTriggerMax / 100);
        }

        /// <summary>Friendly label for a button code.</summary>
        public static string FriendlyName(int code)
        {
            if (code >= RawButtonCodeBase)
                return $"Button {code - RawButtonCodeBase}";

            return code switch
            {
                CodeA => "A",
                CodeB => "B",
                CodeX => "X",
                CodeY => "Y",
                CodeDPadUp => "D-Pad Up",
                CodeDPadDown => "D-Pad Down",
                CodeDPadLeft => "D-Pad Left",
                CodeDPadRight => "D-Pad Right",
                CodeLB => "LB",
                CodeRB => "RB",
                CodeLS => "LS (Click)",
                CodeRS => "RS (Click)",
                CodeMenu => "Menu",
                CodeView => "View",
                CodeGuide => "Guide",
                CodeLT => "LT",
                CodeRT => "RT",
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

        private const int ActiveIntervalMs = 16;   // ~60 Hz while devices present
        private const int IdleIntervalMs = 200;     // when nothing is connected
        private const long RescanIntervalMs = 1000; // (re)enumerate devices ~1/s

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
            try { _thread?.Join(TimeSpan.FromMilliseconds(800)); }
            catch { /* ignore */ }
            _thread = null;
        }

        public void Dispose() => Stop();

        private sealed class OpenDevice
        {
            public IntPtr Handle;
            public bool IsController;
            public bool[] Prev = Array.Empty<bool>();
            public bool PrevLT;   // analog trigger edge state
            public bool PrevRT;
            public string Name = "Controller";
        }

        private void PollLoop()
        {
            // Open devices keyed by SDL instance id. Touched only on this thread.
            var open = new Dictionary<int, OpenDevice>();

            // ---- Init SDL (joystick + game controller). Disable gracefully if
            // SDL2.dll is missing or init fails. ----
            try
            {
                SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
                if (SDL_Init(SDL_INIT_GAMECONTROLLER) != 0)
                    return; // init failed — give up quietly
            }
            catch
            {
                return; // SDL2.dll not present / not loadable
            }

            try
            {
                long lastScan = 0;

                while (_running)
                {
                    // Pump SDL events so device add/remove (hotplug) and the
                    // device list stay current; also refreshes controller state.
                    while (SDL_PollEvent(out _) != 0) { }
                    SDL_GameControllerUpdate();

                    long now = Environment.TickCount64;
                    if (now - lastScan >= RescanIntervalMs)
                    {
                        lastScan = now;
                        Reenumerate(open);
                    }

                    foreach (var dev in open.Values)
                        ReadDevice(dev);

                    Thread.Sleep(open.Count > 0 ? ActiveIntervalMs : IdleIntervalMs);
                }
            }
            catch
            {
                // Never let a hardware hiccup crash the app.
            }
            finally
            {
                foreach (var dev in open.Values)
                    CloseDevice(dev);
                open.Clear();
                try { SDL_Quit(); } catch { /* ignore */ }
            }
        }

        // Opens newly connected devices and closes removed ones.
        private static void Reenumerate(Dictionary<int, OpenDevice> open)
        {
            int count;
            try { count = SDL_NumJoysticks(); }
            catch { return; }

            var seen = new HashSet<int>();

            for (int idx = 0; idx < count; idx++)
            {
                int iid = SDL_JoystickGetDeviceInstanceID(idx);
                if (iid < 0) continue;
                seen.Add(iid);

                if (open.ContainsKey(iid))
                    continue;

                if (SDL_IsGameController(idx) != 0)
                {
                    var h = SDL_GameControllerOpen(idx);
                    if (h != IntPtr.Zero)
                        open[iid] = new OpenDevice
                        {
                            Handle = h,
                            IsController = true,
                            Prev = new bool[GcButtonCount],
                            Name = PtrToString(SDL_GameControllerName(h), "Controller")
                        };
                }
                else
                {
                    var h = SDL_JoystickOpen(idx);
                    if (h != IntPtr.Zero)
                    {
                        int nb = Math.Max(0, SDL_JoystickNumButtons(h));
                        open[iid] = new OpenDevice
                        {
                            Handle = h,
                            IsController = false,
                            Prev = new bool[nb],
                            Name = PtrToString(SDL_JoystickName(h), "Controller")
                        };
                    }
                }
            }

            // Close+forget devices that are gone.
            if (open.Count > 0)
            {
                List<int>? stale = null;
                foreach (var kvp in open)
                    if (!seen.Contains(kvp.Key))
                        (stale ??= new List<int>()).Add(kvp.Key);
                if (stale != null)
                    foreach (var id in stale)
                    {
                        CloseDevice(open[id]);
                        open.Remove(id);
                    }
            }
        }

        private void ReadDevice(OpenDevice dev)
        {
            if (dev.IsController)
            {
                if (SDL_GameControllerGetAttached(dev.Handle) == 0) return;

                foreach (var (sdlButton, code) in GcButtonMap)
                {
                    bool cur = SDL_GameControllerGetButton(dev.Handle, sdlButton) != 0;
                    bool prev = dev.Prev[sdlButton];
                    if (cur && !prev)
                        ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(
                            dev.Name, dev.Name, code, FriendlyName(code)));
                    dev.Prev[sdlButton] = cur;
                }

                // Analog triggers: count a press when the pull crosses the
                // configurable threshold (rising edge from below to above).
                int threshold = _triggerThresholdAxis;

                bool ltCur = SDL_GameControllerGetAxis(dev.Handle, SDL_CONTROLLER_AXIS_TRIGGERLEFT) >= threshold;
                if (ltCur && !dev.PrevLT)
                    ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(
                        dev.Name, dev.Name, CodeLT, FriendlyName(CodeLT)));
                dev.PrevLT = ltCur;

                bool rtCur = SDL_GameControllerGetAxis(dev.Handle, SDL_CONTROLLER_AXIS_TRIGGERRIGHT) >= threshold;
                if (rtCur && !dev.PrevRT)
                    ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(
                        dev.Name, dev.Name, CodeRT, FriendlyName(CodeRT)));
                dev.PrevRT = rtCur;
            }
            else
            {
                if (SDL_JoystickGetAttached(dev.Handle) == 0) return;

                for (int i = 0; i < dev.Prev.Length; i++)
                {
                    bool cur = SDL_JoystickGetButton(dev.Handle, i) != 0;
                    bool prev = dev.Prev[i];
                    if (cur && !prev)
                    {
                        int code = RawButtonCodeBase + i;
                        ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(
                            dev.Name, dev.Name, code, FriendlyName(code)));
                    }
                    dev.Prev[i] = cur;
                }
            }
        }

        private static void CloseDevice(OpenDevice dev)
        {
            try
            {
                if (dev.IsController) SDL_GameControllerClose(dev.Handle);
                else SDL_JoystickClose(dev.Handle);
            }
            catch { /* ignore */ }
        }

        private static string PtrToString(IntPtr p, string fallback)
        {
            try
            {
                var s = Marshal.PtrToStringUTF8(p);
                return string.IsNullOrWhiteSpace(s) ? fallback : s!;
            }
            catch { return fallback; }
        }

        // ---- SDL2 interop (SDL2.dll, cdecl) ----

        private const string LIB = "SDL2";
        private const uint SDL_INIT_GAMECONTROLLER = 0x00002000u; // implies JOYSTICK + EVENTS

        // 56-byte opaque event blob — we pump but don't inspect events.
        [StructLayout(LayoutKind.Explicit, Size = 56)]
        private struct SDL_Event { }

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_Init(uint flags);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_Quit();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_SetHint(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_PollEvent(out SDL_Event e);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_GameControllerUpdate();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_NumJoysticks();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_JoystickGetDeviceInstanceID(int device_index);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_IsGameController(int joystick_index);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GameControllerOpen(int joystick_index);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_GameControllerClose(IntPtr gamecontroller);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte SDL_GameControllerGetButton(IntPtr gamecontroller, int button);

        // SDL_GameControllerAxis: TRIGGERLEFT = 4, TRIGGERRIGHT = 5 (0..32767).
        private const int SDL_CONTROLLER_AXIS_TRIGGERLEFT = 4;
        private const int SDL_CONTROLLER_AXIS_TRIGGERRIGHT = 5;

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern short SDL_GameControllerGetAxis(IntPtr gamecontroller, int axis);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_GameControllerGetAttached(IntPtr gamecontroller);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GameControllerName(IntPtr gamecontroller);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_JoystickOpen(int device_index);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_JoystickClose(IntPtr joystick);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_JoystickNumButtons(IntPtr joystick);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte SDL_JoystickGetButton(IntPtr joystick, int button);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_JoystickGetAttached(IntPtr joystick);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_JoystickName(IntPtr joystick);
    }
}
