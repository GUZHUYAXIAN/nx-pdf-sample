using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Controls;
using NxDrawingPdfExporter.App.Dialogs;
using NxDrawingPdfExporter.App.Localization;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class SkinPaintingTests
{
    [TestMethod]
    [DataRow("cloud", false, false)]
    [DataRow("graphite", false, false)]
    [DataRow("rem", false, false)]
    [DataRow("rem", true, false)]
    [DataRow("rem", false, true)]
    public void ComboBoxRenderingMatchesAvailableBackdrop(string themeId, bool hideImage, bool highContrast)
    {
        RunSta(() =>
        {
            var themes = ThemeCatalog.Load(AppContext.BaseDirectory);
            var theme = themes.Single(item => item.Id == themeId);
            var ui = new UiText { Language = "zh-CN" };
            using var dialog = new AppearanceDialog(ui, new UiSettings { ThemeId = themeId }, themes,
                new ImportedImageStore(AppContext.BaseDirectory), (_, _, _) => { }, _ => { });
            using var nxHost = new Form();
            using var nxPicker = new SkinComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true };
            nxPicker.Format += (_, args) => args.Value = "NX " + args.ListItem;
            nxPicker.Items.AddRange(["10", "2506"]);
            nxPicker.SelectedIndex = 1;
            nxHost.Controls.Add(nxPicker);
            var pickers = Descendants(dialog).OfType<SkinComboBox>().ToArray();
            Assert.HasCount(3, pickers);
            var themePicker = pickers.Single(picker => picker.SelectedItem is ThemeDefinition);
            Assert.AreEqual(ui.Get("Theme." + themeId), themePicker.GetItemText(themePicker.SelectedItem));
            Assert.HasCount(themes.Count, themePicker.Items.Cast<object>());
            Assert.AreEqual("NX 2506", nxPicker.GetItemText(nxPicker.SelectedItem));

            var parameters = hideImage ? theme.Defaults with { ImageVisibility = 0 } : theme.Defaults;
            using var frame = ThemeFrame.Render(new(1, theme.ImagePath, new Size(160, 120), theme, parameters, highContrast), default);
            var primary = new SkinButton { Tag = "Primary" };
            nxHost.Controls.Add(primary);
            var selectedIndices = pickers.Append(nxPicker).Select(picker => picker.SelectedIndex).ToArray();
            foreach (var picker in pickers.Append(nxPicker))
                _ = picker.Handle;
            ThemeControlStyler.Apply(dialog, theme, new WindowBackdrop(dialog, () => frame));
            ThemeControlStyler.Apply(nxHost, theme, new WindowBackdrop(nxHost, () => frame));
            var expected = theme.IsImageTheme && !hideImage && !highContrast ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
            var index = 0;
            foreach (var picker in pickers.Append(nxPicker))
            {
                Assert.AreEqual(expected, picker.DrawMode, picker.GetItemText(picker.SelectedItem));
                Assert.AreEqual(selectedIndices[index++], picker.SelectedIndex);
                Assert.IsFalse(string.IsNullOrWhiteSpace(picker.GetItemText(picker.SelectedItem)));
            }
            if (expected == DrawMode.Normal)
                Assert.AreEqual(theme.Accent, primary.BackColor);
        });

        static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>()
            .SelectMany(child => Descendants(child).Prepend(child));
    }

    [TestMethod]
    public void PhotoDropdownGrowsWhenInterfaceFontIncreases()
    {
        RunSta(() =>
        {
            var theme = ThemeCatalog.Load(AppContext.BaseDirectory).Single(item => item.Id == "rem");
            using var form = new Form();
            using var small = new Font("Microsoft YaHei UI", 9);
            using var large = new Font("Microsoft YaHei UI", 18);
            form.Font = small;
            var picker = new SkinComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            picker.Items.AddRange(["NX 10", "NX 2506"]);
            picker.SelectedIndex = 1;
            form.Controls.Add(picker);
            using var frame = ThemeFrame.Render(new(1, theme.ImagePath, new Size(160, 120), theme, theme.Defaults, false), default);
            _ = picker.Handle;
            ThemeControlStyler.Apply(form, theme, new WindowBackdrop(form, () => frame));
            var originalHeight = picker.ItemHeight;

            form.Font = large;

            Assert.IsGreaterThan(originalHeight, picker.ItemHeight);
            Assert.IsGreaterThanOrEqualTo(picker.Font.Height, picker.ItemHeight);
            Assert.AreEqual(1, picker.SelectedIndex);
        });
    }

    [TestMethod]
    [DataRow(DrawItemState.Default)]
    [DataRow(DrawItemState.ComboBoxEdit)]
    public void OwnerDrawFallbackPaintsTextWhenAFrameHasNoPhoto(DrawItemState state)
    {
        RunSta(() =>
        {
            using var picker = new SkinComboBox { DrawMode = DrawMode.OwnerDrawFixed, DropDownStyle = ComboBoxStyle.DropDownList };
            picker.Items.Add("NX 2506");
            picker.SelectedIndex = 0;
            using var canvas = new Bitmap(180, 28);
            using var graphics = Graphics.FromImage(canvas);
            graphics.Clear(Color.Red);
            var args = new DrawItemEventArgs(graphics, picker.Font, new Rectangle(Point.Empty, canvas.Size),
                state == DrawItemState.ComboBoxEdit ? -1 : 0, state, Color.Black, Color.White);
            var draw = typeof(SkinComboBox).GetMethod("OnDrawItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            draw.Invoke(picker, [args]);
            Assert.AreEqual(Color.White.ToArgb(), canvas.GetPixel(179, 27).ToArgb());
            Assert.IsTrue(Enumerable.Range(0, 120).Any(x => Enumerable.Range(0, canvas.Height)
                .Any(y => canvas.GetPixel(x, y).GetBrightness() < .45f)));
        });
    }

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
