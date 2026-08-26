using System.Runtime.Serialization;

namespace NxDrawingPdfExporter.Contracts
{
    [DataContract]
    public sealed class JobItem
    {
        [DataMember(Order = 1, IsRequired = true)]
        public string SourcePath { get; set; } = "";

        [DataMember(Order = 2, IsRequired = true)]
        public string FinalOutputPath { get; set; } = "";

        [DataMember(Order = 3, IsRequired = true)]
        public string WorkerTempOutputPath { get; set; } = "";
    }
}
