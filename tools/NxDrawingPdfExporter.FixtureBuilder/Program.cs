using System;
using System.IO;
using NXOpen;
using NXOpen.Drawings;

// Creates one NEW synthetic part with a drawing sheet that has zero drafting
// views (the no-valid-sheet fixture for Gate 3 / EV-03).
//
// Usage (launched through the verified run_managed.exe):
//   NxDrawingPdfExporter.FixtureBuilder.exe <output-prt>
//
// The tool saves ONLY the brand-new part it created at the caller-provided
// disposable path. It never opens, saves, or mutates any sample PRT.
internal static class Program
{
    public static int Main(string[] args)
    {
        if (args == null || args.Length != 1)
        {
            Console.Error.WriteLine("用法: NxDrawingPdfExporter.FixtureBuilder.exe <output-prt>");
            return 2;
        }

        var outputPath = Path.GetFullPath(args[0]);
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("输出路径缺少目录。");
            }

            Directory.CreateDirectory(directory);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            var session = Session.GetSession();
            var newPart = session.Parts.NewDisplay(outputPath, Part.Units.Millimeters);
            if (newPart is null)
            {
                throw new InvalidOperationException("NX 未返回新建部件。");
            }

            // 一张 A4 图纸页，零件中没有任何制图视图。
            newPart.DrawingSheets.InsertSheet(
                "S1",
                DrawingSheet.StandardSheetSize.A4,
                1.0,
                1.0,
                DrawingSheet.ProjectionAngleType.ThirdAngle);

            var sheetCount = 0;
            foreach (DrawingSheet sheet in newPart.DrawingSheets)
            {
                sheetCount++;
            }

            if (sheetCount != 1)
            {
                throw new InvalidOperationException("夹具应恰好包含一张图纸页，实际 " + sheetCount + " 张。");
            }

            newPart.Save(BasePart.SaveComponents.True, BasePart.CloseAfterSave.False);
            Console.WriteLine("FIXTURE OK: " + outputPath + " sheets=" + sheetCount);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 3;
        }
    }
}
