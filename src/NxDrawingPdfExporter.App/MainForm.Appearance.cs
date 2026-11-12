using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Dialogs;
using NxDrawingPdfExporter.App.Controls;
using NxDrawingPdfExporter.App.Theming;
using NxDrawingPdfExporter.Core.Input;

namespace NxDrawingPdfExporter.App;

public sealed partial class MainForm
{
    private IReadOnlyList<ThemeDefinition> themes = [];
    private readonly ImportedImageStore importedImages = new(Path.Combine(JsonUiSettingsStore.LocalRoot, "theme-images"));
    private ThemeRenderCoordinator? themeRenderer;
    private WindowBackdrop? backdrop;
    private ThemeFrame? frame;
    private ThemeFrame? prePreviewFrame;
    private (ThemeDefinition Theme, ThemeParameters Effects, string? Path) appearance;
    private long themeRevision;
    private bool themeWarningShown;
    private bool previousHighContrast;
    private AppearanceDialog? appearanceDialog;
    private WindowBackdrop? dialogBackdrop;

    partial void InitializeAppearance()
    {
        themes = ThemeCatalog.Load(AppContext.BaseDirectory);
        backdrop = new(this, () => frame);
        themeRenderer = new(ThemeFrame.Render, AcceptFrame, OnThemeFailure, TimeSpan.FromMilliseconds(100),
            deliver => BeginInvoke(deliver));
        previousHighContrast = SystemInformation.HighContrast;
        var theme = themes.First(definition => definition.Id == "cloud");
        frame = new(new(0, null, new Size(1, 1), theme, theme.Defaults, previousHighContrast), null, null);
        BindSkinControls();
        RefreshThemeColors();
        ClientSizeChanged += (_, _) => RequestThemeFrame();
        DpiChanged += (_, _) => RequestThemeFrame();
        SystemColorsChanged += (_, _) =>
        {
            var highContrast = SystemInformation.HighContrast;
            if (previousHighContrast != highContrast)
            {
                previousHighContrast = highContrast;
                RequestThemeFrame();
            }
            else
                RefreshThemeColors();
        };
        Shown += (_, _) => ApplySavedAppearance();
        Disposed += (_, _) =>
        {
            themeRenderer.Dispose();
            frame?.Dispose();
            prePreviewFrame?.Dispose();
            hints.Dispose();
        };
    }

    private void ApplySavedAppearance() =>
        ApplyAppearance(settings.ThemeId, settings.ThemeParametersById.GetValueOrDefault(settings.ThemeId), settings.ImportedImage);

    partial void OpenAppearance()
    {
        var before = appearance;
        prePreviewFrame = frame;
        using var dialog = new AppearanceDialog(Ui, settings, themes, importedImages,
            (id, effects, image) => ApplyAppearance(id, effects, image),
            error => BeginInvoke(() => Notify("SettingsError", details: error.Message))) { Font = Font };
        appearanceDialog = dialog;
        dialogBackdrop = new(dialog, () => frame);
        ThemeControlStyler.Apply(dialog, CurrentPalette(), dialogBackdrop);
        if (frame is not null)
            dialog.FrameReady(frame);
        var result = dialog.ShowDialog(this);
        appearanceDialog = null;
        dialogBackdrop = null;
        if (result == DialogResult.OK)
        {
            settings = dialog.AppliedSettings;
            SaveSettings();
            if (!ReferenceEquals(prePreviewFrame, frame))
                prePreviewFrame?.Dispose();
            prePreviewFrame = null;
            ApplySavedAppearance();
        }
        else
        {
            themeRenderer?.Cancel();
            appearance = before;
            var restore = prePreviewFrame;
            prePreviewFrame = null;
            if (restore is not null && restore.Request.HighContrast == SystemInformation.HighContrast)
                AcceptFrame(restore);
            else
                restore?.Dispose();
            RequestThemeFrame();
        }
    }

    private void ApplyAppearance(string id, ThemeParameters? effects, string? reference)
    {
        var theme = themes.FirstOrDefault(theme => theme.Id == id) ?? themes[0];
        var path = id == "imported" ? importedImages.Resolve(reference) : theme.ImagePath;
        if (!theme.HasAssets || (theme.IsImageTheme && path is null))
        {
            themeRenderer?.Cancel();
            if (frame is { } current)
                appearance = (current.Request.Theme, current.Request.Effects, current.Request.ImagePath);
            if (prePreviewFrame is null)
                WarnThemeUnavailable();
            return;
        }
        appearance = (theme, (effects ?? theme.Defaults).Normalize(), path);
        RequestThemeFrame();
    }

