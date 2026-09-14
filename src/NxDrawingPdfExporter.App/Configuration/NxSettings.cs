namespace NxDrawingPdfExporter.App.Configuration;

public sealed record NxSettings(int SchemaVersion = 1, string? NxRootDirectory = null);

public enum NxSettingsLoadStatus
{
    Missing,
    Loaded,
    InvalidJson,
    UnsupportedSchema,
    ReadFailed,
}

public sealed record NxSettingsLoadResult(
    NxSettings Settings,
    NxSettingsLoadStatus Status,
    string? Diagnostic = null);
