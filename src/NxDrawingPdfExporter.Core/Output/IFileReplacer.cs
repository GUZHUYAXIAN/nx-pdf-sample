using System;
using System.IO;

namespace NxDrawingPdfExporter.Core.Output
{
    /// <summary>
    /// 目标 PDF 的低层原子替换操作。
    /// <see cref="DeleteFile"/> 只能由调用方用于本次运行自有的临时/备份路径，不得用于用户其他文件。
    /// </summary>
    public interface IFileReplacer
    {
        void MoveIntoPlace(string tempPath, string finalPath);

        void ReplaceWithBackup(string tempPath, string finalPath, string backupPath);

        void RestoreBackup(string backupPath, string finalPath);

        void DeleteFile(string path);
    }

    /// <summary>基于同卷 File.Move / File.Replace / File.Copy 的默认实现。</summary>
    public sealed class FileReplacer : IFileReplacer
    {
        public void MoveIntoPlace(string tempPath, string finalPath)
        {
            File.Move(tempPath, finalPath);
        }

        public void ReplaceWithBackup(string tempPath, string finalPath, string backupPath)
        {
            File.Replace(tempPath, finalPath, backupPath);
        }

        public void RestoreBackup(string backupPath, string finalPath)
        {
            File.Copy(backupPath, finalPath, overwrite: true);
            File.Delete(backupPath);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }
}
