using System;
using System.Collections.Generic;
using System.IO;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Output
{
    public interface ITargetProbe
    {
        bool TargetExists(string path);
    }

    public interface IOutputPlanner
    {
        IReadOnlyList<OutputPlanItem> Plan(
            IReadOnlyList<string> sources,
            OutputMode mode,
            string? unifiedDirectory,
            ExistingPdfPolicy policy);
    }

    public sealed class OutputPlanner : IOutputPlanner
    {
        private readonly ITargetProbe targetProbe;

        public OutputPlanner(ITargetProbe targetProbe)
        {
            this.targetProbe = targetProbe ?? throw new ArgumentNullException(nameof(targetProbe));
        }

        public IReadOnlyList<OutputPlanItem> Plan(
            IReadOnlyList<string> sources,
            OutputMode mode,
            string? unifiedDirectory,
            ExistingPdfPolicy policy)
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (!Enum.IsDefined(typeof(OutputMode), mode))
            {
                throw new InvalidOperationException($"未知的输出模式: {mode}");
            }

            if (!Enum.IsDefined(typeof(ExistingPdfPolicy), policy))
            {
                throw new InvalidOperationException($"未知的已有 PDF 策略: {policy}");
            }

            if (mode == OutputMode.UnifiedDirectory &&
                (string.IsNullOrWhiteSpace(unifiedDirectory) || !Path.IsPathRooted(unifiedDirectory)))
            {
                throw new InvalidOperationException("统一输出目录必须是非空绝对路径。");
            }

            var items = new List<OutputPlanItem>(sources.Count);
            foreach (var source in sources)
            {
                var outputDirectory = mode == OutputMode.BesideSource
                    ? Path.GetDirectoryName(source)
                    : unifiedDirectory;
                var outputName = Path.ChangeExtension(Path.GetFileName(source), ".pdf");
                var finalOutputPath = Path.Combine(outputDirectory!, outputName);
                items.Add(new OutputPlanItem
                {
                    SourcePath = source,
                    FinalOutputPath = finalOutputPath,
                    Status = targetProbe.TargetExists(finalOutputPath) && policy == ExistingPdfPolicy.Skip
                        ? OutputPlanStatus.SkippedExisting
                        : OutputPlanStatus.Ready,
                    Message = targetProbe.TargetExists(finalOutputPath) && policy == ExistingPdfPolicy.Skip
                        ? "目标 PDF 已存在，已跳过。"
                        : ""
                });
            }

            var collisions = new Dictionary<string, List<OutputPlanItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (!collisions.TryGetValue(item.FinalOutputPath, out var sameTargetItems))
                {
                    sameTargetItems = new List<OutputPlanItem>();
                    collisions.Add(item.FinalOutputPath, sameTargetItems);
                }

                sameTargetItems.Add(item);
            }

            foreach (var pair in collisions)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                foreach (var item in pair.Value)
                {
                    item.Status = OutputPlanStatus.NameConflict;
                    item.Message = "输出名称冲突。";
                }
            }

            return items;
        }
    }
}
