using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class FolderSelectionViewModel : BaseViewModel
{
    private readonly ConfigService _config;

    private bool _desktop, _documents, _downloads, _pictures, _music, _videos;
    public bool Desktop   { get => _desktop;   set => SetProperty(ref _desktop,   value); }
    public bool Documents { get => _documents; set => SetProperty(ref _documents, value); }
    public bool Downloads { get => _downloads; set => SetProperty(ref _downloads, value); }
    public bool Pictures  { get => _pictures;  set => SetProperty(ref _pictures,  value); }
    public bool Music     { get => _music;     set => SetProperty(ref _music,     value); }
    public bool Videos    { get => _videos;    set => SetProperty(ref _videos,    value); }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public System.Windows.Input.ICommand SaveCommand { get; }

    public FolderSelectionViewModel(ConfigService config)
    {
        _config = config;
        SaveCommand = new RelayCommand(Save);
        Load();
    }

    public void Load()
    {
        var sf = _config.Config.SelectedFolders;
        Desktop   = sf.GetValueOrDefault("Desktop",   true);
        Documents = sf.GetValueOrDefault("Documents", true);
        Downloads = sf.GetValueOrDefault("Downloads", true);
        Pictures  = sf.GetValueOrDefault("Pictures",  true);
        Music     = sf.GetValueOrDefault("Music",     false);
        Videos    = sf.GetValueOrDefault("Videos",    false);
        StatusMessage = "";
    }

    private void Save()
    {
        var sf = _config.Config.SelectedFolders;
        sf["Desktop"]   = Desktop;
        sf["Documents"] = Documents;
        sf["Downloads"] = Downloads;
        sf["Pictures"]  = Pictures;
        sf["Music"]     = Music;
        sf["Videos"]    = Videos;
        _config.Save();
        StatusMessage = "Folder selection saved.";
    }
}
