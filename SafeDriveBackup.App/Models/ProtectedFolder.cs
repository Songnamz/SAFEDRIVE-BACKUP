namespace SafeDriveBackup.Models;

public class ProtectedFolder
{
    public string Name { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string StatusText { get; set; } = "Not selected";
    public BackupStatusEnum Status { get; set; } = BackupStatusEnum.NotConfigured;
    public bool IsSelected { get; set; }
}
