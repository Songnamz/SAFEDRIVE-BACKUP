using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SafeDriveBackup.Helpers;

namespace SafeDriveBackup.Services;

public class LocalStorageProvider : IStorageProvider
{
    private readonly string _rootPath;

    public LocalStorageProvider(string rootPath)
    {
        _rootPath = rootPath;
    }

    private string GetFullPath(string path) => path;

    public Task<bool> ExistsAsync(string path)
    {
        return Task.FromResult(File.Exists(GetFullPath(path)));
    }

    public Task<long> GetFileSizeAsync(string path)
    {
        var fi = new FileInfo(GetFullPath(path));
        return Task.FromResult(fi.Exists ? fi.Length : 0);
    }

    public Task<DateTime> GetLastWriteTimeUtcAsync(string path)
    {
        var fi = new FileInfo(GetFullPath(path));
        return Task.FromResult(fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue);
    }

    public Task<string> GetSha256HashAsync(string path)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(GetFullPath(path));
            return Task.FromResult(BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower());
        }
        catch
        {
            return Task.FromResult("");
        }
    }

    public async Task ReadFileAsync(string sourcePath, string localDestinationPath)
    {
        var fullSrc = GetFullPath(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(localDestinationPath)!);
        await SafeCopyHelper.SafeCopyAsync(fullSrc, localDestinationPath);
    }

    public async Task WriteFileAsync(string sourceLocalPath, string destinationPath)
    {
        var fullDst = GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDst)!);
        await SafeCopyHelper.SafeCopyAsync(sourceLocalPath, fullDst);
    }

    public async Task WriteStreamAsync(Stream stream, string destinationPath)
    {
        var fullDst = GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDst)!);
        await using var fs = new FileStream(fullDst, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fs);
    }

    public Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        var fullSrc = GetFullPath(sourcePath);
        var fullDst = GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDst)!);
        File.Move(fullSrc, fullDst, true);
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string path)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string path)
    {
        var fullPath = GetFullPath(path);
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(string path)
    {
        Directory.CreateDirectory(GetFullPath(path));
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync()
    {
        if (string.IsNullOrEmpty(_rootPath)) return Task.FromResult(false);
        try
        {
            return Task.FromResult(Directory.Exists(_rootPath) || new DriveInfo(Path.GetPathRoot(_rootPath)!).IsReady);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<long> GetAvailableSpaceAsync()
    {
        if (string.IsNullOrEmpty(_rootPath)) return Task.FromResult(0L);
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_rootPath)!);
            if (drive.IsReady)
                return Task.FromResult(drive.AvailableFreeSpace);
        }
        catch { }
        return Task.FromResult(0L);
    }

    public Task<IEnumerable<string>> EnumerateFilesAsync(string path, string searchPattern = "*")
    {
        var fullPath = GetFullPath(path);
        if (!Directory.Exists(fullPath)) return Task.FromResult(Enumerable.Empty<string>());
        return Task.FromResult(Directory.EnumerateFiles(fullPath, searchPattern));
    }

    public Task<IEnumerable<string>> EnumerateDirectoriesAsync(string path)
    {
        var fullPath = GetFullPath(path);
        if (!Directory.Exists(fullPath)) return Task.FromResult(Enumerable.Empty<string>());
        return Task.FromResult(Directory.EnumerateDirectories(fullPath));
    }

    public Task WriteAllTextAsync(string path, string content)
    {
        var fullPath = GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return Task.CompletedTask;
    }
}
