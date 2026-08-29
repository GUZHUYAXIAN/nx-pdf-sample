using System;
using System.Collections.Generic;
using NxDrawingPdfExporter.Core.Drawing;
using NXOpen;

namespace NxDrawingPdfExporter.Worker
{
    internal sealed class NxPartProcessor
    {
        private readonly SourceFileGuard sourceFileGuard = new SourceFileGuard();

        public SheetInventoryReport Inventory(string sourcePath, out SheetNameMap? nameMap)
        {
            nameMap = null;
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
                    return SheetInventoryReport.Failed("NX 未返回可用制图部件。");
                }

                var inventory = new NxSheetInventory().Collect(workPart, CaptureLoadDiagnostics(loadStatus), out var collectedMap);
                if (sourceFileGuard.HasChanged(before))
                {
                    return SheetInventoryReport.Failed("源 PRT 在 NX 清点过程中发生变化，已停止。");
                }

                nameMap = collectedMap;
                return inventory;
            }
            finally
            {
                if (openedPart != null)
                {
                    new NxPartCloser().CloseWithoutSaving(openedPart);
                }
            }
        }

        private static IReadOnlyList<string> CaptureLoadDiagnostics(PartLoadStatus? loadStatus)
        {
            if (loadStatus == null || loadStatus.NumberUnloadedParts == 0)
            {
                return Array.Empty<string>();
            }

            var diagnostics = new List<string>(loadStatus.NumberUnloadedParts);
            for (var index = 0; index < loadStatus.NumberUnloadedParts; index++)
            {
                diagnostics.Add(loadStatus.GetStatusDescription(index));
            }

            return diagnostics;
        }
    }
}
