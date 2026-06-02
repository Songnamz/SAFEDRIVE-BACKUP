using System.Globalization;
using System.IO;
using SafeDriveBackup.Helpers;
using SafeDriveBackup.Models;

namespace SafeDriveBackup.Services;

public class RestoreService
{
    private readonly ConfigService _config;
    private readonly LogService _log;

    public RestoreService(ConfigService config, LogService log)
    {
        _config = config;
        _log = log;
    }

    private IStorageProvider GetStorage()
    {
        var cfg = _config.Config;
        if (cfg.DestinationType == DestinationType.S3Compatible)
            return new S3StorageProvider(cfg);
        
        return new LocalStorageProvider(cfg.BackupRoot ?? "");
    }

    public async Task<List<RestoreItem>> GetCurrentFilesAsync()
    {
        var items = new List<RestoreItem>();
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return items;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var currentBase = PathHelper.GetCurrentFolder(cfg.BackupRoot, id);
        var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);
        var storage = GetStorage();
        try
        {
            var files = await storage.EnumerateFilesAsync(currentBase, "*");
            foreach (var file in files)
            {
                var fiName = Path.GetFileName(file);
                var rel = Path.GetRelativePath(currentBase, file);
                var parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                // Skip files not inside a named folder (unexpected backup structure)
                if (parts.Length < 2) continue;

                var folder = parts[0];
                var relWithoutFolder = string.Join(Path.DirectorySeparatorChar, parts.Skip(1));

                var verFolder = Path.Combine(versionsBase, folder,
                    Path.GetDirectoryName(relWithoutFolder) ?? "",
                    Path.GetFileNameWithoutExtension(fiName));
                
                var versionFiles = await storage.EnumerateFilesAsync(verFolder, "*");
                var versionCount = versionFiles.Count();

                items.Add(new RestoreItem
                {
                    FileName = fiName,
                    OriginalFolder = folder,
                    FullPath = file,
                    BackupDate = await storage.GetLastWriteTimeUtcAsync(file),
                    FileSize = await storage.GetFileSizeAsync(file),
                    ItemType = RestoreItemType.Current,
                    RelativePath = rel,
                    VersionCount = versionCount
                });
            }
        }
        catch (Exception ex) { _log.LogError("GetCurrentFiles error", ex); }

        return items.OrderBy(i => i.OriginalFolder).ThenBy(i => i.FileName).ToList();
    }

    public async Task<List<RestoreItem>> GetVersionFilesAsync()
    {
        var items = new List<RestoreItem>();
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return items;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);
        var storage = GetStorage();
        try
        {
            var files = await storage.EnumerateFilesAsync(versionsBase, "*");
            foreach (var file in files)
            {
                var fiName = Path.GetFileName(file);
                var rel = Path.GetRelativePath(versionsBase, file);
                var parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                // Skip files not inside a named folder
                if (parts.Length < 2) continue;

                var folder = parts[0];

                items.Add(new RestoreItem
                {
                    FileName = fiName,
                    OriginalFolder = folder,
                    FullPath = file,
                    BackupDate = await storage.GetLastWriteTimeUtcAsync(file),
                    FileSize = await storage.GetFileSizeAsync(file),
                    ItemType = RestoreItemType.Version,
                    RelativePath = rel
                });
            }
        }
        catch (Exception ex) { _log.LogError("GetVersionFiles error", ex); }

        return items.OrderBy(i => i.OriginalFolder).ThenBy(i => i.FileName)
                    .ThenByDescending(i => i.BackupDate).ToList();
    }

    public async Task<List<RestoreItem>> GetDeletedFilesAsync()
    {
        var items = new List<RestoreItem>();
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return items;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var deletedBase = PathHelper.GetDeletedFolder(cfg.BackupRoot, id);
        var storage = GetStorage();
        try
        {
            var files = await storage.EnumerateFilesAsync(deletedBase, "*");
            foreach (var file in files)
            {
                var fiName = Path.GetFileName(file);
                var rel = Path.GetRelativePath(deletedBase, file);
                var parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                // Deleted folder structure: {yyyy-MM-dd}\{folderName}\{file}
                DateTime.TryParseExact(
                    parts.Length > 0 ? parts[0] : "",
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var deletedDate);

                var folder = parts.Length > 1 ? parts[1] : "";

                items.Add(new RestoreItem
                {
                    FileName = fiName,
                    OriginalFolder = folder,
                    FullPath = file,
                    BackupDate = deletedDate != default ? deletedDate : await storage.GetLastWriteTimeUtcAsync(file),
                    FileSize = await storage.GetFileSizeAsync(file),
                    ItemType = RestoreItemType.Deleted,
                    RelativePath = rel
                });
            }
        }
        catch (Exception ex) { _log.LogError("GetDeletedFiles error", ex); }

        return items.OrderByDescending(i => i.BackupDate).ThenBy(i => i.FileName).ToList();
    }

    public async Task<bool> RestoreFileAsync(RestoreItem item, string? customDestination = null)
    {
        try
        {
            string targetPath;

            if (!string.IsNullOrEmpty(customDestination))
            {
                targetPath = Path.Combine(customDestination, item.FileName);
            }
            else
            {
                targetPath = GetOriginalPath(item);
                if (string.IsNullOrEmpty(targetPath))
                {
                    _log.Log($"Cannot determine original path for: {item.FileName}");
                    return false;
                }

                // Avoid overwriting existing file without user approval — add suffix
                if (File.Exists(targetPath))
                {
                    var nwe = Path.GetFileNameWithoutExtension(targetPath);
                    var ext = Path.GetExtension(targetPath);
                    var dir = Path.GetDirectoryName(targetPath)!;
                    targetPath = Path.Combine(dir,
                        $"{nwe}_restored_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
            }

            var destDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir); // Always local destination!

            var storage = GetStorage();
            
            await storage.ReadFileAsync(item.FullPath, targetPath);
            _log.Log($"Restored: {item.FileName} → {targetPath}");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"Restore error: {item.FileName}", ex);
            return false;
        }
    }

    private string GetOriginalPath(RestoreItem item)
    {
        try
        {
            var srcRoot = PathHelper.GetSourceFolderPath(item.OriginalFolder);
            var parts = item.RelativePath.Split(Path.DirectorySeparatorChar);

            string relFile = item.ItemType switch
            {
                RestoreItemType.Deleted => string.Join(Path.DirectorySeparatorChar, parts.Skip(2)),
                RestoreItemType.Current => string.Join(Path.DirectorySeparatorChar, parts.Skip(1)),
                _ => item.FileName
            };

            return Path.Combine(srcRoot, relFile);
        }
        catch { return ""; }
    }
}
