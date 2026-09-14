using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NxDrawingPdfExporter.App.Configuration;

public sealed class JsonNxSettingsStore : INxSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string settingsPath;
    private readonly Action<string, string> moveIntoPlace;

    public JsonNxSettingsStore(string settingsPath)
        : this(settingsPath, (temp, target) => File.Move(temp, target, overwrite: true))
    {
    }

    internal JsonNxSettingsStore(string settingsPath, Action<string, string> moveIntoPlace)
    {
        this.settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
        this.moveIntoPlace = moveIntoPlace ?? throw new ArgumentNullException(nameof(moveIntoPlace));
    }

    public static JsonNxSettingsStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NxDrawingPdfExporter",
        "settings.json"));

    public async Task<NxSettingsLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(settingsPath))
        {
            return new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.Missing);
        }

        try
        {
            byte[] json = await File.ReadAllBytesAsync(settingsPath, cancellationToken);
            ReadOnlyMemory<byte> content = WithoutUtf8Bom(json);
            using JsonDocument document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("schemaVersion", out JsonElement schemaElement)
                || schemaElement.ValueKind != JsonValueKind.Number
                || !schemaElement.TryGetInt32(out int schemaVersion))
            {
                return new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.InvalidJson);
            }

            if (schemaVersion != 1)
            {
                return new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.UnsupportedSchema);
            }

            NxSettings? settings = JsonSerializer.Deserialize<NxSettings>(content.Span, JsonOptions);
            return settings is null
                ? new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.InvalidJson)
                : new NxSettingsLoadResult(settings, NxSettingsLoadStatus.Loaded);
        }
        catch (JsonException error)
        {
            return new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.InvalidJson, error.GetType().Name);
        }
        catch (IOException error)
        {
            return new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.ReadFailed, error.GetType().Name);
        }
        catch (UnauthorizedAccessException error)
        {
            return new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.ReadFailed, error.GetType().Name);
        }
    }

    public async Task SaveAsync(NxSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(settings));
        }

        string directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("NX settings path must include a directory.");
        Directory.CreateDirectory(directory);
        string temp = Path.Combine(directory, $".settings.json.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(settings, JsonOptions) + "\n");
            await using (var stream = new FileStream(
                temp,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            moveIntoPlace(temp, settingsPath);
        }
        finally
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static ReadOnlyMemory<byte> WithoutUtf8Bom(byte[] json)
    {
        ReadOnlySpan<byte> bom = Encoding.UTF8.Preamble;
        return json.AsSpan().StartsWith(bom) ? json.AsMemory(bom.Length) : json;
    }
}
