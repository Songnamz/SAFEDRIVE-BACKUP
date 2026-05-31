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

    public List<RestoreItem> GetCurrentFiles()
    {
        var items = new List<RestoreItem>();
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return items;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var currentBase = PathHelper.GetCurrentFolder(cfg.BackupRoot, id);
        var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);
        if (!Directory.Exists(currentBase)) return items;

        try
        {
            foreach (var file in Directory.EnumerateFiles(currentBase, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(file);
                var rel = Path.GetRelativePath(currentBase, file);
                var parts = rel.Split(Path.DirectorySeparatorChar);

                // Skip files not inside a named folder (unexpected backup structure)
                if (parts.Length < 2) continue;

                var folder = parts[0];
                var relWithoutFolder = string.Join(Path.DirectorySeparatorChar, parts.Skip(1));

                var verFolder = Path.Combine(versionsBase, folder,
                    Path.GetDirectoryName(relWithoutFolder) ?? "",
                    Path.GetFileNameWithoutExtension(fi.Name));
                var versionCount = Directory.Exists(verFolder) ? Directory.GetFiles(verFolder).Length : 0;

                items.Add(new RestoreItem
                {
                    FileName = fi.Name,
                    OriginalFolder = folder,
                    FullPath = file,
                    BackupDate = fi.LastWriteTime,
                    FileSize = fi.Length,
                    ItemType = RestoreItemType.Current,
                    RelativePath = rel,
                    VersionCount = versionCount
                });
            }
        }
        catch (Exception ex) { _log.LogError("GetCurrentFiles error", ex); }

        return items.OrderBy(i => i.OriginalFolder).ThenBy(i => i.FileName).ToList();
    }

    public List<RestoreItem> GetVersionFiles()
    {
        var items = new List<RestoreItem>();
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return items;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var versionsBase = PathHelper.GetVersionsFolder(cfg.BackupRoot, id);
        if (!Directory.Exists(versionsBase)) return items;

        try
        {
            foreach (var file in Directory.EnumerateFiles(versionsBase, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(file);
                var rel = Path.GetRelativePath(versionsBase, file);
                var parts = rel.Split(Path.DirectorySeparatorChar);

                // Skip files not inside a named folder
                if (parts.Length < 2) continue;

                var folder = parts[0];

                items.Add(new RestoreItem
                {
                    FileName = fi.Name,
                    OriginalFolder = folder,
                    FullPath = file,
                    BackupDate = fi.LastWriteTime,
                    FileSize = fi.Length,
                    ItemType = RestoreItemType.Version,
                    RelativePath = rel
                });
            }
        }
        catch (Exception ex) { _log.LogError("GetVersionFiles error", ex); }

        return items.OrderBy(i => i.OriginalFolder).ThenBy(i => i.FileName)
                    .ThenByDescending(i => i.BackupDate).ToList();
    }

    public List<RestoreItem> GetDeletedFiles()
    {
        var items = new List<RestoreItem>();
        var cfg = _config.Config;
        if (string.IsNullOrEmpty(cfg.BackupRoot)) return items;

        var id = PathHelper.GetBackupIdentifier(cfg.ComputerName, cfg.Username);
        var deletedBase = PathHelper.GetDeletedFolder(cfg.BackupRoot, id);
        if (!Directory.Exists(deletedBase)) return items;

        try
        {
            foreach (var file in Directory.EnumerateFiles(deletedBase, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(file);
                var rel = Path.GetRelativePath(deletedBase, file);
                var parts = rel.Split(Path.DirectorySeparatorChar);

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
                    FileName = fi.Name,
                    OriginalFolder = folder,
                    FullPath = file,
                    BackupDate = deletedDate != default ? deletedDate : fi.LastWriteTime,
                    FileSize = fi.Length,
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
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

            var ok = await SafeCopyHelper.SafeCopyAsync(item.FullPath, targetPath);
            _log.Log(ok
                ? $"Restored: {item.FileName} → {targetPath}"
                : $"Restore FAILED: {item.FileName}");
            return ok;
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
