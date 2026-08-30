using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NxDrawingPdfExporter.Core.Drawing
{
    // Structural completeness rules for a sheet inventory report. A report is
    // usable only when success, order indices, size facts, and identity tokens
    // are all coherent; exit codes and file existence are never enough.
    public static class SheetInventoryReportRules
    {
        private static readonly Regex NameTokenPattern = new Regex("^[0-9a-f]{16}$", RegexOptions.Compiled);

        public static IReadOnlyList<string> Validate(SheetInventoryReport report)
        {
            if (report == null)
            {
                return new[] { "报告对象为空。" };
            }

            if (!report.Success)
            {
                return string.IsNullOrWhiteSpace(report.Failure)
                    ? new[] { "失败报告缺少原因。" }
                    : Array.Empty<string>();
            }

            var issues = new List<string>();
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

                if (sheet.ExportOrderIndex >= 0 && sheet.ExportOrderIndex < report.Sheets.Length && exportOrderIndices.Add(sheet.ExportOrderIndex) == false)
                {
                    issues.Add($"第 {index} 项的导出顺序索引重复。");
                }

                if (sheet.DraftingViewCount < 0)
                {
                    issues.Add($"第 {index} 项制图视图数量为负。");
                }

                if (!IsPositiveFinite(sheet.Length) || !IsPositiveFinite(sheet.Height))
                {
                    issues.Add($"第 {index} 项图幅尺寸无效。");
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
            }

            if (report.Sheets.All(s => s != null) && exportOrderIndices.Count != report.Sheets.Length)
            {
                issues.Add("导出顺序索引不是 0..N-1 的完整排列。");
            }

            return issues;
        }

        private static bool IsPositiveFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        }
    }
}
