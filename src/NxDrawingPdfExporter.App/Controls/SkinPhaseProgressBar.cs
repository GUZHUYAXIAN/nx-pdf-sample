using System.Drawing.Drawing2D;
using NxDrawingPdfExporter.App.Progress;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

internal sealed class SkinPhaseProgressBar : Control
{
    private PhaseSegments segments = new(0, 0, 1);
    private ProgressVisualPalette palette = new(Color.Gainsboro, Color.RoyalBlue, Color.DarkOrange, Color.DimGray, Color.Black);

    public SkinPhaseProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AccessibleRole = AccessibleRole.ProgressBar;
        TabStop = false;
    }

    internal void SetProgress(PhaseSegments next, string accessibleDescription)
    {
        AccessibleDescription = accessibleDescription;
        if (segments == next)
            return;
        segments = next;
        Invalidate();
    }

    internal void SetPalette(ProgressVisualPalette next)
    {
        palette = next;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(palette.Track);
        var width = Math.Max(0, Width - 2);
        var height = Math.Max(0, Height - 2);
        var complete = (int)Math.Floor(width * segments.Completed);
        var activeEnd = Math.Min(width, (int)Math.Floor(width * (segments.Completed + segments.Active)));
        using var solid = new SolidBrush(palette.Completed);
        e.Graphics.FillRectangle(solid, 1, 1, complete, height);
        using var pattern = new HatchBrush(HatchStyle.ForwardDiagonal,
            ColorContrast.Over(palette.Active, Color.FromArgb(100, palette.Track)), palette.Active);
        e.Graphics.FillRectangle(pattern, complete + 1, 1, activeEnd - complete, height);
        using var outline = new Pen(Color.FromArgb(120, palette.Outline));
        if (Width > 0 && Height > 0)
            e.Graphics.DrawRectangle(outline, 0, 0, Width - 1, Height - 1);
    }
}
