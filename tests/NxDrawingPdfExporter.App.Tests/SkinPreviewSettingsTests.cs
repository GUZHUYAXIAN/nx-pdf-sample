using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class SkinPreviewSettingsTests
{
    [TestMethod]
    public void PreviewEditsAndResetLeaveSavedSettingsUntouchedUntilApply()
    {
        var original = new UiSettings
        {
            ThemeId = "rem", Language = "en", ImportedImage = "deadbeefdeadbeefdeadbeefdeadbeef.png",
            ThemeParametersById = new() { ["rem"] = new(PanelOpacity: 12), ["roxy"] = new(PanelOpacity: 66) }
        };
        var preview = new SkinPreviewSettings(original);
        preview.Set("rem", new ThemeParameters(92, 15, 30, 50, 23, 78, 9,
            4, SurfaceMode.Light, false, Color.Gray.ToArgb(), SkinGradientDirection.TopToBottom));
        preview.Reset("roxy");
        Assert.AreEqual(12, original.ThemeParametersById["rem"].PanelOpacity);
        Assert.AreEqual(66, original.ThemeParametersById["roxy"].PanelOpacity);
        var applied = preview.Apply("rem", original.ImportedImage);
        Assert.AreEqual("en", applied.Language);
        Assert.AreEqual(original.ImportedImage, applied.ImportedImage);
        Assert.AreEqual(4, applied.ThemeParametersById["rem"].PanelOpacity);
        Assert.AreEqual(SkinGradientDirection.TopToBottom, applied.ThemeParametersById["rem"].GradientDirection);
        Assert.IsFalse(applied.ThemeParametersById.ContainsKey("roxy"));
    }

    [TestMethod]
    public void FourBundledSkinsUseValidatedMaterialsAndRetainVisibleBackdrop()
    {
        var themes = ThemeCatalog.Load(AppContext.BaseDirectory).Where(theme => theme.ImagePath is not null).ToArray();
        Assert.HasCount(4, themes);
        foreach (var theme in themes)
        {
            using var frame = ThemeFrame.Render(new(1, theme.ImagePath, new Size(560, 315),
                theme, theme.Defaults, false), default);
            Assert.IsNotNull(frame.Backdrop);
            var materials = frame.Materials!;
            Assert.IsGreaterThanOrEqualTo(7, materials.Body.Contrast.WorstContrast, theme.Id);
            Assert.IsLessThan(100, materials.Body.Contrast.EffectiveOpacity, theme.Id);
            Assert.IsGreaterThanOrEqualTo(4.5, materials.Status(SkinTextRole.Error).Contrast.WorstContrast, theme.Id);
        }
    }

    [TestMethod]
    public void ProtectionDoesNotRewriteManualRequestedOpacityOrSwitchRowText()
    {
        var effects = new ThemeParameters(PanelOpacity: 3, AutomaticTextColor: false, ManualTextColorArgb: Color.Gray.ToArgb());
        ColorBounds[] backgrounds = [new(Color.Black, Color.Black), new(Color.White, Color.White)];
        var theme = ThemeCatalog.Load(AppContext.BaseDirectory)[0];
        var analysis = new PaletteAnalysis([Color.Black, Color.White], 0, 1, backgrounds);
        var palette = SurfaceContrastResolver.Choose(analysis, effects);
        var groups = GroupPaletteTests.Groups(6);
        var styles = GroupPaletteResolver.Resolve(analysis, theme, effects, groups, new Dictionary<string, double>());
        var materials = new SkinMaterials(backgrounds, palette, theme, effects, default, styles);
        foreach (var group in groups)
            foreach (var state in Enum.GetValues<SkinRowState>())
            {
                var row = materials.Row(group, state);
                Assert.AreEqual(Color.Gray.ToArgb(), row.Foreground.ToArgb());
                Assert.IsGreaterThanOrEqualTo(4.5, row.Contrast.WorstContrast);
            }
        Assert.AreEqual(3, effects.PanelOpacity);
        Assert.IsTrue(materials.Protected);
    }
}
