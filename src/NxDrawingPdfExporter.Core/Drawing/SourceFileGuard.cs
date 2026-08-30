using System;
using System.IO;
using System.Security.Cryptography;

namespace NxDrawingPdfExporter.Core.Drawing
{
    public sealed class SourceFileSnapshot
    {
        public string FullPath { get; set; } = "";

        public long Length { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public string Sha256 { get; set; } = "";
    }

    public sealed class SourceFileGuard
    {
        public SourceFileSnapshot Capture(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("源 PRT 文件不存在。", fullPath);
            }

            return new SourceFileSnapshot
            {
                FullPath = fullPath,
                Length = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                Sha256 = ComputeSha256(fullPath)
            };
        }

        public bool HasChanged(SourceFileSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var current = Capture(snapshot.FullPath);
            return current.Length != snapshot.Length ||
                   current.LastWriteTimeUtc != snapshot.LastWriteTimeUtc ||
                   !string.Equals(current.Sha256, snapshot.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }
    }
}
