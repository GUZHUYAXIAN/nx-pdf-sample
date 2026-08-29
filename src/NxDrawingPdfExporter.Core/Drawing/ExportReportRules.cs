using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Drawing
{
    // Structural completeness rules for an export report. A report is usable
    // only when success, selection coherence, order indices, size facts, and
    // identity tokens all agree; exit codes and file existence are never
    // enough. A successful report requires an unchanged source file and every
    // selected sheet to carry at least one actual drafting view.
    public static class ExportReportRules
    {
        private static readonly Regex NameTokenPattern = new Regex("^[0-9a-f]{16}$", RegexOptions.Compiled);

        public static IReadOnlyList<string> Validate(ExportReport? report)
        {
            if (report == null)
            {
                return new[] { "报告对象为空。" };
            }

            var issues = new List<string>();
            if (!report.Success)
            {
                if (string.IsNullOrWhiteSpace(report.Failure))
                {
                    issues.Add("失败报告缺少原因。");
                }

                if (report.Outcome is FileResultStatus.Success or FileResultStatus.Overwritten)
                {
                    issues.Add("失败报告的结果状态无效。");
                }

                return issues;
            }

            if (report.Outcome != FileResultStatus.Success)
            {
                issues.Add("成功报告的结果状态必须是 Success。");
            }

            if (!string.IsNullOrWhiteSpace(report.Failure))
            {
                issues.Add("成功报告不应携带失败原因。");
            }

            if (report.LoadDiagnostics == null)
            {
                issues.Add("缺少加载诊断数组。");
            }

            if (report.Sheets == null)
            {
                issues.Add("缺少图纸页数组。");
                return issues;
            }

            if (report.Sheets.Length == 0)
            {
                issues.Add("成功报告未包含任何图纸页事实。");
            }

            var selectedCount = 0;
            var seenTokens = new HashSet<string>(StringComparer.Ordinal);
            var exportOrderIndices = new HashSet<int>();
            for (var index = 0; index < report.Sheets.Length; index++)
            {
                var sheet = report.Sheets[index];
                if (sheet == null)
                {
                    issues.Add($"第 {index} 项图纸页为空。");
                    continue;
                }

                if (sheet.NativeIndex != index)
                {
                    issues.Add($"第 {index} 项的原生枚举索引与数组位置不一致。");
                }

                if (sheet.ExportOrderIndex != index)
                {
                    issues.Add($"第 {index} 项的导出顺序索引与数组位置不一致。");
                }

                if (sheet.ExportOrderIndex >= 0 && sheet.ExportOrderIndex < report.Sheets.Length && !exportOrderIndices.Add(sheet.ExportOrderIndex))
                {
                    issues.Add($"第 {index} 项的导出顺序索引重复。");
                }

                if (sheet.DraftingViewCount < 0)
                {
                    issues.Add($"第 {index} 项制图视图数量为负。");
                }

                if (string.IsNullOrWhiteSpace(sheet.Units))
                {
                    issues.Add($"第 {index} 项缺少单位。");
                }

                if (string.IsNullOrEmpty(sheet.NameToken) || !NameTokenPattern.IsMatch(sheet.NameToken))
                {
                    issues.Add($"第 {index} 项名称令牌无效。");
                }
                else if (!seenTokens.Add(sheet.NameToken))
                {
                    issues.Add($"第 {index} 项名称令牌重复。");
                }

                if (sheet.Selected)
                {
                    selectedCount++;
                    if (sheet.DraftingViewCount < 1)
                    {
                        issues.Add($"第 {index} 项被选中但缺少实际制图视图。");
                    }

                    if (!IsPositiveFinite(sheet.Length) || !IsPositiveFinite(sheet.Height))
                    {
                        issues.Add($"第 {index} 项图幅尺寸无效。");
                    }
                }
                else if (sheet.DraftingViewCount != 0)
                {
                    issues.Add($"第 {index} 项未被选中但包含实际制图视图。");
                }
            }

            if (report.Sheets.All(s => s != null) && exportOrderIndices.Count != report.Sheets.Length)
            {
                issues.Add("导出顺序索引不是 0..N-1 的完整排列。");
            }

            if (selectedCount != report.SelectedCount)
            {
                issues.Add("选中页数量与报告的 SelectedCount 不一致。");
            }

            if (report.Sheets.Length - selectedCount != report.SkippedCount)
            {
                issues.Add("跳过页数量与报告的 SkippedCount 不一致。");
            }

            if (!report.SourceUnchanged)
            {
                issues.Add("成功报告必须确认源 PRT 未变化。");
            }

            return issues;
        }

        private static bool IsPositiveFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        }
    }
}
