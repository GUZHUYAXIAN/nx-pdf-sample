namespace NxDrawingPdfExporter.App.Theming;

internal readonly record struct OklabColor(double L, double A, double B)
{
    internal double Hue => (Math.Atan2(B, A) * 180 / Math.PI + 360) % 360;
}

internal static class PerceptualColor
{
    // D65 matrices from Björn Ottosson's Oklab reference.
    internal static OklabColor FromSrgb(Color color)
    {
        double Linear(int value) => value <= 10 ? value / (255d * 12.92) : Math.Pow((value / 255d + .055) / 1.055, 2.4);
        var r = Linear(color.R);
        var g = Linear(color.G);
        var b = Linear(color.B);
        var l = Math.Cbrt(.4122214708 * r + .5363325363 * g + .0514459929 * b);
        var m = Math.Cbrt(.2119034982 * r + .6806995451 * g + .1073969566 * b);
        var s = Math.Cbrt(.0883024619 * r + .2817188376 * g + .6299787005 * b);
        return new(.2104542553 * l + .7936177850 * m - .0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + .4505937099 * s,
            .0259040371 * l + .7827717662 * m - .8086757660 * s);
    }

    internal static double Distance(Color first, Color second)
    {
        var x = FromSrgb(first);
        var y = FromSrgb(second);
        return Math.Sqrt(Math.Pow(x.L - y.L, 2) + Math.Pow(x.A - y.A, 2) + Math.Pow(x.B - y.B, 2));
    }

    internal static Color FromLch(double lightness, double chroma, double hue)
    {
        var angle = hue * Math.PI / 180;
        while (true)
        {
            var a = chroma * Math.Cos(angle);
            var b = chroma * Math.Sin(angle);
            var l = Math.Pow(lightness + .3963377774 * a + .2158037573 * b, 3);
            var m = Math.Pow(lightness - .1055613458 * a - .0638541728 * b, 3);
            var s = Math.Pow(lightness - .0894841775 * a - 1.2914855480 * b, 3);
            var r = 4.0767416621 * l - 3.3077115913 * m + .2309699292 * s;
            var g = -1.2684380046 * l + 2.6097574011 * m - .3413193965 * s;
            var blue = -.0041960863 * l - .7034186147 * m + 1.7076147010 * s;
            if (r is >= 0 and <= 1 && g is >= 0 and <= 1 && blue is >= 0 and <= 1)
                return Color.FromArgb(Encode(r), Encode(g), Encode(blue));
            chroma *= .9;
        }
    }

    private static int Encode(double linear) => (int)Math.Round(255 *
        (linear <= .0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1d / 2.4) - .055));
}
