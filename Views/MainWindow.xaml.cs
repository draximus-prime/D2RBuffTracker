using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using D2RBuffTracker.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;
using Wpf.Ui.Controls;

namespace D2RBuffTracker.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;
    private bool _reallyExit;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // The handle is now valid (and stays valid while hidden to the tray), so
        // hand it to the engine for the gamepad's background cooperative level.
        _vm.InputWindowHandle = new WindowInteropHelper(this).Handle;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyExit && _vm.Settings.MinimizeToTrayOnClose)
        {
            // Keep running: hide to the tray instead of exiting, and let the
            // user know where the app went the first time it ever happens.
            e.Cancel = true;
            Hide();

            if (!_vm.Settings.TrayHintShown)
            {
                _vm.Settings.TrayHintShown = true;
                _vm.Settings.Save();
                Tray.ShowBalloonTip(
                    "Still running",
                    "D2R Buff Tracker is minimised to the tray and keeps tracking. Right-click the tray icon to exit.",
                    BalloonIcon.Info);
            }
            return;
        }

        if (!_reallyExit)
        {
            // Close-to-tray disabled: really exit. Because ShutdownMode is
            // OnExplicitShutdown, closing the window alone leaves an invisible
            // process behind — shut the application down explicitly.
            _vm.Shutdown();
            Tray.Dispose();
            base.OnClosing(e);
            Application.Current.Shutdown();
            return;
        }
        base.OnClosing(e);
    }

    private void Tray_OnDoubleClick(object sender, RoutedEventArgs e) => ShowFromTray();

    private void TrayOpen_OnClick(object sender, RoutedEventArgs e) => ShowFromTray();

    private void TrayExit_OnClick(object sender, RoutedEventArgs e)
    {
        _reallyExit = true;
        _vm.Shutdown();
        Tray.Dispose();
        Application.Current.Shutdown();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
