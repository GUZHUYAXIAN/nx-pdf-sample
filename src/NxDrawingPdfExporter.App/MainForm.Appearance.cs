using System.Drawing.Drawing2D;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Dialogs;
using NxDrawingPdfExporter.App.Theming;
using NxDrawingPdfExporter.Core.Input;

namespace NxDrawingPdfExporter.App;

public sealed partial class MainForm
{
    private IReadOnlyList<ThemeDefinition> themes = [];
    private readonly ImportedImageStore importedImages = new(Path.Combine(JsonUiSettingsStore.LocalRoot, "theme-images"));
    private readonly System.Windows.Forms.Timer paintDelay = new() { Interval = 120 };
    private ThemeDefinition? activeTheme;
    private ThemeParameters activeEffects = new();
    private string? activeImagePath;
    private int paintVersion;
    private bool painting;
    private bool themeWarningShown;
    private Bitmap? workspaceBackground;

    partial void InitializeAppearance()
    {
        themes = ThemeCatalog.Load(AppContext.BaseDirectory);
        paintDelay.Tick += async (_, _) => await PaintBackgroundAsync();
        artwork.SizeChanged += (_, _) => SchedulePaint();
        ClientSizeChanged += (_, _) => { ResizeArtwork(); SchedulePaint(); };
        ApplyAppearance(settings.ThemeId, settings.ThemeParametersById.GetValueOrDefault(settings.ThemeId), settings.ImportedImage);
        FormClosed += (_, _) =>
        {
            paintDelay.Dispose();
            workspaceBackground?.Dispose();
            artwork.Image?.Dispose();
            hints.Dispose();
        };
    }

