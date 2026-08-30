using System;
using System.Runtime.Serialization;

namespace NxDrawingPdfExporter.Contracts
{
    [DataContract]
    public sealed class JobResult
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string ProtocolVersion { get; set; } = NxDrawingPdfExporter.Contracts.ProtocolVersion.Current;

        [DataMember(Order = 2, IsRequired = true)]
        public string RunId { get; set; } = "";

        [DataMember(Order = 3, IsRequired = true)]
        public DateTime StartedUtc { get; set; }

        [DataMember(Order = 4, IsRequired = true)]
        public DateTime EndedUtc { get; set; }

        [DataMember(Order = 5, IsRequired = true)]
        public bool Cancelled { get; set; }

        /// <summary>Per-file results in job order. This file is the authoritative completion record.</summary>
        [DataMember(Order = 6, IsRequired = true)]
        public FileResult[] Files { get; set; } = Array.Empty<FileResult>();

        /// <summary>Short mapped fatal error description for the whole run; never a stack trace.</summary>
        [DataMember(Order = 7)]
        public string? FatalError { get; set; }
    }
}
