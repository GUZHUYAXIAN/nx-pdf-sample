using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Localization;
using NxDrawingPdfExporter.App.Theming;

namespace NxDrawingPdfExporter.App.Dialogs;

internal sealed class AppearanceDialog : Form
{
    private readonly UiText ui;
    private readonly IReadOnlyList<ThemeDefinition> themes;
    private readonly ImportedImageStore imported;
    private readonly Dictionary<string, ThemeParameters> values;
    private readonly Action<string, ThemeParameters, string?> preview;
    private readonly ComboBox choices = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly PictureBox thumbnail = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly TrackBar[] sliders;
    private readonly Button apply = new() { AutoSize = true };
    private bool updating;
    private bool importing;

    private static readonly string[] Parameters = ["ImageVisibility", "Zoom", "OffsetX", "OffsetY", "Blur", "Mist", "Gradient"];
    private readonly List<string> newImports = new();
    public string? ImageReference { get; private set; }
    public string ThemeId => ((ThemeDefinition)choices.SelectedItem!).Id;
    public Dictionary<string, ThemeParameters> AppliedParameters => values;

    public AppearanceDialog(UiText ui, UiSettings settings, IReadOnlyList<ThemeDefinition> themes, ImportedImageStore imported,
        Action<string, ThemeParameters, string?> preview)
    {
        this.ui = ui;
        this.themes = themes;
        this.imported = imported;
        this.preview = preview;
        values = new(settings.ThemeParametersById);
        ImageReference = settings.ImportedImage;

        Text = ui.Get("Appearance");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 445);
        var layout = new TableLayoutPanel { ColumnCount = 2, RowCount = 4, Dock = DockStyle.Fill, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
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
        var importButton = new Button { Text = ui.Get("Import"), AutoSize = true, Anchor = AnchorStyles.Right };
        importButton.Click += async (_, _) => await ImportAsync(importButton);
        layout.Controls.Add(choices, 0, 0);
        layout.Controls.Add(importButton, 1, 0);
        layout.Controls.Add(thumbnail, 0, 1);

        var effects = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoScroll = true };
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        effects.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sliders = Parameters.Select((key, index) =>
        {
            var caption = new Label { Text = ui.Get(key), AutoSize = true, Anchor = AnchorStyles.Left };
            var slider = new TrackBar { Minimum = 0, Maximum = 100, TickStyle = TickStyle.None,
                Dock = DockStyle.Fill, AutoSize = false, Height = 34, AccessibleName = ui.Get(key), SmallChange = 1, LargeChange = 10 };
            slider.ValueChanged += (_, _) =>
            {
                caption.Text = ui.Get(key) + " " + slider.Value;
                if (!updating)
                    PreviewSliders();
            };
            effects.Controls.Add(caption, 0, index);
            effects.Controls.Add(slider, 1, index);
            return slider;
        }).ToArray();
        layout.Controls.Add(effects, 1, 1);

        var reset = new Button { Text = ui.Get("RestoreTheme"), AutoSize = true };
        reset.Click += (_, _) => { values.Remove(ThemeId); LoadChoice(); };
        apply.Text = ui.Get("Apply");
        apply.Click += (_, _) => DialogResult = DialogResult.OK;
        var cancel = new Button { Text = ui.Get("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.AddRange([cancel, apply, reset]);
        layout.Controls.Add(actions, 0, 3);
        layout.SetColumnSpan(actions, 2);
        Controls.Add(layout);
        AcceptButton = apply;
        CancelButton = cancel;
        choices.SelectedIndexChanged += (_, _) => LoadChoice();
        choices.SelectedItem = themes.FirstOrDefault(theme => theme.Id == settings.ThemeId) ?? themes[0];
    }

    private void LoadChoice()
    {
        var theme = (ThemeDefinition)choices.SelectedItem!;
        var parameter = values.GetValueOrDefault(theme.Id) ?? theme.Defaults;
        updating = true;
        int[] current = [parameter.ImageVisibility, parameter.Zoom, parameter.OffsetX, parameter.OffsetY,
            parameter.Blur, parameter.MistOpacity, parameter.GradientStrength];
        var path = theme.Id == "imported" ? imported.Resolve(ImageReference) : theme.ThumbnailPath;
        var available = path is not null && theme.HasAssets;
        for (var index = 0; index < sliders.Length; index++)
        {
            sliders[index].Value = current[index];
            sliders[index].Enabled = available;
        }
        updating = false;
        thumbnail.Image?.Dispose();
        thumbnail.Image = null;
        thumbnail.BackColor = theme.Background;
        if (available)
        {
            try
            {
                using var image = Image.FromFile(path!);
                thumbnail.Image = new Bitmap(image, new Size(320, 180));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException
                or System.Runtime.InteropServices.ExternalException)
            {
                System.Diagnostics.Debug.WriteLine(error);
                available = false;
            }
        }
        apply.Enabled = !theme.IsImageTheme || available;
        preview(theme.Id, parameter, ImageReference);
    }

    private void PreviewSliders()
    {
        var parameters = new ThemeParameters(sliders[0].Value, sliders[4].Value, sliders[5].Value, sliders[6].Value,
            sliders[1].Value, sliders[2].Value, sliders[3].Value);
        values[ThemeId] = parameters;
        preview(ThemeId, parameters, ImageReference);
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
            ImageReference = await Task.Run(() => imported.Import(picker.FileName));
            newImports.Add(ImageReference);
            choices.SelectedItem = themes.Single(theme => theme.Id == "imported");
            LoadChoice();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException
            or System.Runtime.InteropServices.ExternalException)
        {
            MessageBox.Show(this, ui.Get("ImageError") + Environment.NewLine + error.Message, Text);
        }
        finally
        {
            importing = false;
            importButton.Enabled = choices.Enabled = true;
            LoadChoice();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs args)
    {
        base.OnFormClosing(args);
        args.Cancel |= importing;
    }

    protected override void OnFormClosed(FormClosedEventArgs args)
    {
        thumbnail.Image?.Dispose();
        thumbnail.Image = null;
        foreach (var reference in newImports)
        {
            if (DialogResult == DialogResult.OK && reference == ImageReference)
                continue;
            if (imported.Resolve(reference) is not { } path)
                continue;
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, ui.Get("SettingsError") + Environment.NewLine + error.Message, Text);
            }
        }
        base.OnFormClosed(args);
    }
}
