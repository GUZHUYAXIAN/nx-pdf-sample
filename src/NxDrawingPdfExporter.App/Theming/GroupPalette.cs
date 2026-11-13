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
        Color Composite(GroupStyle style) => ColorContrast.Over(basis, Color.FromArgb(Math.Max(150, style.Opacity), style.Tint));
        var candidateColors = candidates.Select(Composite).ToArray();
        var separation = Enumerable.Repeat(1d, candidates.Length).ToArray();
        var spacing = Enumerable.Repeat(180d, candidates.Length).ToArray();
        void Include(GroupStyle style)
        {
            var color = Composite(style);
            for (var index = 0; index < candidates.Length; index++)
            {
                separation[index] = Math.Min(separation[index], PerceptualColor.Distance(color, candidateColors[index]));
                spacing[index] = Math.Min(spacing[index], HueDistance(style.Hue, candidates[index].Hue));
            }
        }
        foreach (var hue in assignedHues.Values.Distinct())
            Include(Style(hue, effects));
        foreach (var key in keys)
        {
            if (styles.ContainsKey(key))
                continue;
            var available = Enumerable.Range(0, candidates.Length).Where(index => spacing[index] >= 1).ToArray();
            var selected = available.Length == 0 ? Style((anchorHue + styles.Count * 137.507764) % 360, effects)
                : candidates[available.MaxBy(index => Math.Min(separation[index], PerceptualColor.Distance(candidateColors[index], basis))
                    + .07 * spacing[index] / 180 - .006 * HueDistance(anchorHue, candidates[index].Hue) / 180)];
            styles[key] = selected;
            Include(selected);
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
