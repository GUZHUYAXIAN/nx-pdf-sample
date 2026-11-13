using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class GroupPaletteTests
{
    [TestMethod]
    public void PerceptualDistanceUsesLinearSrgb()
    {
        Assert.AreEqual(1d, PerceptualColor.Distance(Color.White, Color.Black), .000001);
        Assert.AreEqual(0d, PerceptualColor.Distance(Color.Navy, Color.Navy), .000001);
    }

    [TestMethod]
    [DataRow(false, 24, 38, 64)]
    [DataRow(true, 234, 214, 190)]
    [DataRow(false, 108, 108, 108)]
    public void GroupColorsAreSeparatedAfterComposition(bool light, int r, int g, int b)
    {
        var background = Color.FromArgb(r, g, b);
        ColorBounds[] bounds = [new(background, background)];
        var analysis = new PaletteAnalysis([background], 0, 1, bounds);
        var theme = ThemeCatalog.Load(AppContext.BaseDirectory)[0];
        var effects = new ThemeParameters(SurfaceMode: light ? SurfaceMode.Light : SurfaceMode.Dark);
        var palette = SurfaceContrastResolver.Choose(analysis, effects);
        var groups = Groups(6);
        var styles = GroupPaletteResolver.Resolve(analysis, theme, effects, groups, new Dictionary<string, double>());
        var materials = new SkinMaterials(bounds, palette, theme, effects, default, styles);
        var ungrouped = materials.Body.Composite(background);
        var colors = groups.Select(group => materials.Row(group, SkinRowState.Normal).Composite(background)).ToArray();
        foreach (var group in groups)
            Assert.IsGreaterThanOrEqualTo(4.5, materials.Row(group, SkinRowState.Selected).Contrast.WorstContrast);
        for (var i = 0; i < colors.Length; i++)
        {
            Assert.IsGreaterThanOrEqualTo(.04, PerceptualColor.Distance(colors[i], ungrouped), $"Group {i} vs body");
            for (var j = 0; j < i; j++)
                Assert.IsGreaterThanOrEqualTo(.05, PerceptualColor.Distance(colors[i], colors[j]), $"Groups {i}/{j}");
        }
    }

    [TestMethod]
    public void DirectoryIdentitySurvivesLabelReuseAndMoreGroups()
    {
        var theme = ThemeCatalog.Load(AppContext.BaseDirectory)[0];
        var background = Color.Navy;
        var analysis = new PaletteAnalysis([background], 0, 1, [new(background, background)]);
        var first = GroupPaletteResolver.Resolve(analysis, theme, new(), Groups(6), new Dictionary<string, double>());
        var hue = first.ToDictionary(pair => pair.Key, pair => pair.Value.Hue, StringComparer.OrdinalIgnoreCase);
        var more = GroupPaletteResolver.Resolve(analysis, theme, new(SurfaceMode: SurfaceMode.Light), Groups(8).Reverse().ToArray(), hue);
        foreach (var pair in hue)
            Assert.AreEqual(pair.Value, more[pair.Key].Hue);
        Assert.HasCount(8, more.Values.Select(style => style.Hue).Distinct().ToArray());
    }

    [TestMethod]
    public void StrengthZeroKeepsMarkerAndIncreasingStrengthKeepsHue()
    {
        var zero = GroupPaletteResolver.Style(187, new(GroupColorStrength: 0));
        var normal = GroupPaletteResolver.Style(187, new());
        var strong = GroupPaletteResolver.Style(187, new(GroupColorStrength: 100));
        Assert.AreEqual(0, zero.Opacity);
        Assert.AreEqual(zero.Marker, strong.Marker);
        Assert.AreEqual(zero.Hue, strong.Hue);
        Assert.IsGreaterThan(normal.Opacity, strong.Opacity);
        Assert.IsGreaterThanOrEqualTo(4.5, ColorContrast.Ratio(zero.Marker, zero.MarkerText));
    }

    internal static GroupVisualKey[] Groups(int count) => Enumerable.Range(0, count)
        .Select(i => new GroupVisualKey($@"E:\groups\{i}", "A")).ToArray();
}
