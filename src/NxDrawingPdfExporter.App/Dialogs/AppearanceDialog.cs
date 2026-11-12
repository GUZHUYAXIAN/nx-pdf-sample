using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Controls;
using NxDrawingPdfExporter.App.Localization;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Dialogs;

internal sealed class AppearanceDialog : SkinDialog
{
    private static readonly string[] ParameterKeys = ["ImageVisibility", "PanelOpacity", "Zoom", "OffsetX", "OffsetY", "Blur", "Mist", "Gradient"];
    private readonly UiText ui;
    private readonly IReadOnlyList<ThemeDefinition> themes;
    private readonly ImportedImageStore imported;
    private readonly SkinPreviewSettings settings;
    private readonly Action<string, ThemeParameters, string?> preview;
    private readonly Action<Exception> reportCleanupFailure;
    private readonly SkinComboBox choices = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly PictureBox thumbnail = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly TrackBar[] sliders;
    private readonly SkinComboBox direction = Option();
    private readonly SkinComboBox surface = Option();
    private readonly CheckBox automatic = new() { AutoSize = true };
    private readonly SkinButton manualColor = new() { AutoSize = true };
    private readonly SkinButton apply = new() { AutoSize = true, Tag = "Primary", Enabled = false };
    private readonly Label notice = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
    private readonly List<string> newImports = [];
    private string? imageReference;
    private long thumbnailRevision;
    private bool updating;
    private bool importing;
    private Color? analyzedColor;

