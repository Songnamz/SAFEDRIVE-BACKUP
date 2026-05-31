using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class DestinationViewModel : BaseViewModel
{
    private readonly ConfigService _config;
    private readonly BackupService _backup;

    private string _destination = "";
    private string _backupMode = "Continuous";
    private string _statusMessage = "";
    private bool _destinationAvailable;
    private string _availableSpace = "";

    public string Destination
    {
        get => _destination;
        set => SetProperty(ref _destination, value);
    }

    public string BackupMode
    {
        get => _backupMode;
        set
        {
            if (!SetProperty(ref _backupMode, value)) return;
            // Notify all radio-button properties that derive from this value
            OnPropertyChanged(nameof(ModeContinuous));
            OnPropertyChanged(nameof(Mode30Min));
            OnPropertyChanged(nameof(Mode1Hour));
            OnPropertyChanged(nameof(ModeDaily));
            OnPropertyChanged(nameof(ModeDriveConnect));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool DestinationAvailable
    {
        get => _destinationAvailable;
        set => SetProperty(ref _destinationAvailable, value);
    }

    public string AvailableSpace
    {
        get => _availableSpace;
        set => SetProperty(ref _availableSpace, value);
    }

    // Backup mode selections (radio-button style)
    public bool ModeContinuous   { get => _backupMode == "Continuous";   set { if (value) BackupMode = "Continuous";   } }
    public bool Mode30Min        { get => _backupMode == "Every30Min";   set { if (value) BackupMode = "Every30Min";   } }
    public bool Mode1Hour        { get => _backupMode == "Every1Hour";   set { if (value) BackupMode = "Every1Hour";   } }
    public bool ModeDaily        { get => _backupMode == "Daily";        set { if (value) BackupMode = "Daily";        } }
    public bool ModeDriveConnect { get => _backupMode == "DriveConnect"; set { if (value) BackupMode = "DriveConnect"; } }

    public System.Windows.Input.ICommand BrowseCommand { get; }
    public System.Windows.Input.ICommand SaveCommand   { get; }
    public System.Windows.Input.ICommand CheckCommand  { get; }

    public DestinationViewModel(ConfigService config, BackupService backup)
    {
        _config = config;
        _backup = backup;

        BrowseCommand = new RelayCommand(Browse);
        SaveCommand   = new RelayCommand(Save);
        CheckCommand  = new RelayCommand(CheckAvailability);

        Load();
    }

    public void Load()
    {
        var cfg = _config.Config;
        Destination = cfg.BackupRoot;
        BackupMode  = cfg.BackupMode; // setter notifies all radio properties
        CheckAvailability();
    }

    private void Browse()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select backup destination folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            Destination = dlg.SelectedPath;
    }

    private void Save()
    {
        var cfg = _config.Config;
        cfg.BackupRoot = Destination;
        cfg.BackupMode = BackupMode;
        _config.Save();
        _backup.SetupLogFolder();
        CheckAvailability();
        StatusMessage = "Destination saved.";
    }

    private void CheckAvailability()
    {
        DestinationAvailable = _backup.IsDestinationAvailable();

        if (DestinationAvailable)
        {
            var bytes = _backup.GetAvailableDiskSpace();
            AvailableSpace = FormatBytes(bytes) + " free";
            StatusMessage = "Destination is available.";
        }
        else
        {
            AvailableSpace = "—";
            StatusMessage = string.IsNullOrEmpty(Destination)
                ? "No destination configured."
                : "Destination is not reachable.";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}
