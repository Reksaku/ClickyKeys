using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ClickyKeys
{
    public sealed class GlobalInputHook : IDisposable
    {
        public static GlobalInputHook Instance { get; } = new();

        private GlobalInputHook() { }

        // ==== Public API ====
        public event Action<Key>? KeyDown;
        public event Action<Key>? KeyUp;
        public event Action<MouseButton>? MouseDown;
        public event Action<InputType>? Wheel;


        public bool IsRunning => _hKeyboard != IntPtr.Zero || _hMouse != IntPtr.Zero;

        public void Start()
        {
            if (IsRunning) return;

            _kbProc = KeyboardHookCallback;
            _msProc = MouseHookCallback;

            _hKeyboard = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc!, GetModuleHandle(IntPtr.Zero), 0);
            _hMouse = SetWindowsHookEx(WH_MOUSE_LL, _msProc!, GetModuleHandle(IntPtr.Zero), 0);

            if (_hKeyboard == IntPtr.Zero || _hMouse == IntPtr.Zero)
            {
                Stop();
                throw new InvalidOperationException("Keyboard/mouse hooks initialization error.");
            }
        }

        public void Stop()
        {
            if (_hKeyboard != IntPtr.Zero) { UnhookWindowsHookEx(_hKeyboard); _hKeyboard = IntPtr.Zero; }
            if (_hMouse != IntPtr.Zero) { UnhookWindowsHookEx(_hMouse); _hMouse = IntPtr.Zero; }
            _kbProc = null;
            _msProc = null;
        }

        public void Dispose() => Stop();

        
        public Task<Key> CaptureNextKeyPressAsync(
            Func<Key, bool>? filter = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            => CaptureOneShotAsync(filter ?? (_ => true), timeout, cancellationToken, addKey: true);

        
        public Task<MouseButton> CaptureNextMouseButtonAsync(
            Func<MouseButton, bool>? filter = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            => CaptureOneShotAsync(filter ?? (_ => true), timeout, cancellationToken, addMouse: true);


        public Task<(bool IsKey, Key Key, MouseButton MouseButton)> CaptureNextInputAsync(
            Func<Key, bool>? keyFilter = null,
            Func<MouseButton, bool>? mouseFilter = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<(bool, Key, MouseButton)>(TaskCreationOptions.RunContinuationsAsynchronously);

            void KD(Key k)
            {
                if (keyFilter != null && !keyFilter(k)) return;
                Unsubscribe();
                tcs.TrySetResult((true, k, default));
            }
            void MD(MouseButton b)
            {
                if (mouseFilter != null && !mouseFilter(b)) return;
                Unsubscribe();
                tcs.TrySetResult((false, default, b));
            }
            void Unsubscribe()
            {
                KeyDown -= KD;
                MouseDown -= MD;
                _mixedGuards.Remove(tcs);
            }

            _mixedGuards.Add(tcs, (KD, MD));
            KeyDown += KD;
            MouseDown += MD;

            RegisterTimeoutOrCancel(timeout, cancellationToken, () =>
            {
                Unsubscribe();
                tcs.TrySetCanceled(cancellationToken);
            });

            return tcs.Task;
        }

        // ==== Hooks implementation ====

        private static Key NormalizeKey(Key key) => key switch
        {
            Key.LeftShift or Key.RightShift => Key.LeftShift,
            Key.LeftCtrl or Key.RightCtrl => Key.LeftCtrl,
            Key.LeftAlt or Key.RightAlt or Key.System => Key.System,
            _ => key
        };

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var msg = (KeyboardMessage)wParam;
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var wpfKey = KeyInterop.KeyFromVirtualKey((int)data.vkCode);
                var norm = NormalizeKey(wpfKey);

                if (msg is KeyboardMessage.WM_KEYDOWN or KeyboardMessage.WM_SYSKEYDOWN)
                    KeyDown?.Invoke(norm);
                else if (msg is KeyboardMessage.WM_KEYUP or KeyboardMessage.WM_SYSKEYUP)
                    KeyUp?.Invoke(norm);
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var msg = (MouseMessage)wParam;

                if (msg == MouseMessage.WM_LBUTTONDOWN) MouseDown?.Invoke(MouseButton.Left);
                else if (msg == MouseMessage.WM_RBUTTONDOWN) MouseDown?.Invoke(MouseButton.Right);
                else if (msg == MouseMessage.WM_MBUTTONDOWN) MouseDown?.Invoke(MouseButton.Middle);
                else if (msg == MouseMessage.WM_XBUTTONDOWN)
                {
                    var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    var hiword = (ms.mouseData >> 16) & 0xFFFF;
                    var btn = hiword == 1 ? MouseButton.XButton1 :
                              hiword == 2 ? MouseButton.XButton2 : MouseButton.XButton1;
                    MouseDown?.Invoke(btn);
                }
                else if ((int)wParam == WM_MOUSEWHEEL)
                {
                    var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    short delta = (short)((ms.mouseData >> 16) & 0xFFFF);
                    if (delta > 0) Wheel?.Invoke(InputType.MouseWheelUp);
                    else if (delta < 0) Wheel?.Invoke(InputType.MouseWheelDown);
                }
                else if ((int)wParam == WM_MOUSEHWHEEL)
                {
                    var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    short delta = (short)((ms.mouseData >> 16) & 0xFFFF);
                    if (delta > 0) Wheel?.Invoke(InputType.MouseWheelRight);
                    else if (delta < 0) Wheel?.Invoke(InputType.MouseWheelLeft);
                }

            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // ==== one-shot + timeout/cancel ====

        private Task<T> CaptureOneShotAsync<T>(
            Func<T, bool> filter,
            TimeSpan? timeout,
            CancellationToken cancellationToken,
            bool addKey = false,
            bool addMouse = false)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            Action<Key>? kd = null;
            Action<MouseButton>? md = null;

            void Cleanup()
            {
                if (kd != null) KeyDown -= kd;
                if (md != null) MouseDown -= md;
                _oneShotGuards.Remove(tcs);
            }

            if (addKey)
            {
                kd = k =>
                {
                    if (k is T asT && filter((T)(object)k))
                    {
                        Cleanup();
                        tcs.TrySetResult((T)(object)k);
                    }
                };
                KeyDown += kd;
            }
            if (addMouse)
            {
                md = b =>
                {
                    if (b is T asT && filter((T)(object)b))
                    {
                        Cleanup();
                        tcs.TrySetResult((T)(object)b);
                    }
                };
                MouseDown += md;
            }

            _oneShotGuards.Add(tcs, (kd, md));

            RegisterTimeoutOrCancel(timeout, cancellationToken, () =>
            {
                Cleanup();
                tcs.TrySetCanceled(cancellationToken);
            });

            return tcs.Task;
        }

        private static void RegisterTimeoutOrCancel(TimeSpan? timeout, CancellationToken ct, Action onCancel)
        {
            CancellationToken token = ct;
            CancellationTokenSource? linked = null;
            if (timeout is TimeSpan to)
            {
                linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(to);
                token = linked.Token;
            }
            if (token.CanBeCanceled)
            {
                token.Register(() =>
                {
                    onCancel();
                    linked?.Dispose();
                });
            }
        }

        
        private IntPtr _hKeyboard = IntPtr.Zero;
        private IntPtr _hMouse = IntPtr.Zero;
        private LowLevelProc? _kbProc;
        private LowLevelProc? _msProc;

        
        private readonly Dictionary<object, (Action<Key>? kd, Action<MouseButton>? md)> _oneShotGuards = new();
        private readonly Dictionary<object, (Action<Key> kd, Action<MouseButton> md)> _mixedGuards = new();

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;

        private enum KeyboardMessage : int
        {
            WM_KEYDOWN = 0x0100,
            WM_KEYUP = 0x0101,
            WM_SYSKEYDOWN = 0x0104,
            WM_SYSKEYUP = 0x0105
        }

        private enum MouseMessage : int
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_RBUTTONDOWN = 0x0204,
            WM_MBUTTONDOWN = 0x0207,
            WM_XBUTTONDOWN = 0x020B
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(IntPtr lpModuleName);
    }
}
