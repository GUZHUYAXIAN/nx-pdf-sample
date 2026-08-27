using System.Collections.Generic;

namespace NxDrawingPdfExporter.Core.Input
{
    public interface IFileSystem
    {
        bool DirectoryExists(string path);

        bool FileExists(string path);

        string GetFullPath(string path);

        IReadOnlyList<string> EnumeratePrtFiles(string folder, bool includeSubfolders);
    }
}
