using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SafeDriveBackup.Services;

public interface IStorageProvider
{
    Task<bool> ExistsAsync(string path);
    Task<long> GetFileSizeAsync(string path);
    Task<DateTime> GetLastWriteTimeUtcAsync(string path);
    Task<string> GetSha256HashAsync(string path);
    Task ReadFileAsync(string sourcePath, string localDestinationPath);
    Task WriteFileAsync(string sourceLocalPath, string destinationPath);
    Task WriteStreamAsync(Stream stream, string destinationPath);
    Task MoveFileAsync(string sourcePath, string destinationPath);
    Task DeleteFileAsync(string path);
    Task DeleteDirectoryAsync(string path);
    Task CreateDirectoryAsync(string path);
    Task<bool> IsAvailableAsync();
    Task<long> GetAvailableSpaceAsync();
    Task<IEnumerable<string>> EnumerateFilesAsync(string path, string searchPattern = "*");
    Task<IEnumerable<string>> EnumerateDirectoriesAsync(string path);
    Task WriteAllTextAsync(string path, string content);
}
