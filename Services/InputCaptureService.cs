using System.Windows.Forms;
using System.Windows.Threading;
using D2RBuffTracker.Models;

namespace D2RBuffTracker.Services;

/// <summary>
/// Provides a responsive, low-latency "press any key/mouse/gamepad button to
/// bind" capture. It briefly installs the global input monitor, grabs the very
/// next input, and returns it immediately — far snappier than picking from
/// a dropdown. Escape cancels.
/// </summary>
public sealed class InputCaptureService : IDisposable
{
    // The in-progress capture, if any. Each Begin() creates a fresh, fully
    // self-contained session so a completion or cancellation from one capture
    // can never tear down or complete a later one.
    private CaptureSession? _active;

    public bool IsCapturing => _active != null;

    /// <summary>
    /// Begin listening. <paramref name="completed"/> is invoked on the UI thread
    /// with the captured binding, or null if the user pressed Escape/cancelled.
    /// </summary>
    /// <param name="windowHandle">
    /// Top-level window handle used for the gamepad's background cooperative
    /// level so controller buttons are captured reliably.
    /// </param>
    public void Begin(Dispatcher dispatcher, IntPtr windowHandle, Action<InputBinding?> completed)
    {
        Cancel();

        var session = new CaptureSession(dispatcher, completed, s =>
        {
            // Only forget the session if it is still the active one; a newer
            // Begin() may already have replaced it.
            if (ReferenceEquals(_active, s))
                _active = null;
        });
        _active = session;
        session.Start(windowHandle);
    }

    public void Cancel()
    {
        var session = Interlocked.Exchange(ref _active, null);
        session?.CancelNow();
    }

    public void Dispose() => Cancel();

    /// <summary>
    /// One capture attempt. Owns its own <see cref="InputMonitor"/> and a
    /// one-shot completion latch, so concurrent presses (hook vs gamepad thread)
    /// and overlapping captures cannot interfere with each other.
    /// </summary>
    private sealed class CaptureSession
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<InputBinding?> _completed;
        private readonly Action<CaptureSession> _onDone;
        private InputMonitor? _monitor;
        private int _done;
        private volatile bool _canceled;

        public CaptureSession(Dispatcher dispatcher, Action<InputBinding?> completed, Action<CaptureSession> onDone)
        {
            _dispatcher = dispatcher;
            _completed = completed;
            _onDone = onDone;
        }

        public void Start(IntPtr windowHandle)
        {
            _monitor = new InputMonitor(windowHandle);
            _monitor.Pressed += OnPressed;
            _monitor.Start();
        }

        private void OnPressed(InputBinding binding)
        {
            // Only the first press wins; further concurrent presses are ignored.
            if (Interlocked.Exchange(ref _done, 1) == 1)
                return;

            // Escape cancels the capture without binding.
            var result = binding.Kind == InputKind.Keyboard && binding.Code == (int)Keys.Escape
                ? (InputBinding?)null
                : binding;

            // Tear the hook down off the callback thread to avoid re-entrancy.
            _dispatcher.BeginInvoke(() =>
            {
                Teardown();
                _onDone(this);
                // If the session was cancelled after this press latched (e.g. a
                // newer capture started), swallow the result so a stale binding is
                // never applied to the replacement capture.
                if (!_canceled)
                    _completed(result);
            });
        }

        public void CancelNow()
        {
            // Mark cancelled first so any already-queued completion suppresses its
            // callback when it runs on the UI thread.
            _canceled = true;

            // If a press already latched the session, its queued teardown will
            // run; otherwise tear down now.
            if (Interlocked.Exchange(ref _done, 1) == 1)
                return;
            Teardown();
        }

        private void Teardown()
        {
            if (_monitor == null)
                return;
            _monitor.Pressed -= OnPressed;
            _monitor.Dispose();
            _monitor = null;
        }
    }
}
