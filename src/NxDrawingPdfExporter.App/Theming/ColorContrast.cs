namespace NxDrawingPdfExporter.App.Theming;

internal static class ColorContrast
{
    private static readonly double[] Linear = Enumerable.Range(0, 256).Select(value =>
    {
        var channel = value / 255d;
        return channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4);
    }).ToArray();

    internal static double RelativeLuminance(Color color) => Luminance(color.R, color.G, color.B);

    internal static double Luminance(int r, int g, int b) => .2126 * Linear[r] + .7152 * Linear[g] + .0722 * Linear[b];

    internal static double Ratio(Color foreground, Color background) =>
        Ratio(RelativeLuminance(foreground), RelativeLuminance(background));

    internal static double Ratio(double first, double second) => (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);

    internal static Color Over(Color background, Color overlay) => Color.FromArgb(
        Mix(background.R, overlay.R, overlay.A), Mix(background.G, overlay.G, overlay.A), Mix(background.B, overlay.B, overlay.A));

    private static int Mix(int background, int foreground, int alpha) => (background * (255 - alpha) + foreground * alpha + 127) / 255;
}

internal readonly record struct ColorBounds(Color Low, Color High)
{
    internal ColorBounds Include(Color value) => new(
        Color.FromArgb(Math.Min(Low.R, value.R), Math.Min(Low.G, value.G), Math.Min(Low.B, value.B)),
        Color.FromArgb(Math.Max(High.R, value.R), Math.Max(High.G, value.G), Math.Max(High.B, value.B)));

    internal ColorBounds Over(Color overlay)
    {
        if (overlay.A == 0)
            return this;
        if (overlay.A == 255)
            return new(overlay, overlay);
        // Bound the one-level differences between integer alpha rounding and GDI+ compositing.
        var low = ColorContrast.Over(Low, overlay);
        var high = ColorContrast.Over(High, overlay);
        return new(Color.FromArgb(Math.Max(0, low.R - 1), Math.Max(0, low.G - 1), Math.Max(0, low.B - 1)),
            Color.FromArgb(Math.Min(255, high.R + 1), Math.Min(255, high.G + 1), Math.Min(255, high.B + 1)));
    }

    internal double WorstContrast(double textLuminance)
    {
        var low = ColorContrast.RelativeLuminance(Low);
        var high = ColorContrast.RelativeLuminance(High);
        return ColorContrast.Ratio(textLuminance, Math.Clamp(textLuminance, low, high));
    }
}
