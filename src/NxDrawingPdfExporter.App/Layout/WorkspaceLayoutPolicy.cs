namespace NxDrawingPdfExporter.App.Layout;

public sealed record WorkspaceHeights(int PreflightDip = 280, int ResultsDip = 320)
{
    public WorkspaceHeights Normalize() => new(Clamp(PreflightDip), Clamp(ResultsDip));
    internal static int Clamp(int height) => Math.Clamp(height, 120, 2000);
}

internal readonly record struct ScrollExtent(int Offset, int MaximumOffset)
{
    public bool CanScroll(int direction) => MaximumOffset > 0 && (direction < 0 ? Offset > 0 : Offset < MaximumOffset);
}

internal enum WheelOwner { None, Local, Workspace }

internal static class WorkspaceLayoutPolicy
{
    internal static WheelOwner RouteWheel(bool overList, ScrollExtent local, ScrollExtent workspace, int direction) =>
        overList && local.CanScroll(direction) ? WheelOwner.Local :
        workspace.CanScroll(direction) ? WheelOwner.Workspace : WheelOwner.None;

    internal static int ResizeHeightDip(int startDip, int pointerDeltaPixels, int dpi, bool topHandle) =>
        WorkspaceHeights.Clamp(startDip + (int)Math.Round(pointerDeltaPixels * 96d / dpi) * (topHandle ? -1 : 1));

    internal static bool RowsOverflow(int available, int header, IEnumerable<int> heights, int horizontalBar) =>
        header + heights.Sum(height => (long)height) + horizontalBar > available;
}

internal sealed class WheelAccumulator
{
    private int remainder;
    private WheelOwner previousOwner;
    private nint previousTarget;

    internal int Add(int delta, WheelOwner owner, nint target)
    {
        if (owner != previousOwner || target != previousTarget || Math.Sign(delta) != Math.Sign(remainder))
            remainder = 0;
        previousOwner = owner;
        previousTarget = target;
        remainder += delta;
        var steps = remainder / 120;
        remainder %= 120;
        return steps;
    }
}
