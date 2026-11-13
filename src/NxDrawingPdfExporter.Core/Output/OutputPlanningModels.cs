using System;
using System.Collections.Generic;
using System.Linq;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Output;

public enum PlannedPublishIntent { None, CreateNew, ReplaceExpected }

public readonly struct RevisionChoice
{
    public RevisionChoice(int value, bool isManual)
    {
        Value = value;
        IsManual = isManual;
    }

    public int Value { get; }
    public bool IsManual { get; }
}

public sealed class OutputPlanningRequest
{
    public OutputPlanningRequest(IReadOnlyList<string> sources, OutputRoutingSettings routing, ExistingPdfPolicy policy,
        Dictionary<string, RevisionChoice> manualChoices)
    {
        Sources = sources;
        Routing = routing;
        Policy = policy;
        ManualChoices = new Dictionary<string, RevisionChoice>(manualChoices, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Sources { get; }
    public OutputRoutingSettings Routing { get; }
    public ExistingPdfPolicy Policy { get; }
    public IReadOnlyDictionary<string, RevisionChoice> ManualChoices { get; }
}

public sealed class PlannedOutputItem
{
    internal PlannedOutputItem(string sourcePath, int sourceOrdinal, string groupKey)
    {
        SourcePath = sourcePath;
        SourceOrdinal = sourceOrdinal;
        GroupKey = groupKey;
    }

    public string SourcePath { get; }
    public int SourceOrdinal { get; }
    public string GroupKey { get; }
    public string FinalOutputPath { get; internal set; } = "";
    public int? SuggestedRevision { get; internal set; }
    public int? ChosenRevision { get; internal set; }
    public bool IsManual { get; internal set; }
    public string ReasonCode { get; internal set; } = ResultReasons.Ready;
    public IReadOnlyList<string> ReasonArguments { get; internal set; } = Array.Empty<string>();
    public string TechnicalDetails { get; internal set; } = "";
    public RevisionCatalogSnapshot? CatalogSnapshot { get; internal set; }
    public PlannedPublishIntent PublishIntent { get; internal set; }
    public TargetSnapshot? ExpectedTarget { get; internal set; }
    public bool IsSkipped => ReasonCode == ResultReasons.SkippedExisting;
    public bool IsBlocking => PublishIntent == PlannedPublishIntent.None && !IsSkipped;

    internal void Block(string reason, params string[] arguments)
    {
        ReasonCode = reason;
        ReasonArguments = Array.AsReadOnly(arguments);
        PublishIntent = PlannedPublishIntent.None;
    }
}

public sealed class OutputPlan
{
    internal OutputPlan(PlannedOutputItem[] items) => Items = Array.AsReadOnly(items);
    public IReadOnlyList<PlannedOutputItem> Items { get; }
    public bool HasBlockingIssues => Items.Any(item => item.IsBlocking);
}