    internal AppearanceDialog(UiText ui, UiSettings current, IReadOnlyList<ThemeDefinition> themes, ImportedImageStore imported,
        Action<string, ThemeParameters, string?> preview, Action<Exception> reportCleanupFailure)
    {
        this.ui = ui;
        this.themes = themes;
        this.imported = imported;
        this.preview = preview;
        this.reportCleanupFailure = reportCleanupFailure;
        settings = new(current);
        imageReference = current.ImportedImage;
        Text = ui.Get("Appearance");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(490, 350);
        ClientSize = new Size(700, 540);
        var layout = new SkinSurface { ColumnCount = 2, RowCount = 4, Dock = DockStyle.Fill, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        choices.FormattingEnabled = true;
        choices.Format += (_, args) =>
        {
            if (args.ListItem is ThemeDefinition definition)
                args.Value = ui.Get("Theme." + definition.Id);
        };
        choices.Items.AddRange(themes.Cast<object>().ToArray());
        var importButton = new SkinButton { Text = ui.Get("Import"), AutoSize = true, Anchor = AnchorStyles.Right };
        importButton.Click += async (_, _) => await ImportAsync(importButton);
        layout.Controls.Add(choices, 0, 0);
        layout.Controls.Add(importButton, 1, 0);
        layout.Controls.Add(thumbnail, 0, 1);

        var effects = new SkinSurface { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 4, 6, 4) };
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sliders = ParameterKeys.Select((key, index) =>
        {
            var caption = new Label { Text = ui.Get(key) + "  0", AutoSize = true, Anchor = AnchorStyles.Left };
            var slider = new TrackBar
            {
                Minimum = 0, Maximum = 100, TickStyle = TickStyle.None, Dock = DockStyle.Fill, AutoSize = false,
                Height = 29, AccessibleName = ui.Get(key), SmallChange = 1, LargeChange = 10
            };
            slider.ValueChanged += (_, _) =>
            {
                caption.Text = ui.Get(key) + "  " + slider.Value;
                SaveDraft();
            };
            effects.Controls.Add(caption, 0, index);
            effects.Controls.Add(slider, 1, index);
            return slider;
        }).ToArray();

        AddChoice(direction, "GradientDirection", ["GradientLeftRight", "GradientRightLeft", "GradientTopBottom", "GradientBottomTop"], 8);
        AddChoice(surface, "SurfaceMode", ["SurfaceDark", "SurfaceLight"], 9);
        automatic.Text = ui.Get("AutomaticTextColor");
        automatic.CheckedChanged += (_, _) => SaveDraft();
        manualColor.Text = ui.Get("ManualTextColor");
        manualColor.Click += (_, _) => ChooseColor();
        effects.Controls.Add(automatic, 0, 10);
        effects.Controls.Add(manualColor, 1, 10);
        var viewport = new SkinWorkspaceViewport { Dock = DockStyle.Fill, AutoScroll = true };
        viewport.Controls.Add(effects);
        layout.Controls.Add(viewport, 1, 1);
        layout.Controls.Add(notice, 0, 2);
        layout.SetColumnSpan(notice, 2);

        var reset = new SkinButton { Text = ui.Get("RestoreTheme"), AutoSize = true };
        reset.Click += (_, _) => { settings.Reset(Theme.Id); LoadChoice(); };
        apply.Text = ui.Get("Apply");
        apply.Click += (_, _) => DialogResult = DialogResult.OK;
        var cancel = new SkinButton { Text = ui.Get("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var actions = new SkinFlowPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.AddRange([cancel, apply, reset]);
        layout.Controls.Add(actions, 0, 3);
        layout.SetColumnSpan(actions, 2);
        Controls.Add(layout);
        AcceptButton = apply;
        CancelButton = cancel;
        choices.SelectedIndexChanged += (_, _) => LoadChoice();
        choices.SelectedItem = themes.FirstOrDefault(theme => theme.Id == current.ThemeId) ?? themes[0];

        void AddChoice(SkinComboBox field, string title, string[] labels, int row)
        {
            field.AccessibleName = ui.Get(title);
            field.Items.AddRange(labels.Select(key => (object)ui.Get(key)).ToArray());
            field.SelectedIndexChanged += (_, _) => SaveDraft();
            effects.Controls.Add(new Label { Text = ui.Get(title), AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            effects.Controls.Add(field, 1, row);
        }
    }

    private ThemeDefinition Theme => (ThemeDefinition)choices.SelectedItem!;
    private string? ImagePath => Theme.Id == "imported" ? imported.Resolve(imageReference) : Theme.ImagePath;
    internal UiSettings AppliedSettings => settings.Apply(Theme.Id, imageReference);

    private static SkinComboBox Option() => new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

    private void LoadChoice()
    {
        var parameter = settings.Get(Theme);
        updating = true;
        int[] current = [parameter.ImageVisibility, parameter.PanelOpacity, parameter.Zoom, parameter.OffsetX, parameter.OffsetY,
            parameter.Blur, parameter.MistOpacity, parameter.GradientStrength];
        var available = ImagePath is not null && Theme.HasAssets;
        for (var index = 0; index < sliders.Length; index++)
        {
            sliders[index].Value = current[index];
            sliders[index].Enabled = available;
        }
        direction.SelectedIndex = (int)parameter.GradientDirection;
        surface.SelectedIndex = (int)parameter.SurfaceMode;
        automatic.Checked = parameter.AutomaticTextColor;
        direction.Enabled = surface.Enabled = automatic.Enabled = available;
        manualColor.Enabled = available;
        SetColorCaption(parameter);
        updating = false;

        thumbnail.BackColor = Theme.Background;
        thumbnail.Image?.Dispose();
        thumbnail.Image = null;
        var thumb = Theme.Id == "imported" ? ImagePath : Theme.ThumbnailPath;
        var version = ++thumbnailRevision;
        if (available && thumb is not null)
            _ = LoadThumbnailAsync(thumb, version);
        Preview();
    }

    private async Task LoadThumbnailAsync(string path, long version)
    {
        Bitmap? image = null;
        try
        {
            image = await Task.Run(() =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                using var original = Image.FromStream(stream);
                var scale = Math.Min(320d / original.Width, 200d / original.Height);
                return new Bitmap(original, new Size(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale))));
            });
            if (!IsDisposed && version == thumbnailRevision)
            {
                thumbnail.Image = image;
                image = null;
            }
        }
        catch (Exception error) when (IsImageError(error)) { System.Diagnostics.Debug.WriteLine(error); }
        finally { image?.Dispose(); }
    }

    private void SaveDraft()
    {
        if (updating || choices.SelectedIndex < 0)
            return;
        var previous = settings.Get(Theme);
        var parameter = new ThemeParameters(sliders[0].Value, sliders[5].Value, sliders[6].Value, sliders[7].Value,
            sliders[2].Value, sliders[3].Value, sliders[4].Value, sliders[1].Value,
            (SurfaceMode)surface.SelectedIndex, automatic.Checked,
            automatic.Checked ? previous.ManualTextColorArgb : previous.ManualTextColorArgb ?? analyzedColor?.ToArgb(),
            (SkinGradientDirection)direction.SelectedIndex);
        settings.Set(Theme.Id, parameter);
        SetColorCaption(parameter);
        Preview();
    }

    private void SetColorCaption(ThemeParameters parameter)
    {
        var color = parameter.ManualTextColorArgb is int manual ? Color.FromArgb(manual) : analyzedColor;
        manualColor.Text = ui.Get("ManualTextColor") + (color is Color value ? $"  #{value.ToArgb() & 0xFFFFFF:X6}" : "");
    }

    private void ChooseColor()
    {
        var parameter = settings.Get(Theme);
        using var picker = new ColorDialog { FullOpen = true, Color = parameter.ManualTextColorArgb is int value
            ? Color.FromArgb(value) : analyzedColor ?? (parameter.SurfaceMode == SurfaceMode.Dark ? Color.White : Color.Black) };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        updating = true;
        automatic.Checked = false;
        updating = false;
        parameter = parameter with { AutomaticTextColor = false, ManualTextColorArgb = picker.Color.ToArgb() };
        settings.Set(Theme.Id, parameter);
        SetColorCaption(parameter);
        Preview();
    }

    private void Preview()
    {
        apply.Enabled = false;
        if (Theme.IsImageTheme && (ImagePath is null || !Theme.HasAssets))
        {
            preview(Theme.Id, settings.Get(Theme), imageReference);
            notice.Text = ui.Get(Theme.Id == "imported" ? "ChooseSkinImage" : "ThemeUnavailable");
            return;
        }
        notice.Text = Theme.IsImageTheme ? ui.Get("PreviewPending") : "";
        preview(Theme.Id, settings.Get(Theme), imageReference);
    }

    internal void FrameReady(ThemeFrame frame)
    {
        if (IsDisposed || frame.Request.Theme.Id != Theme.Id || frame.Request.Effects != settings.Get(Theme)
            || frame.Request.ImagePath != ImagePath)
            return;
        analyzedColor = frame.Palette?.Body;
        SetColorCaption(settings.Get(Theme));
        apply.Enabled = !importing;
        notice.Text = frame.Materials?.Protected == true ? ui.Get("ContrastProtection") : "";
    }

    internal void FrameFailed(Exception error)
    {
        apply.Enabled = false;
        notice.Text = ui.Get("ImageError") + " " + error.Message;
    }

    private async Task ImportAsync(Button importButton)
    {
        using var picker = new OpenFileDialog { Filter = "PNG / JPEG|*.png;*.jpg;*.jpeg" };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        importing = true;
        importButton.Enabled = choices.Enabled = apply.Enabled = false;
        try
        {
            imageReference = await Task.Run(() => imported.Import(picker.FileName));
            newImports.Add(imageReference);
            importing = false;
            var target = themes.Single(theme => theme.Id == "imported");
            if (ReferenceEquals(choices.SelectedItem, target))
                LoadChoice();
            else
                choices.SelectedItem = target;
        }
        catch (Exception error) when (IsImageError(error))
        {
            var previousValid = notice.Text.Length == 0 || notice.Text == ui.Get("ContrastProtection");
            FrameFailed(error);
            apply.Enabled = previousValid;
        }
        finally
        {
            importing = false;
            importButton.Enabled = choices.Enabled = true;
        }
    }

    private static bool IsImageError(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.ExternalException;

    protected override void OnFormClosing(FormClosingEventArgs args)
    {
        base.OnFormClosing(args);
        args.Cancel |= importing;
    }

    protected override void OnFormClosed(FormClosedEventArgs args)
    {
        thumbnailRevision++;
        thumbnail.Image?.Dispose();
        thumbnail.Image = null;
        foreach (var reference in newImports)
        {
            if (DialogResult == DialogResult.OK && reference == imageReference)
                continue;
            if (imported.Resolve(reference) is not { } path)
                continue;
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { reportCleanupFailure(error); }
        }
        base.OnFormClosed(args);
    }
}
