using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class ThemeTests
{
    [TestMethod]
    public void CatalogHasFiveColorsAndFourNamedCharacters()
    {
        var themes = ThemeCatalog.Load(AppContext.BaseDirectory);
        CollectionAssert.AreEqual(new[] { "cloud", "graphite", "lake", "pine", "sand", "rem", "inori", "frieren", "roxy", "imported" },
            themes.Select(theme => theme.Id).ToArray());
    }

    [TestMethod]
    public void BuiltThemesResolveFullArtAndThumbnails()
    {
        foreach (var theme in ThemeCatalog.Load(AppContext.BaseDirectory).Where(theme => theme.ImagePath is not null))
        {
            Assert.IsTrue(theme.HasAssets, theme.Id);
            using var artwork = new Bitmap(theme.ImagePath!);
            using var thumbnail = new Bitmap(theme.ThumbnailPath!);
            Assert.IsGreaterThan(1500, artwork.Width);
            Assert.AreEqual(320, thumbnail.Width);
        }
    }

    [TestMethod]
    public void ImportCopiesWithoutHoldingOrChangingOriginal()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "NxDrawingPdfExporter.slnx")))
            root = root.Parent ?? throw new DirectoryNotFoundException();
        var directory = Directory.CreateDirectory(Path.Combine(root.FullName, "artifacts", "image-import-tests", Guid.NewGuid().ToString("N")));
        var source = Path.Combine(directory.FullName, "source.png");
        try
        {
            using (var bitmap = new Bitmap(30, 20))
                bitmap.Save(source, System.Drawing.Imaging.ImageFormat.Png);
            var bytes = File.ReadAllBytes(source);
            var store = new ImportedImageStore(Path.Combine(directory.FullName, "imports"));
            var path = store.Resolve(store.Import(source));
            Assert.IsNotNull(path);
            using (File.Open(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(source));
            Assert.AreNotEqual(source, path);
            using (var oversized = File.Create(Path.Combine(directory.FullName, "large.png")))
                oversized.SetLength(ImportedImageStore.MaximumBytes + 1);
            Assert.ThrowsExactly<InvalidDataException>(() => store.Import(Path.Combine(directory.FullName, "large.png")));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void ZeroVisibilityReturnsPlainBackground()
    {
        using var source = new Bitmap(200, 100);
        using var graphics = Graphics.FromImage(source);
        graphics.Clear(Color.Red);
        var theme = ThemeCatalog.Load(AppContext.BaseDirectory)[0];
        using var output = BackgroundRenderer.Render(source, new Size(60, 90), theme,
            new ThemeParameters(0, 0, 0, 0));
        Assert.AreEqual(theme.Background.ToArgb(), output.GetPixel(30, 40).ToArgb());
    }
}
