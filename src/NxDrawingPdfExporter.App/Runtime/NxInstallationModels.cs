using System.Collections.Generic;

namespace NxDrawingPdfExporter.App.Runtime;

public enum NxCandidateSource
{
    SavedConfiguration,
    Registry,
    InstalledApplication,
    Environment,
    Manual,
}

public enum NxDetectionIssueCode
{
    InvalidPath,
    RootMissing,
    LauncherMissing,
    NxOpenMissing,
    VersionUnreadable,
    UnsupportedVersion,
    CandidateSourceFailed,
    SettingsUnavailable,
}

public sealed record NxInstallationCandidate(string RootDirectory, NxCandidateSource Source);

public sealed record NxInstallation(
    string RootDirectory,
    string LauncherPath,
    string NxOpenPath,
    string DetectedVersion,
    IReadOnlyList<NxCandidateSource> Sources);

public sealed record NxDetectionIssue(
    NxDetectionIssueCode Code,
    string UserMessage,
    NxCandidateSource? Source = null,
    string? Diagnostic = null);

public sealed record NxInstallationDiscoveryResult(
    IReadOnlyList<NxInstallation> Installations,
    IReadOnlyList<NxDetectionIssue> Issues,
    NxInstallation? SelectedInstallation)
{
    public bool RequiresSelection => Installations.Count > 1 && SelectedInstallation is null;

    public bool IsReady => SelectedInstallation is not null;
}
