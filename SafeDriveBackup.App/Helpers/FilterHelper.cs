using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace SafeDriveBackup.Helpers;

public static class FilterHelper
{
    public static bool IsExcluded(string path, IEnumerable<string> excludedPatterns)
    {
        if (string.IsNullOrEmpty(path)) return false;

        var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var fileName = Path.GetFileName(path);

        foreach (var pattern in excludedPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            // Simple exact match for directory names like "node_modules" or ".git"
            if (parts.Contains(pattern, StringComparer.OrdinalIgnoreCase))
                return true;

            // Wildcard match for file names like "~$*" or "*.tmp"
            if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
                return true;
        }

        return false;
    }
}
