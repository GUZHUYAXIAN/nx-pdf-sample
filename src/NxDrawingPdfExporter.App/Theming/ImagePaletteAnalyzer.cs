using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NxDrawingPdfExporter.App.Theming;

internal sealed record PaletteAnalysis(IReadOnlyList<Color> DominantColors, double MinimumLuminance,
    double MaximumLuminance, IReadOnlyList<ColorBounds> Bounds);

internal static class ImagePaletteAnalyzer
{
    internal static PaletteAnalysis Analyze(Bitmap bitmap, CancellationToken cancellationToken = default)
    {
        var bins = new ColorBounds[512];
        var counts = new int[bins.Length];
        var min = 1d;
        var max = 0d;
        var bounds = new Rectangle(Point.Empty, bitmap.Size);
        using var normalized = bitmap.Clone(bounds, PixelFormat.Format32bppArgb);
        var data = normalized.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[bitmap.Width * 4];
            for (var y = 0; y < bitmap.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Marshal.Copy(data.Scan0 + y * data.Stride, bytes, 0, bytes.Length);
                for (var offset = 0; offset < bytes.Length; offset += 4)
                {
                    var color = Color.FromArgb(bytes[offset + 2], bytes[offset + 1], bytes[offset]);
                    if (bytes[offset + 3] != 255)
                        color = ColorContrast.Over(Color.Black, Color.FromArgb(bytes[offset + 3], color));
                    var luminance = ColorContrast.RelativeLuminance(color);
                    min = Math.Min(min, luminance);
                    max = Math.Max(max, luminance);
                    var bin = (color.R >> 5) * 64 + (color.G >> 5) * 8 + (color.B >> 5);
                    bins[bin] = counts[bin]++ == 0 ? new(color, color) : bins[bin].Include(color);
                }
            }
        }
        finally { normalized.UnlockBits(data); }
        var occupied = Enumerable.Range(0, counts.Length).Where(index => counts[index] > 0).ToArray();
        var dominant = occupied.OrderByDescending(index => counts[index]).Take(6)
            .Select(index => ColorContrast.Over(bins[index].Low, Color.FromArgb(128, bins[index].High))).ToArray();
        return new(dominant, min, max, occupied.Select(index => bins[index]).ToArray());
    }
}
