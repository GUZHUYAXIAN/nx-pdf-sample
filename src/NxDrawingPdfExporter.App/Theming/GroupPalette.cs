using System.Collections.ObjectModel;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.Core.Input;

namespace NxDrawingPdfExporter.App.Theming;

internal sealed record GroupVisualKey(string DirectoryKey, string Label)
{
    internal static GroupVisualKey FromSource(string source, string label) =>
        new(SourceDirectoryGrouper.ParentDirectory(source), label);
}

internal readonly record struct GroupStyle(Color Tint, int Opacity, Color Marker, Color MarkerText, double Hue)
{
    internal Color Layer => Color.FromArgb(Opacity, Tint);
}

internal static class GroupPaletteResolver
{
    internal static IReadOnlyDictionary<string, GroupStyle> Resolve(PaletteAnalysis analysis, ThemeDefinition theme,
        ThemeParameters effects, IReadOnlyList<GroupVisualKey> groups, IReadOnlyDictionary<string, double> assignedHues)
    {
        var anchor = analysis.DominantColors.FirstOrDefault(theme.Accent);
        var anchorHue = PerceptualColor.FromSrgb(anchor).Hue;
        var styles = new Dictionary<string, GroupStyle>(StringComparer.OrdinalIgnoreCase);
        var keys = groups.Select(group => group.DirectoryKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var key in keys)
            if (assignedHues.TryGetValue(key, out var hue))
                styles[key] = Style(hue, effects);

        var light = effects.SurfaceMode == SurfaceMode.Light;
        var surface = light ? Color.White : Color.FromArgb(12, 18, 29);
        var basis = ColorContrast.Over(anchor, Color.FromArgb(effects.PanelOpacity * 255 / 100, surface));
        var candidates = Enumerable.Range(0, 120).Select(i => Style((anchorHue + i * 3) % 360, effects)).ToArray();
        foreach (var key in keys)
        {
            if (styles.ContainsKey(key))
                continue;
            var unused = candidates.Where(style => styles.Values.All(existing => HueDistance(existing.Hue, style.Hue) >= 1));
            var selected = unused.DefaultIfEmpty(Style((anchorHue + styles.Count * 137.507764) % 360, effects))
                .MaxBy(style =>
                {
                    var color = ColorContrast.Over(basis, Color.FromArgb(Math.Max(150, style.Opacity), style.Tint));
                    var separation = styles.Count == 0 ? 1 : styles.Values.Min(existing => PerceptualColor.Distance(color,
                        ColorContrast.Over(basis, Color.FromArgb(Math.Max(150, existing.Opacity), existing.Tint))));
                    var spacing = styles.Count == 0 ? 1 : styles.Values.Min(existing => HueDistance(existing.Hue, style.Hue)) / 180;
                    return Math.Min(separation, PerceptualColor.Distance(color, basis)) + .07 * spacing
                        - .006 * HueDistance(anchorHue, style.Hue) / 180;
                });
            styles[key] = selected;
        }
        return new ReadOnlyDictionary<string, GroupStyle>(styles);
    }

    internal static GroupStyle Style(double hue, ThemeParameters effects)
    {
        var light = effects.SurfaceMode == SurfaceMode.Light;
        var tint = PerceptualColor.FromLch(light ? .72 : .46, light ? .24 : .21, hue);
        var marker = PerceptualColor.FromLch(light ? .61 : .74, .20, hue);
        var alpha = (int)Math.Round(250 * Math.Pow(effects.GroupColorStrength / 100d, .70));
        return new(tint, alpha, marker, ThemeControlStyler.SolidText(marker), hue);
    }

    private static double HueDistance(double first, double second) =>
        Math.Min(Math.Abs(first - second), 360 - Math.Abs(first - second));
}