    partial void OpenAppearance()
    {
        if (!CommitRevisionEdit())
            return;
        var before = settings;
        using var dialog = new AppearanceDialog(Ui, before, themes, importedImages,
            (id, effects, image) => ApplyAppearance(id, effects, image)) { Font = Font };
        ApplyControlPalette(dialog, CurrentPalette());
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings = settings with
            {
                ThemeId = dialog.ThemeId,
                ThemeParametersById = new Dictionary<string, ThemeParameters>(dialog.AppliedParameters),
                ImportedImage = dialog.ImageReference
            };
            SaveSettings();
        }
        ApplyAppearance(settings.ThemeId, settings.ThemeParametersById.GetValueOrDefault(settings.ThemeId), settings.ImportedImage);
    }

    private void ApplyAppearance(string id, ThemeParameters? effects, string? reference)
    {
        var theme = themes.FirstOrDefault(theme => theme.Id == id) ?? themes[0];
        var imagePath = id == "imported" ? importedImages.Resolve(reference) : theme.ImagePath;
        if (!theme.HasAssets || (theme.IsImageTheme && imagePath is null))
        {
            FallBackTheme();
            return;
        }
        activeTheme = theme;
        activeEffects = effects ?? theme.Defaults;
        activeImagePath = imagePath;
        ResizeArtwork();
        RefreshThemeColors();
        SchedulePaint();
    }

    private void FallBackTheme()
    {
        activeTheme = themes[0];
        activeImagePath = null;
        ResizeArtwork();
        RefreshThemeColors();
        SchedulePaint();
        if (!themeWarningShown)
        {
            themeWarningShown = true;
            if (IsHandleCreated)
                BeginInvoke(() => Notify("ThemeUnavailable"));
            else
                Shown += (_, _) => Notify("ThemeUnavailable");
        }
    }

    private void ResizeArtwork()
    {
        if (settingsBand.ColumnStyles.Count < 2)
            return;
        var show = activeImagePath is not null && activeEffects.ImageVisibility > 0 &&
            !SystemInformation.HighContrast && ClientSize.Width * 96 / DeviceDpi >= 820;
        var width = show ? Math.Min(380 * DeviceDpi / 96, ClientSize.Width / 4) : 0;
        if (settingsBand.ColumnStyles[1].Width != width)
            settingsBand.ColumnStyles[1].Width = width;
        artwork.Visible = show;
    }

    private void SchedulePaint()
    {
        paintVersion++;
        if (IsDisposed)
            return;
        if (activeImagePath is null || activeEffects.ImageVisibility == 0 || SystemInformation.HighContrast)
        {
            SwapBackground(null, null);
            return;
        }
        if (IsHandleCreated)
        {
            paintDelay.Stop();
            paintDelay.Start();
        }
        else
            Shown += (_, _) => SchedulePaint();
    }

    private async Task PaintBackgroundAsync()
    {
        paintDelay.Stop();
        if (painting || activeTheme is null || activeImagePath is null || IsDisposed)
            return;
        painting = true;
        var version = paintVersion;
        var path = activeImagePath;
        var theme = activeTheme;
        var effects = activeEffects;
        var canvas = BoundedSize(ClientSize);
        var portrait = artwork.Visible ? BoundedSize(artwork.ClientSize) : Size.Empty;
        try
        {
            var images = await Task.Run(() =>
            {
                using var source = new Bitmap(path);
                var background = BackgroundRenderer.Render(source, canvas, theme, effects);
                try
                {
                    var side = portrait.IsEmpty ? null : BackgroundRenderer.Render(source, portrait, theme, effects);
                    return (background, side);
                }
                catch
                {
                    background.Dispose();
                    throw;
                }
            });
            if (!IsDisposed && version == paintVersion)
                SwapBackground(images.background, images.side);
            else
            {
                images.background.Dispose();
                images.side?.Dispose();
            }
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException
            or OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        {
            System.Diagnostics.Debug.WriteLine(error);
            if (!IsDisposed && version == paintVersion)
                FallBackTheme();
        }
        finally
        {
            painting = false;
            if (!IsDisposed && version != paintVersion && activeImagePath is not null)
            {
                paintDelay.Stop();
                paintDelay.Start();
            }
        }
    }

    private static Size BoundedSize(Size size)
    {
        var scale = Math.Min(1d, 1800d / Math.Max(1, Math.Max(size.Width, size.Height)));
        return new Size(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale)));
    }

    private void SwapBackground(Bitmap? background, Bitmap? side)
    {
        var old = workspaceBackground;
        workspaceBackground = background;
        old?.Dispose();
        var oldSide = artwork.Image;
        artwork.Image = side;
        oldSide?.Dispose();
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs args)
    {
        base.OnPaintBackground(args);
        if (!SystemInformation.HighContrast && workspaceBackground is not null)
        {
            args.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            args.Graphics.DrawImage(workspaceBackground, ClientRectangle);
        }
    }

    private ThemeDefinition CurrentPalette()
    {
        var theme = activeTheme ?? themes[0];
        return SystemInformation.HighContrast
            ? theme with { Background = SystemColors.Window, Surface = SystemColors.Window, Foreground = SystemColors.WindowText, Accent = SystemColors.Highlight }
            : theme;
    }

    partial void RefreshThemeColors()
    {
        if (themes.Count == 0 || activeTheme is null)
            return;
        var palette = CurrentPalette();
        BackColor = palette.Background;
        ApplyControlPalette(shell, palette);
        shell.BackColor = Color.Transparent;
        settingsBand.BackColor = Color.Transparent;
        menu.Renderer = new ToolStripProfessionalRenderer(new ThemeMenuColors(palette)) { RoundedEdges = false };
        ColorMenu(menu.Items, palette);
        hints.BackColor = palette.Surface;
        hints.ForeColor = palette.Foreground;
        foreach (var grid in new[] { preflight, results, manualGrid })
        {
            grid.BackgroundColor = palette.Surface;
            grid.GridColor = Blend(palette.Surface, palette.Foreground, .15f);
            grid.DefaultCellStyle.BackColor = palette.Surface;
            grid.DefaultCellStyle.ForeColor = palette.Foreground;
            grid.DefaultCellStyle.SelectionBackColor = palette.Accent;
            grid.DefaultCellStyle.SelectionForeColor = Contrast(palette.Accent);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Blend(palette.Surface, palette.Foreground, .09f);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Foreground;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
            foreach (DataGridViewRow row in grid.Rows)
                row.DefaultCellStyle.BackColor = row.Tag is GridRow data ? GroupColor(data.Group) : Color.Empty;
        }
        foreach (Control row in outputRows.Controls)
            if (row.Tag is SourceDirectoryGroup group)
                row.BackColor = GroupColor(group.Label).IsEmpty ? palette.Surface : GroupColor(group.Label);
        Invalidate(true);
    }

    private static void ApplyControlPalette(Control root, ThemeDefinition palette)
    {
        root.ForeColor = palette.Foreground;
        if (root is not PictureBox)
            root.BackColor = palette.Surface;
        if (root is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Blend(palette.Surface, palette.Foreground, .27f);
            button.FlatAppearance.MouseOverBackColor = Blend(palette.Surface, palette.Accent, .2f);
            if (Equals(button.Tag, "Primary"))
            {
                button.BackColor = palette.Accent;
                button.ForeColor = Contrast(palette.Accent);
                button.FlatAppearance.MouseOverBackColor = Blend(palette.Accent, palette.Surface, .14f);
            }
        }
        foreach (Control control in root.Controls)
            if (control is not ScrollBar)
                ApplyControlPalette(control, palette);
    }

    private static void ColorMenu(ToolStripItemCollection items, ThemeDefinition palette)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = palette.Surface;
            item.ForeColor = palette.Foreground;
            if (item is ToolStripDropDownItem dropdown)
                ColorMenu(dropdown.DropDownItems, palette);
        }
    }

    partial void CustomizeGroupColor(ref Color color)
    {
        if (activeTheme is { IsDark: true } theme)
            color = Blend(theme.Surface, color, .16f);
    }

    private static Color Contrast(Color color) => (.2126 * color.R + .7152 * color.G + .0722 * color.B) >= 146
        ? Color.FromArgb(18, 27, 42) : Color.White;

    private static Color Blend(Color first, Color second, float amount) => Color.FromArgb(
        (int)(first.R + (second.R - first.R) * amount),
        (int)(first.G + (second.G - first.G) * amount),
        (int)(first.B + (second.B - first.B) * amount));

    private sealed class ThemeMenuColors(ThemeDefinition theme) : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => theme.Surface;
        public override Color MenuStripGradientEnd => theme.Surface;
        public override Color ToolStripDropDownBackground => theme.Surface;
        public override Color ImageMarginGradientBegin => theme.Surface;
        public override Color ImageMarginGradientMiddle => theme.Surface;
        public override Color ImageMarginGradientEnd => theme.Surface;
        public override Color MenuItemSelected => Blend(theme.Surface, theme.Accent, .24f);
        public override Color MenuItemBorder => Blend(theme.Surface, theme.Accent, .45f);
        public override Color MenuBorder => Blend(theme.Surface, theme.Accent, .45f);
        public override Color MenuItemPressedGradientBegin => theme.Surface;
        public override Color MenuItemPressedGradientEnd => theme.Surface;
    }
}
