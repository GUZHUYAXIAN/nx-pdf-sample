using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

internal sealed class SkinPreviewSettings(UiSettings saved)
{
    private readonly Dictionary<string, ThemeParameters> effects = new(saved.ThemeParametersById);

    internal ThemeParameters Get(ThemeDefinition theme) => effects.GetValueOrDefault(theme.Id) ?? theme.Defaults;
    internal void Set(string theme, ThemeParameters values) => effects[theme] = values.Normalize();
    internal void Reset(string theme) => effects.Remove(theme);
    internal UiSettings Apply(string theme, string? imageReference) => saved with
    {
        ThemeId = theme,
        ImportedImage = imageReference,
        ThemeParametersById = new(effects)
    };
}
