using System.IO;

namespace SafeDriveBackup.Helpers;

public static class PathHelper
{
    public static string GetSourceFolderPath(string folderName) => folderName switch
    {
        "Desktop"   => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        "Pictures"  => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "Music"     => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        "Videos"    => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        _ => throw new ArgumentException($"Unknown folder name: {folderName}")
    };

    public static string GetBackupIdentifier(string computerName, string username)
    {
        var name = $"{computerName}_{username}";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    public static string GetCurrentFolder(string backupRoot, string identifier) =>
        Path.Combine(backupRoot, identifier, "Current");

    public static string GetVersionsFolder(string backupRoot, string identifier) =>
        Path.Combine(backupRoot, identifier, "Versions");

    public static string GetDeletedFolder(string backupRoot, string identifier) =>
        Path.Combine(backupRoot, identifier, "Deleted");

    public static string GetLogsFolder(string backupRoot, string identifier) =>
        Path.Combine(backupRoot, identifier, "Logs");

    public static string GetVersionFileName(string fileName, DateTime versionDate)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var timestamp = versionDate.ToString("yyyy-MM-dd_HH-mm-ss");
        return string.IsNullOrEmpty(nameWithoutExt)
            ? $"file_{timestamp}{ext}"
            : $"{nameWithoutExt}_{timestamp}{ext}";
    }

    public static bool IsNetworkPath(string path) => path.StartsWith(@"\\");

    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SafeDriveBackup");
}
