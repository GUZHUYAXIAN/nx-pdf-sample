using System;
using System.Runtime.Serialization;

namespace NxDrawingPdfExporter.Core.Drawing
{
    // NX-free inventory DTOs. Raw sheet names must never be stored here;
    // workers emit a per-run salted NameToken and keep the mapping only in
    // gitignored run artifacts.
    [DataContract]
    public sealed class SheetInventoryReport
    {
        [DataMember(Order = 1)]
        public bool Success { get; set; }

        [DataMember(Order = 2)]
        public string? Failure { get; set; }

        [DataMember(Order = 3)]
        public string[] LoadDiagnostics { get; set; } = Array.Empty<string>();

        [DataMember(Order = 4)]
        public SheetInventoryItem[] Sheets { get; set; } = Array.Empty<SheetInventoryItem>();

        public static SheetInventoryReport Failed(string failure)
        {
            return new SheetInventoryReport { Success = false, Failure = failure };
        }
    }

    [DataContract]
    public sealed class SheetInventoryItem
    {
        // Stable V1 export order from the public NXOpen DrawingSheets
        // collection. Part Navigator display preferences are intentionally
        // outside this contract. Must equal the item's array position.
        [DataMember(Order = 1)]
        public int ExportOrderIndex { get; set; }

        [DataMember(Order = 2)]
        public int DraftingViewCount { get; set; }

        [DataMember(Order = 3)]
        public double Length { get; set; }

        [DataMember(Order = 4)]
        public double Height { get; set; }

        [DataMember(Order = 5)]
        public string Units { get; set; } = "";

        [DataMember(Order = 6)]
        public string NameToken { get; set; } = "";

        // Diagnostic position in that same native DrawingSheets enumeration.
        // Retained to make the API-order provenance explicit in evidence.
        [DataMember(Order = 7)]
        public int NativeIndex { get; set; }
    }
}
