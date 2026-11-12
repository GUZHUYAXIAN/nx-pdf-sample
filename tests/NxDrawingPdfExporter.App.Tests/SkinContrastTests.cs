using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class SkinContrastTests
{
    [TestMethod]
    public void SrgbContrastDoesNotRoundThreshold()
    {
        Assert.AreEqual(21d, ColorContrast.Ratio(Color.Black, Color.White), .00001);
        Assert.AreEqual(1d, ColorContrast.Ratio(Color.Red, Color.Red), .00001);
        Assert.IsLessThan(4.5, ColorContrast.Ratio(Color.FromArgb(119, 119, 119), Color.White));
    }

    [TestMethod]
    public void WhitePixelInMostlyDarkImageStillRequiresProtection()
    {
        using var photo = new Bitmap(97, 63);
        using (var graphics = Graphics.FromImage(photo))
            graphics.Clear(Color.FromArgb(6, 20, 42));
        photo.SetPixel(95, 61, Color.White);
        var analysis = ImagePaletteAnalyzer.Analyze(photo);
        Assert.AreEqual(1d, analysis.MaximumLuminance, .00001);
        var result = SurfaceContrastResolver.Resolve(analysis.Bounds, Color.White, Color.Black, 12);
        Assert.IsTrue(result.Protected);
        Assert.IsGreaterThanOrEqualTo(7, result.WorstContrast);
        Assert.IsLessThan(100, result.EffectiveOpacity);
        Assert.AreEqual(Color.White, result.Foreground);
    }

    [TestMethod]
    public void ManualMidGrayKeepsItsColorWithAccessibleFallbackMaterial()
    {
        var gray = Color.FromArgb(119, 119, 119);
        var result = SurfaceContrastResolver.Resolve([Color.Black, Color.White], gray, Color.Navy, 0);
        Assert.IsGreaterThanOrEqualTo(4.5, result.WorstContrast);
        Assert.IsLessThan(7, result.WorstContrast);
        Assert.AreEqual(gray, result.Foreground);
    }

    [TestMethod]
    public void CompatibleDarkColorsKeepRequestedTransparency()
    {
        var result = SurfaceContrastResolver.Resolve([Color.Navy, Color.Black], Color.White, Color.Black, 16);
        Assert.AreEqual(16, result.EffectiveOpacity);
        Assert.IsFalse(result.Protected);
    }
}
