using System;
using System.IO;
using System.Linq;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Input;

namespace NxDrawingPdfExporter.Core.Output;

public interface IRevisionOutputPlanner
{
    OutputPlan Plan(OutputPlanningRequest request);
    bool IsCurrent(PlannedOutputItem item);
}

public sealed class RevisionOutputPlanner : IRevisionOutputPlanner
{
    private readonly RevisionCatalog catalog;
    private readonly ITargetStateReader targets;

    public RevisionOutputPlanner(RevisionCatalog catalog, ITargetStateReader targets)
    {
        this.catalog = catalog;
        this.targets = targets;
    }

    public OutputPlan Plan(OutputPlanningRequest request)
    {
        var items = request.Sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((source, index) => PlanOne(request, source, index + 1)).ToArray();

        foreach (var collision in items.Where(x => x.FinalOutputPath.Length > 0)
            .GroupBy(x => x.FinalOutputPath, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            foreach (var item in collision)
                item.Block(ResultReasons.NameConflict, collision.Key);
        }

        if (request.Policy == ExistingPdfPolicy.Revision)
        {
            var revisionItems = items.Where(x => x.CatalogSnapshot is not null).ToArray();
            for (var i = 0; i < revisionItems.Length; i++)
            {
                for (var j = i + 1; j < revisionItems.Length; j++)
                {
                    if (!FamiliesOverlap(revisionItems[i], revisionItems[j]))
                        continue;
                    revisionItems[i].Block(ResultReasons.FamilyConflict, revisionItems[j].SourcePath);
                    revisionItems[j].Block(ResultReasons.FamilyConflict, revisionItems[i].SourcePath);
                }
            }
        }

        return new OutputPlan(items);
    }

    public bool IsCurrent(PlannedOutputItem item)
    {
        if (item.ExpectedTarget is null)
            return false;
        var snapshot = item.CatalogSnapshot;
        return (snapshot is null || snapshot.Matches(catalog.Read(item.SourcePath, snapshot.TargetDirectory)))
            && item.ExpectedTarget.Matches(targets.Capture(item.FinalOutputPath));
    }

    private PlannedOutputItem PlanOne(OutputPlanningRequest request, string source, int ordinal)
    {
        var item = new PlannedOutputItem(source, ordinal, SourceDirectoryGrouper.ParentDirectory(source));
        var targetDirectory = "";
        try
        {
            targetDirectory = request.Routing.ResolveDirectory(source);
            var revision = 0;
            if (request.Policy == ExistingPdfPolicy.Revision)
            {
                var snapshot = catalog.Read(source, targetDirectory);
                item.CatalogSnapshot = snapshot;
                var highest = Math.Max(snapshot.SourceHighest ?? -1, snapshot.TargetHighest ?? -1);
                item.SuggestedRevision = highest == int.MaxValue ? (int?)null : highest + 1;
                var manual = request.ManualChoices.TryGetValue(source, out var choice) && choice.IsManual;
                item.IsManual = manual;
                item.ChosenRevision = manual ? choice.Value : item.SuggestedRevision;
                if (snapshot.Aliases.Count != 0)
                {
                    item.Block(ResultReasons.RevisionAlias, string.Join(Environment.NewLine, snapshot.Aliases));
                    return item;
                }
                if (item.ChosenRevision is null || item.ChosenRevision < 0)
                {
                    item.Block(manual ? ResultReasons.InvalidRevision : ResultReasons.RevisionLimit);
                    return item;
                }
                revision = item.ChosenRevision.Value;
            }
            else
            {
                catalog.CheckDirectory(targetDirectory);
            }

            item.FinalOutputPath = Path.Combine(targetDirectory, RevisionFileName.Format(source, revision));
            var expected = targets.Capture(item.FinalOutputPath);
            item.ExpectedTarget = expected;
            if (expected.Exists && request.Policy == ExistingPdfPolicy.Skip)
                item.ReasonCode = ResultReasons.SkippedExisting;
            else
                item.PublishIntent = expected.Exists ? PlannedPublishIntent.ReplaceExpected : PlannedPublishIntent.CreateNew;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is NotSupportedException)
        {
            item.Block(ResultReasons.DirectoryUnreadable, targetDirectory);
            item.TechnicalDetails = error.Message;
        }

        return item;
    }

    private static bool FamiliesOverlap(PlannedOutputItem left, PlannedOutputItem right)
    {
        var first = left.CatalogSnapshot!;
        var second = right.CatalogSnapshot!;
        var comparer = StringComparer.OrdinalIgnoreCase;
        var leftName = Path.GetFileNameWithoutExtension(left.SourcePath);
        var rightName = Path.GetFileNameWithoutExtension(right.SourcePath);
        if (comparer.Equals(leftName, rightName))
            return comparer.Equals(first.TargetDirectory, second.TargetDirectory);

        var sharesDirectory = new[] { first.SourceDirectory, first.TargetDirectory }
            .Intersect(new[] { second.SourceDirectory, second.TargetDirectory }, comparer).Any();
        return sharesDirectory && (RevisionFileName.Parse(leftName, rightName + ".pdf").IsMatch
            || RevisionFileName.Parse(rightName, leftName + ".pdf").IsMatch);
    }
}
