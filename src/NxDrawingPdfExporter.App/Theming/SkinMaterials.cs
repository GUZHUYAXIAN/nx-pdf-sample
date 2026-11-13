using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

internal enum SkinTextRole { Body, Secondary, Success, Warning, Error }
internal enum SkinRowState { Normal, Hovered, Selected }

internal readonly record struct SkinMaterial(SurfaceContrastResult Contrast, IReadOnlyList<Color> Tints)
{
    internal Color Foreground => Contrast.Foreground;

    internal Color Composite(Color background)
    {
        var color = ColorContrast.Over(background, Contrast.Overlay);
        foreach (var tint in Tints)
            color = ColorContrast.Over(color, tint);
        return color;
    }

    internal void Paint(Graphics graphics, Rectangle bounds)
    {
        if (Contrast.EffectiveOpacity > 0)
        {
            using var overlay = new SolidBrush(Contrast.Overlay);
            graphics.FillRectangle(overlay, bounds);
        }
        foreach (var tint in Tints)
        {
            using var overlay = new SolidBrush(tint);
            graphics.FillRectangle(overlay, bounds);
        }
    }
}

internal sealed class SkinMaterials
{
    private readonly Dictionary<string, SkinMaterial[]> groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly SkinMaterial[] neutral;
    private readonly SkinMaterial[] status;

    internal SkinMaterials(IReadOnlyList<ColorBounds> bounds, TextPalette palette, ThemeDefinition theme,
        ThemeParameters effects, CancellationToken token, IReadOnlyDictionary<string, GroupStyle>? styles = null,
        bool solid = false)
    {
        Palette = palette;
        Styles = styles ?? new Dictionary<string, GroupStyle>();
        var dark = effects.SurfaceMode == SurfaceMode.Dark;
        var preferred = solid ? theme.Surface : ColorContrast.Over(dark ? Color.FromArgb(12, 18, 29) : Color.White,
            Color.FromArgb(dark ? 18 : 35, theme.Accent));

        SkinMaterial Make(Color text, int opacity, Color[] tints, double desired = 7)
        {
            token.ThrowIfCancellationRequested();
            var result = SurfaceContrastResolver.Resolve(bounds, text, preferred, opacity, tints, desired);
            if (result.Foreground.ToArgb() != text.ToArgb() && tints.Length > 0)
            {
                var darkMaterial = ColorContrast.Ratio(text, Color.Black) >= ColorContrast.Ratio(text, Color.White);
                // Extreme manual text colors need a narrower luminance range. Keep the
                // group tint's alpha and hue, moving the tint toward the protective material.
                for (var protection = 32; protection < 256; protection += 32)
                {
                    var adjusted = tints.Select(tint => Color.FromArgb(tint.A, ColorContrast.Over(tint,
                        Color.FromArgb(protection, darkMaterial ? Color.Black : Color.White)))).ToArray();
                    var guarded = SurfaceContrastResolver.Resolve(bounds, text, preferred, opacity, adjusted, desired);
                    if (guarded.Foreground.ToArgb() == text.ToArgb())
                        return new(guarded with { Protected = true }, adjusted);
                }
            }
            return new(result, tints);
        }

        var requested = solid ? 100 : effects.PanelOpacity;

        SkinMaterial[] MakeRows(Color? tint)
        {
            Color[] layers = tint is { A: > 0 } color ? [color] : [];
            return [
                Make(palette.Body, requested, layers),
                Make(palette.Body, requested, [.. layers, Color.FromArgb(15, theme.Accent)]),
                Make(palette.Selected, requested, [.. layers, Color.FromArgb(35, theme.Accent)])
            ];
        }

        Header = Make(palette.Secondary, Math.Min(100, requested + 12), []);
        Editor = Make(palette.Body, Math.Min(100, requested + 18), []);
        neutral = MakeRows(null);
        foreach (var pair in Styles)
            groups.Add(pair.Key, MakeRows(pair.Value.Layer));
        status = [Make(palette.Success, requested, []), Make(palette.Warning, requested, []), Make(palette.Error, requested, [])];
        Protected = Header.Contrast.Protected || Editor.Contrast.Protected
            || neutral.Concat(groups.Values.SelectMany(row => row)).Concat(status).Any(material => material.Contrast.Protected);
    }

    internal TextPalette Palette { get; }
    internal IReadOnlyDictionary<string, GroupStyle> Styles { get; }
    internal SkinMaterial Header { get; }
    internal SkinMaterial Editor { get; }
    internal SkinMaterial Body => neutral[0];
    internal bool Protected { get; }

    internal SkinMaterial Row(GroupVisualKey? group, SkinRowState state) =>
        (group is not null && groups.TryGetValue(group.DirectoryKey, out var rows) ? rows : neutral)[(int)state];

    internal SkinMaterial Status(SkinTextRole role) => status[(int)role - (int)SkinTextRole.Success];
}
