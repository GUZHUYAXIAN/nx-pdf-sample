using System.Drawing.Drawing2D;

namespace NxDrawingPdfExporter.App.Theming;

internal static class BackdropMapping
{
    internal static RectangleF SourceRegion(Rectangle rootClient, Size bitmapSize, Rectangle destinationInRoot)
    {
        if (rootClient.Width <= 0 || rootClient.Height <= 0)
            return RectangleF.Empty;
        var x = bitmapSize.Width / (float)rootClient.Width;
        var y = bitmapSize.Height / (float)rootClient.Height;
        return new((destinationInRoot.X - rootClient.X) * x, (destinationInRoot.Y - rootClient.Y) * y,
            destinationInRoot.Width * x, destinationInRoot.Height * y);
    }
}

internal interface IBackdropSource
{
    ThemeFrame? Frame { get; }
    Point Origin(Control control);
    void PaintBackdrop(Graphics graphics, Rectangle localBounds, Point originInRoot);
}

internal sealed class WindowBackdrop(Control root, Func<ThemeFrame?> current) : IBackdropSource
{
    public ThemeFrame? Frame => current();
    public Point Origin(Control control) => root.PointToClient(control.PointToScreen(Point.Empty));

    public void PaintBackdrop(Graphics graphics, Rectangle localBounds, Point originInRoot)
    {
        if (Frame?.Backdrop is not { } bitmap || localBounds.Width <= 0 || localBounds.Height <= 0)
            return;
        var state = graphics.Save();
        try
        {
            graphics.SetClip(localBounds, CombineMode.Intersect);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            var bounds = root.ClientRectangle;
            bounds.Offset(-originInRoot.X, -originInRoot.Y);
            // Clip the same full-window projection at every control boundary, including partially scrolled rows.
            graphics.DrawImage(bitmap, bounds, new Rectangle(Point.Empty, bitmap.Size), GraphicsUnit.Pixel);
        }
        finally { graphics.Restore(state); }
    }

    internal Color Sample(Control control, Color fallback)
    {
        if (Frame?.Backdrop is not { } bitmap)
            return fallback;
        var center = Origin(control);
        center.Offset(control.Width / 2, control.Height / 2);
        var point = BackdropMapping.SourceRegion(root.ClientRectangle, bitmap.Size, new Rectangle(center, Size.Empty));
        return bitmap.GetPixel(Math.Clamp((int)point.X, 0, bitmap.Width - 1), Math.Clamp((int)point.Y, 0, bitmap.Height - 1));
    }
}
