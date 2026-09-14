using System;
using System.IO;

namespace NxDrawingPdfExporter.App.Logging;

/// <summary>只接收控制器脱敏后的 NX 诊断；没有诊断时不创建目录。</summary>
internal sealed class NxDetectionLog : ILogSink
{
    private readonly string directory;
    private RunLog? log;

    public NxDetectionLog(string? directory = null)
    {
        this.directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NxDrawingPdfExporter", "diagnostics");
    }

    public void Write(string message)
    {
        log ??= new RunLog(directory);
        log.Write(message);
    }
}
