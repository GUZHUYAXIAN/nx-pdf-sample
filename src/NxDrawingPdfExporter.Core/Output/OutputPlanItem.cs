namespace NxDrawingPdfExporter.Core.Output
{
    public enum OutputPlanStatus
    {
        Ready = 0,
        SkippedExisting = 1,
        NameConflict = 2
    }

    public sealed class OutputPlanItem
    {
        public string SourcePath { get; set; } = "";

        public string FinalOutputPath { get; set; } = "";

        public OutputPlanStatus Status { get; set; }

        public string Message { get; set; } = "";
    }
}
