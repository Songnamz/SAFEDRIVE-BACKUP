using System.IO;

namespace SafeDriveBackup.Helpers;

public static class SafeCopyHelper
{
    private const string TempExtension = ".safedrive-tmp";

    /// <summary>
    /// Copies to a temp file first, then renames — prevents broken partial backups.
    /// </summary>
    public static async Task<bool> SafeCopyAsync(string sourceFile, string destFile,
        CancellationToken cancellationToken = default)
    {
        var tempFile = destFile + TempExtension;

        try
        {
            var destDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            await using (var src = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            await using (var dst = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                await src.CopyToAsync(dst, cancellationToken);
            }

            // Preserve source timestamp so change detection stays accurate
            File.SetLastWriteTimeUtc(tempFile, new FileInfo(sourceFile).LastWriteTimeUtc);

            if (File.Exists(destFile)) File.Delete(destFile);
            File.Move(tempFile, destFile);

            return true;
        }
        catch
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { /* ignore */ }
            return false;
        }
    }

    /// <summary>
    /// Moves a file; auto-handles destination name conflicts by appending a counter.
    /// </summary>
    public static bool SafeMove(string sourceFile, string destFile)
    {
        try
        {
            var destDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            if (File.Exists(destFile))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(destFile);
                var ext = Path.GetExtension(destFile);
                var dir = Path.GetDirectoryName(destFile)!;
                var counter = 1;
                while (File.Exists(destFile))
                {
                    destFile = Path.Combine(dir, $"{nameWithoutExt}_{counter++}{ext}");
                }
            }

            File.Move(sourceFile, destFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
