using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Runtime.Detection;

public sealed class EnvironmentNxCandidateSource : INxInstallationCandidateSource
{
    public NxCandidateSource? Source => NxCandidateSource.Environment;

    private readonly Func<string, string?> environmentReader;

    public EnvironmentNxCandidateSource(Func<string, string?>? environmentReader = null)
    {
        this.environmentReader = environmentReader ?? Environment.GetEnvironmentVariable;
    }

    public Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = new List<NxInstallationCandidate>();
        AddIfPresent(candidates, environmentReader("UGII_BASE_DIR"));

        string? ugiiRoot = environmentReader("UGII_ROOT_DIR");
        if (!string.IsNullOrWhiteSpace(ugiiRoot))
        {
            string trimmed = ugiiRoot.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
                trimmed = trimmed[1..^1].Trim();
            trimmed = Path.TrimEndingDirectorySeparator(trimmed);
            string? parent = string.Equals(Path.GetFileName(trimmed), "UGII", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(trimmed)
                : trimmed;
            AddIfPresent(candidates, parent);
        }

        return Task.FromResult<IReadOnlyList<NxInstallationCandidate>>(candidates);
    }

    private static void AddIfPresent(List<NxInstallationCandidate> candidates, string? rootDirectory)
    {
        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            candidates.Add(new NxInstallationCandidate(rootDirectory, NxCandidateSource.Environment));
        }
    }
}
