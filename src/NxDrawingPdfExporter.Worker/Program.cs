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
        private const int ReportWriteFailure = 21;

        public static int Main(string[] args)
        {
            if (args == null || args.Length != 3 || !string.Equals(args[0], "--inventory", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("用法: NxDrawingPdfExporter.Worker.exe --inventory <drawing-prt> <report-json>");
                return UsageError;
            }

            var reportPath = Path.GetFullPath(args[2]);
            SheetInventoryReport report;
            SheetNameMap? nameMap = null;
            try
            {
                var inventory = new NxPartProcessor().Inventory(args[1], out var collectedMap);
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

        private static void WriteJson(string path, object graph)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("清点报告路径缺少目录。");
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
