using System.IO;

namespace NxDrawingPdfExporter.Core.Output
{
    /// <summary>真实目标文件存在性探测。</summary>
    public sealed class PhysicalTargetProbe : ITargetProbe
    {
        public bool TargetExists(string path) => File.Exists(path);
    }
}
