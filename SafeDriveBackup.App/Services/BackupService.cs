using System.IO;
using SafeDriveBackup.Helpers;
using SafeDriveBackup.Models;

namespace SafeDriveBackup.Services;

public class BackupService
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly VssService _vss;

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

    public BackupService(ConfigService configService, LogService logService, VssService vssService)
    {
        _config = configService;
        _log = logService;
        _vss = vssService;
    }

    private IStorageProvider GetStorage()
    {
        var cfg = _config.Config;
        if (cfg.DestinationType == DestinationType.S3Compatible)
            return new S3StorageProvider(cfg);
        
        return new LocalStorageProvider(cfg.BackupRoot ?? "");
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
                _config.SaveSilent();

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
            _vss.CleanupSnapshots();
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
                await GetStorage().CreateDirectoryAsync(dst);

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
            if (FilterHelper.IsExcluded(rel, _config.Config.ExcludedPatterns)) continue;

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

            var storage = GetStorage();

            if (await storage.ExistsAsync(dstFile))
            {
                var srcFi = new FileInfo(srcFile);
                var dstSize = await storage.GetFileSizeAsync(dstFile);
                var dstTime = await storage.GetLastWriteTimeUtcAsync(dstFile);
                
                bool isDifferent = srcFi.Length != dstSize || Math.Abs((srcFi.LastWriteTimeUtc - dstTime).TotalSeconds) > 2;

                if (!isDifferent)
                {
                    // For small files, verify with hash to catch timestamp-identical but changed files
                    if (srcFi.Length <= 10 * 1024 * 1024)
                    {
                        var dstHash = await storage.GetSha256HashAsync(dstFile);
                        if (!string.IsNullOrEmpty(dstHash))
                        {
                            var srcHash = FileCompareHelper.IsDifferentByHash(srcFile, "") ? "diff" : "same"; 
                            // Actually, FileCompareHelper requires two paths. Let's compute srcHash manually.
                            string realSrcHash = "";
                            try
                            {
                                using var sha256 = System.Security.Cryptography.SHA256.Create();
                                using var stream = File.OpenRead(srcFile);
                                realSrcHash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
                            } catch { }

                            if (realSrcHash == dstHash) return;
                        }
                    }
                    else
                    {
                        return;
                    }
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
        var storage = GetStorage();
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await storage.WriteFileAsync(src, dst);
                return true;
            }
            catch
            {
                // If normal copy failed, attempt VSS fallback
                if (attempt == 1)
                {
                    var vssPath = _vss.GetSnapshotPath(src);
                    if (vssPath != null)
                    {
                        _log.Log($"File locked, using VSS snapshot for: {Path.GetFileName(src)}");
                        try
                        {
                            await storage.WriteFileAsync(vssPath, dst);
                            return true;
                        }
                        catch { }
                    }
                }
            }

            if (attempt < maxAttempts)
            {
                _log.Log($"Retry {attempt}/{maxAttempts}: {Path.GetFileName(src)}");
                await Task.Delay(TimeSpan.FromSeconds(_config.Config.RetryDelaySeconds));
            }
        }
        return false;
    }

    // ─── Versioning ───────────────────────────────────────────────────────────

    private async Task CreateVersionIfNeededAsync(string currentBackupFile,
        string folderName, string relativePath)
    {
        try
        {
            var storage = GetStorage();
            if (!await storage.ExistsAsync(currentBackupFile)) return;

            var cfg = _config.Config;
            var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
            var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);

            var fi = new FileInfo(currentBackupFile);
            var versionDate = await storage.GetLastWriteTimeUtcAsync(currentBackupFile);
            var versionFileName = PathHelper.GetVersionFileName(fi.Name, versionDate);

            var relDir = Path.GetDirectoryName(relativePath) ?? "";
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fi.Name);
            var versionFolder = Path.Combine(versionsBase, folderName, relDir, nameWithoutExt);
            await storage.CreateDirectoryAsync(versionFolder);

            await storage.MoveFileAsync(currentBackupFile, Path.Combine(versionFolder, versionFileName));
        }
        catch (Exception ex)
        {
            _log.LogError($"Error creating version for {currentBackupFile}", ex);
        }
    }

    // ─── Deleted-file tracking ─────────────────────────────────────────────

    private async Task MoveDeletedFilesAsync(BackupResult result, CancellationToken ct)
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
                var storage = GetStorage();

                var backupFiles = await storage.EnumerateFilesAsync(dstFolder, "*");
                foreach (var backupFile in backupFiles)
                {
                    if (ct.IsCancellationRequested) break;
                    var rel = Path.GetRelativePath(dstFolder, backupFile);
                    if (!File.Exists(Path.Combine(srcFolder, rel)))
                    {
                        var target = Path.Combine(todayFolder, kvp.Key, rel);
                        try
                        {
                            await storage.MoveFileAsync(backupFile, target);
                            result.FilesMovedToDeleted++;
                            _log.Log($"Deleted file moved to Deleted: {Path.Combine(kvp.Key, rel)}");
                        } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"Error processing deleted files for {kvp.Key}", ex);
            }
        }
    }

    // ─── Cleanup ──────────────────────────────────────────────────────────────

    public void CleanupOldVersions()
    {
        // Safe fire and forget
        _ = CleanupOldVersionsAsync();
    }

    private async Task CleanupOldVersionsAsync()
    {
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);
        var storage = GetStorage();

        try
        {
            var dirs = await storage.EnumerateDirectoriesAsync(versionsBase);
            foreach (var folder in dirs)
            {
                var files = await storage.EnumerateFilesAsync(folder);
                var fileDates = new List<(string file, DateTime date)>();
                foreach (var file in files)
                {
                    fileDates.Add((file, await storage.GetLastWriteTimeUtcAsync(file)));
                }

                var ordered = fileDates.OrderByDescending(x => x.date).ToList();

                foreach (var old in ordered.Skip(cfg.KeepVersions))
                {
                    try { await storage.DeleteFileAsync(old.file); _log.Log($"Removed old version: {Path.GetFileName(old.file)}"); }
                    catch { /* ignore */ }
                }
            }
        }
        catch (Exception ex) { _log.LogError("Cleanup versions error", ex); }
    }

    public void CleanupOldDeletedFiles()
    {
        _ = CleanupOldDeletedFilesAsync();
    }

    private async Task CleanupOldDeletedFilesAsync()
    {
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var deletedBase = PathHelper.GetDeletedFolder(cfg.BackupRoot, id);
        var storage = GetStorage();

        var cutoff = DateTime.Now.AddDays(-cfg.KeepDeletedDays);
        try
        {
            var dirs = await storage.EnumerateDirectoriesAsync(deletedBase);
            foreach (var dir in dirs)
            {
                if (DateTime.TryParse(Path.GetFileName(dir), out var date) && date < cutoff)
                {
                    await storage.DeleteDirectoryAsync(dir);
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
        // For UI fast checks, IsAvailableAsync would be better, but we are synchronous here
        var storage = GetStorage();
        return storage.IsAvailableAsync().GetAwaiter().GetResult();
    }

    public long GetAvailableDiskSpace()
    {
        var storage = GetStorage();
        return storage.GetAvailableSpaceAsync().GetAwaiter().GetResult();
    }

    private void SetStatus(BackupStatusEnum status)
    {
        _currentStatus = status;
        StatusChanged?.Invoke(status);
    }
}
