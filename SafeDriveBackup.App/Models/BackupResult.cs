namespace SafeDriveBackup.Models;

public class BackupResult
{
    public int FilesCopied { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesFailed { get; set; }
    public int FilesMovedToDeleted { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public bool Success => FilesFailed == 0 && Errors.Count == 0;
    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
}
