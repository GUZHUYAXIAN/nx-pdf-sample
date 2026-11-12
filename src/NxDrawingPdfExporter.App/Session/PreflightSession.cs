using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Input;
using NxDrawingPdfExporter.Core.Output;

namespace NxDrawingPdfExporter.App.Session;

public sealed class PreflightSession(IRevisionOutputPlanner planner)
{
    private readonly Dictionary<string, RevisionChoice> manualChoices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> invalidEdits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> labels = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Inputs { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<SourceDirectoryGroup> Groups { get; private set; } = Array.Empty<SourceDirectoryGroup>();
    public OutputRoutingSettings Routing { get; private set; } = new(OutputMode.GroupedDirectories, "", new Dictionary<string, string>());
    public ExistingPdfPolicy Policy { get; private set; }
    public OutputPlan? CurrentPlan { get; private set; }
    public bool HasInvalidEdit => Policy == ExistingPdfPolicy.Revision && invalidEdits.Count > 0;
    public bool IsStale { get; private set; } = true;

    public void Invalidate() => IsStale = true;

    public void Update(IReadOnlyList<string> inputs, OutputRoutingSettings routing, ExistingPdfPolicy policy)
    {
        var normalized = inputs.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!Inputs.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase)
            || Routing.Mode != routing.Mode || Routing.UnifiedDirectory != routing.UnifiedDirectory
            || !SameTargets(Routing.GroupTargets, routing.GroupTargets) || Policy != policy)
            Invalidate();

        Inputs = Array.AsReadOnly(normalized);
        Routing = routing;
        Policy = policy;
        Groups = SourceDirectoryGrouper.Group(Inputs, labels);
        foreach (var group in Groups)
            labels[group.Key] = group.Label;

        var retained = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var source in manualChoices.Keys.Concat(invalidEdits.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
        {
            if (retained.Contains(source))
                continue;
            manualChoices.Remove(source);
            invalidEdits.Remove(source);
        }
    }

    public string? InvalidEdit(string sourcePath) => invalidEdits.GetValueOrDefault(sourcePath);

    public bool SetRevision(string sourcePath, string text) => SetRevisions([sourcePath], text);

    public bool SetRevisions(IReadOnlyList<string> selectedSources, string text)
    {
        var selected = selectedSources.Where(source => Inputs.Contains(source, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (Policy != ExistingPdfPolicy.Revision || selected.Length == 0)
            return false;
        var valid = RevisionNumber.TryParse(text, out var number);
        foreach (var source in selected)
        {
            if (valid)
            {
                invalidEdits.Remove(source);
                manualChoices[source] = new RevisionChoice(number, true);
            }
            else
                invalidEdits[source] = text;
        }
        if (valid && !IsStale)
            Refresh();
        return valid;
    }

    public void RestoreSuggested(IReadOnlyList<string> selectedSources)
    {
        foreach (var source in selectedSources)
        {
            manualChoices.Remove(source);
            invalidEdits.Remove(source);
        }
        if (!IsStale)
            Refresh();
    }

    public OutputPlan Refresh()
    {
        CurrentPlan = planner.Plan(new OutputPlanningRequest(Inputs, Routing, Policy, manualChoices));
        IsStale = false;
        return CurrentPlan;
    }

    public FrozenExportBatch FreezeForRun(NxInstallation selectedNx)
    {
        if (CurrentPlan is null || IsStale || HasInvalidEdit || CurrentPlan.HasBlockingIssues)
            throw new InvalidOperationException("The export plan needs review.");
        foreach (var item in CurrentPlan.Items)
        {
            if (!IsCurrent(item, out var details))
            {
                Invalidate();
                throw new PreflightChangedException(item, details);
            }
        }

        return new FrozenExportBatch(selectedNx with { Sources = Array.AsReadOnly(selectedNx.Sources.ToArray()) },
            Array.AsReadOnly(CurrentPlan.Items.ToArray()), Routing, Policy);
    }

    public bool IsCurrent(PlannedOutputItem item, out string details)
    {
        try
        {
            details = "";
            return File.Exists(item.SourcePath) && planner.IsCurrent(item);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            details = error.ToString();
            return false;
        }
    }

    private static bool SameTargets(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var target) && pair.Value == target);
}

public sealed class PreflightChangedException(PlannedOutputItem item, string details) : InvalidOperationException(details)
{
    public PlannedOutputItem Item { get; } = item;
}
