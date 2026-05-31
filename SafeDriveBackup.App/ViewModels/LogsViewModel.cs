using System.Collections.ObjectModel;
using System.IO;
using SafeDriveBackup.Services;

namespace SafeDriveBackup.ViewModels;

public class LogsViewModel : BaseViewModel
{
    private readonly LogService _log;

    private string _selectedLogFile = "";
    private string _logContent = "";

    public ObservableCollection<string> LogFiles { get; } = new();

    public string SelectedLogFile
    {
        get => _selectedLogFile;
        set
        {
            SetProperty(ref _selectedLogFile, value);
            if (!string.IsNullOrEmpty(value)) LoadLogFile(value);
        }
    }

    public string LogContent
    {
        get => _logContent;
        set => SetProperty(ref _logContent, value);
    }

    public System.Windows.Input.ICommand RefreshCommand  { get; }
    public System.Windows.Input.ICommand OpenFolderCommand { get; }

    public LogsViewModel(LogService log)
    {
        _log = log;
        RefreshCommand    = new RelayCommand(Refresh);
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public void Refresh()
    {
        LogFiles.Clear();
        foreach (var f in _log.GetLogFileNames())
            LogFiles.Add(f);

        if (LogFiles.Count > 0)
        {
            // Auto-select today's log
            SelectedLogFile = LogFiles[0];
        }
        else
        {
            LogContent = "(No log files found)";
        }
    }

    private void LoadLogFile(string fileName)
    {
        var lines = _log.ReadLogFile(fileName, maxLines: 2000);
        LogContent = lines.Length == 0
            ? "(Log file is empty)"
            : string.Join(Environment.NewLine, lines);
    }

    private void OpenFolder()
    {
        var folder = _log.LogFolder;
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", folder); }
            catch { /* ignore */ }
        }
    }
}
