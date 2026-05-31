using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ConfigService _config;
    private readonly BackupService _backup;
    private readonly FileWatcherService _watcher;

    private BaseViewModel? _current;
    private bool _setupVisible;
    private bool _navVisible;

    public BaseViewModel? Current   { get => _current;      set => SetProperty(ref _current,      value); }
    public bool SetupVisible        { get => _setupVisible; set => SetProperty(ref _setupVisible, value); }
    public bool NavVisible          { get => _navVisible;   set => SetProperty(ref _navVisible,   value); }

    // Child VMs
    public SetupViewModel           SetupVM      { get; }
    public StatusViewModel          StatusVM     { get; }
    public FolderSelectionViewModel FoldersVM    { get; }
    public DestinationViewModel     DestVM       { get; }
    public RestoreViewModel         RestoreVM    { get; }
    public SettingsViewModel        SettingsVM   { get; }
    public LogsViewModel            LogsVM       { get; }

    // Nav selected state
    private string _activeSection = "";
    public bool IsStatusSel      => _activeSection == "status";
    public bool IsFoldersSel     => _activeSection == "folders";
    public bool IsDestinationSel => _activeSection == "destination";
    public bool IsRestoreSel     => _activeSection == "restore";
    public bool IsSettingsSel    => _activeSection == "settings";
    public bool IsLogsSel        => _activeSection == "logs";

    // Navigation commands
    public System.Windows.Input.ICommand NavStatusCommand      { get; }
    public System.Windows.Input.ICommand NavFoldersCommand     { get; }
    public System.Windows.Input.ICommand NavDestCommand        { get; }
    public System.Windows.Input.ICommand NavRestoreCommand     { get; }
    public System.Windows.Input.ICommand NavSettingsCommand    { get; }
    public System.Windows.Input.ICommand NavLogsCommand        { get; }
    public System.Windows.Input.ICommand BackupNowCommand      { get; }

    public MainViewModel(
        ConfigService config, BackupService backup, FileWatcherService watcher,
        SetupViewModel setupVM, StatusViewModel statusVM,
        FolderSelectionViewModel foldersVM, DestinationViewModel destVM,
        RestoreViewModel restoreVM, SettingsViewModel settingsVM, LogsViewModel logsVM)
    {
        _config = config;
        _backup = backup;
        _watcher = watcher;
        SetupVM      = setupVM;
        StatusVM     = statusVM;
        FoldersVM    = foldersVM;
        DestVM       = destVM;
        RestoreVM    = restoreVM;
        SettingsVM   = settingsVM;
        LogsVM       = logsVM;

        NavStatusCommand   = new RelayCommand(() => NavigateTo("status"));
        NavFoldersCommand  = new RelayCommand(() => NavigateTo("folders"));
        NavDestCommand     = new RelayCommand(() => NavigateTo("destination"));
        NavRestoreCommand  = new RelayCommand(() => NavigateTo("restore"));
        NavSettingsCommand = new RelayCommand(() => NavigateTo("settings"));
        NavLogsCommand     = new RelayCommand(() => NavigateTo("logs"));
        BackupNowCommand   = new AsyncRelayCommand(async () => await backup.StartBackupAsync());

        SetupVM.SetupCompleted += OnSetupCompleted;
    }

    public void Initialize()
    {
        if (!_config.Config.SetupComplete)
        {
            Current      = SetupVM;
            SetupVisible = true;
            NavVisible   = false;
        }
        else
        {
            SetupVisible = false;
            NavVisible   = true;
            NavigateTo("status");
        }
    }

    private void OnSetupCompleted()
    {
        SetupVisible = false;
        NavVisible   = true;
        NavigateTo("status");
        _watcher.StartWatching();
    }

    private void NavigateTo(string section)
    {
        _activeSection = section;
        OnPropertyChanged(nameof(IsStatusSel));
        OnPropertyChanged(nameof(IsFoldersSel));
        OnPropertyChanged(nameof(IsDestinationSel));
        OnPropertyChanged(nameof(IsRestoreSel));
        OnPropertyChanged(nameof(IsSettingsSel));
        OnPropertyChanged(nameof(IsLogsSel));

        Current = section switch
        {
            "status"      => StatusVM,
            "folders"     => FoldersVM,
            "destination" => DestVM,
            "restore"     => RestoreVM,
            "settings"    => SettingsVM,
            "logs"        => LogsVM,
            _ => StatusVM
        };

        // Refresh data when navigating to a section
        switch (section)
        {
            case "status":      StatusVM.Refresh();   break;
            case "restore":     RestoreVM.Refresh();  break;
            case "logs":        LogsVM.Refresh();     break;
            case "destination": DestVM.Load();        break;
            case "folders":     FoldersVM.Load();     break;
        }
    }

    public void GoToLogs() => NavigateTo("logs");
}
