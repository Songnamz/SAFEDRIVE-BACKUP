namespace SafeDriveBackup.Models;

public enum RestoreItemType
{
    Current,
    Version,
    Deleted
}

public class RestoreItem
{
    public string FileName { get; set; } = "";
    public string OriginalFolder { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTime BackupDate { get; set; }
    public long FileSize { get; set; }
    public RestoreItemType ItemType { get; set; }
    public int VersionCount { get; set; }
    public string RelativePath { get; set; } = "";

    public string DisplaySize => FormatSize(FileSize);
    public string DisplayVersionCount => ItemType == RestoreItemType.Current && VersionCount > 0
        ? VersionCount.ToString()
        : "—";
    public string DisplayDate => BackupDate.ToString("yyyy-MM-dd HH:mm:ss");
    public string TypeLabel => ItemType switch
    {
        RestoreItemType.Current => "Current",
        RestoreItemType.Version => "Version",
        RestoreItemType.Deleted => "Deleted",
        _ => "Unknown"
    };

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}
