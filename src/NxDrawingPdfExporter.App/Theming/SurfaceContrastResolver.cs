using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

internal sealed record TextPalette(Color Body, Color Secondary, Color Selected, Color Success, Color Warning, Color Error);

internal readonly record struct SurfaceContrastResult(Color Foreground, Color OverlayColor, int EffectiveOpacity,
    double WorstContrast, bool Protected)
{
    internal Color Overlay => Color.FromArgb(EffectiveOpacity * 255 / 100, OverlayColor);
}

internal static class SurfaceContrastResolver
{
    internal static SurfaceContrastResult Resolve(IReadOnlyList<Color> backgrounds, Color foreground,
        Color preferredOverlay, int requestedOpacity, double desiredRatio = 7, double minimumRatio = 4.5) =>
        Resolve(backgrounds.Select(color => new ColorBounds(color, color)).ToArray(), foreground,
            preferredOverlay, requestedOpacity, [], desiredRatio, minimumRatio);

    internal static SurfaceContrastResult Resolve(IReadOnlyList<ColorBounds> backgrounds, Color foreground,
        Color preferredOverlay, int requestedOpacity, IReadOnlyList<Color>? tints = null,
        double desiredRatio = 7, double minimumRatio = 4.5)
    {
        tints ??= [];
        requestedOpacity = Math.Clamp(requestedOpacity, 0, 100);
        var luminance = ColorContrast.RelativeLuminance(foreground);
        var materials = new[] { preferredOverlay, Color.Black, Color.White }.DistinctBy(color => color.ToArgb()).ToArray();
        foreach (var target in new[] { desiredRatio, minimumRatio })
        {
            for (var opacity = requestedOpacity; opacity <= 100; opacity++)
            {
                foreach (var material in materials)
                {
                    var overlay = Color.FromArgb(opacity * 255 / 100, material);
                    var worst = double.PositiveInfinity;
                    foreach (var background in backgrounds)
                    {
                        var composite = background.Over(overlay);
                        foreach (var tint in tints)
                            composite = composite.Over(tint);
                        worst = Math.Min(worst, composite.WorstContrast(luminance));
                        if (worst < target)
                            break;
                    }
                    if (worst >= target)
                        return new(foreground, material, opacity, worst, opacity > requestedOpacity || material != preferredOverlay);
                }
            }
        }

        // Highly saturated state tints can preclude a safe manual gray. Keep the state
        // tint and use a verified black or white reading color for that surface.
        return Resolve(backgrounds, luminance > .18 ? Color.White : Color.Black,
            luminance > .18 ? Color.Black : Color.White, requestedOpacity, tints, minimumRatio, 1) with { Protected = true };
    }

    internal static TextPalette Choose(PaletteAnalysis analysis, ThemeParameters effects)
    {
        var dark = effects.SurfaceMode == SurfaceMode.Dark;
        var baseText = dark ? Color.White : Color.Black;
        Color Toned(Color color, int alpha) => ColorContrast.Over(baseText, Color.FromArgb(alpha, color));
        var candidates = analysis.DominantColors
            .OrderByDescending(color => Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)))
            .Select(color => Toned(color, dark ? 32 : 48));
        var body = !effects.AutomaticTextColor && effects.ManualTextColorArgb is int manual
            ? Color.FromArgb(255, Color.FromArgb(manual))
            : candidates.FirstOrDefault(baseText);
        return new(body, body, body,
            Toned(Color.FromArgb(22, 184, 110), dark ? 122 : 119),
            Toned(Color.FromArgb(241, 158, 18), dark ? 125 : 107),
            Toned(Color.FromArgb(245, 69, 92), dark ? 115 : 122));
    }
}
