using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

internal sealed class SkinButton : Button
{
    internal IBackdropSource? Backdrop;

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Backdrop?.Frame is not { Backdrop: not null, Materials: { } materials })
        {
            base.OnPaint(e);
            return;
        }
        var inside = Enabled && ClientRectangle.Contains(PointToClient(Cursor.Position));
        var state = inside && Capture ? SkinRowState.Selected :
            inside ? SkinRowState.Hovered : Enabled && Equals(Tag, "Primary") ? SkinRowState.Selected : SkinRowState.Normal;
        var material = state == SkinRowState.Normal ? materials.Header : materials.Row(null, state);
        SkinPainter.Paint(this, Backdrop, e.Graphics, ClientRectangle, material);
        using var border = new Pen(Color.FromArgb(!Enabled ? 46 : Equals(Tag, "Primary") ? 170 : 92, material.Foreground));
        var bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        e.Graphics.DrawRectangle(border, bounds);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, material.Foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -3, -3), material.Foreground, BackColor);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); Invalidate(); }
}

internal sealed class SkinComboBox : ComboBox
{
    internal void ApplyBackdrop(IBackdropSource? source)
    {
        Backdrop = source;
        DrawMode = source?.Frame is { Backdrop: not null, Materials: not null } ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
        UpdateItemHeight();
    }

    private IBackdropSource? Backdrop { get; set; }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateItemHeight();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateItemHeight();
    }

    private void UpdateItemHeight()
    {
        if (DrawMode == DrawMode.OwnerDrawFixed)
            ItemHeight = Font.Height + Math.Max(1, 6 * DeviceDpi / 96);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var foreground = e.ForeColor;
        if (Backdrop?.Frame is { Backdrop: { } photo, Materials: { } materials })
        {
            var selected = (e.State & DrawItemState.Selected) != 0;
            var material = selected ? materials.Row(null, SkinRowState.Selected) : materials.Editor;
            foreground = material.Foreground;
            if ((e.State & DrawItemState.ComboBoxEdit) != 0)
                SkinPainter.Paint(this, Backdrop, e.Graphics, e.Bounds, material);
            else
            {
                using var brush = new SolidBrush(material.Composite(photo.GetPixel(0, 0)));
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
        }
        else
            e.DrawBackground();

        var text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
        TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }
}
