using System;
using System.Collections.Generic;

namespace NxDrawingPdfExporter.Core.Drawing
{
    public enum SheetSelectionKind
    {
        PureModel = 0,
        NoValidSheets = 1,
        Exportable = 2,
        Failed = 3
    }

    public sealed class SheetSelection
    {
        public SheetSelectionKind Kind { get; set; }

        public IReadOnlyList<int> ExportIndices { get; set; } = Array.Empty<int>();

        public IReadOnlyList<int> SkipIndices { get; set; } = Array.Empty<int>();

        public string? FatalFailure { get; set; }
    }

    public sealed class SheetSelectionService
    {
        public SheetSelection Select(IReadOnlyList<SheetFacts> sheets)
        {
            if (sheets is null)
            {
                throw new ArgumentNullException(nameof(sheets));
            }

            if (sheets.Count == 0)
            {
                return new SheetSelection { Kind = SheetSelectionKind.PureModel };
            }

            var exportIndices = new List<int>();
            var skipIndices = new List<int>();
            foreach (var sheet in sheets)
            {
                if (sheet is null)
                {
                    return new SheetSelection
                    {
                        Kind = SheetSelectionKind.Failed,
                        FatalFailure = "图纸页事实包含空项。"
                    };
                }

                if (!string.IsNullOrWhiteSpace(sheet.InspectionFailure))
                {
                    return new SheetSelection
                    {
                        Kind = SheetSelectionKind.Failed,
                        FatalFailure = sheet.InspectionFailure
                    };
                }

                if (sheet.DraftingViewCount >= 1)
                {
                    exportIndices.Add(sheet.ExportOrderIndex);
                }
                else
                {
                    skipIndices.Add(sheet.ExportOrderIndex);
                }
            }

            return new SheetSelection
            {
                Kind = exportIndices.Count > 0 ? SheetSelectionKind.Exportable : SheetSelectionKind.NoValidSheets,
                ExportIndices = exportIndices,
                SkipIndices = skipIndices
            };
        }
    }
}
