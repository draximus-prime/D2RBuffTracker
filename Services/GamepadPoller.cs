using System.Diagnostics;
using Vortice.DirectInput;
using Vortice.XInput;

namespace D2RBuffTracker.Services;

/// <summary>
/// Polls the first attached controller on a background thread and raises an
/// event when a button transitions to pressed.
/// <para>
/// XInput is preferred because it reads Xbox-style controllers globally,
/// regardless of which window has focus, so tracking works while the game is
/// the foreground application. DirectInput (which cannot read Xbox pads in the
/// background) is used only as a fallback for non-XInput devices.
/// </para>
/// </summary>
public sealed class GamepadPoller : IDisposable
{
    // Stable button-index scheme for XInput controllers. The index becomes the
    // binding's Code, so this ordering must stay fixed for saved bindings.
    private static readonly (GamepadButtons Flag, string Name)[] XInputButtons =
    {
        (GamepadButtons.A, "A"),
        (GamepadButtons.B, "B"),
        (GamepadButtons.X, "X"),
        (GamepadButtons.Y, "Y"),
        (GamepadButtons.LeftShoulder, "LB"),
        (GamepadButtons.RightShoulder, "RB"),
        (GamepadButtons.Back, "Back"),
        (GamepadButtons.Start, "Start"),
        (GamepadButtons.LeftThumb, "LS"),
        (GamepadButtons.RightThumb, "RS"),
        (GamepadButtons.DPadUp, "D-Pad Up"),
        (GamepadButtons.DPadDown, "D-Pad Down"),
        (GamepadButtons.DPadLeft, "D-Pad Left"),
        (GamepadButtons.DPadRight, "D-Pad Right"),
        (GamepadButtons.Guide, "Guide"),
    };

    private const int LeftTriggerCode = 100;
    private const int RightTriggerCode = 101;
    private const byte TriggerThreshold = 64;

    private readonly IntPtr _windowHandle;
    private Thread? _thread;
    private volatile bool _running;

    private enum Backend { None, XInput, DirectInput }
    private Backend _backend = Backend.None;

    // XInput state.
    private int _xinputUserIndex = -1;
    private readonly Dictionary<int, bool> _xPrevious = new();

    // DirectInput fallback state.
    private IDirectInput8? _directInput;
    private IDirectInputDevice8? _joystick;
    private bool[] _diPrevious = Array.Empty<bool>();

    /// <param name="windowHandle">
    /// Top-level window handle used to give the DirectInput fallback a
    /// background cooperative level. Not needed for XInput, which is always
    /// global. Pass <see cref="IntPtr.Zero"/> to skip it.
    /// </param>
    public GamepadPoller(IntPtr windowHandle = default) => _windowHandle = windowHandle;

    /// <summary>Raised with the button's Code and display name when it is pressed.</summary>
    public event Action<int, string>? ButtonPressed;

