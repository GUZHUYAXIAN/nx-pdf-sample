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
        else
        {
            Console.Error.WriteLine("未知场景: " + args[0]);
            return 2;
        }

        await controller.RunAsync();

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