    private void RequestThemeFrame()
    {
        if (themeRenderer is null || appearance.Theme is null || !IsHandleCreated || IsDisposed)
            return;
        var (theme, effects, path) = appearance;
        var highContrast = SystemInformation.HighContrast;
        var size = BoundedSize(ClientSize);
        var previous = frame?.Request;
        var sameAppearance = previous is not null && previous.Theme == theme && previous.Effects == effects
            && previous.ImagePath == path && previous.HighContrast == highContrast;
        if (sameAppearance && previous!.PixelSize == size)
        {
            themeRenderer.Cancel();
            if (frame is { } current)
                appearanceDialog?.FrameReady(current);
            return;
        }
        var request = new ThemeRenderRequest(++themeRevision, path, size, theme, effects, highContrast,
            sameAppearance ? frame?.Palette : null);
        if (highContrast || path is null || effects.ImageVisibility == 0)
        {
            themeRenderer.Cancel();
            AcceptFrame(new(request, null, null));
        }
        else
            _ = themeRenderer.Request(request);
    }

    private void AcceptFrame(ThemeFrame next)
    {
        var previous = frame;
        frame = next;
        if (!ReferenceEquals(previous, prePreviewFrame) && !ReferenceEquals(previous, next))
            previous?.Dispose();
        RefreshThemeColors();
        appearanceDialog?.FrameReady(next);
    }

    private void OnThemeFailure(Exception error)
    {
        System.Diagnostics.Debug.WriteLine(error);
        if (frame is { } current)
            appearance = (current.Request.Theme, current.Request.Effects, current.Request.ImagePath);
        if (appearanceDialog is { IsDisposed: false } dialog)
            dialog.FrameFailed(error);
        else
            WarnThemeUnavailable();
    }

    private void WarnThemeUnavailable()
    {
        if (themeWarningShown || IsDisposed || !IsHandleCreated)
            return;
        themeWarningShown = true;
        BeginInvoke(() => Notify("ThemeUnavailable"));
    }

