using System.Runtime.InteropServices;
using NxDrawingPdfExporter.App.Controls;

namespace NxDrawingPdfExporter.App.Layout;

internal sealed class WorkspaceScrollCoordinator : IMessageFilter, IDisposable
{
    private readonly Form root;
    private readonly ScrollableControl viewport;
    private readonly IReadOnlyList<WorkspaceGrid> grids;
    private readonly WheelAccumulator wheel = new();

    internal WorkspaceScrollCoordinator(Form root, ScrollableControl viewport, IReadOnlyList<WorkspaceGrid> grids)
    {
        this.root = root;
        this.viewport = viewport;
        this.grids = grids;
        Application.AddMessageFilter(this);
    }

    public bool PreFilterMessage(ref Message message)
    {
        const int mouseWheel = 0x020A;
        if (message.Msg != mouseWheel || root.IsDisposed || Form.ActiveForm != root
            || Control.ModifierKeys != Keys.None || Control.MouseButtons != MouseButtons.None)
            return false;

        var packed = message.LParam.ToInt64();
        var pointer = new Point(unchecked((short)packed), unchecked((short)(packed >> 16)));
        if (!root.RectangleToScreen(root.ClientRectangle).Contains(pointer))
            return false;

        var hovered = Control.FromChildHandle(WindowFromPoint(pointer));
        if (OwnsGesture(hovered) || OwnsGesture(Control.FromChildHandle(message.HWnd)))
            return false;
        var grid = viewport.RectangleToScreen(viewport.ClientRectangle).Contains(pointer)
            ? grids.FirstOrDefault(item => item.Visible && item.Enabled && item.RectangleToScreen(item.ClientRectangle).Contains(pointer))
            : null;

        var delta = unchecked((short)(message.WParam.ToInt64() >> 16));
        if (delta == 0)
            return true;
        var owner = WorkspaceLayoutPolicy.RouteWheel(grid is not null, grid?.VerticalExtent ?? default,
            new(-viewport.AutoScrollPosition.Y, Math.Max(0, viewport.DisplayRectangle.Height - viewport.ClientSize.Height)),
            delta < 0 ? 1 : -1);
        var ticks = wheel.Add(delta, owner, owner == WheelOwner.Local ? grid!.Handle : viewport.Handle);
        if (ticks == 0)
            return true;

        if (owner == WheelOwner.Local)
            grid!.ScrollWheel(ticks * 120);
        else if (owner == WheelOwner.Workspace)
        {
            var lines = SystemInformation.MouseWheelScrollLines;
            var distance = lines < 0 ? viewport.ClientSize.Height : lines * Math.Max(1, root.Font.Height);
            viewport.AutoScrollPosition = new Point(-viewport.AutoScrollPosition.X,
                Math.Max(0, -viewport.AutoScrollPosition.Y - ticks * distance));
            viewport.Invalidate(true);
        }
        return true;
    }

    private static bool OwnsGesture(Control? control)
    {
        for (var item = control; item is not null; item = item.Parent)
            if (item is ComboBox { DroppedDown: true } or TrackBar { Capture: true } or ToolStripDropDown)
                return true;
        return false;
    }

    public void Dispose() => Application.RemoveMessageFilter(this);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);
}
