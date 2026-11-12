using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Localization;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class UiSettingsTests
{
    [TestMethod]
    public void AllReasonKeysRenderInBothLanguages()
    {
        var ui = new UiText();
        var codes = typeof(ResultReasons).GetFields().Select(field => (string)field.GetRawConstantValue()!).ToArray();
        foreach (var code in codes.Where(code => code != ResultReasons.Unknown))
            Assert.AreNotEqual(ui.Reason(ResultReasons.Unknown), ui.Reason(code));
        var chinese = ui.Reason(ResultReasons.TargetChanged);
        ui.Language = "en";
        Assert.AreNotEqual(chinese, ui.Reason(ResultReasons.TargetChanged));
        Assert.AreEqual(ui.Reason(ResultReasons.Unknown), ui.Reason("FutureCode"));
    }

    [TestMethod]
    public void InvalidLayoutAndUnknownSchemaUseDefaults()
    {
        var defaults = new UiSettings();
        var invalid = (defaults with { SplitterRatio = 2, FontScale = 300 }).Normalize();
        Assert.AreEqual(defaults.SplitterRatio, invalid.SplitterRatio);
        Assert.AreEqual(defaults.FontScale, invalid.FontScale);
        Assert.AreEqual("cloud", (defaults with { SchemaVersion = 100, ThemeId = "roxy" }).Normalize().ThemeId);
    }
}
