using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NxDrawingPdfExporter.Core.Drawing;
using NXOpen;
using NXOpen.Drawings;

namespace NxDrawingPdfExporter.Worker
{
    /// <summary>
    /// Processes exactly one drawing PRT: open without saving, inventory in
    /// public DrawingSheets order, select exportable sheets, update views,
    /// write one temp PDF, and guard the source file. Never parallelized.
    /// </summary>
    internal sealed class NxFileJobRunner
    {
        private static readonly SheetSelectionService SelectionService = new SheetSelectionService();
        private readonly SourceFileGuard sourceFileGuard = new SourceFileGuard();

        public ExportReport ExportSingle(string sourcePath, string tempPdfPath, out SheetNameMap? nameMap)
        {
            nameMap = null;
            var stopwatch = Stopwatch.StartNew();
            var before = sourceFileGuard.Capture(sourcePath);
            BasePart? openedPart = null;
            PartLoadStatus? loadStatus = null;
            try
            {
                var session = Session.GetSession();
                openedPart = session.Parts.OpenDisplay(before.FullPath, out loadStatus);
                var workPart = openedPart as Part;
                if (workPart == null)
                {
                    return ExportReport.Failed("NX 未返回可用制图部件。");
                }

                var loadDiagnostics = CaptureLoadDiagnostics(loadStatus);
                var salt = SheetNameTokenizer.NewSalt();
                var nativeSheets = new List<DrawingSheet>();
                foreach (DrawingSheet sheet in workPart.DrawingSheets)
                {
                    nativeSheets.Add(sheet);
                }

                var facts = new List<SheetFacts>();
                var entries = new List<SheetNameMapEntry>();
                var reportItems = new List<ExportSheetFact>();
                for (var index = 0; index < nativeSheets.Count; index++)
                {
                    var sheet = nativeSheets[index];
                    var token = SheetNameTokenizer.Tokenize(sheet.Name, salt);
                    var viewCount = sheet.GetDraftingViews().Length;
                    facts.Add(new SheetFacts { ExportOrderIndex = index, Name = token, DraftingViewCount = viewCount });
                    entries.Add(new SheetNameMapEntry { Token = token, Name = sheet.Name });
                    reportItems.Add(new ExportSheetFact
                    {
                        ExportOrderIndex = index,
                        NativeIndex = index,
                        DraftingViewCount = viewCount,
                        Selected = false,
                        Length = sheet.Length,
                        Height = sheet.Height,
                        Units = sheet.Units.ToString(),
                        NameToken = token
                    });
                }

                var selection = SelectionService.Select(facts);
                if (selection.Kind == SheetSelectionKind.Failed)
                {
                    return ExportReport.Failed(selection.FatalFailure ?? "图纸页检查失败。");
                }

                if (selection.Kind == SheetSelectionKind.PureModel)
                {
                    return ExportReport.Failed("纯模型部件，没有图纸页。");
                }

                if (selection.Kind == SheetSelectionKind.NoValidSheets)
                {
                    return ExportReport.Failed("没有包含实际制图视图的图纸页。");
                }

                var selectedSheets = selection.ExportIndices.Select(i => nativeSheets[i]).ToArray();

                // Update/load failures must fail this PRT, never degrade into
                // a skipped or blank page. Exceptions propagate to Program.
                new NxViewUpdater().UpdateForExport(workPart, selectedSheets);
                new NxPdfExporter().Export(workPart, selectedSheets, Path.GetFullPath(tempPdfPath));

                if (sourceFileGuard.HasChanged(before))
                {
                    return ExportReport.Failed("源 PRT 在导出过程中发生变化，已停止。");
                }

                foreach (var exportIndex in selection.ExportIndices)
                {
                    reportItems[exportIndex].Selected = true;
                }

                nameMap = new SheetNameMap
                {
                    SaltBase64 = Convert.ToBase64String(salt),
                    Entries = entries.ToArray()
                };

                return new ExportReport
                {
                    Success = true,
                    LoadDiagnostics = loadDiagnostics,
                    Sheets = reportItems.ToArray(),
                    SelectedCount = selectedSheets.Length,
                    SkippedCount = reportItems.Count - selectedSheets.Length,
                    SourceUnchanged = true,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                if (openedPart != null)
                {
                    new NxPartCloser().CloseWithoutSaving(openedPart);
                }
            }
        }

        private static string[] CaptureLoadDiagnostics(PartLoadStatus? loadStatus)
        {
            if (loadStatus == null || loadStatus.NumberUnloadedParts == 0)
            {
                return Array.Empty<string>();
            }

            var diagnostics = new string[loadStatus.NumberUnloadedParts];
            for (var index = 0; index < loadStatus.NumberUnloadedParts; index++)
            {
                diagnostics[index] = loadStatus.GetStatusDescription(index);
            }

            return diagnostics;
        }
    }
}
