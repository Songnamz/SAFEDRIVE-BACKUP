namespace SafeDriveBackup.Models;

public class AppConfig
{
    public string AppName { get; set; } = "SafeDrive Backup";
    public string ComputerName { get; set; } = Environment.MachineName;
    public string Username { get; set; } = Environment.UserName;
    public string BackupRoot { get; set; } = "";
    public string BackupMode { get; set; } = "Continuous";
    public int KeepVersions { get; set; } = 5;
    public int KeepDeletedDays { get; set; } = 30;
    public Dictionary<string, bool> SelectedFolders { get; set; } = new()
    {
        ["Desktop"] = true,
        ["Documents"] = true,
        ["Downloads"] = true,
        ["Pictures"] = true,
        ["Music"] = false,
        ["Videos"] = false
    };
    public DateTime? LastSuccessfulBackup { get; set; }
    public bool StartWithWindows { get; set; } = false;
    public bool SetupComplete { get; set; } = false;
    public int RansomwareThreshold { get; set; } = 500;
    public int RansomwareWindowMinutes { get; set; } = 5;
    public int PeriodicScanHours { get; set; } = 4;
    public int RetryDelaySeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
}
