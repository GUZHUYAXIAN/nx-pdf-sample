using System.Drawing;
using System.Windows.Forms;

namespace NxDrawingPdfExporter.App;

public sealed partial class MainForm
{
    private readonly MenuStrip menu = new() { Dock = DockStyle.Fill, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(10, 3, 10, 3) };
    private readonly TableLayoutPanel shell = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12, 6, 12, 8), BackColor = Color.Transparent };
    private readonly TableLayoutPanel settingsBand = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
    private readonly PictureBox artwork = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage, Margin = new Padding(6, 0, 0, 6) };
    private readonly Panel settingsScroll = new() { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0, 0, 0, 6) };
    private readonly TableLayoutPanel settingsPanel = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(10, 5, 10, 5) };
    private readonly FlowLayoutPanel nxLine = Flow();
    private readonly Label nxStatus = Caption();
    private readonly ComboBox nxCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
    private readonly FlowLayoutPanel inputLine = Flow();
    private readonly RadioButton scanMode = Radio();
    private readonly RadioButton manualMode = Radio();
    private readonly CheckBox recursive = new() { AutoSize = true };
    private readonly TextBox scanPath = new() { Dock = DockStyle.Fill, AccessibleName = "PRT" };
    private readonly TableLayoutPanel scanLine = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
    private readonly FlowLayoutPanel manualLine = Flow();
    private readonly DataGridView manualGrid = MakeGrid();
    private readonly FlowLayoutPanel outputModeLine = Flow();
    private readonly RadioButton groupMode = Radio();
    private readonly RadioButton unifiedMode = Radio();
    private readonly TableLayoutPanel outputRows = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
    private readonly FlowLayoutPanel policyLine = Flow();
    private readonly RadioButton skip = Radio();
    private readonly RadioButton overwrite = Radio();
    private readonly RadioButton revision = Radio();
    private readonly SplitContainer split = new() { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8, Margin = new Padding(0) };
    private readonly FlowLayoutPanel reviewTools = Flow();
    private readonly FlowLayoutPanel resultTools = Flow();
    private readonly TextBox search = new() { Width = 190 };
    private readonly CheckBox problems = new() { AutoSize = true };
    private readonly Label visibleCount = Caption();
    private readonly Label settingsSummary = Caption();
    private readonly Label progressText = Caption();
    private readonly Label summary = Caption();
    private readonly ProgressBar progress = new() { Height = 5, Dock = DockStyle.Fill, Margin = new Padding(4, 2, 4, 0) };
    private readonly DataGridView preflight = MakeGrid();
    private readonly DataGridView results = MakeGrid();
    private readonly ToolTip hints = new() { AutoPopDelay = 15000, InitialDelay = 400 };
    private Button start = null!;
    private Button cancel = null!;
    private Button inspect = null!;
    private Button batchRevision = null!;
    private Button restoreRevision = null!;
    private Button toggleManual = null!;
    private Button addManual = null!;
    private Button removeManual = null!;
    private Button toggleSettings = null!;

    private void InitializeComponent()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9F);
        ClientSize = new Size(1220, 840);
        MinimumSize = new Size(660, 550);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 264));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var overview = Flow();
        toggleSettings = ButtonFor("Settings", ToggleSettings);
        overview.Controls.AddRange([toggleSettings, settingsSummary]);
        settingsSummary.Margin = new Padding(12, 8, 2, 0);
        settingsScroll.Controls.Add(settingsPanel);
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nxLine.Controls.AddRange([nxCombo, ButtonFor("SelectNx", () => RunNxSelection()),
            ButtonFor("RefreshNx", () => _ = TryNxAsync(controller.RefreshNxStatusAsync)),
            ButtonFor("BrowseNx", () => _ = BrowseNxAsync())]);
        nxStatus.Padding = new Padding(3, 5, 3, 1);
        inputLine.Controls.AddRange([scanMode, manualMode, recursive]);
        scanLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scanLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scanLine.Controls.Add(scanPath, 0, 0);
        scanLine.Controls.Add(ButtonFor("Browse", BrowseScan), 1, 0);
        toggleManual = ButtonFor("ManualList", ToggleManualList);
        addManual = ButtonFor("Add", AddManual);
        removeManual = ButtonFor("Remove", RemoveManual);
        manualLine.Controls.AddRange([toggleManual, addManual, removeManual]);
        manualGrid.Height = 115;
        manualGrid.Dock = DockStyle.Top;
        AddColumns(manualGrid, [("Ordinal", 56), ("Group", 80), ("Source", 480)]);
        manualGrid.ReadOnly = true;
        outputModeLine.Controls.AddRange([groupMode, unifiedMode]);
        outputRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        policyLine.Controls.AddRange([skip, overwrite, revision]);
        foreach (var control in new Control[] { nxStatus, nxLine, inputLine, scanLine, manualLine, manualGrid, outputModeLine, outputRows, policyLine })
            settingsPanel.Controls.Add(control);

        inspect = ButtonFor("Preflight", RefreshPlan);
        batchRevision = ButtonFor("BatchRev", EditSelectedRevisions);
        restoreRevision = ButtonFor("RestoreRev", RestoreSelectedRevisions);
        reviewTools.Controls.AddRange([inspect, search, problems, visibleCount, batchRevision, restoreRevision]);
        AddColumns(preflight, [("Ordinal", 56), ("Group", 82), ("Source", 270), ("Target", 300), ("Status", 156), ("Rev", 95)]);
        preflight.Columns["Rev"]!.ReadOnly = false;
        preflight.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        AttachGridPane(split.Panel1, reviewTools, preflight);

        var resultHeading = Caption();
        Localize(resultHeading, "Results");
        resultHeading.Margin = new Padding(6, 8, 8, 3);
        resultTools.Controls.AddRange([resultHeading, summary, ButtonFor("Detail", () => ShowSelectedDetails(results))]);
        AddColumns(results, [("Ordinal", 56), ("Group", 82), ("Source", 250), ("Target", 280), ("Status", 180)]);
        results.ReadOnly = true;
        AttachGridPane(split.Panel2, resultTools, results);

        var actions = Flow();
        start = ButtonFor("Start", () => _ = StartAsync());
        start.Tag = "Primary";
        cancel = ButtonFor("Cancel", RequestCancel);
        progressText.Margin = new Padding(10, 8, 2, 2);
        actions.Controls.AddRange([start, cancel, progressText]);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2, Padding = new Padding(4) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 9));
        bottom.Controls.Add(actions, 0, 0);
        bottom.Controls.Add(progress, 0, 1);
        shell.Controls.Add(menu, 0, 0);
        shell.Controls.Add(overview, 0, 1);
        settingsBand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settingsBand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        settingsBand.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        settingsBand.Controls.Add(settingsScroll, 0, 0);
        settingsBand.Controls.Add(artwork, 1, 0);
        shell.Controls.Add(settingsBand, 0, 2);
        shell.Controls.Add(split, 0, 3);
        shell.Controls.Add(bottom, 0, 4);
        Controls.Add(shell);
        MainMenuStrip = menu;
        BuildMenu();
        ResumeLayout();
    }

    private static FlowLayoutPanel Flow() => new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0), Padding = new Padding(2), WrapContents = true
    };

    private static Label Caption() => new() { AutoSize = true, Margin = new Padding(4, 5, 3, 3) };
    private static RadioButton Radio() => new() { AutoSize = true, Margin = new Padding(4, 6, 12, 4) };

    private Button ButtonFor(string key, Action action)
    {
        var button = new Button { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 1, 6, 1), FlatStyle = FlatStyle.Flat };
        button.Click += (_, _) => action();
        Localize(button, key);
        return button;
    }

    private static DataGridView MakeGrid() => new()
    {
        Dock = DockStyle.Fill, Margin = new Padding(0), BorderStyle = BorderStyle.None, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        EnableHeadersVisualStyles = false, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True, Padding = new Padding(6, 4, 6, 4) }
    };

    private static void AddColumns(DataGridView grid, (string Key, int Width)[] columns)
    {
        foreach (var (key, width) in columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = key, Width = width, MinimumWidth = 45, ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
    }

    private static void AttachGridPane(Control host, Control toolbar, DataGridView grid)
    {
        var pane = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), ColumnCount = 1, RowCount = 2 };
        pane.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pane.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pane.Controls.Add(toolbar, 0, 0);
        pane.Controls.Add(grid, 0, 1);
        host.Controls.Add(pane);
    }
}
