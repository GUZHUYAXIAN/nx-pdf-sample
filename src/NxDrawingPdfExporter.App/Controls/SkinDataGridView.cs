using System.Drawing.Drawing2D;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Controls;

internal sealed class SkinDataGridView : WorkspaceGrid
{
    private readonly SkinScrollBar verticalSkin;
    private readonly SkinScrollBar horizontalSkin;
    private int hoverRow = -1;

    internal IBackdropSource? BackdropSource;
    internal Func<int, Color>? GroupTint;
    internal Func<int, SkinTextRole>? RowStatus;

    public SkinDataGridView()
    {
        DoubleBuffered = true;
        verticalSkin = new(VerticalScrollBar, () => BackdropSource, true);
        horizontalSkin = new(HorizontalScrollBar, () => BackdropSource, false);
    }

    internal void RefreshSkin()
    {
        if (EditingControl is SkinGridEditor editor)
        {
            editor.Backdrop = BackdropSource;
            if (BackdropSource?.Frame?.Materials is { } materials)
            {
                editor.ForeColor = materials.Editor.Foreground;
                var sample = (BackdropSource as WindowBackdrop)?.Sample(editor, BackgroundColor) ?? BackgroundColor;
                editor.BackColor = materials.Editor.Composite(sample);
                EditingPanel.BackColor = editor.BackColor;
            }
            else
            {
                editor.ForeColor = DefaultCellStyle.ForeColor;
                editor.BackColor = DefaultCellStyle.BackColor;
                EditingPanel.BackColor = editor.BackColor;
            }
            editor.Invalidate();
        }
        Invalidate(true);
    }

    internal void NameScrollBars(string vertical, string horizontal)
    {
        VerticalScrollBar.AccessibleName = vertical;
        HorizontalScrollBar.AccessibleName = horizontal;
    }

    protected override void OnEditingControlShowing(DataGridViewEditingControlShowingEventArgs e)
    {
        base.OnEditingControlShowing(e);
        if (BackdropSource?.Frame?.Materials is { } materials)
        {
            e.CellStyle.ForeColor = materials.Editor.Foreground;
            var sample = (BackdropSource as WindowBackdrop)?.Sample(e.Control, BackgroundColor) ?? BackgroundColor;
            e.CellStyle.BackColor = materials.Editor.Composite(sample);
        }
        if (e.Control is SkinGridEditor editor)
            editor.Backdrop = BackdropSource;
    }

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if (e.Handled || e.ColumnIndex < 0 || e.Graphics is not { } canvas ||
            e.CellStyle is not { } cellStyle || BackdropSource?.Frame?.Materials is not { } materials)
            return;

