using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

internal sealed record ThemeRenderRequest(long Revision, string? ImagePath, Size PixelSize, ThemeDefinition Theme,
    ThemeParameters Effects, bool HighContrast, TextPalette? StablePalette = null);

internal sealed class ThemeFrame(ThemeRenderRequest request, Bitmap? backdrop, SkinMaterials? materials) : IDisposable
{
    internal ThemeRenderRequest Request { get; } = request;
    internal long Revision => Request.Revision;
    internal Bitmap? Backdrop { get; } = backdrop;
    internal SkinMaterials? Materials { get; } = materials;
    internal TextPalette? Palette => Materials?.Palette;
    internal bool IsDisposed { get; private set; }

    internal static ThemeFrame Render(ThemeRenderRequest request, CancellationToken token)
    {
        if (request.HighContrast || request.ImagePath is null || request.Effects.ImageVisibility == 0)
            return new(request, null, null);
        token.ThrowIfCancellationRequested();
        using var stream = new FileStream(request.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        using var source = new Bitmap(stream);
        var image = BackgroundRenderer.Render(source, request.PixelSize, request.Theme, request.Effects);
        try
        {
            var analysis = ImagePaletteAnalyzer.Analyze(image, token);
            var palette = request.StablePalette ?? SurfaceContrastResolver.Choose(analysis, request.Effects);
            return new(request, image, new SkinMaterials(analysis.Bounds, palette, request.Theme, request.Effects, token));
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        Backdrop?.Dispose();
    }
}
