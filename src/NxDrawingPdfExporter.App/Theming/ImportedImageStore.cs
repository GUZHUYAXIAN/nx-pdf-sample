using System.Drawing;
using System.Drawing.Imaging;

namespace NxDrawingPdfExporter.App.Theming;

public sealed class ImportedImageStore(string directory)
{
    public const long MaximumBytes = 32L * 1024 * 1024;
    public const long MaximumPixels = 40_960_000;

    public string? Resolve(string? reference)
    {
        if (reference is not { Length: 36 } || !reference.EndsWith(".png", StringComparison.Ordinal)
            || !Guid.TryParseExact(reference[..32], "N", out _))
            return null;
        var file = Path.Combine(directory, reference);
        return File.Exists(file) ? file : null;
    }

    public string Import(string inputPath)
    {
        using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumBytes)
            throw new InvalidDataException("Image exceeds the byte limit.");
        using var image = Image.FromStream(input, useEmbeddedColorManagement: true, validateImageData: true);
        if ((long)image.Width * image.Height > MaximumPixels
            || (image.RawFormat.Guid != ImageFormat.Png.Guid && image.RawFormat.Guid != ImageFormat.Jpeg.Guid))
            throw new InvalidDataException("Unsupported image type or pixel count.");

        Orient(image);
        var scale = Math.Min(1d, 3200d / Math.Max(image.Width, image.Height));
        using var bitmap = new Bitmap(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
        using (var canvas = Graphics.FromImage(bitmap))
        {
            canvas.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            canvas.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
        }
        Directory.CreateDirectory(directory);
        var reference = Guid.NewGuid().ToString("N") + ".png";
        var path = Path.Combine(directory, reference);
        try
        {
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            bitmap.Save(output, ImageFormat.Png);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
        return reference;
    }

    private static void Orient(Image image)
    {
        const int orientation = 0x0112;
        if (!image.PropertyIdList.Contains(orientation) || image.GetPropertyItem(orientation)?.Value is not { Length: >= 2 } value)
            return;
        var operation = value[0] switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
        image.RotateFlip(operation);
    }
}
