using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

public static class BackgroundRenderer
{
    public static Bitmap Render(Bitmap source, Size target, ThemeDefinition theme, ThemeParameters parameters)
    {
        var output = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppPArgb);
        try
        {
            using var canvas = Graphics.FromImage(output);
            canvas.Clear(theme.Background);
            var values = parameters.Normalize();
            if (values.ImageVisibility == 0)
                return output;

            // Downsample and reconstruct the cropped image once, then blend it as a layer.
            var reduction = 1 + values.Blur / 9f;
            using var layer = new Bitmap(Math.Max(1, (int)(target.Width / reduction)), Math.Max(1, (int)(target.Height / reduction)));
            using (var layerCanvas = Graphics.FromImage(layer))
            {
                layerCanvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var scale = Math.Max((double)layer.Width / source.Width, (double)layer.Height / source.Height) * (1 + values.Zoom / 100d);
                var width = (float)(source.Width * scale);
                var height = (float)(source.Height * scale);
                layerCanvas.DrawImage(source, new RectangleF((layer.Width - width) * values.OffsetX / 100f,
                    (layer.Height - height) * values.OffsetY / 100f, width, height));
            }
            canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = values.ImageVisibility / 100f });
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            var bounds = new Rectangle(Point.Empty, target);
            canvas.DrawImage(layer, bounds, 0, 0, layer.Width, layer.Height, GraphicsUnit.Pixel, attributes);
            if (values.MistOpacity > 0)
            {
                using var mist = new SolidBrush(Color.FromArgb(values.MistOpacity * 255 / 100, theme.Surface));
                canvas.FillRectangle(mist, bounds);
            }
            if (values.GradientStrength > 0)
            {
                using var gradient = new LinearGradientBrush(bounds,
                    Color.FromArgb(values.GradientStrength * 255 / 100, theme.Background),
                    Color.FromArgb(0, theme.Background), LinearGradientMode.Horizontal);
                canvas.FillRectangle(gradient, bounds);
            }
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
}
