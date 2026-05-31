namespace SafeDriveBackup.Models;

public enum BackupStatusEnum
{
    Protected,
    BackingUp,
    Warning,
    Error,
    Paused,
    NotConfigured
}

public class BackupStatus
{
    public BackupStatusEnum Status { get; set; } = BackupStatusEnum.NotConfigured;
    public string StatusText { get; set; } = "Not Configured";
    public DateTime? LastSuccessfulBackup { get; set; }
    public int FilesCopiedLastRun { get; set; }
    public int FilesUpdatedLastRun { get; set; }
    public int FilesFailedLastRun { get; set; }
    public bool IsPaused { get; set; }
    public string CurrentOperation { get; set; } = "";
    public bool IsBackingUp { get; set; }
}
