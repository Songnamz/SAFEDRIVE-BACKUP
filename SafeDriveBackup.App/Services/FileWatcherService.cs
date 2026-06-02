using System.IO;
using SafeDriveBackup.Helpers;

namespace SafeDriveBackup.Services;

public class FileWatcherService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly BackupService _backupService;
    private readonly LogService _log;

    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly System.Timers.Timer _periodicTimer;
    private readonly System.Timers.Timer _debounceTimer;
    private System.Timers.Timer? _driveConnectTimer;
    private bool _wasDriveAvailable;
    private bool _disposed;
    private readonly object _queueLock = new();

    public event Action? BackupTriggered;

    public FileWatcherService(ConfigService configService, BackupService backupService, LogService log)
    {
        _configService = configService;
        _backupService = backupService;
        _log = log;

        _periodicTimer = new System.Timers.Timer();
        _periodicTimer.Elapsed += async (_, _) =>
        {
            _log.Log("Scheduled backup triggered.");
            await TriggerBackupAsync();
        };

        // 10-second debounce: consolidate rapid file-change events into one backup
        _debounceTimer = new System.Timers.Timer(10_000) { AutoReset = false };
        _debounceTimer.Elapsed += async (_, _) => await TriggerBackupAsync();
    }

    public void StartWatching()
    {
        StopWatching();

        var cfg = _configService.Config;
        if (!cfg.SetupComplete || string.IsNullOrEmpty(cfg.BackupRoot)) return;

        switch (cfg.BackupMode)
        {
            case "Continuous":
                StartFileWatchers(cfg);
                var hours = Math.Max(1, cfg.PeriodicScanHours);
                _periodicTimer.Interval = TimeSpan.FromHours(hours).TotalMilliseconds;
                _periodicTimer.Start();
                _log.Log($"Continuous mode: file watchers active, periodic scan every {hours}h.");
                break;

            case "Every5Min":
                _periodicTimer.Interval = TimeSpan.FromMinutes(5).TotalMilliseconds;
                _periodicTimer.Start();
                _log.Log("Scheduled mode: backup every 5 minutes.");
                break;

            case "Every10Min":
                _periodicTimer.Interval = TimeSpan.FromMinutes(10).TotalMilliseconds;
                _periodicTimer.Start();
                _log.Log("Scheduled mode: backup every 10 minutes.");
                break;

            case "Every30Min":
                _periodicTimer.Interval = TimeSpan.FromMinutes(30).TotalMilliseconds;
                _periodicTimer.Start();
                _log.Log("Scheduled mode: backup every 30 minutes.");
                break;

            case "Every1Hour":
                _periodicTimer.Interval = TimeSpan.FromHours(1).TotalMilliseconds;
                _periodicTimer.Start();
                _log.Log("Scheduled mode: backup every 1 hour.");
                break;

            case "Daily":
                _periodicTimer.Interval = TimeSpan.FromHours(24).TotalMilliseconds;
                _periodicTimer.Start();
                _log.Log("Scheduled mode: backup daily.");
                break;

            case "DriveConnect":
                _wasDriveAvailable = _backupService.IsDestinationAvailable();
                _driveConnectTimer = new System.Timers.Timer(30_000) { AutoReset = true };
                _driveConnectTimer.Elapsed += OnDriveConnectTick;
                _driveConnectTimer.Start();
                _log.Log("Drive-connect mode: watching for backup drive to be connected.");
                break;
        }
    }

    private void StartFileWatchers(Models.AppConfig cfg)
    {
        foreach (var kvp in cfg.SelectedFolders)
        {
            if (!kvp.Value) continue;

            try
            {
                var path = PathHelper.GetSourceFolderPath(kvp.Key);
                if (!Directory.Exists(path)) continue;

                var w = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName | NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    InternalBufferSize = 65536
                };

                w.Changed += OnChanged;
                w.Created += OnChanged;
                w.Deleted += OnChanged;
                w.Renamed += OnChanged;
                w.Error += OnError;

                _watchers.Add(w);
                _log.Log($"Watching: {kvp.Key} ({path})");
            }
            catch (Exception ex)
            {
                _log.LogError($"Cannot watch {kvp.Key}", ex);
            }
        }
    }

    public void StopWatching()
    {
        _periodicTimer.Stop();
        _debounceTimer.Stop();

        _driveConnectTimer?.Stop();
        _driveConnectTimer?.Dispose();
        _driveConnectTimer = null;

        foreach (var w in _watchers)
        {
            try { w.EnableRaisingEvents = false; w.Dispose(); }
            catch { /* ignore */ }
        }
        _watchers.Clear();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (_backupService.IsPaused) return;
        
        var relativePath = e.Name;
        if (!string.IsNullOrEmpty(relativePath) && FilterHelper.IsExcluded(relativePath, _configService.Config.ExcludedPatterns)) return;

        // Restart debounce window — many rapid events collapse into one backup
        lock (_queueLock)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _log.LogError("FileSystemWatcher error", e.GetException());
        // Restart watchers on the UI thread
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(StartWatching);
    }

    private async void OnDriveConnectTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var isAvailable = _backupService.IsDestinationAvailable();
        if (isAvailable && !_wasDriveAvailable)
        {
            _log.Log("Backup drive connected — triggering backup.");
            await TriggerBackupAsync();
        }
        _wasDriveAvailable = isAvailable;
    }

    private async Task TriggerBackupAsync()
    {
        BackupTriggered?.Invoke();
        await _backupService.StartBackupAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
        _periodicTimer.Dispose();
        _debounceTimer.Dispose();
    }
}
