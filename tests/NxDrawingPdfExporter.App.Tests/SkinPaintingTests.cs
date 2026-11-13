using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Controls;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class SkinPaintingTests
{
    [TestMethod]
    public void ScrollingReadOfNativeEditorKeepsUncommittedTextSeparate()
    {
        RunSta(() =>
        {
            using var grid = new SkinDataGridView { AllowUserToAddRows = false };
            grid.Columns.Add(new DataGridViewTextBoxColumn { CellTemplate = new SkinEditCell() });
            grid.Rows.Add("R2");
            grid.CurrentCell = grid[0, 0];
            using var editor = new SkinGridEditor { EditingControlDataGridView = grid, Text = "未确认" };
            Assert.AreEqual("R2", editor.GetEditingControlFormattedValue(DataGridViewDataErrorContexts.Scroll));
            Assert.AreEqual("未确认", editor.GetEditingControlFormattedValue(DataGridViewDataErrorContexts.Commit));
            Assert.AreEqual("未确认", editor.Text);
        });
    }

    [TestMethod]
    public void ALastWrappedRowTallerThanItsViewportGetsEnoughSpaceToRead()
    {
        RunSta(() =>
        {
            using var grid = new SkinDataGridView { AllowUserToAddRows = false, Width = 260, Height = 166 };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Width = 130 });
            grid.Rows.Add("测试长行");
            grid.Rows[0].Height = 460;
            _ = grid.Handle;
            grid.PerformLayout();
            grid.Height = grid.MinimumHeightForWrappedRows(grid.Font.Height + 16);
            grid.PerformLayout();
            Assert.IsLessThanOrEqualTo(grid.ClientSize.Height, grid.GetRowDisplayRectangle(0, false).Bottom);
            Assert.IsFalse(grid.VerticalExtent.CanScroll(1));
        });
    }

    [TestMethod]
    public void EffectiveMaterialMeetsContrastOnRenderedPixels()
    {
        using var picture = new Bitmap(3, 1);
        picture.SetPixel(0, 0, Color.Black);
        picture.SetPixel(1, 0, Color.White);
        picture.SetPixel(2, 0, Color.Navy);
        var pixels = ImagePaletteAnalyzer.Analyze(picture);
        var effects = new ThemeParameters(PanelOpacity: 9);
        var palette = SurfaceContrastResolver.Choose(pixels, effects);
        var theme = ThemeCatalog.Load(AppContext.BaseDirectory)[0];
        var groups = GroupPaletteTests.Groups(3);
        var styles = GroupPaletteResolver.Resolve(pixels, theme, effects, groups, new Dictionary<string, double>());
        var skin = new SkinMaterials(pixels.Bounds, palette, theme, effects, default, styles);
        var surfaces = new[]
        {
            skin.Row(groups[2], SkinRowState.Selected),
            skin.Row(groups[2], SkinRowState.Hovered),
            skin.Editor, skin.Status(SkinTextRole.Error)
        };
        foreach (var surface in surfaces)
        {
            using var actual = (Bitmap)picture.Clone();
            using (var graphics = Graphics.FromImage(actual))
                surface.Paint(graphics, new Rectangle(0, 0, 3, 1));
            for (var x = 0; x < actual.Width; x++)
                Assert.IsGreaterThanOrEqualTo(4.5, ColorContrast.Ratio(surface.Foreground, actual.GetPixel(x, 0)));
        }
    }

    private static void RunSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        failure?.Throw();
    }
}
