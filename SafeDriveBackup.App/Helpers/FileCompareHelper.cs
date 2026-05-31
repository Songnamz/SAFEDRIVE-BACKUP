using System.IO;
using System.Security.Cryptography;

namespace SafeDriveBackup.Helpers;

public static class FileCompareHelper
{
    /// <summary>
    /// Fast comparison by size and last-write time. Tolerates 2-second FAT32 rounding.
    /// </summary>
    public static bool IsDifferent(string sourceFile, string destFile)
    {
        try
        {
            var src = new FileInfo(sourceFile);
            var dst = new FileInfo(destFile);

            if (src.Length != dst.Length) return true;

            // 2-second tolerance for FAT32 file systems
            return Math.Abs((src.LastWriteTimeUtc - dst.LastWriteTimeUtc).TotalSeconds) > 2;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Accurate comparison using SHA-256. Slower — opt-in only.
    /// </summary>
    public static bool IsDifferentByHash(string sourceFile, string destFile)
    {
        try
        {
            return !GetSha256(sourceFile).Equals(GetSha256(destFile), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static string GetSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
    }
}
