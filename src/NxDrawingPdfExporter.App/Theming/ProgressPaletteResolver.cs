using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

internal readonly record struct ProgressVisualPalette(Color Track, Color Completed, Color Active, Color Outline, Color Text);

internal static class ProgressPaletteResolver
{
    internal static ProgressVisualPalette Resolve(PaletteAnalysis? analysis, ThemeDefinition theme,
        ThemeParameters effects, TextPalette? text, bool highContrast)
    {
        if (highContrast)
            return new(SystemColors.Window, SystemColors.Highlight, SystemColors.WindowText, SystemColors.WindowText, SystemColors.WindowText);
        var dark = analysis is null ? theme.IsDark : effects.SurfaceMode == SurfaceMode.Dark;
        var anchor = PerceptualColor.FromSrgb(analysis?.DominantColors.FirstOrDefault(theme.Accent) ?? theme.Accent).Hue;
        var track = PerceptualColor.FromLch(dark ? .24 : .94, .035, anchor);
        var candidates = Enumerable.Range(0, 5).Select(i => PerceptualColor.FromLch(dark ? .79 - i * .03 : .53 - i * .045,
            .16, anchor)).ToArray();
        var complete = candidates.FirstOrDefault(color => ColorContrast.Ratio(color, track) >= 3, ThemeControlStyler.SolidText(track));
        var active = Enumerable.Range(0, 6)
            .Select(i => PerceptualColor.FromLch(dark ? .73 : .49, .18, (anchor + 65 + i * 43) % 360))
            .Where(color => ColorContrast.Ratio(color, track) >= 3)
            .DefaultIfEmpty(ThemeControlStyler.SolidText(track))
            .MaxBy(color => PerceptualColor.Distance(color, complete));
        var ink = text?.Body ?? theme.Foreground;
        return new(track, complete, active, ThemeControlStyler.SolidText(track),
            ColorContrast.Ratio(ink, track) >= 4.5 ? ink : ThemeControlStyler.SolidText(track));
    }
}
