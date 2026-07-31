using System.Windows.Threading;
using D2RBuffTracker.Models;

namespace D2RBuffTracker.Services;

/// <summary>
/// Drives live buff tracking: it listens to the global input stream and, using
/// each buff's select/use sequence rules, decides when a buff should fire.
/// </summary>
public sealed class TrackingEngine : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private InputMonitor? _monitor;
    private IReadOnlyList<TrackedBuff> _buffs = Array.Empty<TrackedBuff>();

    // Serialises OnPressed, which is invoked from both the input-hook thread
    // (keyboard/mouse) and the gamepad poller thread.
    private readonly object _gate = new();

    // Bumped on every Start/Stop. Queued UI activations captured under an old
    // generation are dropped, so presses from a previous session can never fire
    // a buff after tracking has stopped or restarted.
    private int _generation;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Top-level window handle used to give the gamepad poller a background
    /// cooperative level so controller input is read while the game has focus.
    /// Set before <see cref="Start"/>; when zero, gamepad input is only read
    /// while our own window is focused.
    /// </summary>
    public IntPtr WindowHandle { get; set; }

    /// <summary>Raised on the UI thread when a buff activates and should be shown.</summary>
    public event Action<TrackedBuff>? BuffActivated;

    public TrackingEngine(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public void Start(IEnumerable<TrackedBuff> buffs)
    {
        Stop();

        lock (_gate)
        {
            _buffs = buffs.ToList();
            foreach (var b in _buffs)
                b.ResetSequence();

            // Bind this session's generation into the handler so a press that was
            // already in flight on the poller thread when a later Stop() ran is
            // rejected instead of mutating the new session's state or firing.
            var gen = _generation;
            _monitor = new InputMonitor(WindowHandle);
            _monitor.Pressed += binding => OnPressed(binding, gen);
            _monitor.Start();
            IsRunning = true;
        }
    }

    private void OnPressed(InputBinding binding, int gen)
    {
        lock (_gate)
        {
            // Stale press from a monitor whose session has already stopped/restarted.
            if (gen != _generation)
                return;

            var used = new List<TrackedBuff>();

            foreach (var buff in _buffs)
            {
                if (!buff.IsEnabled || buff.UseKey is null || !buff.UseKey.Equals(binding))
                    continue;
                used.Add(buff);
                if (buff.OnUseKeyPressed())
                {
                    var fired = buff;
                    _dispatcher.BeginInvoke(() =>
                    {
                        if (gen == Volatile.Read(ref _generation))
                            BuffActivated?.Invoke(fired);
                    });
                }
            }

            foreach (var buff in _buffs)
            {
                if (!buff.IsEnabled || buff.SelectKey is null || !buff.SelectKey.Equals(binding))
                    continue;
                used.Add(buff);
                buff.OnSelectKeyPressed();
            }

            foreach (var buff in _buffs)
            {
                if (buff.IsEnabled && !used.Contains(buff))
                    buff.ResetSequence();
            }
        }
    }

    public void Stop()
    {
        InputMonitor? toDispose;
        lock (_gate)
        {
            // Invalidate the current session: any in-flight or queued work bound
            // to the old generation is rejected.
            Interlocked.Increment(ref _generation);
            toDispose = _monitor;
            _monitor = null;
            IsRunning = false;
        }

        // Dispose outside the lock: disposing joins the gamepad poller thread,
        // which itself may be blocked entering OnPressed on _gate — disposing
        // while holding the lock would deadlock.
        toDispose?.Dispose();
    }

    public void Dispose() => Stop();
}
