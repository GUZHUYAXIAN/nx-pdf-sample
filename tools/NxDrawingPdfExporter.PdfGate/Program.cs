using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using NxDrawingPdfExporter.App.Pdf;

// Gate host modes:
//   inspect <pdf> <out.json>  Read-only PDF inspection via PdfSharpInspector.
// Exit codes: 0 inspected without error, 2 usage error, 3 inspection error.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 3 || !string.Equals(args[0], "inspect", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("用法: NxDrawingPdfExporter.PdfGate inspect <pdf> <out-json>");
            return 2;
        }

        var pdfPath = Path.GetFullPath(args[1]);
        var outPath = Path.GetFullPath(args[2]);
        var result = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["PdfExists"] = File.Exists(pdfPath)
        };

        try
        {
            var inspection = new PdfSharpInspector().Inspect(pdfPath);
            result["Length"] = inspection.Length;
            result["HasPdfHeader"] = inspection.HasPdfHeader;
            result["PageCount"] = inspection.PageCount;
            result["PageSizes"] = inspection.PageSizes
                .Select(p => new { widthPoints = p.WidthPoints, heightPoints = p.HeightPoints })
                .ToArray();
            result["Error"] = null;
        }
        catch (Exception error)
        {
            result["Error"] = error.Message.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        var directory = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        File.WriteAllText(outPath, JsonSerializer.Serialize(result, options));
        return result["Error"] is null ? 0 : 3;
    }
}
