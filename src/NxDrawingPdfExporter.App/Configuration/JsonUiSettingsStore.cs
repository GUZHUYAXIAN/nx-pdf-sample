using System.Drawing;
using System.Text.Json;
using NxDrawingPdfExporter.App.Layout;

namespace NxDrawingPdfExporter.App.Configuration;

public sealed class JsonUiSettingsStore(string settingsPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string LocalRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxDrawingPdfExporter");

    public (UiSettings Settings, bool Recovered) Load()
    {
        try
        {
            return Read(File.ReadAllBytes(settingsPath));
        }
        catch (FileNotFoundException) { return (new UiSettings(), false); }
        catch (DirectoryNotFoundException) { return (new UiSettings(), false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Diagnostics.Debug.WriteLine(error);
            return (new UiSettings(), true);
        }
    }

    internal static (UiSettings Settings, bool Recovered) Read(byte[] json)
    {
        using var document = JsonDocument.Parse(json.AsMemory(json.AsSpan().StartsWith(System.Text.Encoding.UTF8.Preamble) ? 3 : 0));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema is not (1 or 2))
            return (new(), true);

        var recovered = false;
        T Field<T>(JsonElement container, string name, T fallback)
        {
            if (!container.TryGetProperty(name, out var value))
                return fallback;
            try
            {
                var parsed = value.Deserialize<T>(JsonOptions);
                if (parsed is null && fallback is not null)
                    recovered = true;
                return parsed ?? fallback;
            }
            catch (JsonException) { recovered = true; return fallback; }
        }

        Dictionary<string, T> Map<T>(string name, Func<JsonElement, T> read)
        {
            var values = new Dictionary<string, T>();
            if (!root.TryGetProperty(name, out var map))
                return values;
            if (map.ValueKind != JsonValueKind.Object)
            {
                recovered = true;
                return values;
            }
            foreach (var property in map.EnumerateObject())
            {
                try { values[property.Name] = read(property.Value); }
                catch (JsonException) { recovered = true; }
            }
            return values;
        }

        ThemeParameters ReadEffects(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
                throw new JsonException();
            var defaults = new ThemeParameters();
            return new ThemeParameters(Field(value, "imageVisibility", defaults.ImageVisibility), Field(value, "blur", defaults.Blur),
                Field(value, "mistOpacity", defaults.MistOpacity), Field(value, "gradientStrength", defaults.GradientStrength),
                Field(value, "zoom", defaults.Zoom), Field(value, "offsetX", defaults.OffsetX), Field(value, "offsetY", defaults.OffsetY),
                Field(value, "panelOpacity", defaults.PanelOpacity), Field(value, "surfaceMode", defaults.SurfaceMode),
                Field(value, "automaticTextColor", defaults.AutomaticTextColor), Field<int?>(value, "manualTextColorArgb", null),
                Field(value, "gradientDirection", defaults.GradientDirection),
                Field(value, "groupColorStrength", defaults.GroupColorStrength));
        }

        var heights = new WorkspaceHeights();
        if (schema == 2 && root.TryGetProperty("workspaceHeights", out var heightFields))
        {
            if (heightFields.ValueKind == JsonValueKind.Object)
                heights = new(Field(heightFields, "preflightDip", heights.PreflightDip), Field(heightFields, "resultsDip", heights.ResultsDip));
            else
                recovered = true;
        }
        var settings = new UiSettings
        {
            Language = Field(root, "language", "zh-CN"),
            ThemeId = Field(root, "themeId", "cloud"),
            ImportedImage = Field<string?>(root, "importedImage", null),
            FontScale = Field(root, "fontScale", 100),
            WindowBounds = Field(root, "windowBounds", Rectangle.Empty),
            SplitterRatio = Field(root, "splitterRatio", .58),
            ColumnWidths = Map("columnWidths", value => value.Deserialize<int>(JsonOptions)),
            ThemeParametersById = Map("themeParametersById", ReadEffects),
            WorkspaceHeights = heights
        };
        var normalized = settings.Normalize();
        return (normalized, recovered || settings.Language != normalized.Language || settings.ThemeId != normalized.ThemeId
            || settings.ImportedImage != normalized.ImportedImage || settings.WindowBounds != normalized.WindowBounds
            || settings.FontScale != normalized.FontScale || settings.WorkspaceHeights != normalized.WorkspaceHeights
            || settings.SplitterRatio != normalized.SplitterRatio || settings.ColumnWidths.Count != normalized.ColumnWidths.Count
            || settings.ThemeParametersById.Any(pair => pair.Value != normalized.ThemeParametersById[pair.Key]));
    }

    public void Save(UiSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".ui-settings.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings.Normalize(), JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