        var origin = BackdropSource.Origin(this);
        var selected = (e.State & DataGridViewElementStates.Selected) != 0 && e.RowIndex >= 0;
        var state = selected ? SkinRowState.Selected : e.RowIndex == hoverRow ? SkinRowState.Hovered : SkinRowState.Normal;
        var material = e.RowIndex < 0 ? materials.Header : materials.Row(GroupTint?.Invoke(e.RowIndex) ?? Color.Empty, state);
        var cell = e.CellBounds;
        var paint = canvas.Save();
        try
        {
            canvas.SetClip(Rectangle.Intersect(cell, e.ClipBounds), CombineMode.Intersect);
            BackdropSource.PaintBackdrop(canvas, cell, origin);
            material.Paint(canvas, cell);
            using var separator = new Pen(Color.FromArgb(selected ? 130 : 43, material.Foreground));
            canvas.DrawLine(separator, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);
            if (e.RowIndex < 0)
                canvas.DrawLine(separator, cell.Right - 1, cell.Top, cell.Right - 1, cell.Bottom);
            if (selected && e.ColumnIndex == 0)
                canvas.DrawLine(separator, cell.Left + 2, cell.Top + 2, cell.Left + 2, cell.Bottom - 3);

            var isEditing = IsCurrentCellInEditMode && CurrentCellAddress == new Point(e.ColumnIndex, e.RowIndex);
            var text = Convert.ToString(e.FormattedValue) ?? "";
            if (!isEditing && text.Length > 0)
            {
                var inset = cellStyle.Padding;
                var textBounds = Rectangle.FromLTRB(cell.Left + inset.Left + 2, cell.Top + inset.Top,
                    cell.Right - inset.Right - 2, cell.Bottom - inset.Bottom);
                var color = material.Foreground;
                if (e.RowIndex >= 0 && Columns[e.ColumnIndex].Name == "Status" &&
                    RowStatus?.Invoke(e.RowIndex) is SkinTextRole.Success or SkinTextRole.Warning or SkinTextRole.Error)
                {
                    var role = RowStatus(e.RowIndex);
                    var mark = role == SkinTextRole.Success ? "●" : role == SkinTextRole.Warning ? "▲" : "■";
                    // Keep the group tint across the row. Status text uses a separate verified material.
                    if (!selected)
                    {
                        var status = materials.Status(role);
                        var capsule = Rectangle.Inflate(textBounds, 2, 0);
                        BackdropSource.PaintBackdrop(canvas, capsule, origin);
                        status.Paint(canvas, capsule);
                        color = status.Foreground;
                    }
                    TextRenderer.DrawText(canvas, mark, cellStyle.Font ?? Font,
                        new Rectangle(cell.Left - 1, textBounds.Top, inset.Left + 5, textBounds.Height), color,
                        TextFormatFlags.NoPadding | TextFormatFlags.Top | TextFormatFlags.PreserveGraphicsClipping);
                }
                TextRenderer.DrawText(canvas, text, cellStyle.Font ?? Font, textBounds, color,
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping);
            }
            if (Focused && !isEditing && CurrentCellAddress == new Point(e.ColumnIndex, e.RowIndex))
                ControlPaint.DrawFocusRectangle(canvas, Rectangle.Inflate(cell, -2, -2), material.Foreground, BackgroundColor);
            if (e.RowIndex >= 0 && !string.IsNullOrEmpty(e.ErrorText))
                e.Paint(e.ClipBounds, DataGridViewPaintParts.ErrorIcon);
        }
        finally { canvas.Restore(paint); }
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (BackdropSource?.Frame?.Materials is not { } materials)
            return;
        var area = ClientRectangle;
        if (VerticalScrollBar.Visible) area.Width = Math.Max(0, area.Width - VerticalScrollBar.Width);
        if (HorizontalScrollBar.Visible) area.Height = Math.Max(0, area.Height - HorizontalScrollBar.Height);
        var last = Rows.GetLastRow(DataGridViewElementStates.Displayed);
        var endY = last < 0 ? ColumnHeadersHeight : GetRowDisplayRectangle(last, true).Bottom;
        var endX = Columns.GetColumnsWidth(DataGridViewElementStates.Visible) - HorizontalScrollingOffset;
        var origin = BackdropSource.Origin(this);

        PaintGap(Rectangle.FromLTRB(0, Math.Min(area.Bottom, endY), area.Right, area.Bottom));
        PaintGap(Rectangle.FromLTRB(Math.Clamp(endX, 0, area.Right), 0, area.Right, Math.Min(area.Bottom, endY)));
        if (VerticalScrollBar.Visible && HorizontalScrollBar.Visible)
            PaintGap(Rectangle.FromLTRB(area.Right, area.Bottom, ClientSize.Width, ClientSize.Height));

        void PaintGap(Rectangle gap)
        {
            var clip = Rectangle.Intersect(gap, e.ClipRectangle);
            if (clip.Width <= 0 || clip.Height <= 0)
                return;
            BackdropSource.PaintBackdrop(e.Graphics, clip, origin);
            materials.Body.Paint(e.Graphics, clip);
        }
    }

    protected override void OnScroll(ScrollEventArgs e)
    {
        base.OnScroll(e);
        if (BackdropSource?.Frame?.Materials is not null)
            Invalidate(true);
    }

    protected override void OnCellMouseEnter(DataGridViewCellEventArgs e)
    {
        base.OnCellMouseEnter(e);
        SetHoverRow(e.RowIndex);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoverRow(-1);
    }

    private void SetHoverRow(int row)
    {
        if (hoverRow == row)
            return;
        var previous = hoverRow;
        hoverRow = row;
        if (BackdropSource?.Frame?.Materials is null)
            return;
        if (previous >= 0 && previous < RowCount) InvalidateRow(previous);
        if (row >= 0 && row < RowCount) InvalidateRow(row);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            verticalSkin?.Dispose();
            horizontalSkin?.Dispose();
        }
        base.Dispose(disposing);
    }
}
