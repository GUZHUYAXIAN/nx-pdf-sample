using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Output;

namespace NxDrawingPdfExporter.Core.Tests;

[TestClass]
public sealed class RevisionOutputPlannerTests
{
    [TestMethod]
    [DataRow(" r100 ", 100)]
    [DataRow("R0", 0)]
    [DataRow("002", 2)]
    public void NumericEdits(string value, int expected)
    {
        Assert.IsTrue(RevisionNumber.TryParse(value, out var number));
        Assert.AreEqual(expected, number);
        Assert.AreEqual("组件-R2-R1.pdf", RevisionFileName.Format("组件-R2.prt", 1));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-2")]
    [DataRow("1.5")]
    [DataRow("R2147483648")]
    [DataRow("R1/2")]
    public void InvalidEdits(string value) => Assert.IsFalse(RevisionNumber.TryParse(value, out _));

    [TestMethod]
    public void PlansFromBothDirectoriesAndPreservesExplicitOldRevision()
    {
        var paths = new[] { @"C:\source\组件.prt" };
        var disk = new CatalogDisk(@"C:\source\组件-R100.PDF", @"C:\target\组件-r3.pdf",
            @"C:\target\组件-R900.bak.pdf", @"C:\target\组件B-R500.pdf");
        var planner = new RevisionOutputPlanner(new RevisionCatalog(disk), disk);
        var automatic = planner.Plan(Request(paths)).Items.Single();
        Assert.AreEqual(101, automatic.SuggestedRevision);
        Assert.AreEqual(@"C:\target\组件-R101.pdf", automatic.FinalOutputPath);
        Assert.AreEqual(PlannedPublishIntent.CreateNew, automatic.PublishIntent);
        var manual = planner.Plan(Request(paths, 3)).Items.Single();
        Assert.AreEqual(100, manual.CatalogSnapshot!.SourceHighest);
        Assert.AreEqual(3, manual.CatalogSnapshot.TargetHighest);
        Assert.AreEqual(PlannedPublishIntent.ReplaceExpected, manual.PublishIntent);
        Assert.AreEqual(@"C:\target\组件-R3.pdf", manual.FinalOutputPath);
        Assert.AreEqual(PlannedPublishIntent.CreateNew, planner.Plan(Request(paths, 100)).Items.Single().PublishIntent);
        Assert.AreEqual(@"C:\target\组件.pdf", planner.Plan(Request(paths, 0)).Items.Single().FinalOutputPath);
    }

    [TestMethod]
    public void AmbiguousFamiliesAreBlockedOnlyForRevision()
    {
        var paths = new[] { @"C:\one\A.prt", @"C:\two\A-R1.prt" };
        var disk = new CatalogDisk();
        var planner = new RevisionOutputPlanner(new RevisionCatalog(disk), disk);
        Assert.IsTrue(planner.Plan(Request(paths)).HasBlockingIssues);
        Assert.IsFalse(planner.Plan(Request(paths, policy: ExistingPdfPolicy.Overwrite)).HasBlockingIssues);
        Assert.IsTrue(planner.Plan(Request([paths[0], @"C:\two\A.prt"], 2)).HasBlockingIssues);
    }

    [TestMethod]
    public void AliasAndUnreadableDirectoryBlockAndLimitAllowsManualChoice()
    {
        var paths = new[] { @"C:\source\A.prt" };
        var disk = new CatalogDisk(@"C:\source\A-R2147483647.pdf");
        var planner = new RevisionOutputPlanner(new RevisionCatalog(disk), disk);
        Assert.AreEqual(ResultReasons.RevisionLimit, planner.Plan(Request(paths)).Items.Single().ReasonCode);
        Assert.IsFalse(planner.Plan(Request(paths, 2)).HasBlockingIssues);
        disk.Paths.Add(@"C:\target\A-R02.pdf");
        Assert.AreEqual(ResultReasons.RevisionAlias, planner.Plan(Request(paths, 2)).Items.Single().ReasonCode);
        disk.Unreadable = true;
        Assert.AreEqual(ResultReasons.DirectoryUnreadable, planner.Plan(Request(paths)).Items.Single().ReasonCode);
    }

    private static OutputPlanningRequest Request(string[] paths, int? revision = null,
        ExistingPdfPolicy policy = ExistingPdfPolicy.Revision) =>
        new(paths, new OutputRoutingSettings(OutputMode.UnifiedDirectory, @"C:\target", new Dictionary<string, string>()),
            policy, revision is int number ? paths.ToDictionary(p => p, _ => new RevisionChoice(number, true)) : new());

    private sealed class CatalogDisk(params string[] paths) : IOutputDirectoryReader, ITargetStateReader
    {
        public List<string> Paths { get; } = paths.ToList();
        public bool Unreadable { get; set; }

        public IReadOnlyList<string> ReadFiles(string directory) => Unreadable ? throw new UnauthorizedAccessException() :
            Paths.Where(p => string.Equals(Path.GetDirectoryName(p), directory, StringComparison.OrdinalIgnoreCase)).ToArray();

        public TargetSnapshot Capture(string path) => Paths.Contains(path, StringComparer.OrdinalIgnoreCase)
            ? new TargetSnapshot(true, 100, 42, "hash") : TargetSnapshot.Missing;
    }
}
