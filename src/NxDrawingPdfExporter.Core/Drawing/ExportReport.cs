using System;
using System.Runtime.Serialization;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Drawing
{
    // NX-free export DTOs. Raw sheet names must never be stored here; the
    // worker emits a per-run salted NameToken and keeps the mapping only in
    // gitignored run artifacts.
    [DataContract]
    public sealed class ExportReport
    {
        [DataMember(Order = 1)]
        public bool Success { get; set; }

        [DataMember(Order = 2)]
        public string? Failure { get; set; }

        // Mapped batch outcome. Success for an exported temp PDF; PureModel,
        // NoValidSheets, or Failed otherwise. Never a GUI publication status
        // like Overwritten, which only the validating side can decide.
        [DataMember(Order = 9)]
        public FileResultStatus Outcome { get; set; } = FileResultStatus.Failed;

        [DataMember(Order = 3)]
        public string[] LoadDiagnostics { get; set; } = Array.Empty<string>();

        [DataMember(Order = 4)]
        public ExportSheetFact[] Sheets { get; set; } = Array.Empty<ExportSheetFact>();

        [DataMember(Order = 5)]
        public int SelectedCount { get; set; }

        [DataMember(Order = 6)]
        public int SkippedCount { get; set; }

        [DataMember(Order = 7)]
        public bool SourceUnchanged { get; set; }

        [DataMember(Order = 8)]
        public long ElapsedMilliseconds { get; set; }

        public static ExportReport Failed(string failure, FileResultStatus outcome = FileResultStatus.Failed)
        {
            return new ExportReport { Success = false, Failure = failure, Outcome = outcome };
        }
    }

    [DataContract]
    public sealed class ExportSheetFact
    {
        // Stable V1 export order from the public NXOpen DrawingSheets
        // collection. Must equal the item's array position.
        [DataMember(Order = 1)]
        public int ExportOrderIndex { get; set; }

        // Diagnostic position in that same native DrawingSheets enumeration.
        [DataMember(Order = 2)]
        public int NativeIndex { get; set; }

        [DataMember(Order = 3)]
        public int DraftingViewCount { get; set; }

        [DataMember(Order = 4)]
        public bool Selected { get; set; }

        [DataMember(Order = 5)]
        public double Length { get; set; }

        [DataMember(Order = 6)]
        public double Height { get; set; }

        [DataMember(Order = 7)]
        public string Units { get; set; } = "";

        [DataMember(Order = 8)]
        public string NameToken { get; set; } = "";
    }
}
