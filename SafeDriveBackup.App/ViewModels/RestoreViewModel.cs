using System.Collections.ObjectModel;
using SafeDriveBackup.Models;
using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class RestoreViewModel : BaseViewModel
{
    private readonly RestoreService _restore;

    private string _selectedTab = "Current";
    private RestoreItem? _selectedItem;
    private string _statusMessage = "";
    private bool _isRestoring;

    public string SelectedTab
    {
        get => _selectedTab;
        set
        {
            SetProperty(ref _selectedTab, value);
            OnPropertyChanged(nameof(IsCurrentTab));
            OnPropertyChanged(nameof(IsVersionsTab));
            OnPropertyChanged(nameof(IsDeletedTab));
            OnPropertyChanged(nameof(VersionsColumnHeader));
            LoadTab();
        }
    }

    public bool IsCurrentTab  => _selectedTab == "Current";
    public bool IsVersionsTab => _selectedTab == "Versions";
    public bool IsDeletedTab  => _selectedTab == "Deleted";

    public string VersionsColumnHeader => _selectedTab switch
    {
        "Current"  => "Versions",
        "Versions" => "Version Of",
        "Deleted"  => "Deleted On",
        _          => "Versions"
    };

    public ObservableCollection<RestoreItem> Items { get; } = new();

    public RestoreItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsRestoring
    {
        get => _isRestoring;
        set => SetProperty(ref _isRestoring, value);
    }

    public System.Windows.Input.ICommand SelectCurrentTabCommand  { get; }
    public System.Windows.Input.ICommand SelectVersionsTabCommand { get; }
    public System.Windows.Input.ICommand SelectDeletedTabCommand  { get; }
    public System.Windows.Input.ICommand RestoreToOriginalCommand { get; }
    public System.Windows.Input.ICommand RestoreToFolderCommand   { get; }
    public System.Windows.Input.ICommand RefreshCommand           { get; }

    public RestoreViewModel(RestoreService restore)
    {
        _restore = restore;

        SelectCurrentTabCommand  = new RelayCommand(() => SelectedTab = "Current");
        SelectVersionsTabCommand = new RelayCommand(() => SelectedTab = "Versions");
        SelectDeletedTabCommand  = new RelayCommand(() => SelectedTab = "Deleted");
        RestoreToOriginalCommand = new AsyncRelayCommand(RestoreToOriginal, () => _selectedItem != null && !_isRestoring);
        RestoreToFolderCommand   = new AsyncRelayCommand(RestoreToFolder,   () => _selectedItem != null && !_isRestoring);
        RefreshCommand           = new RelayCommand(Refresh);
    }

    public void Refresh() => LoadTab();

    private void LoadTab()
    {
        Items.Clear();
        StatusMessage = "Loading…";

        try
        {
            var items = _selectedTab switch
            {
                "Current"  => _restore.GetCurrentFiles(),
                "Versions" => _restore.GetVersionFiles(),
                "Deleted"  => _restore.GetDeletedFiles(),
                _ => new List<RestoreItem>()
            };

            foreach (var i in items) Items.Add(i);
            StatusMessage = $"{items.Count} item(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading items: {ex.Message}";
        }
    }

    private async Task RestoreToOriginal()
    {
        if (_selectedItem == null) return;
        await DoRestore(_selectedItem, null);
    }

    private async Task RestoreToFolder()
    {
        if (_selectedItem == null) return;

        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select restore destination folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        await DoRestore(_selectedItem, dlg.SelectedPath);
    }

    private async Task DoRestore(RestoreItem item, string? folder)
    {
        IsRestoring = true;
        StatusMessage = $"Restoring {item.FileName}…";

        var ok = await _restore.RestoreFileAsync(item, folder);

        StatusMessage = ok
            ? $"Restored successfully: {item.FileName}"
            : $"Restore failed: {item.FileName}";

        if (ok) LoadTab();

        IsRestoring = false;
    }
}
