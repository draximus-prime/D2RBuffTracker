using System.IO;
using System.Threading;
using System.Windows;
using D2RBuffTracker.Models;
using D2RBuffTracker.Services;
using D2RBuffTracker.ViewModels;
using D2RBuffTracker.Views;
using Wpf.Ui.Appearance;

namespace D2RBuffTracker;

public partial class App : Application
{
    private const string MutexName = "D2RBuffTracker_SingleInstance";

    private static Mutex? _mutex;

    public static AppSettings Settings { get; private set; } = new();
    public static string DataFolder { get; private set; } = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Keep running when the main window is closed to the tray.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D2RBuffTracker");
        Directory.CreateDirectory(DataFolder);

        Logger.Initialize(Path.Combine(DataFolder, "log.txt"));
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Log(args.Exception);
            args.Handled = true;
        };

        // Prevent a faulted background task from tearing down the process.
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();

        Settings = AppSettings.Load(Path.Combine(DataFolder, "settings.json"));
        ApplyTheme(Settings.Theme);

        var mainViewModel = new MainViewModel();
        var window = new MainWindow(mainViewModel);
        MainWindow = window;
        window.Show();
    }

    public static void ApplyTheme(AppTheme theme)
    {
        var applied = theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.System => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light
                ? ApplicationTheme.Light
                : ApplicationTheme.Dark,
            _ => ApplicationTheme.Dark
        };
        ApplicationThemeManager.Apply(applied);
    }
}
