using System;
using System.Collections.Generic;
using System.Linq;
using NXOpen;
using NXOpen.Drawings;

namespace NxDrawingPdfExporter.Worker
{
    /// <summary>
    /// Single multi-sheet NX PDF commit: black on white, full scale, no
    /// watermark, no append, original sheet sizes (no common scale and no
    /// explicit X/Y dimensions).
    /// </summary>
    internal sealed class NxPdfExporter
    {
        public void Export(Part workPart, IReadOnlyList<DrawingSheet> sheets, string tempPdfPath)
        {
            if (workPart == null)
            {
                throw new ArgumentNullException(nameof(workPart));
            }

            if (sheets == null || sheets.Count == 0)
            {
                throw new ArgumentException("至少需要一张有效图纸页。", nameof(sheets));
            }

            if (string.IsNullOrWhiteSpace(tempPdfPath))
            {
                throw new ArgumentException("临时 PDF 路径不能为空。", nameof(tempPdfPath));
            }

            PrintPDFBuilder? builder = null;
            try
            {
                builder = workPart.PlotManager.CreatePrintPdfbuilder();
                builder.SourceBuilder.SetSheets(sheets.Cast<NXObject>().ToArray());
                builder.Filename = tempPdfPath;
                builder.Colors = PrintPDFBuilder.Color.BlackOnWhite;
                builder.Size = PrintPDFBuilder.SizeOption.FullScale;
                builder.Watermark = string.Empty;
                builder.Append = false;
                builder.Commit();
            }
            finally
            {
                builder?.Destroy();
            }
        }
    }
}
