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
            // The native vertical bar can stop before its numerical maximum
            // when it already displays the final complete row.
            var canAdvance = first >= 0 && first < last && (lastBounds.IsEmpty || lastBounds.Bottom > bottom);
            return new(offset, canAdvance ? int.MaxValue : offset);
        }
    }

    internal int MinimumHeightForWrappedRows(int standardRowHeight)
    {
        var tallest = standardRowHeight;
        foreach (DataGridViewRow row in Rows)
            if (row.Visible)
                tallest = Math.Max(tallest, row.Height);
        // The native grid has no vertical bar for a single oversized row.
        // Expose the complete row to the workspace viewport in that case.
        var border = BorderStyle == BorderStyle.None ? 0 : 2;
        var horizontal = HorizontalScrollBar.Visible ? HorizontalScrollBar.Height : 0;
        return (ColumnHeadersVisible ? ColumnHeadersHeight : 0) + tallest + horizontal + border;
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
