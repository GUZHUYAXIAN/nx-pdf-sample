using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Input;
using NxDrawingPdfExporter.Core.Output;

namespace NxDrawingPdfExporter.App;

public sealed partial class MainForm
{
    private sealed record GridRow(string Source, string Target, string Group, string Status, string Reason,
        string Technical = "", PlannedOutputItem? Plan = null);

    private IReadOnlyDictionary<string, (int Ordinal, string Group)> runRows =
        new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase);
    private bool pendingRevisionRender;

    private void SetInputMode(GuiInputMode mode)
    {
        if (controller.IsBusy)
            return;
        controller.InputMode = mode;
        manualList.Select(mode);
        ApplyManualState();
        UpdateInputs();
    }

    private void ToggleManualList()
    {
        manualList.Toggle();
        ApplyManualState();
    }

    private void ApplyManualState()
    {
        manualGrid.Visible = manualList.Expanded;
        toggleManual.Text = (manualList.Expanded ? "▾ " : "▸ ") + Ui.Get("ManualList");
        addManual.Visible = removeManual.Visible = manualList.CanEdit;
        scanLine.Visible = recursive.Visible = !manualList.CanEdit;
        RenderManual();
    }

    private void AddManual()
    {
        if (controller.IsBusy || !CommitRevisionEdit())
            return;
        using var picker = new OpenFileDialog { Multiselect = true, Filter = "PRT (*.prt)|*.prt" };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        controller.ManualPaths = controller.ManualPaths.Concat(picker.FileNames)
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!manualMode.Checked)
            manualMode.Checked = true;
        else
        {
            if (!manualList.Expanded)
                manualList.Toggle();
            ApplyManualState();
            UpdateInputs();
        }
    }

    private void RemoveManual()
    {
        if (!manualList.CanEdit || controller.IsBusy)
            return;
        var removed = SelectedRows(manualGrid).Select(row => row.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        controller.ManualPaths = controller.ManualPaths.Where(path => !removed.Contains(path)).ToArray();
        UpdateInputs();
    }

    private void BrowseScan()
    {
        if (controller.IsBusy || !CommitRevisionEdit())
            return;
        if (!TryBrowse(scanPath.Text, out var path))
            return;
        scanPath.Text = path;
        scanMode.Checked = true;
        UpdateInputs();
    }

    private bool TryBrowse(string initial, out string path)
    {
        using var picker = new FolderBrowserDialog();
        if (Directory.Exists(initial))
            picker.SelectedPath = initial;
        var accepted = picker.ShowDialog(this) == DialogResult.OK;
        path = picker.SelectedPath;
        return accepted;
    }

    private void SetOutputMode(OutputMode mode)
    {
        controller.OutputMode = mode;
        UpdateInputs();
    }

    private void SetPolicy(ExistingPdfPolicy policy)
    {
        controller.ExistingPdfPolicy = policy;
        UpdateInputs();
    }

    private void InvalidatePlan()
    {
        controller.Session.Invalidate();
        UpdateDynamicState();
        UpdateVisibleCount(preflight.RowCount, controller.Session.CurrentPlan?.Items.Count ?? 0);
    }

    private void UpdateInputs()
    {
        if (controller.IsBusy)
            return;
        try
        {
            controller.UpdatePreflightInputs();
            RebuildOutputRows();
            RenderManual();
            RenderPlan();
            UpdateDynamicState();
        }
        catch (Exception error) { Notify("PreflightError", details: error.Message); }
    }

    private void RebuildOutputRows()
    {
        outputRows.SuspendLayout();
        while (outputRows.Controls.Count != 0)
            outputRows.Controls[0].Dispose();
        if (unifiedMode.Checked)
        {
            AddOutputRow(null, controller.UnifiedOutputDirectory);
        }
        else if (controller.Session.Groups.Count == 0)
        {
            outputRows.Controls.Add(new Label { Text = Ui.Get("EmptyGroups"), AutoSize = true, Padding = new Padding(4) });
        }
        else
        {
            foreach (var group in controller.Session.Groups)
                AddOutputRow(group, controller.GroupTargets.GetValueOrDefault(group.Key) ?? group.Key);
        }
        outputRows.ResumeLayout();
        RefreshThemeColors();
    }

    private void AddOutputRow(SourceDirectoryGroup? group, string current)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4,
            Margin = new Padding(1, 2, 1, 2), Padding = new Padding(4), Tag = group };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (group is null ? 135 : 175) * DeviceDpi / 96));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var caption = new Label { AutoSize = true, Margin = new Padding(4, 6, 2, 0) };
        void UpdateCaption() => caption.Text = group is null ? Ui.Get("Unified") :
            $"{group.Label} · {new DirectoryInfo(group.Key).Name} ({group.SourcePaths.Count})"
            + (controller.GroupTargets.ContainsKey(group.Key) ? " · " + Ui.Get("Custom") : "");
        UpdateCaption();
        if (group is not null)
            hints.SetToolTip(caption, group.Key);
        var path = new TextBox { Text = current, Dock = DockStyle.Fill, AccessibleName = Ui.Get("Target") };
        hints.SetToolTip(path, current);
        path.TextChanged += (_, _) =>
        {
            var target = path.Text.Trim();
            if (group is null)
                controller.UnifiedOutputDirectory = target;
            else if (string.Equals(target, group.Key, StringComparison.OrdinalIgnoreCase))
                controller.GroupTargets.Remove(group.Key);
            else
                controller.GroupTargets[group.Key] = target;
            hints.SetToolTip(path, target);
            UpdateCaption();
            InvalidatePlan();
        };
        var browse = new Button { Text = Ui.Get("Browse"), AutoSize = true, FlatStyle = FlatStyle.Flat };
        browse.Click += (_, _) => { if (TryBrowse(path.Text, out var chosen)) path.Text = chosen; };
        row.Controls.Add(caption, 0, 0);
        row.Controls.Add(path, 1, 0);
        row.Controls.Add(browse, 2, 0);
        if (group is not null)
        {
            var reset = new Button { Text = Ui.Get("RestoreFolder"), AutoSize = true, FlatStyle = FlatStyle.Flat };
            reset.Click += (_, _) => path.Text = group.Key;
            row.Controls.Add(reset, 3, 0);
        }
        outputRows.Controls.Add(row);
    }

    private void RefreshPlan()
    {
        if (!CommitRevisionEdit() || controller.IsBusy)
            return;
        try
        {
            controller.Preflight();
            RebuildOutputRows();
            RenderManual();
            RenderPlan();
            UpdateDynamicState();
            if (controller.Session.Inputs.Count == 0)
                Notify("NoInput", "Preflight");
        }
        catch (Exception error) { Notify("PreflightError", "Preflight", error.Message); }
    }

    private async Task StartAsync()
    {
        if (!CommitRevisionEdit() || controller.IsBusy || controller.Session.HasInvalidEdit)
            return;
        runRows = controller.Session.CurrentPlan?.Items.ToDictionary(item => item.SourcePath,
            item => (item.SourceOrdinal, GroupLabel(item.GroupKey)), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, (int, string)>();
        try
        {
            await controller.RunAsync();
            RenderPlan();
            UpdateDynamicState();
        }
        catch (Exception error) { Notify("RunError", details: error.Message); }
        finally
        {
            if (closePending)
                Close();
        }
    }

    private void ValidateRevisionEdit(object? sender, DataGridViewCellValidatingEventArgs args)
    {
        if (!preflight.IsCurrentCellInEditMode || args.RowIndex < 0 || args.ColumnIndex != preflight.Columns["Rev"]!.Index)
            return;
        var row = preflight.Rows[args.RowIndex];
        var data = (GridRow)row.Tag!;
        if (!controller.Session.SetRevision(data.Source, Convert.ToString(args.FormattedValue) ?? ""))
        {
            args.Cancel = true;
            row.Cells[args.ColumnIndex].ErrorText = Ui.Get("RevisionInput");
            return;
        }

        row.Cells[args.ColumnIndex].ErrorText = "";
        pendingRevisionRender = true;
    }

    private bool CommitRevisionEdit() => !preflight.IsCurrentCellInEditMode || preflight.EndEdit();

    private void EditSelectedRevisions()
    {
        if (!CommitRevisionEdit())
            return;
        var selected = SelectedRows(preflight);
        if (selected.Length == 0)
        {
            Notify("NoSelection");
            return;
        }

        using var dialog = new Form { Text = Ui.Get("BatchRev"), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, Font = Font,
            MaximizeBox = false, MinimizeBox = false, Padding = new Padding(10) };
        var content = Flow();
        content.FlowDirection = FlowDirection.TopDown;
        var entry = new TextBox { Width = 200, AccessibleName = Ui.Get("Rev") };
        var hint = new Label { AutoSize = true, Text = Ui.Get("RevisionInput") };
        var ok = new Button { Text = Ui.Get("Apply"), AutoSize = true };
        var cancelEdit = new Button { Text = Ui.Get("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = Flow();
        buttons.Controls.AddRange([ok, cancelEdit]);
        content.Controls.AddRange([hint, entry, buttons]);
        dialog.Controls.Add(content);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancelEdit;
        ok.Click += (_, _) =>
        {
            if (!RevisionNumber.TryParse(entry.Text, out _))
            {
                hint.ForeColor = Color.Firebrick;
                entry.Focus();
                return;
            }
            controller.Session.SetRevisions(selected.Select(row => row.Source).ToArray(), entry.Text);
            dialog.DialogResult = DialogResult.OK;
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RenderPlan();
            UpdateDynamicState();
        }
    }

    private void RestoreSelectedRevisions()
    {
        if (!CommitRevisionEdit())
            return;
        var selected = SelectedRows(preflight);
        if (selected.Length == 0)
        {
            Notify("NoSelection");
            return;
        }
        controller.Session.RestoreSuggested(selected.Select(row => row.Source).ToArray());
        RenderPlan();
        UpdateDynamicState();
    }

    private void RenderManual()
    {
        var paths = controller.ManualPaths;
        var groups = manualList.CanEdit ? controller.Session.Groups :
            SourceDirectoryGrouper.Group(paths, new Dictionary<string, string>());
        var labelsBySource = groups.SelectMany(group => group.SourcePaths.Select(path => (path, group.Label)))
            .ToDictionary(item => item.path, item => item.Label, StringComparer.OrdinalIgnoreCase);
        RenderRows(manualGrid, paths.Select((path, index) =>
        {
            var label = labelsBySource.GetValueOrDefault(path) ?? "";
            return (new GridRow(path, "", label, "", ""), new object?[] { index + 1, label, Path.GetFileName(path) });
        }));
    }

    private void RenderPlan()
    {
        if (preflight.IsCurrentCellInEditMode)
            return;
        preflight.Columns["Rev"]!.Visible = revision.Checked;
        var items = controller.Session.CurrentPlan?.Items ?? Array.Empty<PlannedOutputItem>();
        var filtered = items.Where(item =>
            (!problems.Checked || item.IsBlocking || controller.Session.InvalidEdit(item.SourcePath) is not null)
            && (Path.GetFileName(item.SourcePath).Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(item.FinalOutputPath).Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)));
        RenderRows(preflight, filtered.Select(item =>
        {
            var group = GroupLabel(item.GroupKey);
            var invalid = controller.Session.InvalidEdit(item.SourcePath);
            var status = invalid is not null && revision.Checked ? Ui.Get("RevisionInput")
                : controller.Session.IsStale ? Ui.Get("Stale")
                : item.ReasonCode != ResultReasons.Ready ? Ui.Reason(item.ReasonCode)
                : Ui.Get(item.PublishIntent == PlannedPublishIntent.ReplaceExpected ? "Replace" : "Create");
            var reason = Ui.Reason(item.ReasonCode, item.ReasonArguments);
            var row = new GridRow(item.SourcePath, item.FinalOutputPath, group, status, reason, item.TechnicalDetails, item);
            return (row, new object?[] { item.SourceOrdinal, group, Path.GetFileName(item.SourcePath),
                Path.GetFileName(item.FinalOutputPath), status, invalid ?? item.ChosenRevision?.ToString() });
        }));
        UpdateVisibleCount(preflight.RowCount, items.Count);
    }

    private void UpdateVisibleCount(int shown, int total) => visibleCount.Text = Ui.Get("VisibleCount", shown, total)
        + (controller.Session.IsStale && total != 0 ? " · " + Ui.Get("Stale") : "");

    private void RenderResults()
    {
        RenderRows(results, controller.Results.Select((item, index) =>
        {
            var known = runRows.GetValueOrDefault(item.SourcePath, (index + 1, ""));
            var label = Ui.Get("Result." + item.Status);
            var reason = Ui.Reason(item.ReasonCode, item.ReasonArguments);
            var plan = controller.Session.CurrentPlan?.Items.FirstOrDefault(p =>
                string.Equals(p.SourcePath, item.SourcePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.FinalOutputPath, item.FinalOutputPath, StringComparison.OrdinalIgnoreCase));
            var row = new GridRow(item.SourcePath, item.FinalOutputPath, known.Item2, label, reason, item.TechnicalDetails, plan);
            return (row, new object?[] { known.Item1, known.Item2, Path.GetFileName(item.SourcePath),
                Path.GetFileName(item.FinalOutputPath), label });
        }));
    }

    private void RenderRows(DataGridView grid, IEnumerable<(GridRow Data, object?[] Cells)> rows)
    {
        var previous = SelectedRows(grid).Select(row => row.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cursor = grid.CurrentRow?.Tag as GridRow;
        var column = grid.CurrentCell?.ColumnIndex ?? 0;
        var scroll = grid.FirstDisplayedScrollingRowIndex;
        var rowIndex = -1;
        grid.SuspendLayout();
        try
        {
            grid.Rows.Clear();
            foreach (var (data, cells) in rows)
            {
                var row = grid.Rows[grid.Rows.Add(cells.Select(value => value ?? "").ToArray())];
                row.Tag = data;
                row.Cells["Source"].ToolTipText = data.Source;
                if (grid.Columns.Contains("Target"))
                {
                    row.Cells["Target"].ToolTipText = data.Target;
                    row.Cells["Status"].ToolTipText = data.Reason;
                }
                if (cursor?.Source == data.Source)
                    rowIndex = row.Index;
                row.DefaultCellStyle.BackColor = GroupColor(data.Group);
            }
            if (rowIndex >= 0 && grid.Columns[column].Visible)
                grid.CurrentCell = grid[column, rowIndex];
            grid.ClearSelection();
            foreach (DataGridViewRow row in grid.Rows)
                row.Selected = previous.Contains(((GridRow)row.Tag!).Source);
            if (scroll >= 0 && scroll < grid.RowCount)
                grid.FirstDisplayedScrollingRowIndex = scroll;
        }
        finally { grid.ResumeLayout(); }
    }

    private string GroupLabel(string key) => controller.Session.Groups.FirstOrDefault(group =>
        string.Equals(group.Key, key, StringComparison.OrdinalIgnoreCase))?.Label ?? "";

    private static GridRow[] SelectedRows(DataGridView grid) => grid.SelectedRows.Cast<DataGridViewRow>()
        .Where(row => row.Visible).OrderBy(row => row.Index).Select(row => row.Tag as GridRow).OfType<GridRow>().ToArray();

    private void ShowSelectedDetails(DataGridView grid)
    {
        var rows = SelectedRows(grid);
        if (rows.Length == 1)
            ShowDetails(rows[0]);
        else if (grid.CurrentRow?.Tag is GridRow row && rows.Length > 0)
            ShowDetails(row);
        else
            Notify("NoSelection");
    }

    private void ShowDetails(GridRow row)
    {
        var lines = new List<string>
        {
            Ui.Get("Source"), row.Source, "", Ui.Get("Target"), row.Target, "",
            Ui.Get("Group") + "  " + row.Group, row.Status, row.Reason
        };
        if (row.Plan?.CatalogSnapshot is { } catalog)
        {
            lines.Add("");
            lines.Add(Ui.Get("HighestSource") + "  " + (catalog.SourceHighest?.ToString() ?? "—"));
            lines.Add(Ui.Get("HighestTarget") + "  " + (catalog.TargetHighest?.ToString() ?? "—"));
            lines.Add(Ui.Get("Suggested") + "  " + (row.Plan.SuggestedRevision?.ToString() ?? "—"));
            if (row.Plan.IsManual)
                lines.Add(Ui.Get("ManualValue") + "  " + row.Plan.ChosenRevision);
        }
        if (!string.IsNullOrEmpty(row.Technical))
            lines.AddRange(["", row.Technical]);
        using var dialog = new Form { Text = Ui.Get("Detail"), Font = Font, Size = new Size(740, 510), MinimumSize = new Size(400, 300),
            StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, Padding = new Padding(10) };
        var display = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Lines = lines.ToArray() };
        var actions = Flow();
        actions.Dock = DockStyle.Bottom;
        var copy = new Button { Text = Ui.Get("Copy"), AutoSize = true };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(string.Join(Environment.NewLine, new[] { row.Source, row.Target }.Where(path => path.Length > 0))); }
            catch (System.Runtime.InteropServices.ExternalException error) { Notify("Copy", details: error.Message); }
        };
        var folder = new Button { Text = Ui.Get("Folder"), AutoSize = true };
        folder.Click += (_, _) => PickFolder([row], dialog);
        var close = new Button { Text = Ui.Get("Close"), AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange([copy, folder, close]);
        dialog.Controls.Add(display);
        dialog.Controls.Add(actions);
        dialog.CancelButton = close;
        dialog.ShowDialog(this);
    }

    private void OpenSelectedFolder() => PickFolder(lastGrid is null ? [] : SelectedRows(lastGrid), this);

    private void PickFolder(GridRow[] rows, IWin32Window owner)
    {
        var directories = rows.SelectMany(row => new[] { Path.GetDirectoryName(row.Source), Path.GetDirectoryName(row.Target) })
            .OfType<string>().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (directories.Length == 0)
        {
            Notify("NoFolder");
            return;
        }
        if (directories.Length == 1)
        {
            OpenFolder(directories[0]);
            return;
        }
        using var dialog = new Form { Text = Ui.Get("ChooseFolder"), Font = Font, StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(640, 220), MinimizeBox = false, MaximizeBox = false };
        var list = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        list.Items.AddRange(directories);
        var open = new Button { Text = Ui.Get("Folder"), Dock = DockStyle.Bottom, AutoSize = true, Enabled = false };
        list.SelectedIndexChanged += (_, _) => open.Enabled = list.SelectedIndex >= 0;
        open.Click += (_, _) => dialog.DialogResult = DialogResult.OK;
        dialog.Controls.Add(list);
        dialog.Controls.Add(open);
        dialog.AcceptButton = open;
        if (dialog.ShowDialog(owner) == DialogResult.OK && list.SelectedItem is string path)
            OpenFolder(path);
    }

    private Color GroupColor(string label)
    {
        if (label.Length == 0 || SystemInformation.HighContrast)
            return Color.Empty;
        var palette = new[] { Color.FromArgb(229, 240, 252), Color.FromArgb(230, 244, 233), Color.FromArgb(249, 235, 225),
            Color.FromArgb(239, 231, 250), Color.FromArgb(251, 242, 215), Color.FromArgb(243, 231, 239) };
        var index = label.Aggregate(0, (acc, c) => (acc * 26 + c - 'A' + 1) % palette.Length);
        var color = palette[(index + palette.Length - 1) % palette.Length];
        CustomizeGroupColor(ref color);
        return color;
    }

    partial void CustomizeGroupColor(ref Color color);
    partial void RefreshThemeColors();
}
