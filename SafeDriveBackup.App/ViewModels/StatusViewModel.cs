using System.Collections.ObjectModel;
using SafeDriveBackup.Helpers;
using SafeDriveBackup.Models;
using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class StatusViewModel : BaseViewModel
{
    private readonly ConfigService _config;
    private readonly BackupService _backup;

    private string _statusText = "Not Configured";
    private string _statusColor = "#95A5A6";
    private string _lastBackup = "Never";
    private string _destination = "—";
    private string _currentOperation = "";
    private int _filesCopied, _filesUpdated, _filesFailed;
    private bool _isBackingUp;
    private bool _isPaused;
    private string _ransomwareWarning = "";
    private bool _showRansomwareWarning;

    public string StatusText      { get => _statusText;      set => SetProperty(ref _statusText,      value); }
    public string StatusColor     { get => _statusColor;     set => SetProperty(ref _statusColor,     value); }
    public string LastBackup      { get => _lastBackup;      set => SetProperty(ref _lastBackup,      value); }
    public string Destination     { get => _destination;     set => SetProperty(ref _destination,     value); }
    public string CurrentOperation{ get => _currentOperation;set => SetProperty(ref _currentOperation,value); }
    public int    FilesCopied     { get => _filesCopied;     set => SetProperty(ref _filesCopied,     value); }
    public int    FilesUpdated    { get => _filesUpdated;    set => SetProperty(ref _filesUpdated,    value); }
    public int    FilesFailed     { get => _filesFailed;     set => SetProperty(ref _filesFailed,     value); }
    public bool   IsBackingUp     { get => _isBackingUp;     set => SetProperty(ref _isBackingUp,     value); }
    public bool   IsPaused        { get => _isPaused;        set => SetProperty(ref _isPaused,        value); }
    public string RansomwareWarning { get => _ransomwareWarning; set => SetProperty(ref _ransomwareWarning, value); }
    public bool   ShowRansomwareWarning { get => _showRansomwareWarning; set => SetProperty(ref _showRansomwareWarning, value); }

    public ObservableCollection<ProtectedFolder> Folders { get; } = new();

    public System.Windows.Input.ICommand BackupNowCommand   { get; }
    public System.Windows.Input.ICommand PauseCommand       { get; }
    public System.Windows.Input.ICommand ResumeCommand      { get; }
    public System.Windows.Input.ICommand DismissWarningCommand { get; }

    public StatusViewModel(ConfigService config, BackupService backup)
    {
        _config = config;
        _backup = backup;

        BackupNowCommand   = new AsyncRelayCommand(async () => await _backup.StartBackupAsync(), () => !_backup.IsRunning && !_backup.IsPaused);
        PauseCommand       = new RelayCommand(_backup.Pause,  () => !_backup.IsPaused);
        ResumeCommand      = new RelayCommand(_backup.Resume, () =>  _backup.IsPaused);
        DismissWarningCommand = new RelayCommand(DismissWarning);

        _backup.StatusChanged   += OnStatusChanged;
        _backup.BackupCompleted += OnBackupCompleted;
        _backup.ProgressChanged += OnProgressChanged;
        _backup.RansomwareProtectionTriggered += OnRansomware;

        Refresh();
    }

    public void Refresh()
    {
        var cfg = _config.Config;
        Destination = string.IsNullOrEmpty(cfg.BackupRoot) ? "Not configured" : cfg.BackupRoot;
        LastBackup  = cfg.LastSuccessfulBackup.HasValue
            ? cfg.LastSuccessfulBackup.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : "Never";

        IsPaused = _backup.IsPaused;
        IsBackingUp = _backup.IsRunning;
        ApplyStatus(_backup.CurrentStatus);
        RefreshFolders();
    }

    private void RefreshFolders()
    {
        Folders.Clear();
        var cfg = _config.Config;

        foreach (var kvp in cfg.SelectedFolders)
        {
            var selected = kvp.Value;
            var status   = selected ? BackupStatusEnum.Protected : BackupStatusEnum.NotConfigured;
            var text     = selected ? "Protected" : "Not selected";

            try
            {
                var srcPath = PathHelper.GetSourceFolderPath(kvp.Key);
                Folders.Add(new ProtectedFolder
                {
                    Name = kvp.Key,
                    SourcePath = srcPath,
                    IsSelected = selected,
                    Status = status,
                    StatusText = text
                });
            }
            catch { /* ignore unknown folder */ }
        }
    }

    private void OnStatusChanged(BackupStatusEnum status)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsPaused = _backup.IsPaused;
            IsBackingUp = status == BackupStatusEnum.BackingUp;
            ApplyStatus(status);
        });
    }

    private void OnBackupCompleted(BackupResult result)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var cfg = _config.Config;
            LastBackup    = cfg.LastSuccessfulBackup?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";
            FilesCopied   = result.FilesCopied;
            FilesUpdated  = result.FilesUpdated;
            FilesFailed   = result.FilesFailed;
            CurrentOperation = "";
            IsBackingUp   = false;
            RefreshFolders();
        });
    }

    private void OnProgressChanged(string message)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            CurrentOperation = message);
    }

    private void OnRansomware()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var cfg = _config.Config;
            RansomwareWarning = $"Many files changed quickly. Backup paused to protect your data.\n" +
                                $"({cfg.RansomwareThreshold} files in {cfg.RansomwareWindowMinutes} minutes)";
            ShowRansomwareWarning = true;
        });
    }

    private void DismissWarning()
    {
        ShowRansomwareWarning = false;
    }

    private void ApplyStatus(BackupStatusEnum status)
    {
        (StatusText, StatusColor) = status switch
        {
            BackupStatusEnum.Protected    => ("Protected",            "#00B894"),
            BackupStatusEnum.BackingUp    => ("Backing up…",          "#0078D4"),
            BackupStatusEnum.Warning      => ("Warning",              "#FDCB6E"),
            BackupStatusEnum.Error        => ("Error",                "#D63031"),
            BackupStatusEnum.Paused       => ("Paused",               "#636E72"),
            BackupStatusEnum.NotConfigured=> ("Not Configured",       "#95A5A6"),
            _ => ("Unknown", "#95A5A6")
        };
    }
}
