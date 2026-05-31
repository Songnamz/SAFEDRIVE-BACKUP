using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class SetupViewModel : BaseViewModel
{
    private readonly ConfigService _config;
    private readonly BackupService _backup;

    public event Action? SetupCompleted;

    private int _step = 1;
    private string _destination = "";

    public int Step { get => _step; private set { SetProperty(ref _step, value); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); } }
    public bool IsStep1 => _step == 1;
    public bool IsStep2 => _step == 2;
    public bool IsStep3 => _step == 3;

    public string Destination
    {
        get => _destination;
        set { SetProperty(ref _destination, value); OnPropertyChanged(nameof(CanFinish)); }
    }

    private bool _desktop = true, _documents = true, _downloads = true, _pictures = true, _music, _videos;
    public bool Desktop   { get => _desktop;   set => SetProperty(ref _desktop,   value); }
    public bool Documents { get => _documents; set => SetProperty(ref _documents, value); }
    public bool Downloads { get => _downloads; set => SetProperty(ref _downloads, value); }
    public bool Pictures  { get => _pictures;  set => SetProperty(ref _pictures,  value); }
    public bool Music     { get => _music;     set => SetProperty(ref _music,     value); }
    public bool Videos    { get => _videos;    set => SetProperty(ref _videos,    value); }

    public bool CanFinish => !string.IsNullOrWhiteSpace(Destination);

    public System.Windows.Input.ICommand NextCommand    { get; }
    public System.Windows.Input.ICommand BackCommand    { get; }
    public System.Windows.Input.ICommand BrowseCommand  { get; }
    public System.Windows.Input.ICommand FinishCommand  { get; }

    public SetupViewModel(ConfigService config, BackupService backup)
    {
        _config = config;
        _backup = backup;

        NextCommand   = new RelayCommand(() => Step++,  () => _step < 3);
        BackCommand   = new RelayCommand(() => Step--,  () => _step > 1);
        BrowseCommand = new RelayCommand(Browse);
        FinishCommand = new RelayCommand(Finish, () => CanFinish);
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

    private void Finish()
    {
        var cfg = _config.Config;
        cfg.BackupRoot = Destination;
        cfg.SelectedFolders["Desktop"]   = Desktop;
        cfg.SelectedFolders["Documents"] = Documents;
        cfg.SelectedFolders["Downloads"] = Downloads;
        cfg.SelectedFolders["Pictures"]  = Pictures;
        cfg.SelectedFolders["Music"]     = Music;
        cfg.SelectedFolders["Videos"]    = Videos;
        cfg.SetupComplete = true;
        _config.Save();
        _backup.SetupLogFolder();
        SetupCompleted?.Invoke();
    }
}
