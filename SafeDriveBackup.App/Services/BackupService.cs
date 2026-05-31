using System.IO;
using SafeDriveBackup.Helpers;
using SafeDriveBackup.Models;

namespace SafeDriveBackup.Services;

public class BackupService
{
    private readonly ConfigService _config;
    private readonly LogService _log;

    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private BackupResult? _lastResult;
    private BackupStatusEnum _currentStatus = BackupStatusEnum.NotConfigured;

    // Ransomware protection: track how many files changed recently
    private readonly Queue<DateTime> _recentChanges = new();
    private readonly object _changeLock = new();

    public event Action<BackupStatusEnum>? StatusChanged;
    public event Action<BackupResult>? BackupCompleted;
    public event Action<string>? ProgressChanged;
    public event Action? RansomwareProtectionTriggered;

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;
    public BackupResult? LastResult => _lastResult;
    public BackupStatusEnum CurrentStatus => _currentStatus;

    public BackupService(ConfigService configService, LogService logService)
    {
        _config = configService;
        _log = logService;
    }

    public void SetupLogFolder()
    {
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return;
        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        _log.SetLogFolder(PathHelper.GetLogsFolder(cfg.BackupRoot, id));
    }

    // ─── Main entry point ────────────────────────────────────────────────────

    public async Task<BackupResult> StartBackupAsync(CancellationToken ct = default)
    {
        if (_isRunning)  return new BackupResult { Errors = { "Backup already running" } };
        if (_isPaused)   return new BackupResult { Errors = { "Backup is paused" } };

        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot))
            return new BackupResult { Errors = { "Backup destination not configured" } };

        _isRunning = true;
        SetStatus(BackupStatusEnum.BackingUp);
        var result = new BackupResult { StartTime = DateTime.Now };

        try
        {
            _log.Log("Backup started.");
            ProgressChanged?.Invoke("Backup started…");

            await BackupSelectedFoldersAsync(result, ct);

            if (!ct.IsCancellationRequested)
            {
                await MoveDeletedFilesAsync(result, ct);
                CleanupOldVersions();
                CleanupOldDeletedFiles();

                cfg.LastSuccessfulBackup = DateTime.Now;
                _config.Save();

                result.EndTime = DateTime.Now;
                _lastResult = result;

                _log.Log($"Backup completed. Copied: {result.FilesCopied}, " +
                         $"Updated: {result.FilesUpdated}, Failed: {result.FilesFailed}, " +
                         $"Duration: {result.Duration.TotalSeconds:F1}s");

                SetStatus(result.FilesFailed > 0 ? BackupStatusEnum.Warning : BackupStatusEnum.Protected);
                BackupCompleted?.Invoke(result);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Log("Backup cancelled.");
            result.EndTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _log.LogError("Backup failed", ex);
            result.Errors.Add(ex.Message);
            result.EndTime = DateTime.Now;
            SetStatus(BackupStatusEnum.Error);
        }
        finally
        {
            _isRunning = false;
        }

        return result;
    }

    // ─── Backup folders ───────────────────────────────────────────────────────

    private async Task BackupSelectedFoldersAsync(BackupResult result, CancellationToken ct)
    {
        var cfg = _config.Config;
        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var currentBase = PathHelper.GetCurrentFolder(cfg.BackupRoot, id);

        foreach (var kvp in cfg.SelectedFolders)
        {
            if (!kvp.Value || ct.IsCancellationRequested) continue;

            try
            {
                var src = PathHelper.GetSourceFolderPath(kvp.Key);
                if (!Directory.Exists(src)) { _log.Log($"Source not found: {src}"); continue; }

                var dst = Path.Combine(currentBase, kvp.Key);
                Directory.CreateDirectory(dst);

                ProgressChanged?.Invoke($"Backing up {kvp.Key}…");
                _log.Log($"Backing up folder: {kvp.Key}");
                await BackupFolderAsync(src, dst, kvp.Key, result, ct);
            }
            catch (Exception ex)
            {
                _log.LogError($"Error backing up {kvp.Key}", ex);
                result.Errors.Add($"{kvp.Key}: {ex.Message}");
            }
        }
    }

    private async Task BackupFolderAsync(string srcFolder, string dstFolder,
        string folderName, BackupResult result, CancellationToken ct)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(srcFolder, "*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogError($"Access denied: {srcFolder}", ex);
            result.Errors.Add($"Access denied: {srcFolder}");
            return;
        }

        foreach (var srcFile in files)
        {
            if (ct.IsCancellationRequested) break;

            var rel = Path.GetRelativePath(srcFolder, srcFile);
            var dstFile = Path.Combine(dstFolder, rel);
            await BackupFileAsync(srcFile, dstFile, folderName, rel, result);
        }
    }

    public async Task BackupFileAsync(string srcFile, string dstFile,
        string folderName, string relativePath, BackupResult result)
    {
        try
        {
            if (!File.Exists(srcFile)) return;

            if (File.Exists(dstFile))
            {
                if (!FileCompareHelper.IsDifferent(srcFile, dstFile))
                {
                    // For small files, verify with hash to catch timestamp-identical but changed files
                    var fileSize = new FileInfo(srcFile).Length;
                    if (fileSize <= 10 * 1024 * 1024 && !FileCompareHelper.IsDifferentByHash(srcFile, dstFile))
                        return;
                    else if (fileSize > 10 * 1024 * 1024)
                        return;
                }

                await CreateVersionIfNeededAsync(dstFile, folderName, relativePath);

                if (await CopyWithRetryAsync(srcFile, dstFile))
                {
                    result.FilesUpdated++;
                    _log.Log($"Version saved: {Path.Combine(folderName, relativePath)}");
                    RecordChange();
                }
                else
                {
                    result.FilesFailed++;
                    result.Errors.Add($"Failed to update: {relativePath}");
                }
            }
            else
            {
                if (await CopyWithRetryAsync(srcFile, dstFile))
                {
                    result.FilesCopied++;
                    _log.Log($"Copied: {Path.Combine(folderName, relativePath)}");
                    RecordChange();
                }
                else
                {
                    result.FilesFailed++;
                    result.Errors.Add($"Failed to copy: {relativePath}");
                }
            }
        }
        catch (Exception ex)
        {
            result.FilesFailed++;
            _log.LogError($"Error backing up {srcFile}", ex);
            result.Errors.Add($"{relativePath}: {ex.Message}");
        }
    }

    private async Task<bool> CopyWithRetryAsync(string src, string dst, int maxAttempts = 3)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await SafeCopyHelper.SafeCopyAsync(src, dst)) return true;

            if (attempt < maxAttempts)
            {
                _log.Log($"Retry {attempt}/{maxAttempts}: {Path.GetFileName(src)}");
                await Task.Delay(TimeSpan.FromSeconds(_config.Config.RetryDelaySeconds));
            }
        }
        return false;
    }

    // ─── Versioning ───────────────────────────────────────────────────────────

    private Task CreateVersionIfNeededAsync(string currentBackupFile,
        string folderName, string relativePath)
    {
        try
        {
            var cfg = _config.Config;
            var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
            var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);

            var fi = new FileInfo(currentBackupFile);
            var versionFileName = PathHelper.GetVersionFileName(fi.Name, fi.LastWriteTime);

            var relDir = Path.GetDirectoryName(relativePath) ?? "";
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fi.Name);
            var versionFolder = Path.Combine(versionsBase, folderName, relDir, nameWithoutExt);
            Directory.CreateDirectory(versionFolder);

            SafeCopyHelper.SafeMove(currentBackupFile, Path.Combine(versionFolder, versionFileName));
        }
        catch (Exception ex)
        {
            _log.LogError($"Error creating version for {currentBackupFile}", ex);
        }

        return Task.CompletedTask;
    }

    // ─── Deleted-file tracking ─────────────────────────────────────────────

    private Task MoveDeletedFilesAsync(BackupResult result, CancellationToken ct)
    {
        var cfg = _config.Config;
        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var currentBase = PathHelper.GetCurrentFolder(cfg.BackupRoot, id);
        var deletedBase = PathHelper.GetDeletedFolder(cfg.BackupRoot, id);
        var todayFolder = Path.Combine(deletedBase, DateTime.Now.ToString("yyyy-MM-dd"));

        foreach (var kvp in cfg.SelectedFolders)
        {
            if (!kvp.Value || ct.IsCancellationRequested) continue;

            try
            {
                var srcFolder = PathHelper.GetSourceFolderPath(kvp.Key);
                var dstFolder = Path.Combine(currentBase, kvp.Key);
                if (!Directory.Exists(dstFolder)) continue;

                foreach (var backupFile in Directory.GetFiles(dstFolder, "*", SearchOption.AllDirectories))
                {
                    if (ct.IsCancellationRequested) break;
                    var rel = Path.GetRelativePath(dstFolder, backupFile);
                    if (!File.Exists(Path.Combine(srcFolder, rel)))
                    {
                        var target = Path.Combine(todayFolder, kvp.Key, rel);
                        if (SafeCopyHelper.SafeMove(backupFile, target))
                        {
                            result.FilesMovedToDeleted++;
                            _log.Log($"Deleted file moved to Deleted: {Path.Combine(kvp.Key, rel)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"Error processing deleted files for {kvp.Key}", ex);
            }
        }

        return Task.CompletedTask;
    }

    // ─── Cleanup ──────────────────────────────────────────────────────────────

    public void CleanupOldVersions()
    {
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);
        if (!Directory.Exists(versionsBase)) return;

        try
        {
            foreach (var folder in Directory.GetDirectories(versionsBase, "*", SearchOption.AllDirectories))
            {
                var files = Directory.GetFiles(folder)
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToList();

                foreach (var old in files.Skip(cfg.KeepVersions))
                {
                    try { File.Delete(old); _log.Log($"Removed old version: {Path.GetFileName(old)}"); }
                    catch { /* ignore */ }
                }
            }
        }
        catch (Exception ex) { _log.LogError("Cleanup versions error", ex); }
    }

    public void CleanupOldDeletedFiles()
    {
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var deletedBase = PathHelper.GetDeletedFolder(cfg.BackupRoot, id);
        if (!Directory.Exists(deletedBase)) return;

        var cutoff = DateTime.Now.AddDays(-cfg.KeepDeletedDays);
        try
        {
            foreach (var dir in Directory.GetDirectories(deletedBase))
            {
                if (DateTime.TryParse(Path.GetFileName(dir), out var date) && date < cutoff)
                {
                    Directory.Delete(dir, recursive: true);
                    _log.Log($"Removed old deleted folder: {Path.GetFileName(dir)}");
                }
            }
        }
        catch (Exception ex) { _log.LogError("Cleanup deleted files error", ex); }
    }

    // ─── Pause / Resume ───────────────────────────────────────────────────────

    public void Pause()
    {
        _isPaused = true;
        SetStatus(BackupStatusEnum.Paused);
        _log.Log("Backup paused.");
    }

    public void Resume()
    {
        _isPaused = false;
        SetStatus(BackupStatusEnum.Protected);
        _log.Log("Backup resumed.");
        lock (_changeLock) _recentChanges.Clear();
    }

    // ─── Ransomware protection ────────────────────────────────────────────────

    private void RecordChange()
    {
        lock (_changeLock)
        {
            var now = DateTime.Now;
            _recentChanges.Enqueue(now);

            var cfg = _config.Config;
            var windowStart = now.AddMinutes(-cfg.RansomwareWindowMinutes);
            while (_recentChanges.Count > 0 && _recentChanges.Peek() < windowStart)
                _recentChanges.Dequeue();

            if (_recentChanges.Count >= cfg.RansomwareThreshold)
            {
                _isPaused = true;
                _recentChanges.Clear();
                SetStatus(BackupStatusEnum.Warning);
                _log.Log($"RANSOMWARE PROTECTION TRIGGERED: {cfg.RansomwareThreshold} files " +
                         $"changed in {cfg.RansomwareWindowMinutes} minutes. Backup paused.");
                RansomwareProtectionTriggered?.Invoke();
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    public bool IsDestinationAvailable()
    {
        var root = _config.Config.BackupRoot;
        if (string.IsNullOrEmpty(root)) return false;
        try { return Directory.Exists(root); }
        catch { return false; }
    }

    public long GetAvailableDiskSpace()
    {
        var root = _config.Config.BackupRoot;
        if (string.IsNullOrEmpty(root)) return 0;
        try
        {
            var drive = PathHelper.IsNetworkPath(root)
                ? root
                : (Path.GetPathRoot(root) ?? root);
            return new DriveInfo(drive).AvailableFreeSpace;
        }
        catch { return 0; }
    }

    private void SetStatus(BackupStatusEnum status)
    {
        _currentStatus = status;
        StatusChanged?.Invoke(status);
    }
}
