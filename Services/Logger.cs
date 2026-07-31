using System.IO;

namespace D2RBuffTracker.Services;

/// <summary>
/// Extremely small file logger. Failures never throw so logging can be used
/// safely from interop callbacks and finalizers.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static string? _path;

    public static void Initialize(string path)
    {
        _path = path;
    }

    public static void Log(string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_path))
                return;
            lock (Gate)
            {
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // never throw from the logger
        }
    }

    public static void Log(Exception ex) => Log(ex.ToString());
}
