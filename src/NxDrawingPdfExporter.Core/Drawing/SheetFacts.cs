namespace NxDrawingPdfExporter.Core.Drawing
{
    public sealed class SheetFacts
    {
        public int NavigatorIndex { get; set; }

        public string Name { get; set; } = "";

        public int DraftingViewCount { get; set; }

        public string? InspectionFailure { get; set; }
    }
}
