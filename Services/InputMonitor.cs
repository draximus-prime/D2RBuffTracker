using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using D2RBuffTracker.Models;

namespace D2RBuffTracker.Services;

/// <summary>
/// Installs global low-level keyboard and mouse hooks and wraps the gamepad
/// poller, raising a single unified <see cref="Pressed"/> event carrying an
/// <see cref="InputBinding"/>. The same stream powers both live tracking and
/// the responsive "press to bind" capture experience.
/// </summary>
public sealed class InputMonitor : IDisposable
{
    // Raised on the UI thread's message pump for keyboard/mouse, and on the
    // poller thread for gamepad. Consumers should marshal as needed.
    public event Action<InputBinding>? Pressed;

    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WmXButtonDown = 0x020B;

    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;
    private readonly GamepadPoller _gamepad;

    // Virtual keys currently held down, so held-key auto-repeat (which fires
    // repeated WM_KEYDOWN messages) only raises a single press per keystroke.
    private readonly HashSet<int> _keysDown = new();

    // Roots hook delegates that we failed to uninstall. UnhookWindowsHookEx
    // essentially never fails, but if it did the native hook would still be live
    // holding a raw pointer to our delegate; keeping the delegate referenced here
    // for the process lifetime guarantees the OS can never call a collected one.
    private static readonly List<HookProc> LeakedProcs = new();

    /// <param name="windowHandle">
    /// Top-level window handle used to give the gamepad poller a background
    /// cooperative level, so controller input is read even while the game has
    /// focus. Pass <see cref="IntPtr.Zero"/> to use the default foreground level.
    /// </param>
    public InputMonitor(IntPtr windowHandle = default)
    {
        _gamepad = new GamepadPoller(windowHandle);
    }

    public void Start()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;

        using var module = Process.GetCurrentProcess().MainModule!;
        var hModule = GetModuleHandle(module.ModuleName);

        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, hModule, 0);
        if (_keyboardHook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install keyboard hook.");

        try
        {
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, hModule, 0);
            if (_mouseHook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");

            _gamepad.ButtonPressed += OnGamepadButton;
            _gamepad.Start();
        }
        catch
        {
            // Roll back any partially installed hooks so a failed Start never
            // leaks a dangling global hook.
            Dispose();
            throw;
        }
    }

    private void OnGamepadButton(int index, string name)
        => Raise(new InputBinding(InputKind.Gamepad, index, name));

    private void Raise(InputBinding binding)
    {
        try { Pressed?.Invoke(binding); }
        catch (Exception ex) { Logger.Log(ex); }
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            if (msg == WmKeyDown || msg == WmSysKeyDown)
            {
                try
                {
                    var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                    var vk = data.VkCode;
                    // Ignore auto-repeat: only the first down of a held key fires.
                    if (_keysDown.Add(vk))
                        Raise(new InputBinding(InputKind.Keyboard, vk, KeyNames.ForVirtualKey(vk)));
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
            else if (msg == WmKeyUp || msg == WmSysKeyUp)
            {
                try
                {
                    var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                    _keysDown.Remove(data.VkCode);
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                switch ((int)wParam)
                {
                    case WmLButtonDown:
                        Raise(new InputBinding(InputKind.Mouse, 0, "Mouse 1"));
                        break;
                    case WmRButtonDown:
                        Raise(new InputBinding(InputKind.Mouse, 1, "Mouse 2"));
                        break;
                    case WmMButtonDown:
                        Raise(new InputBinding(InputKind.Mouse, 2, "Mouse 3"));
                        break;
                    case WmXButtonDown:
                        var data = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
                        var which = (data.MouseData >> 16) & 0xFFFF;
                        Raise(which == 1
                            ? new InputBinding(InputKind.Mouse, 3, "Mouse 4")
                            : new InputBinding(InputKind.Mouse, 4, "Mouse 5"));
                        break;
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        try { _gamepad.ButtonPressed -= OnGamepadButton; _gamepad.Dispose(); } catch { /* ignore */ }

        // Only clear the handle and drop the delegate root once the OS confirms
        // the hook is removed. If unhooking fails the native hook is still live,
        // so the delegate must stay rooted or the OS could call a collected one.
        if (_keyboardHook != IntPtr.Zero)
        {
            if (UnhookWindowsHookEx(_keyboardHook))
            {
                _keyboardHook = IntPtr.Zero;
                _keyboardProc = null;
            }
            else
            {
                Logger.Log($"Failed to remove keyboard hook (error {Marshal.GetLastWin32Error()}).");
                RootLeakedProc(ref _keyboardProc);
            }
        }

        if (_mouseHook != IntPtr.Zero)
        {
            if (UnhookWindowsHookEx(_mouseHook))
            {
                _mouseHook = IntPtr.Zero;
                _mouseProc = null;
            }
            else
            {
                Logger.Log($"Failed to remove mouse hook (error {Marshal.GetLastWin32Error()}).");
                RootLeakedProc(ref _mouseProc);
            }
        }
    }

    private static void RootLeakedProc(ref HookProc? proc)
    {
        if (proc == null)
            return;
        lock (LeakedProcs)
            LeakedProcs.Add(proc);
        proc = null;
    }

    #region Native

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public int VkCode;
        public int ScanCode;
        public int Flags;
        public int Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct
    {
        public int X;
        public int Y;
        public int MouseData;
        public int Flags;
        public int Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    #endregion
}
