using System.Drawing;
using System.Text.Json;
using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

public sealed record ThemeDefinition(string Id, Color Background, Color Surface, Color Foreground, Color Accent,
    ThemeParameters Defaults, string? ImagePath = null, string? ThumbnailPath = null)
{
    public bool IsDark => Surface.GetBrightness() < .4f;
    public bool IsImageTheme => ImagePath is not null || Id == "imported";
    public bool HasAssets => ImagePath is null || (File.Exists(ImagePath) && File.Exists(ThumbnailPath));
}

public static class ThemeCatalog
{
    private sealed record SkinFile(string Id, string Background, string Surface, string Foreground, string Accent, ThemeParameters Parameters);

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private static readonly (string Id, string Background, string Surface, string Foreground, string Accent)[] Colors =
    [
        ("cloud", "#E7EDF5", "#FFFFFF", "#20304A", "#315F91"),
        ("graphite", "#171D26", "#242D39", "#F2F5FB", "#85B7F2"),
        ("lake", "#DEEDF7", "#F2F8FC", "#183C54", "#286B92"),
        ("pine", "#DEEAE3", "#F2F8F4", "#243F33", "#326B53"),
        ("sand", "#F1E8DB", "#FDF8F0", "#493B2D", "#81572C")
    ];

    public static IReadOnlyList<ThemeDefinition> Load(string baseDirectory)
    {
        var themes = Colors.Select(item => Make(item.Id, item.Background, item.Surface, item.Foreground, item.Accent, new ThemeParameters(0)))
            .ToList();
        var directory = Path.Combine(baseDirectory, "assets", "themes");
        foreach (var id in new[] { "rem", "inori", "frieren", "roxy" })
        {
            var image = Path.Combine(directory, id + ".png");
            var thumbnail = Path.Combine(directory, id + "-thumb.png");
            try
            {
                var definition = JsonSerializer.Deserialize<SkinFile>(File.ReadAllBytes(Path.Combine(directory, id + ".json")), Options);
                if (definition?.Id != id || definition.Parameters is null)
                    throw new InvalidDataException("Invalid theme definition.");
                themes.Add(Make(id, definition.Background, definition.Surface, definition.Foreground, definition.Accent,
                    definition.Parameters.Normalize(), image, thumbnail));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException)
            {
                System.Diagnostics.Debug.WriteLine(error);
                themes.Add(themes[0] with { Id = id, ImagePath = image, ThumbnailPath = null });
            }
        }
        themes.Add(themes[0] with { Id = "imported", Defaults = new ThemeParameters() });
        return themes.AsReadOnly();
    }

    private static ThemeDefinition Make(string id, string background, string surface, string foreground, string accent,
        ThemeParameters parameters, string? image = null, string? thumbnail = null) =>
        new(id, ColorTranslator.FromHtml(background), ColorTranslator.FromHtml(surface), ColorTranslator.FromHtml(foreground),
            ColorTranslator.FromHtml(accent), parameters, image, thumbnail);
}
