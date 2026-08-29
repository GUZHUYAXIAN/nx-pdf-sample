using System;
using System.IO;
using System.Linq;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Jobs;

namespace NxDrawingPdfExporter.Worker
{
    /// <summary>
    /// Worker 内的顺序批处理：一个 NX 会话逐个处理 job 条目，
    /// 通过 Core 状态机推进，并在每个文件后原子重写权威 result.json。
    /// </summary>
    internal sealed class NxBatchRunner
    {
        public JobResult Run(JobRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var machine = new BatchStateMachine(
                new NxJobItemProcessor(),
                new TargetProbe(),
                new CancellationFlag(request.CancellationFlagPath));
            return machine.Run(request, null, snapshot => JobJsonSerializer.WriteToFile(snapshot, request.ResultPath));
        }

        private sealed class TargetProbe : IBatchTargetProbe
        {
            public bool TargetExists(string finalOutputPath) => File.Exists(finalOutputPath);
        }

        private sealed class NxJobItemProcessor : IBatchItemProcessor
        {
            private readonly NxFileJobRunner runner = new NxFileJobRunner();

            public FileResult Process(JobItem item)
            {
                var report = runner.ExportSingle(item.SourcePath, item.WorkerTempOutputPath, out var nameMap);
                var exported = report.Sheets.Where(s => s.Selected).ToArray();
                var skipped = report.Sheets.Where(s => !s.Selected).ToArray();
                return new FileResult
                {
                    SourcePath = item.SourcePath,
                    FinalOutputPath = item.FinalOutputPath,
                    Status = report.Outcome,
                    Message = report.Success ? "临时 PDF 已生成，等待验证发布。" : report.Failure ?? "导出失败。",
                    ExportedSheets = nameMap is null
                        ? Array.Empty<string>()
                        : exported.Select(s => nameMap.Entries.First(e => e.Token == s.NameToken).Name).ToArray(),
                    SkippedSheets = nameMap is null
                        ? Array.Empty<string>()
                        : skipped.Select(s => nameMap.Entries.First(e => e.Token == s.NameToken).Name).ToArray(),
                    ElapsedMilliseconds = report.ElapsedMilliseconds
                };
            }
        }
    }
}
