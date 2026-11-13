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
        Groups = DescribeGroups(Inputs);

        var retained = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var source in manualChoices.Keys.Concat(invalidEdits.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
        {
            if (retained.Contains(source))
                continue;
            manualChoices.Remove(source);
            invalidEdits.Remove(source);
        }
    }

    public IReadOnlyList<SourceDirectoryGroup> DescribeGroups(IReadOnlyList<string> paths)
    {
        var groups = SourceDirectoryGrouper.Group(paths, labels);
        foreach (var group in groups)
            labels[group.Key] = group.Label;
        return groups;
    }

    public string? InvalidEdit(string sourcePath) => invalidEdits.GetValueOrDefault(sourcePath);

    public int? ManualRevision(string sourcePath) => manualChoices.TryGetValue(sourcePath, out var choice) ? choice.Value : null;

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
        if (valid)
            UpdatePlanAfterEdit();
        return valid;
    }

    public void RestoreSuggested(IReadOnlyList<string> selectedSources)
    {
        foreach (var source in selectedSources)
        {
            manualChoices.Remove(source);
            invalidEdits.Remove(source);
        }
        UpdatePlanAfterEdit();
    }

    public OutputPlan Refresh() => AcceptPlan(PlanInputs());

    internal async Task<OutputPlan> RefreshAsync()
    {
        var request = PlanningRequest();
        return AcceptPlan(await Task.Run(() => planner.Plan(request)));
    }

    private OutputPlan AcceptPlan(OutputPlan plan)
    {
        CurrentPlan = plan;
        IsStale = false;
        return plan;
    }

    private OutputPlanningRequest PlanningRequest() => new(Inputs, Routing, Policy, manualChoices);
    private OutputPlan PlanInputs() => planner.Plan(PlanningRequest());

    private void UpdatePlanAfterEdit()
    {
        if (IsStale || CurrentPlan is null)
            return;

        // An edit approves a revision choice. An explicit refresh approves new file facts.
        var edited = PlanInputs();
        for (var index = 0; index < CurrentPlan.Items.Count; index++)
        {
            var original = CurrentPlan.Items[index];
            var updated = edited.Items[index];
            if ((original.CatalogSnapshot is { } catalog &&
                    (updated.CatalogSnapshot is null || !catalog.Matches(updated.CatalogSnapshot)))
                || (original.ExpectedTarget is { } expected
                    && StringComparer.OrdinalIgnoreCase.Equals(original.FinalOutputPath, updated.FinalOutputPath)
                    && (updated.ExpectedTarget is null || !expected.Matches(updated.ExpectedTarget))))
            {
                Invalidate();
                return;
            }
        }

        CurrentPlan = edited;
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
