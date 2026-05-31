using System.IO;
using System.Text.Json;
using SafeDriveBackup.Helpers;
using SafeDriveBackup.Models;

namespace SafeDriveBackup.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private AppConfig _config = new();
    public AppConfig Config => _config;

    public string LocalConfigPath =>
        Path.Combine(PathHelper.AppDataFolder, "safedrive-config.json");

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(PathHelper.AppDataFolder);
            if (File.Exists(LocalConfigPath))
            {
                var json = File.ReadAllText(LocalConfigPath);
                _config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            _config = new AppConfig();
        }
    }

    public event Action? ConfigSaved;

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(PathHelper.AppDataFolder);
            File.WriteAllText(LocalConfigPath, JsonSerializer.Serialize(_config, JsonOptions));
            TrySaveToDestination();
        }
        catch { /* ignore */ }
        ConfigSaved?.Invoke();
    }

    private void TrySaveToDestination()
    {
        if (string.IsNullOrEmpty(_config.BackupRoot)) return;
        try
        {
            var id = PathHelper.GetBackupIdentifier(_config.ComputerName, _config.Username);
            var folder = Path.Combine(_config.BackupRoot, id);
            if (Directory.Exists(folder))
                File.WriteAllText(
                    Path.Combine(folder, "safedrive-config.json"),
                    JsonSerializer.Serialize(_config, JsonOptions));
        }
        catch { /* destination may be offline */ }
    }

    public void SetStartWithWindows(bool enable)
    {
        _config.StartWithWindows = enable;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;
            if (enable)
            {
                var exe = Environment.ProcessPath
                    ?? Path.Combine(System.AppContext.BaseDirectory, "SafeDriveBackup.exe");
                key.SetValue("SafeDriveBackup", $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue("SafeDriveBackup", throwOnMissingValue: false);
            }
        }
        catch { /* ignore registry errors */ }
    }

    public bool IsStartWithWindowsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("SafeDriveBackup") != null;
        }
        catch { return false; }
    }
}
