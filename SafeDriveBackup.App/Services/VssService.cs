using System;
using System.Collections.Generic;
using System.IO;
using Alphaleonis.Win32.Vss;

namespace SafeDriveBackup.Services;

public class VssService : IDisposable
{
    private readonly LogService _log;
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _volumeSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IVssBackupComponents> _components = new();

    public VssService(LogService log)
    {
        _log = log;
    }

    /// <summary>
    /// Gets a VSS snapshot path for the given file, creating a snapshot for the volume if it doesn't exist.
    /// </summary>
    public string? GetSnapshotPath(string filePath)
    {
        lock (_lock)
        {
            try
            {
                var root = Path.GetPathRoot(filePath);
                if (string.IsNullOrEmpty(root)) return null;

                if (!_volumeSnapshots.TryGetValue(root, out var snapshotDeviceObject))
                {
                    _log.Log($"Creating VSS snapshot for volume {root}...");
                    var factory = VssFactoryProvider.Default.GetVssFactory();
                    var backupComponents = factory.CreateVssBackupComponents();
                    backupComponents.InitializeForBackup(null);
                    backupComponents.SetContext(VssSnapshotContext.Backup);
                    
                    Guid snapshotSetId = backupComponents.StartSnapshotSet();
                    Guid snapshotId = backupComponents.AddToSnapshotSet(root);
                    
                    backupComponents.PrepareForBackup();
                    backupComponents.DoSnapshotSet();
                    
                    var props = backupComponents.GetSnapshotProperties(snapshotId);
                    snapshotDeviceObject = props.SnapshotDeviceObject;
                    
                    _volumeSnapshots[root] = snapshotDeviceObject;
                    _components.Add(backupComponents);
                    _log.Log($"VSS snapshot created for {root}.");
                }

                var relativePath = filePath.Substring(root.Length);
                if (relativePath.StartsWith("\\")) relativePath = relativePath.Substring(1);
                
                return Path.Combine(snapshotDeviceObject + "\\", relativePath);
            }
            catch (Exception ex)
            {
                // VSS requires Admin privileges. If this fails, we just log and return null.
                _log.LogError($"Failed to get VSS path for {filePath}", ex);
                return null;
            }
        }
    }

    /// <summary>
    /// Releases all snapshots created during the backup session.
    /// </summary>
    public void CleanupSnapshots()
    {
        lock (_lock)
        {
            foreach (var comp in _components)
            {
                try
                {
                    comp.Dispose();
                }
                catch { /* ignore */ }
            }
            _components.Clear();
            _volumeSnapshots.Clear();
        }
    }

    public void Dispose()
    {
        CleanupSnapshots();
    }
}
