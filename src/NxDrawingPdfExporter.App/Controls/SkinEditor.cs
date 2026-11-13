using System.Runtime.InteropServices;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

internal sealed class SkinTextBox : TextBox
{
    internal IBackdropSource? Backdrop;
    private readonly SkinEditBrush brush = new();

    protected override void WndProc(ref Message m)
    {
        if (!brush.TryPaint(this, Backdrop, ref m))
            base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            brush.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class SkinEditCell : DataGridViewTextBoxCell
{
    public SkinEditCell() { }
    public override Type EditType => typeof(SkinGridEditor);
}

internal sealed class SkinGridEditor : DataGridViewTextBoxEditingControl
{
    public SkinGridEditor() { }
    internal IBackdropSource? Backdrop;

    public override object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
    {
        // WinForms internally requests Commit | Scroll while it moves the viewport.
        // Give that operation the committed cell value, keeping the live editor draft.
        if ((context & DataGridViewDataErrorContexts.Scroll) != 0 && EditingControlDataGridView?.CurrentCell is { } cell)
            return Convert.ToString(cell.Value) ?? "";
        return base.GetEditingControlFormattedValue(context);
    }
    private readonly SkinEditBrush brush = new();

    protected override void WndProc(ref Message m)
    {
        if (!brush.TryPaint(this, Backdrop, ref m))
            base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            brush.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class SkinEditBrush : IDisposable
{
    private nint pattern;
    private (ThemeFrame Frame, Point Origin, Size Size) previous;

    internal bool TryPaint(Control editor, IBackdropSource? source, ref Message message)
    {
        if (message.Msg is not (0x2133 or 0x2138))
            return false;
        if (source?.Frame is not { Materials: { } materials } frame)
        {
            Dispose();
            return false;
        }
        var size = editor.ClientSize;
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * size.Height > 2_000_000)
            return false;
        var origin = source.Origin(editor);
        var key = (frame, origin, size);
        if (pattern == 0 || previous != key)
        {
            Dispose();
            using var bitmap = new Bitmap(size.Width, size.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(frame.Request.Theme.Background);
                source.PaintBackdrop(graphics, editor.ClientRectangle, origin);
                materials.Editor.Paint(graphics, editor.ClientRectangle);
            }
            var handle = bitmap.GetHbitmap();
            try { pattern = CreatePatternBrush(handle); }
            finally { DeleteObject(handle); }
            previous = key;
        }
        if (pattern == 0)
            return false;
        SetTextColor(message.WParam, ColorTranslator.ToWin32(materials.Editor.Foreground));
        SetBkColor(message.WParam, ColorTranslator.ToWin32(editor.BackColor));
        SetBkMode(message.WParam, 1);
        SetBrushOrgEx(message.WParam, 0, 0, 0);
        message.Result = pattern;
        return true;
    }

    public void Dispose()
    {
        if (pattern == 0)
            return;
        DeleteObject(pattern);
        pattern = 0;
        previous = default;
    }

    [DllImport("gdi32.dll")] private static extern nint CreatePatternBrush(nint bitmap);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("gdi32.dll")] private static extern int SetTextColor(nint dc, int color);
    [DllImport("gdi32.dll")] private static extern int SetBkColor(nint dc, int color);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(nint dc, int mode);
    [DllImport("gdi32.dll")] private static extern bool SetBrushOrgEx(nint dc, int x, int y, nint previous);
}
