using System.Collections.Generic;
using System.IO;

namespace NxDrawingPdfExporter.Core.Input
{
    /// <summary>真实文件系统适配器。</summary>
    public sealed class PhysicalFileSystem : IFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public bool FileExists(string path) => File.Exists(path);

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public IReadOnlyList<string> EnumeratePrtFiles(string folder, bool includeSubfolders)
        {
            return new List<string>(Directory.EnumerateFiles(
                folder,
                "*.prt",
                includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));
        }
    }
}
