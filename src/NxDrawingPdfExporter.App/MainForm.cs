using System.Diagnostics;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Localization;
using NxDrawingPdfExporter.App.Logging;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App;

internal sealed record MainFormNxViewState(string StatusText, bool ShowCandidateList, bool EnableNxActions, bool EnableStart);

public sealed partial class MainForm : Form
{
    private readonly ApplicationController controller;
    private readonly JsonUiSettingsStore store = new(Path.Combine(JsonUiSettingsStore.LocalRoot, "ui-settings.json"));
    private readonly ManualListState manualList = new();
    private readonly List<(Control Control, string Key)> labels = new();
    private readonly List<(ToolStripItem Item, string Key)> menuLabels = new();
    private UiSettings settings;
    private bool settingsExpanded = true;
    private bool closePending;
    private bool changingLayout;
    private DataGridView? lastGrid;
    private UiText Ui => controller.Text;

    public MainForm()
    {
        controller = new ApplicationController(new ApplicationServices(), new StartupLogSink(), new NxDetectionLog());
        var loaded = store.Load();
        settings = loaded.Settings;
        Ui.Language = settings.Language;
        InitializeComponent();
        WireEvents();
        ApplySavedLayout();
        RefreshLanguage();
        ApplyManualState();
        RebuildOutputRows();
        UpdateDynamicState();
        InitializeAppearance();
        Shown += async (_, _) =>
        {
            AdjustLayout();
            if (loaded.Recovered)
                Notify("SettingsFallback");
            await TryNxAsync(controller.RefreshNxStatusAsync);
        };
    }

    internal static MainFormNxViewState BuildNxViewState(ApplicationController controller) => new(
        controller.NxStatusMessage, controller.NxDiscovery.Installations.Count > 1,
        !controller.IsBusy && !controller.IsNxDetectionBusy,
        !controller.IsBusy && !controller.IsNxDetectionBusy && controller.SelectedNx is not null);

    internal static NxInstallation? ChooseNxCandidate(NxInstallationDiscoveryResult discovery, string? highlightedRoot) =>
        discovery.Installations.FirstOrDefault(nx => string.Equals(nx.RootDirectory, highlightedRoot, StringComparison.OrdinalIgnoreCase))
        ?? discovery.SelectedInstallation ?? discovery.Installations.FirstOrDefault();

    internal static string FormatNxCandidate(NxInstallation nx) =>
        $"{nx.Profile.Label} | {nx.RootDirectory} — NXOpen {nx.DetectedVersion} / UF {nx.UfVersion}";

    private void WireEvents()
    {
        controller.StateChanged += () =>
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
                BeginInvoke(UpdateDynamicState);
            else
                UpdateDynamicState();
        };
        nxCombo.FormattingEnabled = true;
        nxCombo.Format += (_, args) => { if (args.ListItem is NxInstallation nx) args.Value = FormatNxCandidate(nx); };
        scanMode.Checked = true;
        groupMode.Checked = true;
        skip.Checked = true;
        Localize(scanMode, "Scan");
        Localize(manualMode, "Manual");
        Localize(recursive, "Recursive");
        Localize(groupMode, "Grouped");
        Localize(unifiedMode, "Unified");
        Localize(skip, "Skip");
        Localize(overwrite, "Overwrite");
        Localize(revision, "Revision");
        Localize(problems, "Problems");

