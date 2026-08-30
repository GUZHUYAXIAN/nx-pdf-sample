using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        /// <summary>仅测试使用的条目处理器替换点；生产代码不得设置。null = 真实 NX 处理器。</summary>
        internal static Func<IBatchItemProcessor>? ProcessorOverride { get; set; }

        /// <summary>仅测试使用的快照写入替换点；null = 真实原子协议写入。第二参数为 ResultPath。</summary>
        internal static Action<JobResult, string>? SnapshotWriterOverride { get; set; }

        public JobResult Run(JobRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var machine = new BatchStateMachine(
                CreateProcessor(),
                new TargetProbe(),
                new CancellationFlag(request.CancellationFlagPath));
            var snapshotWriter = SnapshotWriterOverride ?? DefaultSnapshotWriter;
            return machine.Run(request, null, snapshot => snapshotWriter(snapshot, request.ResultPath));
        }

        private static void DefaultSnapshotWriter(JobResult result, string resultPath)
        {
            JobJsonSerializer.WriteToFile(result, resultPath);
        }

        private IBatchItemProcessor CreateProcessor()
        {
            var factory = ProcessorOverride;
            return factory is not null ? factory() : CreateNxProcessor();
        }

        // 非 内联：保证测试替换处理器时，NXOpen 依赖的类型加载不会发生。
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NxJobItemProcessor CreateNxProcessor() => new NxJobItemProcessor();

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
