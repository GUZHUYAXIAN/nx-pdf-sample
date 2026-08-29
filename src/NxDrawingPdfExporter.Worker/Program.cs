using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using NxDrawingPdfExporter.Core.Drawing;

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
            if (args == null || args.Length < 3)
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

            PrintUsage();
            return UsageError;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("用法: NxDrawingPdfExporter.Worker.exe --inventory <drawing-prt> <report-json>");
            Console.Error.WriteLine("      NxDrawingPdfExporter.Worker.exe --export <drawing-prt> <temp-pdf> <report-json>");
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
