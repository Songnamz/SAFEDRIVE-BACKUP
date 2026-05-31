using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly ConfigService _config;

    private bool _startWithWindows;
    private int  _keepVersions;
    private int  _keepDeletedDays;
    private int  _periodicScanHours;
    private int  _ransomwareThreshold;
    private int  _ransomwareWindowMinutes;
    private int  _retryDelaySeconds;
    private int  _maxRetryAttempts;
    private string _statusMessage = "";

    public bool StartWithWindows        { get => _startWithWindows;        set => SetProperty(ref _startWithWindows,        value); }
    public int  KeepVersions            { get => _keepVersions;            set => SetProperty(ref _keepVersions,            value); }
    public int  KeepDeletedDays         { get => _keepDeletedDays;         set => SetProperty(ref _keepDeletedDays,         value); }
    public int  PeriodicScanHours       { get => _periodicScanHours;       set => SetProperty(ref _periodicScanHours,       value); }
    public int  RansomwareThreshold     { get => _ransomwareThreshold;     set => SetProperty(ref _ransomwareThreshold,     value); }
    public int  RansomwareWindowMinutes { get => _ransomwareWindowMinutes; set => SetProperty(ref _ransomwareWindowMinutes, value); }
    public int  RetryDelaySeconds       { get => _retryDelaySeconds;       set => SetProperty(ref _retryDelaySeconds,       value); }
    public int  MaxRetryAttempts        { get => _maxRetryAttempts;        set => SetProperty(ref _maxRetryAttempts,        value); }
    public string StatusMessage         { get => _statusMessage;           set => SetProperty(ref _statusMessage,           value); }

    public System.Windows.Input.ICommand SaveCommand  { get; }
    public System.Windows.Input.ICommand ResetCommand { get; }

    public SettingsViewModel(ConfigService config)
    {
        _config = config;
        SaveCommand  = new RelayCommand(Save);
        ResetCommand = new RelayCommand(Reset);
        Load();
    }

    private void Load()
    {
        var cfg = _config.Config;
        StartWithWindows        = _config.IsStartWithWindowsEnabled();
        KeepVersions            = cfg.KeepVersions;
        KeepDeletedDays         = cfg.KeepDeletedDays;
        PeriodicScanHours       = cfg.PeriodicScanHours;
        RansomwareThreshold     = cfg.RansomwareThreshold;
        RansomwareWindowMinutes = cfg.RansomwareWindowMinutes;
        RetryDelaySeconds       = cfg.RetryDelaySeconds;
        MaxRetryAttempts        = cfg.MaxRetryAttempts;
        StatusMessage = "";
    }

    private void Save()
    {
        var cfg = _config.Config;
        cfg.KeepVersions            = Math.Max(1, KeepVersions);
        cfg.KeepDeletedDays         = Math.Max(1, KeepDeletedDays);
        cfg.PeriodicScanHours       = Math.Max(1, PeriodicScanHours);
        cfg.RansomwareThreshold     = Math.Max(10, RansomwareThreshold);
        cfg.RansomwareWindowMinutes = Math.Max(1, RansomwareWindowMinutes);
        cfg.RetryDelaySeconds       = Math.Max(5, RetryDelaySeconds);
        cfg.MaxRetryAttempts        = Math.Max(1, MaxRetryAttempts);
        _config.SetStartWithWindows(StartWithWindows);
        _config.Save();

        // Verify registry write succeeded and reflect actual state back to UI
        var actualStartWithWindows = _config.IsStartWithWindowsEnabled();
        if (StartWithWindows != actualStartWithWindows)
        {
            StartWithWindows = actualStartWithWindows;
            StatusMessage = "Settings saved. Note: 'Start with Windows' could not be changed — administrator rights may be required.";
        }
        else
        {
            StatusMessage = "Settings saved.";
        }
    }

    private void Reset()
    {
        KeepVersions            = 5;
        KeepDeletedDays         = 30;
        PeriodicScanHours       = 4;
        RansomwareThreshold     = 500;
        RansomwareWindowMinutes = 5;
        RetryDelaySeconds       = 30;
        MaxRetryAttempts        = 3;
        StatusMessage = "Defaults restored. Click Save to apply.";
    }
}
