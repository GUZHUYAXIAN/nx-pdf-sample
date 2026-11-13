using System.Drawing;
using NxDrawingPdfExporter.App.Layout;

namespace NxDrawingPdfExporter.App.Configuration;

public enum SurfaceMode { Dark, Light }
public enum SkinGradientDirection { LeftToRight, RightToLeft, TopToBottom, BottomToTop }

public sealed record ThemeParameters(int ImageVisibility = 68, int Blur = 0, int MistOpacity = 20,
    int GradientStrength = 25, int Zoom = 0, int OffsetX = 50, int OffsetY = 50,
    int PanelOpacity = 30, SurfaceMode SurfaceMode = SurfaceMode.Dark, bool AutomaticTextColor = true,
    int? ManualTextColorArgb = null, SkinGradientDirection GradientDirection = SkinGradientDirection.LeftToRight, int GroupColorStrength = 65)
{
    public ThemeParameters Normalize() => new(Clamp(ImageVisibility), Clamp(Blur), Clamp(MistOpacity),
        Clamp(GradientStrength), Clamp(Zoom), Clamp(OffsetX), Clamp(OffsetY), Clamp(PanelOpacity),
        Enum.IsDefined(SurfaceMode) ? SurfaceMode : SurfaceMode.Dark, AutomaticTextColor, ManualTextColorArgb,
        Enum.IsDefined(GradientDirection) ? GradientDirection : SkinGradientDirection.LeftToRight, Clamp(GroupColorStrength));

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}

public sealed record UiSettings
{
    public int SchemaVersion { get; init; } = 2;
    public WorkspaceHeights WorkspaceHeights { get; init; } = new();
    public string Language { get; init; } = "zh-CN";
    public string ThemeId { get; init; } = "cloud";
    public Dictionary<string, ThemeParameters> ThemeParametersById { get; init; } = new();
    public string? ImportedImage { get; init; }
    public int FontScale { get; init; } = 100;
    public Rectangle WindowBounds { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double SplitterRatio { get; init; } = .58;
    public Dictionary<string, int> ColumnWidths { get; init; } = new();

    public UiSettings Normalize()
    {
        if (SchemaVersion is not (1 or 2))
            return new UiSettings();
        var defaults = new UiSettings();
        return this with
        {
            SchemaVersion = 2,
            WorkspaceHeights = SchemaVersion == 1 ? new() : (WorkspaceHeights ?? new()).Normalize(),
            Language = Language == "en" ? "en" : "zh-CN",
            ThemeId = ThemeId is "cloud" or "graphite" or "lake" or "pine" or "sand" or "rem" or "inori" or "frieren" or "roxy" or "imported" ? ThemeId : "cloud",
            ThemeParametersById = (ThemeParametersById ?? new()).Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value.Normalize()),
            ImportedImage = ImportedImage is { Length: 36 } && ImportedImage.EndsWith(".png", StringComparison.Ordinal)
                && Guid.TryParseExact(ImportedImage[..32], "N", out _) ? ImportedImage : null,
            FontScale = FontScale is >= 85 and <= 135 ? FontScale : defaults.FontScale,
            WindowBounds = WindowBounds.Width is >= 320 and <= 16000 && WindowBounds.Height is >= 300 and <= 16000
                && WindowBounds.X is > -50000 and < 50000 && WindowBounds.Y is > -50000 and < 50000
                ? WindowBounds : Rectangle.Empty,
            SplitterRatio = SplitterRatio is >= .15 and <= .85 ? SplitterRatio : defaults.SplitterRatio,
            ColumnWidths = (ColumnWidths ?? new()).Where(x => x.Value is >= 32 and <= 1600).ToDictionary()
        };
    }
}
