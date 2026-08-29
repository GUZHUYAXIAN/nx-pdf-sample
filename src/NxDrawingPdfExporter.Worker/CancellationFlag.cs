using System;
using System.IO;
using NxDrawingPdfExporter.Core.Jobs;

namespace NxDrawingPdfExporter.Worker
{
    /// <summary>基于标志文件的取消探测。GUI 创建该文件即表示请求取消。</summary>
    internal sealed class CancellationFlag : ICancellationFlag
    {
        private readonly string path;

        public CancellationFlag(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("取消标志路径不能为空。", nameof(path));
            }

            this.path = path;
        }

        public bool IsRequested => File.Exists(path);
    }
}
