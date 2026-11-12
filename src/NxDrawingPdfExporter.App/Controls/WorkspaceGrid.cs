using NxDrawingPdfExporter.App.Layout;

namespace NxDrawingPdfExporter.App.Controls;

internal class WorkspaceGrid : DataGridView
{
    internal ScrollExtent VerticalExtent
    {
        get
        {
            if (!VerticalScrollBar.Visible || !VerticalScrollBar.Enabled)
                return new(0, 0);
            var offset = VerticalScrollBar.Value - VerticalScrollBar.Minimum;
            var first = FirstDisplayedScrollingRowIndex;
            var last = Rows.GetLastRow(DataGridViewElementStates.Visible);
            var bottom = ClientSize.Height - (HorizontalScrollBar.Visible ? HorizontalScrollBar.Height : 0);
            var lastBounds = last < 0 ? Rectangle.Empty : GetRowDisplayRectangle(last, false);
            // The native vertical bar can stop on a complete trailing row before its
            // numerical maximum. Check the actual displayed rows at that boundary.
            var canAdvance = first >= 0 && first < last && (lastBounds.IsEmpty || lastBounds.Bottom > bottom);
            return new(offset, canAdvance ? int.MaxValue : offset);
        }
    }

    internal void ScrollWheel(int delta)
    {
        var dirty = IsCurrentCellDirty;
        var cell = CurrentCell;
        base.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, delta));
        if (dirty && IsCurrentCellInEditMode && ReferenceEquals(CurrentCell, cell))
            NotifyCurrentCellDirty(true);
    }
}
