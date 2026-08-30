using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.Drawings;

namespace NxDrawingPdfExporter.Worker
{
    /// <summary>
    /// Opens each selected sheet and updates its out-of-date drafting views
    /// before PDF export. Any NX failure is thrown to the caller so the PRT
    /// fails instead of being classified as a template page.
    /// </summary>
    internal sealed class NxViewUpdater
    {
        public void UpdateForExport(Part workPart, IReadOnlyList<DrawingSheet> sheets)
        {
            if (workPart == null)
            {
                throw new ArgumentNullException(nameof(workPart));
            }

            if (sheets == null)
            {
                throw new ArgumentNullException(nameof(sheets));
            }

            foreach (var sheet in sheets)
            {
                sheet.Open();
                workPart.DraftingViews.UpdateViews(DraftingViewCollection.ViewUpdateOption.OutOfDate, sheet);
            }
        }
    }
}
