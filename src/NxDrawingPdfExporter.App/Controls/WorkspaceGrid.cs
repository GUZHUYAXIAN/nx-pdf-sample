using NxDrawingPdfExporter.App.Layout;

namespace NxDrawingPdfExporter.App.Controls;

internal class WorkspaceGrid : DataGridView
{
    internal ScrollExtent VerticalExtent => VerticalScrollBar.Visible && VerticalScrollBar.Enabled
        ? new(VerticalScrollBar.Value - VerticalScrollBar.Minimum,
            Math.Max(0, VerticalScrollBar.Maximum - VerticalScrollBar.Minimum - VerticalScrollBar.LargeChange + 1))
        : new(0, 0);

    internal void ScrollWheel(int delta) => base.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta));
}
