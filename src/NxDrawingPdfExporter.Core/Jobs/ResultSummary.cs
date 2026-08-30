using System;
using System.Collections.Generic;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Jobs
{
    /// <summary>逐状态计数的结果汇总，用于 GUI 结果面板与日志。</summary>
    public sealed class ResultSummary
    {
        public int Total { get; private set; }

        public int Succeeded { get; private set; }

        public int Overwritten { get; private set; }

        public int SkippedExisting { get; private set; }

        public int PureModel { get; private set; }

        public int NoValidSheets { get; private set; }

        public int NameConflicts { get; private set; }

        public int Cancelled { get; private set; }

        public int Failed { get; private set; }

        public bool HasFailures => Failed > 0;

        public static ResultSummary From(IReadOnlyList<FileResult> results)
        {
            if (results is null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            var summary = new ResultSummary { Total = results.Count };
            foreach (var result in results)
            {
                switch (result.Status)
                {
                    case FileResultStatus.Success:
                        summary.Succeeded++;
                        break;
                    case FileResultStatus.Overwritten:
                        summary.Overwritten++;
                        break;
                    case FileResultStatus.SkippedExisting:
                        summary.SkippedExisting++;
                        break;
                    case FileResultStatus.PureModel:
                        summary.PureModel++;
                        break;
                    case FileResultStatus.NoValidSheets:
                        summary.NoValidSheets++;
                        break;
                    case FileResultStatus.NameConflict:
                        summary.NameConflicts++;
                        break;
                    case FileResultStatus.Cancelled:
                        summary.Cancelled++;
                        break;
                    case FileResultStatus.Failed:
                        summary.Failed++;
                        break;
                    default:
                        throw new InvalidOperationException($"未知的文件结果状态: {result.Status}");
                }
            }

            return summary;
        }

        public string ToChineseSummary()
        {
            return $"共 {Total} 个文件：成功 {Succeeded}，覆盖 {Overwritten}，已有跳过 {SkippedExisting}，"
                + $"纯模型 {PureModel}，无有效页 {NoValidSheets}，名称冲突 {NameConflicts}，"
                + $"取消 {Cancelled}，失败 {Failed}。";
        }
    }
}
