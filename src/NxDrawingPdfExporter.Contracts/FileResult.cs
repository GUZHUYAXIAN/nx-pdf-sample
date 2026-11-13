using System.Runtime.Serialization;

namespace NxDrawingPdfExporter.Contracts
{
    [DataContract]
    public sealed class FileResult
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string SourcePath { get; set; } = "";

        [DataMember(Order = 2, IsRequired = true)]
        public string FinalOutputPath { get; set; } = "";

        [DataMember(Order = 3, IsRequired = true)]
        public FileResultStatus Status { get; set; }

        /// <summary>Single-line user-facing message. Must never carry stack traces or raw exception dumps.</summary>
        [DataMember(Order = 4)]
        public string Message { get; set; } = "";

        /// <summary>Exported sheet names in the NXOpen DrawingSheets collection order.</summary>
        [DataMember(Order = 5)]
        public string[] ExportedSheets { get; set; } = System.Array.Empty<string>();

        /// <summary>Skipped (template/invalid) sheet names in the NXOpen DrawingSheets collection order.</summary>
        [DataMember(Order = 6)]
        public string[] SkippedSheets { get; set; } = System.Array.Empty<string>();

        [DataMember(Order = 7)]
        public long ElapsedMilliseconds { get; set; }

        [DataMember(Order = 8, IsRequired = true)]
        public string ReasonCode { get; set; } = ResultReasons.Unknown;

        [DataMember(Order = 9, IsRequired = true)]
        public string[] ReasonArguments { get; set; } = System.Array.Empty<string>();

        [DataMember(Order = 10, IsRequired = true)]
        public string TechnicalDetails { get; set; } = "";
    }
}
