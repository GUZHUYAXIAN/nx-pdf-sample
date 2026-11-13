using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Progress;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class PhaseProgressTests
{
    [TestMethod]
    public void OnlyProcessedFilesCountTowardThePercentage()
    {
        var parts = PhaseProgressMath.Segments(32, 10, true);
        Assert.AreEqual(.3125, parts.Completed);
        Assert.AreEqual(.03125, parts.Active);
        Assert.AreEqual(.65625, parts.Remaining);
        Assert.AreEqual(0d, PhaseProgressMath.Segments(0, 0, true).Active);
        Assert.AreEqual(0d, PhaseProgressMath.Segments(1, 1, true).Active);
        Assert.IsLessThan(100, PhaseProgressMath.Percent(int.MaxValue, int.MaxValue - 1));
    }

    [TestMethod]
    public void ProgressPaletteTracksThemeAndContrast()
    {
        var light = NxDrawingPdfExporter.App.Theming.ThemeCatalog.Load(AppContext.BaseDirectory)[0];
        var dark = NxDrawingPdfExporter.App.Theming.ThemeCatalog.Load(AppContext.BaseDirectory)[1];
        foreach (var theme in new[] { light, dark })
        {
            var palette = NxDrawingPdfExporter.App.Theming.ProgressPaletteResolver.Resolve(null, theme, theme.Defaults, null, false);
            Assert.IsGreaterThanOrEqualTo(3, NxDrawingPdfExporter.App.Theming.ColorContrast.Ratio(palette.Track, palette.Completed));
            Assert.IsGreaterThanOrEqualTo(3, NxDrawingPdfExporter.App.Theming.ColorContrast.Ratio(palette.Track, palette.Active));
            Assert.IsGreaterThanOrEqualTo(4.5, NxDrawingPdfExporter.App.Theming.ColorContrast.Ratio(palette.Track, palette.Text));
        }
    }

    [TestMethod]
    public void LocalizedTextKeepsExportAndSaveDenominatorsSeparate()
    {
        var state = new RunProgressState { Phase = RunPhase.Publishing, PublicationReached = true,
            Export = new(32, 10, 5), Publication = new(5, 2, 1), CurrentItemIndex = 2,
            CurrentSourcePath = @"E:\sample\长文件名.prt" };
        var text = new NxDrawingPdfExporter.App.Localization.UiText { Language = "en" };
        var save = ProgressTextFormatter.Format(state, text);
        StringAssert.Contains(save.Title, "2/2");
        StringAssert.Contains(save.Counts, "2/5");
        StringAssert.Contains(save.Counts, "Saved 1");
        StringAssert.Contains(save.Detail, "长文件名.prt");
        var export = ProgressTextFormatter.Format(state with { Phase = RunPhase.Exporting, PublicationReached = false }, text);
        StringAssert.Contains(export.Counts, "10/32");
        StringAssert.Contains(export.Counts, "Generated 5");
        var checking = ProgressTextFormatter.Format(state with { Phase = RunPhase.CheckingWorkerResult,
            PublicationReached = false, CurrentItemIndex = null, Step = WorkerProgressStage.Finished }, text);
        StringAssert.Contains(checking.Detail, "Checking the Worker result");
        Assert.IsFalse(ProgressTextFormatter.Format(state with { Publication = new() }, text).Counts.Contains("0/0", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MissingSnapshotsRecoverAndStoppedMonitorIgnoresLaterReads()
    {
        var available = false;
        var statuses = new List<string?>();
        var received = new List<long>();
        var original = new WorkerProgressSnapshot { RunId = "run", Total = 1, Sequence = 7, Stage = WorkerProgressStage.Ready };
        var value = original;
        await using var monitor = new RunProgressMonitor("ignored", "run", 1, item => received.Add(item.Sequence),
            statuses.Add, _ => available ? value : throw new FileNotFoundException());
        for (var i = 0; i < 5; i++)
            await monitor.ReadOnceAsync(default);
        Assert.HasCount(1, statuses);
        available = true;
        await monitor.ReadOnceAsync(default);
        Assert.HasCount(2, statuses);
        Assert.IsNull(statuses[1]);
        CollectionAssert.AreEqual(new long[] { 7 }, received);
        value = new WorkerProgressSnapshot { RunId = "run", Total = 1, Sequence = 7, Stage = WorkerProgressStage.BetweenItems,
            Processed = 1, Generated = 1 };
        for (var i = 0; i < 4; i++)
            await monitor.ReadOnceAsync(default);
        Assert.HasCount(3, statuses);
        await monitor.ReadOnceAsync(default);
        Assert.HasCount(3, statuses);
        value = original;
        await monitor.ReadOnceAsync(default);
        Assert.HasCount(4, statuses);
        Assert.IsNull(statuses[3]);
        await monitor.StopAsync();
        Assert.IsFalse(monitor.Accept(new WorkerProgressSnapshot
            { RunId = "run", Total = 1, Sequence = 8, Stage = WorkerProgressStage.Ready }));
    }

    [TestMethod]
    public void MonitorAcceptsCumulativeGapsButRejectsWrongRunAndRegression()
    {
        var received = new List<long>();
        using var monitor = new RunProgressMonitor("ignored", "run", 10,
            item => received.Add(item.Sequence), _ => { });
        WorkerProgressSnapshot Value(long seq, int processed, string id = "run") =>
            new() { RunId = id, Total = 10, Sequence = seq, Processed = processed,
                Generated = processed, Stage = WorkerProgressStage.BetweenItems };
        Assert.IsTrue(monitor.Accept(Value(2, 1)));
        Assert.IsFalse(monitor.Accept(Value(3, 2, "other")));
        Assert.IsTrue(monitor.Accept(Value(8, 4)));
        Assert.IsFalse(monitor.Accept(Value(7, 4)));
        Assert.IsFalse(monitor.Accept(Value(9, 3)));
        CollectionAssert.AreEqual(new long[] { 2, 8 }, received);
    }
}
