using System;
using System.IO;

namespace NxDrawingPdfExporter.App.Logging
{
    /// <summary>日志出口抽象，便于测试注入。</summary>
    public interface ILogSink
    {
        void Write(string message);
    }

    /// <summary>写入运行目录 run.log 的本地日志。内容只落在本机，无任何网络遥测。</summary>
    public sealed class RunLog : ILogSink
    {
        private readonly object gate = new();
        private readonly string path;

        public RunLog(string runDirectory)
        {
            if (string.IsNullOrWhiteSpace(runDirectory))
            {
                throw new ArgumentException("运行目录不能为空。", nameof(runDirectory));
            }

            Directory.CreateDirectory(runDirectory);
            path = Path.Combine(runDirectory, "run.log");
        }

        public string LogPath => path;

        public void Write(string message)
        {
            lock (gate)
            {
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
        }
    }
}
