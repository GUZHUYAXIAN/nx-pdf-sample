using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Drawing;
using NxDrawingPdfExporter.Core.Jobs;

namespace NxDrawingPdfExporter.Worker
{
    internal static class Program
    {
        private const int UsageError = 2;
        private const int InventoryFailure = 20;
        private const int ExportFailure = 22;
        private const int ReportWriteFailure = 21;

        public static int Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                PrintUsage();
                return UsageError;
            }

            if (string.Equals(args[0], "--inventory", StringComparison.Ordinal) && args.Length == 3)
            {
                return RunInventory(args[1], Path.GetFullPath(args[2]));
            }

            if (string.Equals(args[0], "--export", StringComparison.Ordinal) && args.Length == 4)
            {
                return RunExport(args[1], Path.GetFullPath(args[2]), Path.GetFullPath(args[3]));
            }

            if (string.Equals(args[0], "--run-job", StringComparison.Ordinal) && args.Length == 2)
            {
                return RunJob(Path.GetFullPath(args[1]));
            }

            PrintUsage();
            return UsageError;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("用法: NxDrawingPdfExporter.Worker.exe --inventory <drawing-prt> <report-json>");
            Console.Error.WriteLine("      NxDrawingPdfExporter.Worker.exe --export <drawing-prt> <temp-pdf> <report-json>");
            Console.Error.WriteLine("      NxDrawingPdfExporter.Worker.exe --run-job <job-json>");
        }

        private static int RunJob(string jobPath)
        {
            JobRequest request;
            try
            {
                request = JobJsonSerializer.ReadFromFile<JobRequest>(jobPath);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("任务文件读取失败: " + Sanitize(error.Message));
                return UsageError;
            }

            JobResult result;
            try
            {
                result = new NxBatchRunner().Run(request);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.ToString());
                // 致命错误不得销毁批处理运行器已持久化的逐文件快照：
                // 读取最后一份可信快照并补写未决条目的失败结果。
                result = FatalResultMerger.Merge(request, TryReadResultSnapshot(request.ResultPath), error.Message, DateTime.UtcNow);
            }

            try
            {
                JobJsonSerializer.WriteToFile(result, request.ResultPath);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("结果写入失败: " + Sanitize(error.Message));
                return ReportWriteFailure;
            }

            var summary = ResultSummary.From(result.Files);
            Console.WriteLine(summary.ToChineseSummary());
            return result.Files.Any(f => f.Status == FileResultStatus.Failed) || result.FatalError != null
                ? ExportFailure
                : 0;
        }

        /// <summary>
        /// 读取最后一份持久化快照用于致命合并。缺失或损坏时返回 null：
        /// 合并器随后把所有条目标记为失败，本次运行仍以非零码退出，
        /// 不会把“没有可恢复结果”误报为成功。
        /// </summary>
        private static JobResult? TryReadResultSnapshot(string resultPath)
        {
            try
            {
                return JobJsonSerializer.ReadFromFile<JobResult>(resultPath);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int RunInventory(string sourcePath, string reportPath)
        {
            SheetInventoryReport report;
            SheetNameMap? nameMap = null;
            try
            {
                var inventory = new NxPartProcessor().Inventory(sourcePath, out var collectedMap);
                var issues = SheetInventoryReportRules.Validate(inventory);
                if (issues.Count > 0)
                {
                    report = SheetInventoryReport.Failed("清点报告结构不完整: " + string.Join(" ", issues));
                }
                else
                {
                    report = inventory;
                    nameMap = collectedMap;
                }
            }
            catch (Exception error)
            {
                report = SheetInventoryReport.Failed("NX 制图页清点失败: " + Sanitize(error.Message));
            }

            try
            {
                if (nameMap != null)
                {
                    WriteJson(Path.Combine(Path.GetDirectoryName(reportPath)!, "sheet-name-map.json"), nameMap);
                }

                WriteJson(reportPath, report);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("清点报告写入失败: " + Sanitize(error.Message));
                return ReportWriteFailure;
            }

            return report.Success ? 0 : InventoryFailure;
        }

        private static int RunExport(string sourcePath, string tempPdfPath, string reportPath)
        {
            ExportReport report;
            SheetNameMap? nameMap = null;
            try
            {
                var export = new NxFileJobRunner().ExportSingle(sourcePath, tempPdfPath, out var collectedMap);
                var issues = ExportReportRules.Validate(export);
                if (issues.Count > 0)
                {
                    report = ExportReport.Failed("导出报告结构不完整: " + string.Join(" ", issues));
                }
                else
                {
                    report = export;
                    nameMap = collectedMap;
                }
            }
            catch (Exception error)
            {
                // Full detail goes to stderr (run artifacts only); the report
                // message stays a single sanitized line.
                Console.Error.WriteLine(error.ToString());
                report = ExportReport.Failed("NX 导出失败: " + Sanitize(error.Message));
            }

            if (!report.Success)
            {
                // A failed PRT must never leave a partial PDF in the temp
                // output location owned by this run.
                report = DeleteFailedTempOutput(tempPdfPath, report);
            }

            try
            {
                if (nameMap != null)
                {
                    WriteJson(Path.Combine(Path.GetDirectoryName(reportPath)!, "export-name-map.json"), nameMap);
                }

                WriteJson(reportPath, report);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("导出报告写入失败: " + Sanitize(error.Message));
                return ReportWriteFailure;
            }

            return report.Success ? 0 : ExportFailure;
        }

        private static ExportReport DeleteFailedTempOutput(string tempPdfPath, ExportReport report)
        {
            try
            {
                if (File.Exists(tempPdfPath))
                {
                    File.Delete(tempPdfPath);
                }
            }
            catch (Exception error)
            {
                return ExportReport.Failed(report.Failure + "（清理失败的临时 PDF 时出错: " + Sanitize(error.Message) + "）");
            }

            return report;
        }

        private static void WriteJson(string path, object graph)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("报告路径缺少目录。");
            }

            Directory.CreateDirectory(directory);
            var serializer = new DataContractJsonSerializer(graph.GetType());
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                serializer.WriteObject(stream, graph);
                stream.Flush();
            }
        }

        private static string Sanitize(string message)
        {
            return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
