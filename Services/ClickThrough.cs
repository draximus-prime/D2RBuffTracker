using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace D2RBuffTracker.Services;

/// <summary>
/// Win32 helpers for making a window transparent to mouse input (click-through)
/// so the overlay never intercepts clicks meant for the game underneath.
/// </summary>
public static class ClickThrough
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    public static void Enable(IntPtr hwnd)
    {
        var style = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    public static void Disable(IntPtr hwnd)
    {
        var style = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, style & ~(WsExTransparent | WsExNoActivate));
    }

    public static IntPtr HandleOf(System.Windows.Window window)
        => new WindowInteropHelper(window).Handle;
}
