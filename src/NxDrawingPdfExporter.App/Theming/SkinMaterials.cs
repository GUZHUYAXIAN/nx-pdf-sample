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
    internal static readonly Color[] GroupColors =
    [
        Color.FromArgb(229, 240, 252), Color.FromArgb(230, 244, 233), Color.FromArgb(249, 235, 225),
        Color.FromArgb(239, 231, 250), Color.FromArgb(251, 242, 215), Color.FromArgb(243, 231, 239)
    ];
    private readonly SkinMaterial[,] rows = new SkinMaterial[GroupColors.Length + 1, 3];
    private readonly SkinMaterial[] status = new SkinMaterial[3];

    internal SkinMaterials(IReadOnlyList<ColorBounds> bounds, TextPalette palette, ThemeDefinition theme,
        ThemeParameters effects, CancellationToken token)
    {
        Palette = palette;
        var dark = effects.SurfaceMode == SurfaceMode.Dark;
        var preferred = ColorContrast.Over(dark ? Color.FromArgb(12, 18, 29) : Color.White,
            Color.FromArgb(dark ? 18 : 35, theme.Accent));
        SkinMaterial Make(Color text, int opacity, Color[] tints, double desired = 7)
        {
            token.ThrowIfCancellationRequested();
            var resolved = SurfaceContrastResolver.Resolve(bounds, text, preferred, opacity, tints, desired);
            while (resolved.Foreground.ToArgb() != text.ToArgb() && tints.Any(tint => tint.A > 0))
            {
                tints = tints.Select(tint => Color.FromArgb(tint.A / 2, tint)).ToArray();
                resolved = SurfaceContrastResolver.Resolve(bounds, text, preferred, opacity, tints, desired) with { Protected = true };
            }
            return new(resolved, tints);
        }

        var requested = effects.PanelOpacity;
        Header = Make(palette.Secondary, Math.Min(100, requested + 12), [], 7);
        Editor = Make(palette.Body, Math.Min(100, requested + 18), []);
        for (var group = 0; group <= GroupColors.Length; group++)
        {
            var groupLayers = group == 0 ? Array.Empty<Color>() : new[] { Color.FromArgb(29, GroupColors[group - 1]) };
            rows[group, 0] = Make(palette.Body, requested, groupLayers);
            rows[group, 1] = Make(palette.Body, requested, [.. groupLayers, Color.FromArgb(24, theme.Accent)]);
            rows[group, 2] = Make(palette.Selected, requested, [.. groupLayers, Color.FromArgb(66, theme.Accent)]);
        }
        status[0] = Make(palette.Success, requested, []);
        status[1] = Make(palette.Warning, requested, []);
        status[2] = Make(palette.Error, requested, []);
        Protected = Header.Contrast.Protected || Editor.Contrast.Protected
            || rows.Cast<SkinMaterial>().Concat(status).Any(material => material.Contrast.Protected);
    }

    internal TextPalette Palette { get; }
    internal SkinMaterial Header { get; }
    internal SkinMaterial Editor { get; }
    internal SkinMaterial Body => rows[0, 0];
    internal bool Protected { get; }

    internal SkinMaterial Row(Color group, SkinRowState state)
    {
        var index = group.IsEmpty ? -1 : Array.FindIndex(GroupColors, color => color.ToArgb() == group.ToArgb());
        return rows[index + 1, (int)state];
    }

    internal SkinMaterial Status(SkinTextRole role) => status[(int)role - (int)SkinTextRole.Success];
}
