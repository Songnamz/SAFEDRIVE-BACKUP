using System.IO;

namespace SafeDriveBackup.Services;

public class CleanupService
{
    private readonly ConfigService _config;
    private readonly BackupService _backup;
    private readonly LogService _log;

    public CleanupService(ConfigService config, BackupService backup, LogService log)
    {
        _config = config;
        _backup = backup;
        _log = log;
    }

    public void RunCleanup()
    {
        _backup.CleanupOldVersions();
        _backup.CleanupOldDeletedFiles();
        CleanupOldLogs();
    }

    private void CleanupOldLogs()
    {
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return;

        var logFolder = _log.LogFolder;
        if (!Directory.Exists(logFolder)) return;

        var cutoff = DateTime.Now.AddDays(-Math.Max(cfg.KeepDeletedDays, 7));
        try
        {
            foreach (var file in Directory.GetFiles(logFolder, "backup-*.log"))
            {
                if (new FileInfo(file).LastWriteTime < cutoff)
                {
                    File.Delete(file);
                    _log.Log($"Removed old log: {Path.GetFileName(file)}");
                }
            }
        }
        catch (Exception ex) { _log.LogError("Log cleanup error", ex); }
    }
}
