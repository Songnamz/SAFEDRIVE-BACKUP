using System.IO;

namespace SafeDriveBackup.Services;

public class LogService
{
    private readonly object _lock = new();
    private string _logFolder = "";

    public void SetLogFolder(string folder)
    {
        _logFolder = folder;
        try { Directory.CreateDirectory(folder); } catch { /* ignore */ }
    }

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        System.Diagnostics.Debug.WriteLine(line);

        if (string.IsNullOrEmpty(_logFolder)) return;

        try
        {
            lock (_lock)
            {
                var logFile = Path.Combine(_logFolder, $"backup-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
        }
        catch { /* never crash due to logging */ }
    }

    public void LogError(string message, Exception? ex = null)
    {
        Log($"ERROR: {(ex != null ? $"{message}: {ex.Message}" : message)}");
    }

    public string[] GetTodayLogs(int maxLines = 1000)
    {
        if (string.IsNullOrEmpty(_logFolder)) return [];
        try
        {
            var logFile = Path.Combine(_logFolder, $"backup-{DateTime.Now:yyyy-MM-dd}.log");
            if (!File.Exists(logFile)) return [];
            return File.ReadLines(logFile).TakeLast(maxLines).ToArray();
        }
        catch { return []; }
    }

    public string[] GetLogFileNames()
    {
        if (string.IsNullOrEmpty(_logFolder)) return [];
        try
        {
            return Directory.GetFiles(_logFolder, "backup-*.log")
                .OrderByDescending(f => f)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToArray();
        }
        catch { return []; }
    }

    public string[] ReadLogFile(string fileName, int maxLines = 1000)
    {
        if (string.IsNullOrEmpty(_logFolder)) return [];
        try
        {
            var path = Path.Combine(_logFolder, fileName);
            if (!File.Exists(path)) return [];
            return File.ReadLines(path).TakeLast(maxLines).ToArray();
        }
        catch { return []; }
    }

    public string LogFolder => _logFolder;
}
