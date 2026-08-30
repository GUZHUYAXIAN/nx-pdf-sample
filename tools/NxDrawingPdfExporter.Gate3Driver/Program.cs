using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using NxDrawingPdfExporter.App;
using NxDrawingPdfExporter.Contracts;

// Gate 3 headless driver. Runs the real ApplicationController once and prints
// the per-file results as JSON. The process exit code is always 0; the JSON
// output is the evidence the gate script validates.
//
// Usage:
//   Gate3Driver scan <folder> <recursive:0|1> <beside|unified> <unifiedDir|-> <skip|overwrite> <runRoot>
//   Gate3Driver manual <path1;path2;...> <beside|unified> <unifiedDir|-> <skip|overwrite> <runRoot>
//   Gate3Driver cancel <path1;path2;...> <beside|unified> <unifiedDir|-> <skip|overwrite> <runRoot>
//     (cancel: end-to-end boundary-cancellation proof — as soon as any run
//      temp PDF appears, i.e. while item 1 is still exporting, the driver
//      requests cancellation through the REAL controller.Cancel() path)
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 6)
        {
            Console.Error.WriteLine("参数不足。");
            return 2;
        }

        var services = new ApplicationServices
        {
            RunRootProvider = () => args[^1],
            WorkerExePath = Environment.GetEnvironmentVariable("NXPDF_WORKER_EXE")
        };

        var controller = new ApplicationController(services, new ConsoleLogSink());
        if (string.Equals(args[0], "scan", StringComparison.Ordinal))
        {
            controller.ScanFolderPath = args[1];
            controller.IncludeSubfolders = args[2] == "1";
            ApplyOutput(controller, args[3], args[4], args[5]);
        }
        else if (string.Equals(args[0], "manual", StringComparison.Ordinal))
        {
            controller.InputMode = GuiInputMode.ManualSelection;
            controller.ManualPaths = args[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
            ApplyOutput(controller, args[2], args[3], args[4]);
        }
        else if (string.Equals(args[0], "cancel", StringComparison.Ordinal))
        {
            controller.InputMode = GuiInputMode.ManualSelection;
            controller.ManualPaths = args[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
            ApplyOutput(controller, args[2], args[3], args[4]);
        }
        else
        {
            Console.Error.WriteLine("未知场景: " + args[0]);
            return 2;
        }

        var runTask = controller.RunAsync();
        if (string.Equals(args[0], "cancel", StringComparison.Ordinal))
        {
            var watchDirs = controller.ManualPaths
                .Select(Path.GetDirectoryName!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var deadline = DateTime.UtcNow.AddMinutes(15);
            var cancelled = false;
            while (!runTask.IsCompleted && DateTime.UtcNow < deadline)
            {
                if (watchDirs.Any(dir => Directory.EnumerateFiles(dir!, "*.tmp.pdf").Any()))
                {
                    // 条目 1 的临时 PDF 已出现（导出进行中）：走真实
                    // controller.Cancel() 路径写入取消标志。必须保持
                    // stdout/stderr 纯净：PS 5.1 在 Stop 偏好下会把原生
                    // stderr 输出变成终止性错误。
                    controller.Cancel();
                    cancelled = true;
                    break;
                }

                await Task.Delay(10);
            }

            if (!cancelled)
            {
                return 3;
            }
        }

        await runTask;

        var payload = new
        {
            runDirectory = controller.CurrentRunDirectory,
            results = controller.Results.Select(r => new
            {
                source = r.SourcePath,
                target = r.FinalOutputPath,
                status = r.Status.ToString(),
                message = r.Message,
                exportedSheetCount = r.ExportedSheets.Length
            }),
            summary = controller.SummaryText,
            progress = controller.ProgressText
        };
        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
        return 0;
    }

    private static void ApplyOutput(ApplicationController controller, string mode, string unified, string policy)
    {
        if (mode == "unified")
        {
            controller.OutputMode = OutputMode.UnifiedDirectory;
            controller.UnifiedOutputDirectory = unified;
        }

        controller.ExistingPdfPolicy = policy == "overwrite"
            ? ExistingPdfPolicy.Overwrite
            : ExistingPdfPolicy.Skip;
    }

    private sealed class ConsoleLogSink : NxDrawingPdfExporter.App.Logging.ILogSink
    {
        // Silent: PowerShell 5.1 turns native stderr output into a terminating
        // error under $ErrorActionPreference='Stop'; stdout must stay pure JSON.
        public void Write(string message)
        {
        }
    }
}
