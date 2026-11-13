using System.Runtime.InteropServices;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

// Only the paint path is intercepted. The real WinForms scrollbar retains its
// range, hit testing, mouse capture, line/page navigation and accessibility.
internal sealed class SkinScrollBar : NativeWindow, IDisposable
{
    private readonly ScrollBar control;
    private readonly Func<IBackdropSource?> source;
    private readonly bool vertical;

    internal SkinScrollBar(ScrollBar control, Func<IBackdropSource?> source, bool vertical)
    {
        this.control = control;
        this.source = source;
        this.vertical = vertical;
        control.HandleCreated += Attach;
        control.HandleDestroyed += Detach;
        control.ValueChanged += Invalidate;
        if (control.IsHandleCreated)
            AssignHandle(control.Handle);
    }

    private void Attach(object? sender, EventArgs args) => AssignHandle(control.Handle);
    private void Detach(object? sender, EventArgs args) { if (Handle != 0) ReleaseHandle(); }
    private void Invalidate(object? sender, EventArgs args) => control.Invalidate();

    protected override void WndProc(ref Message message)
    {
        var backdrop = source();
        if (backdrop?.Frame?.Materials is not null && message.Msg == 0x0014)
        {
            message.Result = 1;
            return;
        }
        if (backdrop?.Frame?.Materials is { } materials && message.Msg == 0x000F && ReadInfo(Handle, -4, out var info))
        {
            using var graphics = Graphics.FromHwnd(Handle);
            Paint(graphics, control.ClientRectangle, backdrop.Origin(control), info, vertical, backdrop, materials);
            ValidateRect(Handle, 0);
            message.Result = 0;
            return;
        }
        base.WndProc(ref message);
        if (backdrop?.Frame?.Materials is not null && message.Msg is 0x0200 or 0x0201 or 0x0202 or 0x02A3)
            control.Invalidate();
    }

    internal static void PaintNonClient(ScrollableControl control, IBackdropSource? source)
    {
        if (source?.Frame?.Materials is not { } materials || !control.VerticalScroll.Visible ||
            !ReadInfo(control.Handle, -5, out var info))
            return;
        var screen = control.RectangleToScreen(control.ClientRectangle);
        var bar = new Rectangle(info.Left - screen.Left, info.Top - screen.Top,
            info.Right - info.Left, info.Bottom - info.Top);
        var dc = GetWindowDC(control.Handle);
        if (dc == 0)
            return;
        try
        {
            using var graphics = Graphics.FromHdc(dc);
            Paint(graphics, bar, source.Origin(control), info, true, source, materials);
        }
        finally { ReleaseDC(control.Handle, dc); }
    }

    private static bool ReadInfo(nint handle, int objectId, out ScrollBarInfo info)
    {
        info = new ScrollBarInfo { Size = Marshal.SizeOf<ScrollBarInfo>(), States = new int[6] };
        return handle != 0 && GetScrollBarInfo(handle, objectId, ref info) && (info.States[0] & 0x8000) == 0;
    }

    private static void Paint(Graphics graphics, Rectangle track, Point origin, ScrollBarInfo info, bool vertical,
        IBackdropSource source, SkinMaterials materials)
    {
        if (track.Width <= 0 || track.Height <= 0)
            return;
        using var paint = new BufferedTrack(graphics, track);
        var canvas = paint.Graphics;
        source.PaintBackdrop(canvas, track, origin);
        materials.Header.Paint(canvas, track);
        var extent = vertical ? track.Height : track.Width;
        var button = Math.Min(info.ButtonSize, extent / 2);
        var thumbStart = Math.Clamp(info.ThumbStart, button, extent - button);
        var thumbEnd = Math.Clamp(info.ThumbEnd, thumbStart, extent - button);
        using var brush = new SolidBrush(Color.FromArgb((info.States[3] & 8) != 0 ? 220 : 156, materials.Body.Foreground));
        if (thumbEnd > thumbStart)
        {
            var thumb = vertical
                ? new Rectangle(track.X + 3, track.Y + thumbStart, Math.Max(1, track.Width - 6), thumbEnd - thumbStart)
                : new Rectangle(track.X + thumbStart, track.Y + 3, thumbEnd - thumbStart, Math.Max(1, track.Height - 6));
            canvas.FillRectangle(brush, thumb);
        }
        using var arrows = new SolidBrush(materials.Header.Foreground);
        var first = vertical ? new Point(track.Left + track.Width / 2, track.Top + button / 2)
            : new Point(track.Left + button / 2, track.Top + track.Height / 2);
        var last = vertical ? new Point(first.X, track.Bottom - button / 2)
            : new Point(track.Right - button / 2, first.Y);
        var radius = Math.Max(2, button / 5);
        Arrow(first, -1);
        Arrow(last, 1);

        void Arrow(Point center, int direction)
        {
            Point[] points = vertical
                ? [new(center.X, center.Y + radius * direction), new(center.X - radius, center.Y - radius * direction), new(center.X + radius, center.Y - radius * direction)]
                : [new(center.X + radius * direction, center.Y), new(center.X - radius * direction, center.Y - radius), new(center.X - radius * direction, center.Y + radius)];
            canvas.FillPolygon(arrows, points);
        }
    }

    private sealed class BufferedTrack(Graphics destination, Rectangle bounds) : IDisposable
    {
        private readonly BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(destination, bounds);
        internal Graphics Graphics => buffer.Graphics;
        public void Dispose()
        {
            try { buffer.Render(destination); }
            finally { buffer.Dispose(); }
        }
    }

    public void Dispose()
    {
        control.HandleCreated -= Attach;
        control.HandleDestroyed -= Detach;
        control.ValueChanged -= Invalidate;
        if (Handle != 0)
            ReleaseHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollBarInfo
    {
        internal int Size, Left, Top, Right, Bottom, ButtonSize, ThumbStart, ThumbEnd, Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        internal int[] States;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetScrollBarInfo(nint window, int objectId, ref ScrollBarInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ValidateRect(nint window, nint rect);

    [DllImport("user32.dll")]
    private static extern nint GetWindowDC(nint window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint window, nint dc);
}