    private static Size BoundedSize(Size size)
    {
        var scale = Math.Min(1d, 1800d / Math.Max(1, Math.Max(size.Width, size.Height)));
        return new Size(Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale)));
    }

    protected override void OnPaintBackground(PaintEventArgs args)
    {
        base.OnPaintBackground(args);
        if (!SystemInformation.HighContrast)
            backdrop?.PaintBackdrop(args.Graphics, args.ClipRectangle, Point.Empty);
    }

    private ThemeDefinition CurrentPalette()
    {
        var theme = frame?.Request.Theme ?? themes[0];
        return SystemInformation.HighContrast
            ? theme with { Background = SystemColors.Window, Surface = SystemColors.Window,
                Foreground = SystemColors.WindowText, Accent = SystemColors.Highlight }
            : theme;
    }

    private void BindSkinControls()
    {
        hints.OwnerDraw = true;
        hints.Draw += (_, e) =>
        {
            using var background = new SolidBrush(hints.BackColor);
            using var border = new Pen(hints.ForeColor);
            e.Graphics.FillRectangle(background, e.Bounds);
            e.Graphics.DrawRectangle(border, Rectangle.Inflate(e.Bounds, -1, -1));
            e.DrawText(TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
        foreach (var grid in new[] { preflight, results, manualGrid })
        {
            grid.GroupTint = row => grid.Rows[row].Tag is GridRow data ? GroupTint(data.Group) : Color.Empty;
            grid.RowStatus = row => grid.Rows[row].Tag is GridRow data ? data.Role : SkinTextRole.Body;
            grid.CellMouseEnter += (_, e) =>
            {
                var cell = e.RowIndex >= 0 && e.ColumnIndex >= 0 ? grid[e.ColumnIndex, e.RowIndex] : null;
                var text = cell is null ? "" : string.IsNullOrEmpty(cell.ErrorText) ? cell.ToolTipText : cell.ErrorText;
                hints.SetToolTip(grid, text);
            };
            grid.MouseLeave += (_, _) => hints.SetToolTip(grid, "");
        }
    }

    partial void RefreshThemeColors()
    {
        if (themes.Count == 0 || frame is null)
            return;
        var palette = CurrentPalette();
        var materials = frame.Materials;
        var sample = frame.Backdrop?.GetPixel(0, 0) ?? palette.Background;
        BackColor = palette.Background;
        ThemeControlStyler.Apply(shell, palette, backdrop);
        shell.BackColor = palette.Background;
        menu.Renderer = SystemInformation.HighContrast ? new ToolStripSystemRenderer()
            : new SkinMenuRenderer(palette, backdrop) { RoundedEdges = false };
        ColorMenu(menu.Items, materials?.Header.Foreground ?? palette.Foreground);
        hints.BackColor = materials?.Header.Composite(sample) ?? palette.Surface;
        hints.ForeColor = materials?.Header.Foreground ?? palette.Foreground;
        foreach (var grid in new[] { preflight, results, manualGrid })
        {
            grid.BackgroundColor = materials?.Body.Composite(sample) ?? palette.Surface;
            grid.GridColor = ColorContrast.Over(palette.Surface, Color.FromArgb(38, palette.Foreground));
            grid.DefaultCellStyle.BackColor = grid.BackgroundColor;
            grid.DefaultCellStyle.ForeColor = materials?.Body.Foreground ?? palette.Foreground;
            grid.DefaultCellStyle.SelectionBackColor = palette.Accent;
            grid.DefaultCellStyle.SelectionForeColor = SystemInformation.HighContrast ? SystemColors.HighlightText
                : ThemeControlStyler.SolidText(palette.Accent);
            grid.ColumnHeadersDefaultCellStyle.BackColor = materials?.Header.Composite(sample)
                ?? ColorContrast.Over(palette.Surface, Color.FromArgb(23, palette.Foreground));
            grid.ColumnHeadersDefaultCellStyle.ForeColor = materials?.Header.Foreground ?? palette.Foreground;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
            foreach (DataGridViewRow row in grid.Rows)
                row.DefaultCellStyle.BackColor = materials is null && row.Tag is GridRow data ? GroupColor(data.Group) : Color.Empty;
            grid.RefreshSkin();
        }
        if (materials is null)
        {
            foreach (Control row in outputRows.Controls)
                if (row.Tag is SourceDirectoryGroup group)
                {
                    var color = GroupColor(group.Label);
                    row.BackColor = color.IsEmpty ? palette.Surface : color;
                    foreach (Control child in row.Controls)
                        if (child is Label)
                            child.BackColor = row.BackColor;
                }
        }
        if (appearanceDialog is { IsDisposed: false } dialog)
            ThemeControlStyler.Apply(dialog, palette, dialogBackdrop);
        Invalidate(true);
    }

    private static void ColorMenu(ToolStripItemCollection items, Color foreground)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = Color.Empty;
            item.ForeColor = foreground;
            if (item is ToolStripDropDownItem dropdown)
                ColorMenu(dropdown.DropDownItems, foreground);
        }
    }

    private DialogResult ShowSkinDialog(SkinDialog dialog, IWin32Window owner)
    {
        ThemeControlStyler.Apply(dialog, CurrentPalette(), new WindowBackdrop(dialog, () => frame));
        return dialog.ShowDialog(owner);
    }

    private void ShowNotice(IWin32Window owner, string title, string message)
    {
        using var notice = new SkinDialog
        {
            Text = title, Font = Font, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(450, 190), Padding = new Padding(12),
            AutoScaleMode = AutoScaleMode.Dpi
        };
        var text = new SkinTextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None, Text = message
        };
        var controls = Flow();
        controls.Dock = DockStyle.Bottom;
        controls.FlowDirection = FlowDirection.RightToLeft;
        var closeNotice = new SkinButton { Text = Ui.Get("Close"), AutoSize = true, DialogResult = DialogResult.OK };
        controls.Controls.Add(closeNotice);
        notice.Controls.Add(text);
        notice.Controls.Add(controls);
        notice.AcceptButton = closeNotice;
        notice.CancelButton = closeNotice;
        ShowSkinDialog(notice, owner);
    }

    partial void CustomizeGroupColor(ref Color color)
    {
        if (frame?.Request.Theme is { IsDark: true } theme)
            color = ColorContrast.Over(theme.Surface, Color.FromArgb(41, color));
    }
}
