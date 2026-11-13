using System.Runtime.Serialization;

namespace NxDrawingPdfExporter.Contracts;

public enum WorkerProgressStage
{
    Initializing, Ready, CheckingItem, Opening, Inspecting, Updating, GeneratingPdf, Closing, BetweenItems, Finished, Faulted
}

[DataContract]
public sealed class WorkerProgressSnapshot
{
    [DataMember(Order = 1, IsRequired = true)]
    public int SchemaVersion { get; set; } = 1;

    [DataMember(Order = 2, IsRequired = true)]
    public string RunId { get; set; } = "";

    [DataMember(Order = 3, IsRequired = true)]
    public long Sequence { get; set; }

    [DataMember(Order = 4, IsRequired = true)]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    [DataMember(Order = 5, IsRequired = true)]
    public WorkerProgressStage Stage { get; set; }

    [DataMember(Order = 6, IsRequired = true)]
    public int Total { get; set; }

    [DataMember(Order = 7, IsRequired = true)]
    public int Processed { get; set; }

    [DataMember(Order = 8, IsRequired = true)]
    public int Generated { get; set; }

    [DataMember(Order = 9, IsRequired = true)]
    public int Failed { get; set; }

    [DataMember(Order = 10, IsRequired = true)]
    public int PureModel { get; set; }

    [DataMember(Order = 11, IsRequired = true)]
    public int NoValidSheets { get; set; }

    [DataMember(Order = 12, IsRequired = true)]
    public int OtherProcessed { get; set; }

    [DataMember(Order = 13, IsRequired = true)]
    public int? CurrentItemIndex { get; set; }
}
