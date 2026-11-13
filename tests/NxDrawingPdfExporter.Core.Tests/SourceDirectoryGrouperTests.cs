using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Input;
using NxDrawingPdfExporter.Core.Output;

namespace NxDrawingPdfExporter.Core.Tests;

[TestClass]
public sealed class SourceDirectoryGrouperTests
{
    [TestMethod]
    public void GroupsKeepFirstOccurrenceAndDistinguishFullParents()
    {
        string[] paths = [@"C:\one\drawings\A.prt", @"c:\ONE\drawings\a.PRT",
            @"C:\two\drawings\A.prt", @"C:\one\drawings\B.prt"];
        var groups = SourceDirectoryGrouper.Group(paths,
            new Dictionary<string, string> { [@"c:\one\drawings\"] = "D" });
        CollectionAssert.AreEqual(new[] { "D", "B" }, groups.Select(x => x.Label).ToArray());
        CollectionAssert.AreEqual(new[] { paths[0], paths[3] }, groups[0].SourcePaths.ToArray());
        Assert.AreEqual(paths[2], groups[1].SourcePaths.Single());
    }

    [TestMethod]
    public void GroupLabelsContinueAfterZ()
    {
        var paths = Enumerable.Range(0, 27).Select(i => $@"C:\drawings\{i}\sheet.prt").ToArray();
        Assert.AreEqual("AA", SourceDirectoryGrouper.Group(paths, new Dictionary<string, string>())[26].Label);
    }

    [TestMethod]
    public void GroupTargetSurvivesUnifiedModeWithoutChangingOtherGroups()
    {
        var targets = new Dictionary<string, string> { [@"c:\ONE\"] = @"C:\custom" };
        var grouped = new OutputRoutingSettings(OutputMode.GroupedDirectories, @"C:\unified", targets);
        var unified = new OutputRoutingSettings(OutputMode.UnifiedDirectory, @"C:\unified", targets);
        Assert.AreEqual(@"C:\custom", grouped.ResolveDirectory(@"C:\one\a.prt"));
        Assert.AreEqual(@"C:\two", grouped.ResolveDirectory(@"C:\two\b.prt"));
        Assert.AreEqual(@"C:\unified", unified.ResolveDirectory(@"C:\one\a.prt"));
        Assert.AreEqual(@"C:\custom", grouped.ResolveDirectory(@"C:\one\a.prt"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new OutputRoutingSettings(OutputMode.UnifiedDirectory, @"C:relative", targets)
                .ResolveDirectory(@"C:\one\a.prt"));
    }
}
