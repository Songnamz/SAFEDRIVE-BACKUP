using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using SafeDriveBackup.Models;

namespace SafeDriveBackup.Services;

public class S3StorageProvider : IStorageProvider
{
    private readonly AppConfig _config;
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly string _backupRoot;

    public S3StorageProvider(AppConfig config)
    {
        _config = config;
        _bucket = config.S3BucketName;
        _backupRoot = config.BackupRoot ?? "";

        var s3Config = new AmazonS3Config
        {
            ServiceURL = config.S3EndpointUrl,
            AuthenticationRegion = config.S3Region,
            ForcePathStyle = true // Often needed for S3-compatible like MinIO/B2
        };

        if (string.IsNullOrEmpty(config.S3EndpointUrl) && !string.IsNullOrEmpty(config.S3Region))
        {
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(config.S3Region);
            s3Config.ServiceURL = null; // AWS standard
            s3Config.ForcePathStyle = false;
        }

        _client = new AmazonS3Client(config.S3AccessKey, config.S3SecretKey, s3Config);
    }

    private string GetS3Key(string path)
    {
        if (path.StartsWith(_backupRoot, StringComparison.OrdinalIgnoreCase))
            path = path.Substring(_backupRoot.Length);
        return path.TrimStart('\\', '/').Replace("\\", "/");
    }

    public async Task<bool> ExistsAsync(string path)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, GetS3Key(path));
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<long> GetFileSizeAsync(string path)
    {
        try
        {
            var res = await _client.GetObjectMetadataAsync(_bucket, GetS3Key(path));
            return res.ContentLength;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<DateTime> GetLastWriteTimeUtcAsync(string path)
    {
        try
        {
            var res = await _client.GetObjectMetadataAsync(_bucket, GetS3Key(path));
            return res.LastModified.GetValueOrDefault().ToUniversalTime();
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    public Task<string> GetSha256HashAsync(string path)
    {
        // S3 ETag is an MD5 hash usually, not SHA256. If we need strict hash comparison,
        // we'd have to store it in metadata or download the file.
        // For simplicity, we'll return an empty string, so it will fall back to timestamp/size comparison.
        return Task.FromResult("");
    }

    public async Task ReadFileAsync(string sourcePath, string localDestinationPath)
    {
        var key = GetS3Key(sourcePath);
        var request = new GetObjectRequest
        {
            BucketName = _bucket,
            Key = key
        };
        using var response = await _client.GetObjectAsync(request);
        Directory.CreateDirectory(Path.GetDirectoryName(localDestinationPath)!);
        using var fs = new FileStream(localDestinationPath, FileMode.Create, FileAccess.Write);
        await response.ResponseStream.CopyToAsync(fs);
    }

    public async Task WriteFileAsync(string sourceLocalPath, string destinationPath)
    {
        var req = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = GetS3Key(destinationPath),
            FilePath = sourceLocalPath
        };
        await _client.PutObjectAsync(req);
    }

    public async Task WriteStreamAsync(Stream stream, string destinationPath)
    {
        var req = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = GetS3Key(destinationPath),
            InputStream = stream
        };
        await _client.PutObjectAsync(req);
    }

    public async Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        // S3 does not have a Move API. We must Copy then Delete.
        var srcKey = GetS3Key(sourcePath);
        var dstKey = GetS3Key(destinationPath);
        
        await _client.CopyObjectAsync(_bucket, srcKey, _bucket, dstKey);
        await _client.DeleteObjectAsync(_bucket, srcKey);
    }

    public async Task DeleteFileAsync(string path)
    {
        await _client.DeleteObjectAsync(_bucket, GetS3Key(path));
    }

    public async Task DeleteDirectoryAsync(string path)
    {
        var prefix = GetS3Key(path);
        if (!prefix.EndsWith("/")) prefix += "/";

        var files = await EnumerateFilesInternalAsync(prefix);
        foreach (var file in files)
        {
            await _client.DeleteObjectAsync(_bucket, file);
        }
    }

    public Task CreateDirectoryAsync(string path)
    {
        // S3 is object storage; directories don't strictly exist.
        return Task.CompletedTask;
    }

    public async Task<bool> IsAvailableAsync()
    {
        if (string.IsNullOrEmpty(_config.S3AccessKey)) return false;
        try
        {
            // Ping the bucket
            await _client.GetBucketAclAsync(new GetBucketAclRequest { BucketName = _bucket });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<long> GetAvailableSpaceAsync()
    {
        // S3 is theoretically infinite
        return Task.FromResult(1024L * 1024 * 1024 * 1024 * 10); // 10 TB
    }

    private async Task<List<string>> EnumerateFilesInternalAsync(string prefix)
    {
        var result = new List<string>();
        var req = new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix };
        ListObjectsV2Response res;
        do
        {
            res = await _client.ListObjectsV2Async(req);
            result.AddRange(res.S3Objects.Select(o => o.Key));
            req.ContinuationToken = res.NextContinuationToken;
        } while (res.IsTruncated == true);
        return result;
    }

    public async Task<IEnumerable<string>> EnumerateFilesAsync(string path, string searchPattern = "*")
    {
        var prefix = GetS3Key(path);
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith("/")) prefix += "/";

        var allKeys = await EnumerateFilesInternalAsync(prefix);
        
        return allKeys.Select(k => Path.Combine(_backupRoot, k.Replace("/", "\\")));
    }

    public async Task<IEnumerable<string>> EnumerateDirectoriesAsync(string path)
    {
        var prefix = GetS3Key(path);
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith("/")) prefix += "/";

        var req = new ListObjectsV2Request { BucketName = _bucket, Prefix = prefix, Delimiter = "/" };
        var res = await _client.ListObjectsV2Async(req);
        
        return res.CommonPrefixes.Select(p => Path.Combine(_backupRoot, p.TrimEnd('/').Replace("/", "\\")));
    }

    public async Task WriteAllTextAsync(string path, string content)
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await WriteStreamAsync(ms, path);
    }
}
