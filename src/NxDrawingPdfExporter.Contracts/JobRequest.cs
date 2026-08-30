using System;
using System.Runtime.Serialization;

namespace NxDrawingPdfExporter.Contracts
{
    [DataContract]
    public sealed class JobRequest
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string ProtocolVersion { get; set; } = NxDrawingPdfExporter.Contracts.ProtocolVersion.Current;

        [DataMember(Order = 2, IsRequired = true)]
        public string RunId { get; set; } = "";

        [DataMember(Order = 3, IsRequired = true)]
        public JobItem[] Items { get; set; } = Array.Empty<JobItem>();

        [DataMember(Order = 4, IsRequired = true)]
        public OutputMode OutputMode { get; set; }

        [DataMember(Order = 5)]
        public string? UnifiedOutputDirectory { get; set; }

        [DataMember(Order = 6, IsRequired = true)]
        public ExistingPdfPolicy ExistingPdfPolicy { get; set; }

        [DataMember(Order = 7, IsRequired = true)]
        public string ResultPath { get; set; } = "";

        [DataMember(Order = 8, IsRequired = true)]
        public string CancellationFlagPath { get; set; } = "";
    }
}
