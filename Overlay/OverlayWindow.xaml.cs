using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using D2RBuffTracker.Services;

namespace D2RBuffTracker.Overlay;

public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _vm;
    private readonly DispatcherTimer _positionSaveTimer;

    public OverlayWindow(OverlayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        Left = App.Settings.OverlayX;
        Top = App.Settings.OverlayY;
        ApplyScale(App.Settings.OverlayScale);

        // Debounce position persistence: dragging fires LocationChanged rapidly,
        // so only write settings once movement settles.
        _positionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _positionSaveTimer.Tick += (_, _) =>
        {
            _positionSaveTimer.Stop();
            App.Settings.Save();
        };

        LocationChanged += (_, _) =>
        {
            if (!_vm.IsPreview)
                return;
            App.Settings.OverlayX = (int)Left;
            App.Settings.OverlayY = (int)Top;
            _positionSaveTimer.Stop();
            _positionSaveTimer.Start();
        };

        MouseLeftButtonDown += (_, e) =>
        {
            if (_vm.IsPreview && e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { /* ignore */ }
            }
        };
    }

    public void ApplyScale(double scale)
    {
        ScaleTransform.ScaleX = scale;
        ScaleTransform.ScaleY = scale;
    }

    public void SetPreview(bool isPreview)
    {
        _vm.IsPreview = isPreview;
        var hwnd = ClickThrough.HandleOf(this);
        if (hwnd == IntPtr.Zero)
            return;

        if (isPreview)
            ClickThrough.Disable(hwnd);
        else
            ClickThrough.Enable(hwnd);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (!_vm.IsPreview)
            ClickThrough.Enable(ClickThrough.HandleOf(this));
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Flush a pending debounced position save so a drag right before close
        // is never lost.
        if (_positionSaveTimer.IsEnabled)
        {
            _positionSaveTimer.Stop();
            App.Settings.Save();
        }
        base.OnClosing(e);
    }
}
