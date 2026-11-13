using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Theming;

internal sealed record ThemeRenderRequest(long Revision, string? ImagePath, Size PixelSize, ThemeDefinition Theme,
    ThemeParameters Effects, bool HighContrast, TextPalette? StablePalette = null,
    IReadOnlyList<GroupVisualKey>? Groups = null, IReadOnlyDictionary<string, double>? GroupHues = null);

internal sealed class ThemeFrame(ThemeRenderRequest request, Bitmap? backdrop, SkinMaterials? materials) : IDisposable
{
    internal ThemeRenderRequest Request { get; } = request;
    internal long Revision => Request.Revision;
    internal Bitmap? Backdrop { get; } = backdrop;
    internal SkinMaterials? Materials { get; } = materials;
    internal TextPalette? Palette => Materials?.Palette;
    internal PaletteAnalysis? Analysis { get; private init; }
    internal bool IsDisposed { get; private set; }

    internal static ThemeFrame Render(ThemeRenderRequest request, CancellationToken token)
    {
        if (request.HighContrast)
            return new(request, null, null);
        if (request.ImagePath is null || request.Effects.ImageVisibility == 0)
        {
            var surface = request.Theme.Surface;
            var analysis = new PaletteAnalysis([request.Theme.Accent], 0, 1, [new(surface, surface)]);
            return Compose(request, null, analysis, true, token);
        }
        token.ThrowIfCancellationRequested();
        using var stream = new FileStream(request.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        using var source = new Bitmap(stream);
        var image = BackgroundRenderer.Render(source, request.PixelSize, request.Theme, request.Effects);
        try
        {
            var analysis = ImagePaletteAnalyzer.Analyze(image, token);
            return Compose(request, image, analysis, false, token);
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private static ThemeFrame Compose(ThemeRenderRequest request, Bitmap? image, PaletteAnalysis analysis, bool solid,
        CancellationToken token)
    {
        var effects = solid ? request.Effects with { SurfaceMode = request.Theme.IsDark ? SurfaceMode.Dark : SurfaceMode.Light } : request.Effects;
        var palette = request.StablePalette ?? SurfaceContrastResolver.Choose(analysis, effects);
        if (solid)
            palette = palette with { Body = request.Theme.Foreground, Secondary = request.Theme.Foreground,
                Selected = request.Theme.Foreground };
        var groups = GroupPaletteResolver.Resolve(analysis, request.Theme, effects, request.Groups ?? [],
            request.GroupHues ?? new Dictionary<string, double>());
        return new(request, image, new SkinMaterials(analysis.Bounds, palette, request.Theme, effects, token, groups, solid))
            { Analysis = analysis };
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        Backdrop?.Dispose();
    }
}
