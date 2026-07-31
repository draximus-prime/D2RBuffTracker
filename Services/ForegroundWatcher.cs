using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace D2RBuffTracker.Services;

/// <summary>Which application currently owns the foreground window.</summary>
public enum ForegroundApp
{
    /// <summary>Some other, unrelated application.</summary>
    Other,
    /// <summary>Diablo II: Resurrected.</summary>
    Game,
    /// <summary>This app (the Buff Tracker window or its overlay).</summary>
    Self
}

/// <summary>
/// Polls the foreground window and raises an event when focus moves between the
/// game, this app, and everything else — so tracking can auto start/pause
/// with the game and the positioning preview can follow our own window.
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    private static readonly string[] GameProcessNames = { "D2R" };
    private const string GameTitleFragment = "Diablo II: Resurrected";
    private static readonly int SelfPid = Environment.ProcessId;

    private readonly DispatcherTimer _timer;
    private ForegroundApp _current = ForegroundApp.Other;

    /// <summary>Raised on the UI thread when the foreground application changes.</summary>
    public event Action<ForegroundApp>? ForegroundChanged;

    public ForegroundApp Current => _current;

    public ForegroundWatcher()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        Poll();
        _timer.Start();
    }

    private void Poll()
    {
        var fg = Detect();
        if (fg == _current)
            return;
        _current = fg;
        ForegroundChanged?.Invoke(fg);
    }

    private static ForegroundApp Detect()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return ForegroundApp.Other;

        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return ForegroundApp.Other;

        if ((int)pid == SelfPid)
            return ForegroundApp.Self;

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            var procName = proc.ProcessName;
            foreach (var name in GameProcessNames)
            {
                if (string.Equals(procName, name, StringComparison.OrdinalIgnoreCase))
                    return ForegroundApp.Game;
            }

            // Title fallback: only trust the window title when the owning process
            // also looks like the game (name begins with "d2"). This stops
            // unrelated windows that merely contain "Diablo II: Resurrected" in
            // their title (e.g. a browser tab or wiki page) from being treated as
            // the game and hijacking tracking.
            if (procName.StartsWith("d2", StringComparison.OrdinalIgnoreCase)
                && GetWindowTitle(hwnd).Contains(GameTitleFragment, StringComparison.OrdinalIgnoreCase))
                return ForegroundApp.Game;
        }
        catch
        {
            // Process may have exited between calls; treat as not-the-game.
        }

        return ForegroundApp.Other;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0)
            return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Dispose() => _timer.Stop();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
