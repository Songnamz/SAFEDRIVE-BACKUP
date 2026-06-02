using Microsoft.Extensions.DependencyInjection;
using SafeDriveBackup.Services;
using SafeDriveBackup.ViewModels;

namespace SafeDriveBackup;

public partial class App : System.Windows.Application
{
    private static IServiceProvider? _services;
    private TrayIconService? _tray;
    private FileWatcherService? _watcher;
    private MainWindow? _mainWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = BuildServices();

        // Load config
        var config = _services.GetRequiredService<ConfigService>();
        config.Load();

        // Set up log folder if already configured
        var backup = _services.GetRequiredService<BackupService>();
        backup.SetupLogFolder();

        // Wire tray icon actions now that services are ready
        _tray = _services.GetRequiredService<TrayIconService>();
        _tray.Initialize();
        _tray.SetStatus("Starting…");

        // Wire backup events → tray
        backup.StatusChanged   += s  => Dispatcher.Invoke(() => UpdateTray(s));
        backup.BackupCompleted += _  => Dispatcher.Invoke(() => UpdateTray(backup.CurrentStatus));
        backup.RansomwareProtectionTriggered += () =>
            Dispatcher.Invoke(() => _tray.ShowBalloon("SafeDrive Backup — Warning",
                "Many files changed quickly. Backup paused to protect your data.",
                System.Windows.Forms.ToolTipIcon.Warning));

        // Fix #6: wire CleanupService — runs after every successful backup
        var cleanup = _services.GetRequiredService<CleanupService>();
        backup.BackupCompleted += _ => Task.Run(() => cleanup.RunCleanup());

        // Fix #4: restart watchers whenever config is saved (e.g. mode change)
        config.ConfigSaved += () =>
        {
            if (_watcher != null)
                Dispatcher.Invoke(() => _watcher.StartWatching());
        };

        // Create and show main window
        var mainVm = _services.GetRequiredService<MainViewModel>();
        mainVm.Initialize();

        _mainWindow = new MainWindow { DataContext = mainVm };
        _mainWindow.StateChanged += OnWindowStateChanged;
        _mainWindow.Closing      += OnWindowClosing;
        _mainWindow.Show();

        // Start file watchers and initial backup if already configured
        if (config.Config.SetupComplete)
        {
            _watcher = _services.GetRequiredService<FileWatcherService>();

            // Fix #9: BackupTriggered event → update tray tooltip
            _watcher.BackupTriggered += () =>
                Dispatcher.Invoke(() => _tray?.SetTooltip("SafeDrive Backup — Backup running…"));

            _watcher.StartWatching();

            // Fix #10: handle startup backup errors visibly
            Task.Run(async () =>
            {
                var result = await backup.StartBackupAsync();
                if (result.FilesFailed > 0)
                    Dispatcher.Invoke(() => _tray?.ShowBalloon(
                        "SafeDrive Backup — Warning",
                        $"Initial backup: {result.FilesFailed} file(s) failed to copy.",
                        System.Windows.Forms.ToolTipIcon.Warning));
            });
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _watcher?.StopWatching();
        _watcher?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }

    // ─── Minimize to tray ────────────────────────────────────────────────────

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow?.WindowState == System.Windows.WindowState.Minimized)
        {
            _mainWindow.Hide();
            _tray?.ShowBalloon("SafeDrive Backup",
                "Running in the background. Double-click the tray icon to open.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Intercept close → minimize to tray instead
        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void ShowWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApp()
    {
        _watcher?.StopWatching();
        _watcher?.Dispose();
        _tray?.Dispose();
        Current.Shutdown();
    }

    private void UpdateTray(Models.BackupStatusEnum status)
    {
        switch (status)
        {
            case Models.BackupStatusEnum.Protected:    _tray?.SetStatus("Protected");            break;
            case Models.BackupStatusEnum.BackingUp:    _tray?.SetStatus("Backing up…");          break;
            case Models.BackupStatusEnum.Warning:      _tray?.SetStatus("Warning",  warning: true); break;
            case Models.BackupStatusEnum.Error:        _tray?.SetStatus("Error",    error: true);   break;
            case Models.BackupStatusEnum.Paused:       _tray?.SetStatus("Paused",   paused: true);  break;
            default:                                   _tray?.SetStatus("Not Configured");          break;
        }
    }

    // ─── DI container ────────────────────────────────────────────────────────

    private IServiceProvider BuildServices()
    {
        var svc = new ServiceCollection();

        // Core services (all singleton — one instance for the app lifetime)
        svc.AddSingleton<ConfigService>();
        svc.AddSingleton<LogService>();
        svc.AddSingleton<VssService>();
        svc.AddSingleton<BackupService>();
        svc.AddSingleton<FileWatcherService>();
        svc.AddSingleton<RestoreService>();
        svc.AddSingleton<CleanupService>();

        // Tray icon: inject actions via lambdas resolved at call time
        svc.AddSingleton<TrayIconService>(sp => new TrayIconService(
            openWindow: ShowWindow,
            backupNow:  () => Task.Run(async () =>
                await sp.GetRequiredService<BackupService>().StartBackupAsync()),
            pause:  () => sp.GetRequiredService<BackupService>().Pause(),
            resume: () => sp.GetRequiredService<BackupService>().Resume(),
            viewLogs: () => Dispatcher.Invoke(() =>
            {
                ShowWindow();
                sp.GetRequiredService<MainViewModel>().GoToLogs();
            }),
            exit: ExitApp));

        // ViewModels
        svc.AddSingleton<SetupViewModel>();
        svc.AddSingleton<StatusViewModel>();
        svc.AddSingleton<FolderSelectionViewModel>();
        svc.AddSingleton<DestinationViewModel>();
        svc.AddSingleton<RestoreViewModel>();
        svc.AddSingleton<SettingsViewModel>();
        svc.AddSingleton<LogsViewModel>();
        svc.AddSingleton<MainViewModel>();

        return svc.BuildServiceProvider();
    }
}
