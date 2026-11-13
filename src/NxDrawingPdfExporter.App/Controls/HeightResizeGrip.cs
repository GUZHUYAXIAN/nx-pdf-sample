using NxDrawingPdfExporter.App.Layout;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

internal sealed class HeightResizeGrip(bool topHandle) : Control
{
    private int startHeight;
    private int startY;
    private bool dragging;
    internal IBackdropSource? Backdrop;

    internal Func<int> CurrentHeightDip = () => 280;
    internal event Action<int>? HeightRequested;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        Focus();
        startHeight = CurrentHeightDip();
        startY = PointToScreen(e.Location).Y;
        dragging = true;
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging)
            HeightRequested?.Invoke(WorkspaceLayoutPolicy.ResizeHeightDip(startHeight,
                PointToScreen(e.Location).Y - startY, DeviceDpi, topHandle));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
            Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        dragging = false;
        Invalidate();
        base.OnMouseCaptureChanged(e);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Up or Keys.Down))
            return;
        var delta = (e.KeyCode == Keys.Up ? -1 : 1) * (e.Shift ? 40 : 12);
        HeightRequested?.Invoke(WorkspaceLayoutPolicy.ResizeHeightDip(CurrentHeightDip(), delta, 96, topHandle));
        e.SuppressKeyPress = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!SkinPainter.PaintSurface(this, Backdrop, e.Graphics, e.ClipRectangle, header: true))
            base.OnPaintBackground(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(ForeColor, Math.Max(1f, DeviceDpi / 96f));
        var width = Math.Min(40 * DeviceDpi / 96, Width);
        var start = (Width - width) / 2;
        var center = Height / 2;
        e.Graphics.DrawLine(pen, start, center - 2, start + width, center - 2);
        e.Graphics.DrawLine(pen, start, center + 2, start + width, center + 2);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2), ForeColor, BackColor);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
