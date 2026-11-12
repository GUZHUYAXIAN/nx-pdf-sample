using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class ThemeFrameTests
{
    [TestMethod]
    public void AdjacentControlsMapToTheSameFixedWindowPixels()
    {
        var client = new Rectangle(0, 0, 1000, 800);
        var pixels = new Size(500, 400);
        var first = BackdropMapping.SourceRegion(client, pixels, new(100, 60, 200, 100));
        var second = BackdropMapping.SourceRegion(client, pixels, new(300, 60, 130, 100));
        Assert.AreEqual(new RectangleF(50, 30, 100, 50), first);
        Assert.AreEqual(first.Right, second.Left);
        Assert.AreEqual(15f, BackdropMapping.SourceRegion(client, pixels, new(100, 30, 200, 100)).Top);
    }

    [TestMethod]
    public async Task CancellingPreviewRejectsItsLateFrameAndDisposesIt()
    {
        using var entered = new ManualResetEventSlim();
        using var unblock = new ManualResetEventSlim();
        var observed = new List<ThemeFrame>();
        ThemeFrame? rejected = null;
        using var coordinator = new ThemeRenderCoordinator((request, _) =>
        {
            var frame = new ThemeFrame(request, new Bitmap(1, 1), null);
            if (request.Revision == 1)
            {
                rejected = frame;
                entered.Set();
                unblock.Wait(TimeSpan.FromSeconds(8));
            }
            return frame;
        }, observed.Add, _ => Assert.Fail("Render failed."), TimeSpan.Zero);
        var preview = coordinator.Request(MakeRequest(1));
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
        var restore = coordinator.Request(MakeRequest(2));
        unblock.Set();
        await Task.WhenAll(preview, restore);
        Assert.HasCount(1, observed);
        Assert.AreEqual(2L, observed[0].Revision);
        Assert.IsTrue(rejected!.IsDisposed);
        observed[0].Dispose();
    }

    [TestMethod]
    public async Task DisposingCoordinatorRejectsFrameFromInFlightRenderer()
    {
        var entered = new TaskCompletionSource<ThemeFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var count = 0;
        using var coordinator = new ThemeRenderCoordinator((request, _) =>
        {
            var frame = new ThemeFrame(request, new Bitmap(1, 1), null);
            entered.SetResult(frame);
            release.Wait(TimeSpan.FromSeconds(8));
            return frame;
        }, _ => Interlocked.Increment(ref count), _ => Assert.Fail(), TimeSpan.Zero);
        var work = coordinator.Request(MakeRequest(1));
        var pending = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Dispose();
        release.Set();
        await work;
        Assert.AreEqual(0, count);
        Assert.IsTrue(pending.IsDisposed);
    }

    [TestMethod]
    public async Task ClosingDisposesQueuedUiDeliveryEvenWhenTheDispatcherNeverRuns()
    {
        ThemeFrame? rendered = null;
        Action? delivery = null;
        var delivered = false;
        using var coordinator = new ThemeRenderCoordinator((request, _) =>
        {
            rendered = new ThemeFrame(request, new Bitmap(1, 1), null);
            return rendered;
        }, _ => delivered = true, _ => Assert.Fail(), TimeSpan.Zero, action => delivery = action);
        await coordinator.Request(MakeRequest(1));
        Assert.IsNotNull(rendered);
        Assert.IsNotNull(delivery);
        Assert.IsFalse(rendered.IsDisposed);
        coordinator.Dispose();
        Assert.IsTrue(rendered.IsDisposed);
        delivery();
        Assert.IsFalse(delivered);
    }

    private static ThemeRenderRequest MakeRequest(long revision) =>
        new(revision, null, new Size(1, 1), ThemeCatalog.Load(AppContext.BaseDirectory)[0], new ThemeParameters(), false);
}