    public void Start()
    {
        if (_running)
            return;

        _running = true;
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "GamepadPoller" };
        _thread.Start();
    }

    /// <summary>
    /// Single supervisor loop: selects a controller backend, polls it, and — when
    /// no controller is present or the current one is unplugged — periodically
    /// rescans so a controller connected after start-up is picked up (hot-plug).
    /// </summary>
    private void RunLoop()
    {
        try
        {
            var lastScan = DateTime.MinValue;
            while (_running)
            {
                if (_backend == Backend.None)
                {
                    // Rescan roughly once a second while nothing is connected.
                    if ((DateTime.UtcNow - lastScan).TotalMilliseconds >= 1000)
                    {
                        lastScan = DateTime.UtcNow;
                        SelectBackend();
                    }

                    if (_backend == Backend.None)
                    {
                        Thread.Sleep(200);
                        continue;
                    }
                }

                try
                {
                    var ok = _backend switch
                    {
                        Backend.XInput => PollXInput(),
                        Backend.DirectInput => PollDirectInput(),
                        _ => true
                    };
                    if (!ok)
                        ResetToScan();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                Thread.Sleep(15);
            }
        }
        finally
        {
            // Own all DirectInput cleanup on this thread so resources are never
            // disposed out from under an in-progress poll (see Dispose()).
            try { _joystick?.Unacquire(); } catch { /* ignore */ }
            try { _joystick?.Dispose(); } catch { /* ignore */ }
            try { _directInput?.Dispose(); } catch { /* ignore */ }
            _joystick = null;
            _directInput = null;
        }
    }

    private void SelectBackend()
    {
        var idx = FindXInputController();
        if (idx >= 0)
        {
            _xinputUserIndex = idx;
            _xPrevious.Clear();
            _backend = Backend.XInput;
            return;
        }

        if (StartDirectInput())
        {
            _diPrevious = Array.Empty<bool>();
            _backend = Backend.DirectInput;
        }
    }

    private void ResetToScan()
    {
        // The active controller went away; drop it so the loop rescans and a
        // reconnected or different controller can take over.
        if (_backend == Backend.DirectInput)
        {
            try { _joystick?.Unacquire(); } catch { /* ignore */ }
            try { _joystick?.Dispose(); } catch { /* ignore */ }
            _joystick = null;
        }
        _xinputUserIndex = -1;
        _backend = Backend.None;
    }

    private static int FindXInputController()
    {
        for (var i = 0u; i < 4u; i++)
        {
            if (XInput.GetState(i, out _))
                return (int)i;
        }

        return -1;
    }

    private bool PollXInput()
    {
        // A false result here means the controller was unplugged.
        if (!XInput.GetState((uint)_xinputUserIndex, out var state))
            return false;

        var pad = state.Gamepad;
        for (var b = 0; b < XInputButtons.Length; b++)
            UpdateXInput(b, (pad.Buttons & XInputButtons[b].Flag) != 0, XInputButtons[b].Name);
        UpdateXInput(LeftTriggerCode, pad.LeftTrigger > TriggerThreshold, "LT");
        UpdateXInput(RightTriggerCode, pad.RightTrigger > TriggerThreshold, "RT");
        return true;
    }

    private void UpdateXInput(int code, bool pressed, string name)
    {
        _xPrevious.TryGetValue(code, out var was);
        if (pressed && !was)
            ButtonPressed?.Invoke(code, name);

        _xPrevious[code] = pressed;
    }

    private bool StartDirectInput()
    {
        IDirectInputDevice8? joystick = null;
        try
        {
            _directInput ??= DInput.DirectInput8Create();
            var device = _directInput.GetDevices(Vortice.DirectInput.DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices)
                             .Concat(_directInput.GetDevices(Vortice.DirectInput.DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                             .FirstOrDefault();
            if (device == null)
                return false;

            // Build into a local and only publish it on full success, so a
            // failure part-way through can't leak a half-configured device that
            // the next rescan would overwrite.
            joystick = _directInput.CreateDevice(device.InstanceGuid);
            joystick.SetDataFormat<RawJoystickState>();

            // Background + non-exclusive so presses are still delivered when the
            // game (not our app) is the foreground window.
            if (_windowHandle != IntPtr.Zero)
                joystick.SetCooperativeLevel(_windowHandle,
                    CooperativeLevel.Background | CooperativeLevel.NonExclusive);

            joystick.Properties.BufferSize = 128;
            joystick.Acquire();

            _joystick = joystick;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Gamepad init failed: {ex.Message}");
            try { joystick?.Unacquire(); } catch { /* ignore */ }
            try { joystick?.Dispose(); } catch { /* ignore */ }
            return false;
        }
    }

    private bool PollDirectInput()
    {
        if (_joystick == null)
            return false;

        try
        {
            _joystick.Poll();
            var buttons = _joystick.GetCurrentJoystickState().Buttons;
            if (_diPrevious.Length != buttons.Length)
                _diPrevious = new bool[buttons.Length];

            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] && !_diPrevious[i])
                    ButtonPressed?.Invoke(i, $"Pad {i + 1}");
                _diPrevious[i] = buttons[i];
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            // Try a single re-acquire; if that fails the device is gone, so
            // signal the loop to rescan for a replacement.
            try { _joystick.Acquire(); return true; }
            catch { return false; }
        }
    }

    public void Dispose()
    {
        _running = false;
        // Cleanup of the DirectInput objects is owned by RunLoop's finally on the
        // poller thread, so we never dispose them out from under an active poll.
        // Just signal the thread and wait for it to unwind.
        try { _thread?.Join(500); } catch { /* ignore */ }
        _thread = null;
    }
}