        scanMode.CheckedChanged += (_, _) => { if (scanMode.Checked) SetInputMode(GuiInputMode.ScanFolder); };
        manualMode.CheckedChanged += (_, _) => { if (manualMode.Checked) SetInputMode(GuiInputMode.ManualSelection); };
        recursive.CheckedChanged += (_, _) =>
        {
            controller.IncludeSubfolders = recursive.Checked;
            UpdateInputs();
        };
        scanPath.TextChanged += (_, _) =>
        {
            controller.ScanFolderPath = scanPath.Text.Trim();
            InvalidatePlan();
        };
        scanPath.Validated += (_, _) => UpdateInputs();
        groupMode.CheckedChanged += (_, _) => { if (groupMode.Checked) SetOutputMode(OutputMode.GroupedDirectories); };
        unifiedMode.CheckedChanged += (_, _) => { if (unifiedMode.Checked) SetOutputMode(OutputMode.UnifiedDirectory); };
        skip.CheckedChanged += (_, _) => { if (skip.Checked) SetPolicy(ExistingPdfPolicy.Skip); };
        overwrite.CheckedChanged += (_, _) => { if (overwrite.Checked) SetPolicy(ExistingPdfPolicy.Overwrite); };
        revision.CheckedChanged += (_, _) => { if (revision.Checked) SetPolicy(ExistingPdfPolicy.Revision); };
        search.TextChanged += (_, _) => { if (CommitRevisionEdit()) { RenderPlan(); RenderResults(); } };
        problems.CheckedChanged += (_, _) => { if (CommitRevisionEdit()) { RenderPlan(); RenderResults(); } };
        foreach (var grid in new[] { preflight, results, manualGrid })
        {
            grid.Enter += (_, _) => lastGrid = grid;
            grid.KeyDown += (_, args) =>
            {
                if (args.KeyCode == Keys.F4)
                {
                    ShowSelectedDetails(grid);
                    args.Handled = true;
                }
            };
            grid.CellDoubleClick += (_, args) =>
            {
                if (args.RowIndex < 0 || args.ColumnIndex < 0)
                    return;
                if (grid == preflight && grid.Columns[args.ColumnIndex].Name == "Rev" && !controller.IsBusy)
                    grid.BeginEdit(true);
                else
                    ShowDetails((GridRow)grid.Rows[args.RowIndex].Tag!);
            };
        }
        preflight.CellValidating += ValidateRevisionEdit;
        preflight.CellEndEdit += (_, _) =>
        {
            if (pendingRevisionRender && !IsDisposed)
                BeginInvoke(() => { pendingRevisionRender = false; RenderPlan(); UpdateDynamicState(); });
        };
        split.SplitterMoved += (_, _) =>
        {
            var space = split.Height - split.SplitterWidth;
            if (!changingLayout && space > 0)
                settings = settings with { SplitterRatio = Math.Clamp((double)split.SplitterDistance / space, .15, .85) };
        };
        SizeChanged += (_, _) => AdjustLayout();
        Ui.Changed += RefreshLanguage;
    }

    private void BuildMenu()
    {
        var file = MenuRoot("File");
        AddItem(file, "ScanFolder", BrowseScan);
        AddItem(file, "Add", AddManual);
        AddItem(file, "Folder", OpenSelectedFolder);
        AddItem(file, "Exit", Close);
        var view = MenuRoot("View");
        AddItem(view, "Settings", ToggleSettings);
        AddItem(view, "Problems", () => problems.Checked = !problems.Checked);
        AddItem(view, "Search", () => search.Focus());
        AddItem(view, "ResetLayout", ResetLayout);
        var options = MenuRoot("Options");
        var language = AddItem(options, "Language", null);
        AddLiteral(language, "简体中文", () => SetLanguage("zh-CN"));
        AddLiteral(language, "English", () => SetLanguage("en"));
        AddItem(options, "Appearance", () => OpenAppearance());
        var font = AddItem(options, "Font", null);
        foreach (int size in new[] { 90, 100, 115, 130 })
            AddLiteral(font, size + "%", () => { settings = settings with { FontScale = size }; ApplyFont(); SaveSettings(); });
        var help = MenuRoot("Help");
        AddItem(help, "Guide", () => Notify("GuideBody", "Guide"));
        AddItem(help, "RevisionRules", () => Notify("RevisionBody", "RevisionRules"));
        AddItem(help, "Log", OpenLog);
        AddItem(help, "About", () => MessageBox.Show(this, Ui.Get("Title") + " " +
            typeof(MainForm).Assembly.GetName().Version?.ToString(3), Ui.Get("About")));
    }

    private ToolStripMenuItem MenuRoot(string key)
    {
        var item = new ToolStripMenuItem();
        menuLabels.Add((item, key));
        menu.Items.Add(item);
        return item;
    }

    private ToolStripMenuItem AddItem(ToolStripMenuItem parent, string key, Action? action)
    {
        var item = new ToolStripMenuItem();
        menuLabels.Add((item, key));
        if (action is not null)
            item.Click += (_, _) => action();
        parent.DropDownItems.Add(item);
        return item;
    }

    private static void AddLiteral(ToolStripMenuItem parent, string label, Action action) =>
        parent.DropDownItems.Add(label, null, (_, _) => action());

    private void Localize(Control control, string key) => labels.Add((control, key));

    private void SetLanguage(string language)
    {
        if (!CommitRevisionEdit())
            return;
        settings = settings with { Language = language };
        Ui.Language = language;
        SaveSettings();
    }

    private void RefreshLanguage()
    {
        Text = Ui.Get("Title");
        foreach (var (control, key) in labels)
            control.Text = Ui.Get(key);
        foreach (var (item, key) in menuLabels)
            item.Text = Ui.Get(key);
        foreach (var grid in new[] { preflight, results, manualGrid })
            foreach (DataGridViewColumn column in grid.Columns)
                column.HeaderText = Ui.Get(column.Name);
        search.PlaceholderText = Ui.Get("Search");
        search.AccessibleName = Ui.Get("Search");
        hints.SetToolTip(skip, Ui.Get("PolicyOriginal"));
        hints.SetToolTip(overwrite, Ui.Get("PolicyOriginal"));
        hints.SetToolTip(revision, Ui.Get("RevisionBody"));
        ApplyManualState();
        RebuildOutputRows();
        RenderPlan();
        RenderResults();
        UpdateDynamicState();
    }

    private void ApplySavedLayout()
    {
        ApplyFont();
        var saved = settings.WindowBounds;
        if (saved.IsEmpty)
            return;
        var area = Screen.FromRectangle(saved).WorkingArea;
        var width = Math.Min(Math.Max(MinimumSize.Width, saved.Width), area.Width);
        var height = Math.Min(Math.Max(MinimumSize.Height, saved.Height), area.Height);
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(Math.Clamp(saved.X, area.Left, area.Right - width),
            Math.Clamp(saved.Y, area.Top, area.Bottom - height), width, height);
        foreach (DataGridViewColumn column in preflight.Columns)
            if (settings.ColumnWidths.TryGetValue(column.Name, out var columnWidth))
            {
                if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
                    column.FillWeight = columnWidth;
                else
                    column.Width = Math.Max(column.MinimumWidth, columnWidth * DeviceDpi / 96);
            }
    }

    private void ApplyFont()
    {
        Font = new Font("Microsoft YaHei UI", 9F * settings.FontScale / 100F);
        AdjustLayout();
    }

    private void AdjustLayout()
    {
        if (changingLayout || IsDisposed)
            return;
        changingLayout = true;
        try
        {
            shell.RowStyles[2].Height = settingsExpanded ? Math.Min(DeviceDpi * 3f, ClientSize.Height * .34f) : 0;
            nxStatus.MaximumSize = new Size(Math.Max(200, settingsScroll.ClientSize.Width - 40), 0);
            var space = split.Height - split.SplitterWidth;
            var minimum = Math.Min(DeviceDpi * 3 / 4, space / 3);
            if (minimum >= 24)
            {
                split.Panel1MinSize = minimum;
                split.Panel2MinSize = minimum;
                split.SplitterDistance = Math.Clamp((int)(settings.SplitterRatio * space), minimum, space - minimum);
            }
        }
        finally { changingLayout = false; }
    }

    private void ResetLayout()
    {
        settings = settings with { WindowBounds = Rectangle.Empty, SplitterRatio = new UiSettings().SplitterRatio, ColumnWidths = new() };
        WindowState = FormWindowState.Normal;
        Size = new Size(Math.Min(1220 * DeviceDpi / 96, Screen.FromControl(this).WorkingArea.Width),
            Math.Min(840 * DeviceDpi / 96, Screen.FromControl(this).WorkingArea.Height));
        CenterToScreen();
        int[] widths = [56, 82, 270, 300, 156, 95];
        for (var index = 0; index < widths.Length; index++)
        {
            var column = preflight.Columns[index];
            if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
                column.FillWeight = widths[index];
            else
                column.Width = widths[index] * DeviceDpi / 96;
        }
        AdjustLayout();
        SaveSettings();
    }

    private void SaveSettings()
    {
        settings = settings with
        {
            WindowBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds,
            ColumnWidths = preflight.Columns.Cast<DataGridViewColumn>()
                .ToDictionary(column => column.Name, column => column.Width * 96 / DeviceDpi)
        };
        try { store.Save(settings); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Notify("SettingsError", details: error.Message); }
    }

    private void ToggleSettings()
    {
        settingsExpanded = !settingsExpanded;
        settingsBand.Visible = settingsExpanded;
        UpdateDynamicState();
        AdjustLayout();
    }

    private void UpdateDynamicState()
    {
        var state = BuildNxViewState(controller);
        nxStatus.Text = state.StatusText;
        var candidates = controller.NxDiscovery.Installations;
        if (!nxCombo.Items.Cast<NxInstallation>().SequenceEqual(candidates))
        {
            var highlighted = ChooseNxCandidate(controller.NxDiscovery, (nxCombo.SelectedItem as NxInstallation)?.RootDirectory);
            nxCombo.Items.Clear();
            nxCombo.Items.AddRange(candidates.Cast<object>().ToArray());
            nxCombo.SelectedItem = highlighted;
        }
        nxCombo.Visible = state.ShowCandidateList;
        nxLine.Controls[1].Visible = state.ShowCandidateList;
        settingsPanel.Enabled = state.EnableNxActions;
        inspect.Enabled = !controller.IsBusy;
        start.Enabled = state.EnableStart && controller.Session.CurrentPlan is { HasBlockingIssues: false, Items.Count: > 0 }
            && !controller.Session.IsStale && !controller.Session.HasInvalidEdit;
        cancel.Enabled = controller.IsBusy;
        preflight.Columns["Rev"]!.ReadOnly = controller.IsBusy;
        batchRevision.Enabled = restoreRevision.Enabled = !controller.IsBusy && revision.Checked && !controller.Session.IsStale;
        progress.Style = controller.IsBusy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        progressText.Text = closePending ? Ui.Get("PendingClose") : controller.ProgressText;
        summary.Text = controller.SummaryText;
        toggleSettings.Text = (settingsExpanded ? "▾ " : "▸ ") + Ui.Get("Settings");
        settingsSummary.Text = Ui.Get("SettingsSummary", controller.SelectedNx?.Profile.Label ?? Ui.Get("NxUnselected"),
            controller.Session.Inputs.Count, Ui.Get(unifiedMode.Checked ? "Unified" : "Grouped"));
        if (!controller.IsBusy)
            RenderResults();
    }

    private async Task TryNxAsync(Func<CancellationToken, Task> action)
    {
        if (controller.IsBusy || controller.IsNxDetectionBusy)
            return;
        try { await action(default); }
        catch (Exception error) { Notify("NxError", details: error.Message); }
    }

    private void RunNxSelection()
    {
        if (nxCombo.SelectedItem is NxInstallation selected)
            _ = TryNxAsync(token => controller.SelectDetectedNxAsync(selected.RootDirectory, token));
    }

    private async Task BrowseNxAsync()
    {
        using var picker = new FolderBrowserDialog { Description = Ui.Get("NxBrowseHint"), UseDescriptionForTitle = true };
        if (picker.ShowDialog(this) == DialogResult.OK)
            await TryNxAsync(token => controller.SelectManualNxAsync(picker.SelectedPath, token));
    }

    private void Notify(string key, string? title = null, string? details = null) =>
        MessageBox.Show(this, Ui.Get(key) + (string.IsNullOrWhiteSpace(details) ? "" : Environment.NewLine + details),
            Ui.Get(title ?? "Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void OpenLog()
    {
        if (controller.CurrentRunDirectory is { } path && Directory.Exists(path))
            OpenFolder(path);
        else
            Notify("NoLog", "Log");
    }

    private void OpenFolder(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception error) { Notify("NoFolder", "Folder", error.Message); }
    }

    private void RequestCancel()
    {
        try { controller.Cancel(); }
        catch (Exception error) { Notify("RunError", details: error.Message); }
    }

    protected override void OnFormClosing(FormClosingEventArgs args)
    {
        base.OnFormClosing(args);
        if (controller.IsBusy)
        {
            closePending = true;
            RequestCancel();
            UpdateDynamicState();
            args.Cancel = true;
        }
        else if (!args.Cancel)
            SaveSettings();
    }

    partial void InitializeAppearance();
    partial void OpenAppearance();

    private sealed class StartupLogSink : ILogSink
    {
        public void Write(string message) => Debug.WriteLine(message);
    }
}
