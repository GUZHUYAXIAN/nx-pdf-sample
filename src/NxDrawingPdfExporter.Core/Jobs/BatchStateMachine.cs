using System;
using System.Collections.Generic;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Jobs
{
    /// <summary>取消标志。仅在两个文件之间检查，绝不中断正在进行的原子导出。</summary>
    public interface ICancellationFlag
    {
        bool IsRequested { get; }
    }

    /// <summary>单个条目的 NX 导出处理器。实现必须串行调用。</summary>
    public interface IBatchItemProcessor
    {
        FileResult Process(JobItem item);
    }

    /// <summary>目标 PDF 存在性探测，用于互斥的“跳过已有”策略。</summary>
    public interface IBatchTargetProbe
    {
        bool TargetExists(string finalOutputPath);
    }

    /// <summary>输出预检查产生的逐条目事实。</summary>
    public sealed class BatchItemPreflight
    {
        public JobItem Item { get; set; } = new JobItem();

        /// <summary>统一输出目录中的名称冲突项绝不启动。</summary>
        public bool NameConflict { get; set; }
    }

    /// <summary>
    /// 顺序批处理状态机：逐个条目推进，单文件失败继续后续文件，
    /// 取消只在文件边界生效，每个文件后回调权威结果快照。
    /// 不使用 Task.WhenAll、Parallel 或第二个 NX 会话。
    /// </summary>
    public sealed class BatchStateMachine
    {
        private readonly IBatchItemProcessor processor;
        private readonly IBatchTargetProbe targetProbe;
        private readonly ICancellationFlag cancellation;

        public BatchStateMachine(IBatchItemProcessor processor, IBatchTargetProbe targetProbe, ICancellationFlag cancellation)
        {
            this.processor = processor ?? throw new ArgumentNullException(nameof(processor));
            this.targetProbe = targetProbe ?? throw new ArgumentNullException(nameof(targetProbe));
            this.cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>snapshot 在每个文件状态落定后被调用，用于原子重写 result.json。</summary>
        public JobResult Run(JobRequest request, IReadOnlyList<BatchItemPreflight>? preflight = null, Action<JobResult>? snapshot = null)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!Enum.IsDefined(typeof(ExistingPdfPolicy), request.ExistingPdfPolicy))
            {
                throw new InvalidOperationException($"未知的已有 PDF 策略: {request.ExistingPdfPolicy}");
            }

            var startedUtc = DateTime.UtcNow;
            var result = new JobResult
            {
                RunId = request.RunId,
                // Snapshots are written after every file, before the final end
                // time exists; default(DateTime) cannot be serialized as JSON.
                StartedUtc = startedUtc,
                EndedUtc = startedUtc
            };

            var files = new List<FileResult>(request.Items.Length);
            for (var index = 0; index < request.Items.Length; index++)
            {
                var item = request.Items[index];
                if (cancellation.IsRequested)
                {
                    result.Cancelled = true;
                    AppendCancelledRemainder(files, request.Items, index);
                    break;
                }

                if (preflight is not null && index < preflight.Count && preflight[index].NameConflict)
                {
                    files.Add(new FileResult
                    {
                        SourcePath = item.SourcePath,
                        FinalOutputPath = item.FinalOutputPath,
                        Status = FileResultStatus.NameConflict,
                        Message = "输出名称冲突，该项未执行。"
                    });
                }
                else if (request.ExistingPdfPolicy == ExistingPdfPolicy.Skip && targetProbe.TargetExists(item.FinalOutputPath))
                {
                    files.Add(new FileResult
                    {
                        SourcePath = item.SourcePath,
                        FinalOutputPath = item.FinalOutputPath,
                        Status = FileResultStatus.SkippedExisting,
                        Message = "已存在，已跳过。"
                    });
                }
                else
                {
                    files.Add(ProcessOne(item));
                }

                result.Files = files.ToArray();
                snapshot?.Invoke(result);
            }

            result.Files = files.ToArray();
            result.EndedUtc = DateTime.UtcNow;
            return result;
        }

        private FileResult ProcessOne(JobItem item)
        {
            try
            {
                var fileResult = processor.Process(item);
                if (fileResult is null)
                {
                    return new FileResult
                    {
                        SourcePath = item.SourcePath,
                        FinalOutputPath = item.FinalOutputPath,
                        Status = FileResultStatus.Failed,
                        Message = "导出处理器未返回结果。"
                    };
                }

                return fileResult;
            }
            catch (Exception error)
            {
                return new FileResult
                {
                    SourcePath = item.SourcePath,
                    FinalOutputPath = item.FinalOutputPath,
                    Status = FileResultStatus.Failed,
                    Message = SingleLine(error.Message)
                };
            }
        }

        private static void AppendCancelledRemainder(List<FileResult> files, JobItem[] items, int startIndex)
        {
            for (var index = startIndex; index < items.Length; index++)
            {
                files.Add(new FileResult
                {
                    SourcePath = items[index].SourcePath,
                    FinalOutputPath = items[index].FinalOutputPath,
                    Status = FileResultStatus.Cancelled,
                    Message = "已取消，未开始。"
                });
            }
        }

        private static string SingleLine(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "未知错误"
                : message.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
