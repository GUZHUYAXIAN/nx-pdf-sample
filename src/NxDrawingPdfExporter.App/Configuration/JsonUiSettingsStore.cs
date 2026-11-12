using System.Text.Json;

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
            using var document = JsonDocument.Parse(File.ReadAllBytes(settingsPath));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) || schema != 1)
                return (new UiSettings(), true);
            var settings = root.Deserialize<UiSettings>(JsonOptions)!;
            var normalized = settings.Normalize();
            var recovered = settings.ThemeId != normalized.ThemeId || settings.ImportedImage != normalized.ImportedImage
                || settings.WindowBounds != normalized.WindowBounds || settings.FontScale != normalized.FontScale
                || settings.SplitterRatio != normalized.SplitterRatio
                || settings.ColumnWidths?.Count != normalized.ColumnWidths.Count;
            return (normalized, recovered);
        }
        catch (FileNotFoundException) { return (new UiSettings(), false); }
        catch (DirectoryNotFoundException) { return (new UiSettings(), false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Diagnostics.Debug.WriteLine(error);
            return (new UiSettings(), true);
        }
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
