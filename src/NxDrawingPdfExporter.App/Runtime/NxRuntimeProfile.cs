namespace NxDrawingPdfExporter.App.Runtime;

/// <summary>Only locally verified component tuples are accepted; directory names are not versions.</summary>
public sealed record NxRuntimeProfile(
    string Label, string BinDirectoryName, string LauncherName, string NxOpenVersion,
    string UfVersion, string UtilitiesVersion, string WorkerRelativePath)
{
    public static NxRuntimeProfile Nx10 { get; } = new("NX 10", "UGII", "run_managed.exe",
        "10.0.0.24", "10.0.0.23", "4.0.0.0", @"worker\nx10\NxDrawingPdfExporter.Worker.exe");
    public static NxRuntimeProfile Nx2506 { get; } = new("NX 2506", "NXBIN", "run_dotnet_nxopen.exe",
        "2506.0.0.0", "2506.1700.0.0", "2506.0.0.0", @"worker\nx2506\NxDrawingPdfExporter.Worker.Nx2506.exe");
    public static IReadOnlyList<NxRuntimeProfile> Supported { get; } = Array.AsReadOnly(new[] { Nx10, Nx2506 });
}
