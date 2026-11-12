using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

internal sealed class SkinButton : Button
{
    internal IBackdropSource? Backdrop;

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Backdrop?.Frame?.Materials is not { } materials)
        {
            base.OnPaint(e);
            return;
        }
        var inside = Enabled && ClientRectangle.Contains(PointToClient(Cursor.Position));
        var state = inside && Capture ? SkinRowState.Selected :
            inside ? SkinRowState.Hovered : Enabled && Equals(Tag, "Primary") ? SkinRowState.Selected : SkinRowState.Normal;
        var material = state == SkinRowState.Normal ? materials.Header : materials.Row(Color.Empty, state);
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
    internal IBackdropSource? Backdrop;

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (Backdrop?.Frame?.Materials is not { } materials)
        {
            base.OnDrawItem(e);
            return;
        }
        var edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        var selected = (e.State & DrawItemState.Selected) != 0;
        var material = selected ? materials.Row(Color.Empty, SkinRowState.Selected) : materials.Editor;
        if (edit)
            SkinPainter.Paint(this, Backdrop, e.Graphics, e.Bounds, material);
        else
        {
            var photo = Backdrop.Frame!.Backdrop!;
            using var brush = new SolidBrush(material.Composite(photo.GetPixel(0, 0)));
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
        var text = e.Index >= 0 ? GetItemText(Items[e.Index]) : Text;
        TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, material.Foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0)
            e.DrawFocusRectangle();
    }
}
