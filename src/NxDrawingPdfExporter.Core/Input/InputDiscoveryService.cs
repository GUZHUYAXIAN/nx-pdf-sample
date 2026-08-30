using System;
using System.Collections.Generic;
using System.IO;

namespace NxDrawingPdfExporter.Core.Input
{
    public interface IInputDiscoveryService
    {
        IReadOnlyList<string> ScanFolder(string folder, bool includeSubfolders);

        IReadOnlyList<string> NormalizeManualSelection(IEnumerable<string> paths);
    }

    public sealed class InputDiscoveryService : IInputDiscoveryService
    {
        private readonly IFileSystem fileSystem;

        public InputDiscoveryService(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public IReadOnlyList<string> ScanFolder(string folder, bool includeSubfolders)
        {
            var fullFolder = fileSystem.GetFullPath(folder);
            if (!fileSystem.DirectoryExists(fullFolder))
            {
                throw new InvalidOperationException($"扫描文件夹不存在: {fullFolder}");
            }

            return fileSystem.EnumeratePrtFiles(fullFolder, includeSubfolders);
        }

        public IReadOnlyList<string> NormalizeManualSelection(IEnumerable<string> paths)
        {
            if (paths is null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();

            foreach (var path in paths)
            {
                var fullPath = fileSystem.GetFullPath(path);
                if (!fileSystem.FileExists(fullPath))
                {
                    missing.Add(fullPath);
                    continue;
                }

                if (seen.Add(fullPath))
                {
                    normalized.Add(fullPath);
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"所选文件不存在: {string.Join("、", missing)}");
            }

            return normalized;
        }
    }
}
