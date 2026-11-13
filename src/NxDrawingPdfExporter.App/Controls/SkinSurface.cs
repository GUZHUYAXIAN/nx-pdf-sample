using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

internal class SkinSurface : TableLayoutPanel
{
    internal IBackdropSource? Backdrop;
    internal GroupVisualKey? Group;
    internal bool Header;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!SkinPainter.PaintSurface(this, Backdrop, e.Graphics, e.ClipRectangle, Group, Header))
            base.OnPaintBackground(e);
    }
}

internal sealed class SkinFlowPanel : FlowLayoutPanel
{
    internal IBackdropSource? Backdrop;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!SkinPainter.PaintSurface(this, Backdrop, e.Graphics, e.ClipRectangle, header: true))
            base.OnPaintBackground(e);
    }
}

internal sealed class SkinWorkspaceViewport : Panel
{
    internal IBackdropSource? Backdrop;

    public SkinWorkspaceViewport() => DoubleBuffered = true;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!SkinPainter.PaintSurface(this, Backdrop, e.Graphics, e.ClipRectangle))
            base.OnPaintBackground(e);
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate(true);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is 0x0085 or 0x00A0 or 0x0115 or 0x00A2)
            SkinScrollBar.PaintNonClient(this, Backdrop);
    }
}

internal static class SkinPainter
{
    internal static bool PaintSurface(Control control, IBackdropSource? source, Graphics graphics, Rectangle clip,
        GroupVisualKey? group = null, bool header = false)
    {
        if (source?.Frame?.Materials is not { } materials)
            return false;
        Paint(control, source, graphics, clip, header ? materials.Header : materials.Row(group, SkinRowState.Normal));
        return true;
    }

    internal static void Paint(Control control, IBackdropSource source, Graphics graphics, Rectangle rectangle, SkinMaterial material)
    {
        source.PaintBackdrop(graphics, rectangle, source.Origin(control));
        material.Paint(graphics, rectangle);
    }
}

internal class SkinDialog : Form
{
    internal IBackdropSource? Backdrop;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!SkinPainter.PaintSurface(this, Backdrop, e.Graphics, e.ClipRectangle))
            base.OnPaintBackground(e);
    }
}
