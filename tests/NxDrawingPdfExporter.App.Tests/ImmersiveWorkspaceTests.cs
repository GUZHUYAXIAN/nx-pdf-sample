using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Layout;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class ImmersiveWorkspaceTests
{
    [TestMethod]
    public void WorkspaceOwnsWheelOnlyWhenTheListCannotScrollInThatDirection()
    {
        Assert.AreEqual(WheelOwner.Local, WorkspaceLayoutPolicy.RouteWheel(true, new(0, 200), new(100, 300), 1));
        Assert.AreEqual(WheelOwner.Workspace, WorkspaceLayoutPolicy.RouteWheel(true, new(200, 200), new(100, 300), 1));
        Assert.AreEqual(WheelOwner.Local, WorkspaceLayoutPolicy.RouteWheel(true, new(200, 200), new(100, 300), -1));
        Assert.AreEqual(WheelOwner.Workspace, WorkspaceLayoutPolicy.RouteWheel(true, new(0, 0), new(100, 300), -1));
        Assert.AreEqual(WheelOwner.None, WorkspaceLayoutPolicy.RouteWheel(false, new(50, 200), new(0, 300), -1));
    }

    [TestMethod]
    public void HeightAdjustmentUsesLogicalPixelsAndHandleOrientation()
    {
        Assert.AreEqual(380, WorkspaceLayoutPolicy.ResizeHeightDip(280, 150, 144, false));
        Assert.AreEqual(180, WorkspaceLayoutPolicy.ResizeHeightDip(280, 150, 144, true));
        Assert.AreEqual(new WorkspaceHeights(120, 2000), new WorkspaceHeights(-30, 9000).Normalize());
    }

    [TestMethod]
    public void WrappedRowsFitExactlyUntilHorizontalScrollbarConsumesSpace()
    {
        Assert.IsFalse(WorkspaceLayoutPolicy.RowsOverflow(112, 28, [24, 60], 0));
        Assert.IsTrue(WorkspaceLayoutPolicy.RowsOverflow(112, 28, [24, 60], 16));
    }

    [TestMethod]
    public void WheelRemainderDoesNotLeakToADifferentOwner()
    {
        var wheel = new WheelAccumulator();
        Assert.AreEqual(0, wheel.Add(45, WheelOwner.Local, 1));
        Assert.AreEqual(0, wheel.Add(75, WheelOwner.Workspace, 1));
        Assert.AreEqual(1, wheel.Add(45, WheelOwner.Workspace, 1));
        Assert.AreEqual(0, wheel.Add(-60, WheelOwner.Workspace, 1));
        Assert.AreEqual(-1, wheel.Add(-60, WheelOwner.Workspace, 1));
    }

    [TestMethod]
    public void SchemaOnePreservesValidFieldsAndRecoversOnlyTheDamagedParameter()
    {
        var (settings, recovered) = JsonUiSettingsStore.Read("""
            {"schemaVersion":1,"language":"en","themeId":"rem","fontScale":115,
             "importedImage":"deadbeefdeadbeefdeadbeefdeadbeef.png","splitterRatio":0.8,
             "columnWidths":{"Source":235},"themeParametersById":{"rem":{"imageVisibility":81,"zoom":44,"blur":"bad"}}}
            """u8.ToArray());
        Assert.IsTrue(recovered);
        Assert.AreEqual(2, settings.SchemaVersion);
        Assert.AreEqual("en", settings.Language);
        Assert.AreEqual("rem", settings.ThemeId);
        Assert.AreEqual(115, settings.FontScale);
        Assert.AreEqual("deadbeefdeadbeefdeadbeefdeadbeef.png", settings.ImportedImage);
        Assert.AreEqual(235, settings.ColumnWidths["Source"]);
        Assert.AreEqual(new WorkspaceHeights(), settings.WorkspaceHeights);
        var effects = settings.ThemeParametersById["rem"];
        Assert.AreEqual(81, effects.ImageVisibility);
        Assert.AreEqual(44, effects.Zoom);
        Assert.AreEqual(30, effects.PanelOpacity);
        Assert.IsTrue(effects.AutomaticTextColor);
    }

    [TestMethod]
    public void SchemaTwoPreservesIndependentHeightsAndManualEffectValues()
    {
        var (settings, recovered) = JsonUiSettingsStore.Read("""
            {"schemaVersion":2,"themeId":"roxy","workspaceHeights":{"preflightDip":420,"resultsDip":187},
             "themeParametersById":{"roxy":{"panelOpacity":8,"surfaceMode":1,"automaticTextColor":false,
               "manualTextColorArgb":-8947849,"gradientDirection":3,"futureSetting":true}}}
            """u8.ToArray());
        Assert.IsFalse(recovered);
        Assert.AreEqual(new WorkspaceHeights(420, 187), settings.WorkspaceHeights);
        var effects = settings.ThemeParametersById["roxy"];
        Assert.AreEqual(8, effects.PanelOpacity);
        Assert.AreEqual(SurfaceMode.Light, effects.SurfaceMode);
        Assert.IsFalse(effects.AutomaticTextColor);
        Assert.AreEqual(-8947849, effects.ManualTextColorArgb);
        Assert.AreEqual(SkinGradientDirection.BottomToTop, effects.GradientDirection);
    }
}
