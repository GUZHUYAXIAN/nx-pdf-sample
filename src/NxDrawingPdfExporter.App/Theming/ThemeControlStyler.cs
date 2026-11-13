using NxDrawingPdfExporter.App.Controls;

namespace NxDrawingPdfExporter.App.Theming;

internal static class ThemeControlStyler
{
    internal static void Apply(Control root, ThemeDefinition palette, IBackdropSource? backdrop)
    {
        var frame = backdrop?.Frame;
        var materials = frame?.Materials;
        var body = materials?.Body;
        var editor = materials?.Editor;
        var photo = frame is { Backdrop: not null, Materials: not null };
        var referenceColor = frame?.Backdrop?.GetPixel(0, 0) ?? palette.Background;
        var surface = body?.Composite(referenceColor) ?? palette.Surface;
        var foreground = body?.Foreground ?? palette.Foreground;

        void Walk(Control control)
        {
            control.ForeColor = foreground;
            if (control is not PictureBox and not ScrollBar)
                control.BackColor = surface;
            switch (control)
            {
                case SkinDialog dialog: dialog.Backdrop = backdrop; break;
                case SkinSurface panel: panel.Backdrop = backdrop; break;
                case SkinWorkspaceViewport viewport: viewport.Backdrop = backdrop; break;
                case SkinFlowPanel flow: flow.Backdrop = backdrop; break;
                case HeightResizeGrip grip: grip.Backdrop = backdrop; break;
                case SkinTextBox text:
                    text.Backdrop = backdrop;
                    text.BackColor = editor?.Composite(referenceColor) ?? surface;
                    text.ForeColor = editor?.Foreground ?? foreground;
                    break;
                case SkinComboBox combo:
                    combo.Backdrop = backdrop;
                    combo.DrawMode = photo ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
                    combo.ItemHeight = Math.Max(combo.Font.Height + 6, 16);
                    combo.BackColor = editor?.Composite(referenceColor) ?? surface;
                    combo.ForeColor = editor?.Foreground ?? foreground;
                    break;
                case SkinDataGridView grid:
                    grid.BackdropSource = backdrop;
                    break;
            }

            if (control is Label or RadioButton or CheckBox && materials is not null)
            {
                control.BackColor = Color.Transparent;
                if (control.Parent is SkinSurface parent)
                    control.ForeColor = (parent.Header ? materials.Header : materials.Row(parent.Group, SkinRowState.Normal)).Foreground;
                else if (control.Parent is SkinFlowPanel)
                    control.ForeColor = materials.Header.Foreground;
            }

            if (control is Button button)
            {
                if (button is SkinButton skinned)
                    skinned.Backdrop = backdrop;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = ColorContrast.Over(surface, Color.FromArgb(69, foreground));
                button.FlatAppearance.MouseOverBackColor = ColorContrast.Over(surface, Color.FromArgb(51, palette.Accent));
                if (Equals(button.Tag, "Primary") && !photo)
                {
                    button.BackColor = palette.Accent;
                    button.ForeColor = SolidText(palette.Accent);
                }
            }

            foreach (Control child in control.Controls)
                if (child is not ScrollBar and not SkinGridEditor)
                    Walk(child);
        }

        Walk(root);
        root.Invalidate(true);
    }

    internal static Color SolidText(Color background) =>
        ColorContrast.Ratio(Color.Black, background) >= ColorContrast.Ratio(Color.White, background) ? Color.Black : Color.White;
}

internal sealed class SkinMenuRenderer(ThemeDefinition palette, IBackdropSource? backdrop) :
    ToolStripProfessionalRenderer(new SkinMenuColors(palette))
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is MenuStrip && backdrop?.Frame?.Materials is { } materials)
            SkinPainter.Paint(e.ToolStrip, backdrop, e.Graphics, e.AffectedBounds, materials.Header);
        else if (backdrop?.Frame is { Materials: { } popup, Backdrop: { } photo })
        {
            using var brush = new SolidBrush(popup.Header.Composite(photo.GetPixel(0, 0)));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }
        else
            base.OnRenderToolStripBackground(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (backdrop?.Frame is not { Materials: { } materials, Backdrop: { } photo })
        {
            base.OnRenderMenuItemBackground(e);
            return;
        }
        if (!e.Item.Selected && !e.Item.Pressed)
            return;
        var material = materials.Row(null, SkinRowState.Selected);
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        if (e.ToolStrip is MenuStrip)
        {
            var origin = backdrop.Origin(e.ToolStrip);
            origin.Offset(e.Item.Bounds.Location);
            backdrop.PaintBackdrop(e.Graphics, bounds, origin);
            material.Paint(e.Graphics, bounds);
        }
        else
        {
            using var brush = new SolidBrush(material.Composite(photo.GetPixel(0, 0)));
            e.Graphics.FillRectangle(brush, bounds);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (backdrop?.Frame?.Materials is { } materials)
            e.TextColor = e.Item.Selected || e.Item.Pressed
                ? materials.Row(null, SkinRowState.Selected).Foreground : materials.Header.Foreground;
        base.OnRenderItemText(e);
    }

    private sealed class SkinMenuColors(ThemeDefinition theme) : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => theme.Surface;
        public override Color MenuStripGradientEnd => theme.Surface;
        public override Color ToolStripDropDownBackground => theme.Surface;
        public override Color ImageMarginGradientBegin => theme.Surface;
        public override Color ImageMarginGradientMiddle => theme.Surface;
        public override Color ImageMarginGradientEnd => theme.Surface;
        public override Color MenuItemSelected => ColorContrast.Over(theme.Surface, Color.FromArgb(58, theme.Accent));
        public override Color MenuItemBorder => ColorContrast.Over(theme.Surface, Color.FromArgb(114, theme.Accent));
        public override Color MenuBorder => MenuItemBorder;
        public override Color MenuItemPressedGradientBegin => theme.Surface;
        public override Color MenuItemPressedGradientEnd => theme.Surface;
    }
}
