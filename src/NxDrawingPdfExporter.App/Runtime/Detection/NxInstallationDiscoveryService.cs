using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Runtime.Detection;

public sealed class NxInstallationDiscoveryService : INxInstallationDiscoveryService
{
    private readonly IReadOnlyList<INxInstallationCandidateSource> sources;
    private readonly NxInstallationValidator validator;

    public NxInstallationDiscoveryService(
        IEnumerable<INxInstallationCandidateSource> sources,
        NxInstallationValidator validator)
    {
        this.sources = sources?.ToArray() ?? throw new ArgumentNullException(nameof(sources));
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<NxInstallationDiscoveryResult> DetectAsync(
        string? savedRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = new List<NxInstallationCandidate>();
        var sourceIssues = new List<NxDetectionIssue>();
        if (!string.IsNullOrWhiteSpace(savedRoot))
        {
            candidates.Add(new NxInstallationCandidate(savedRoot, NxCandidateSource.SavedConfiguration));
        }

        foreach (INxInstallationCandidateSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                candidates.AddRange(await source.GetCandidatesAsync(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error) when (IsExpectedSourceError(error))
            {
                sourceIssues.Add(new NxDetectionIssue(
                    NxDetectionIssueCode.CandidateSourceFailed,
                    "一个 NX 安装来源无法读取，已继续检查其他来源。",
                    Source: source.Source,
                    Diagnostic: error.GetType().Name));
            }
        }

        NxInstallationDiscoveryResult validated = validator.ValidateAndDeduplicate(candidates);
        return validated with
        {
            Issues = sourceIssues.Concat(validated.Issues).ToArray(),
            SelectedInstallation = Select(validated.Installations, savedRoot),
        };
    }

    public NxInstallationDiscoveryResult ValidateManual(string rootDirectory)
    {
        NxInstallationDiscoveryResult validated = validator.ValidateAndDeduplicate(
            new[] { new NxInstallationCandidate(rootDirectory, NxCandidateSource.Manual) });
        return validated with { SelectedInstallation = validated.Installations.SingleOrDefault() };
    }

    private static NxInstallation? Select(IReadOnlyList<NxInstallation> installations, string? savedRoot)
    {
        if (installations.Count == 1)
        {
            return installations[0];
        }

        string? normalizedSavedRoot = NormalizeSavedRoot(savedRoot);
        return normalizedSavedRoot is null
            ? null
            : installations.FirstOrDefault(installation => string.Equals(
                installation.RootDirectory,
                normalizedSavedRoot,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeSavedRoot(string? savedRoot)
    {
        if (string.IsNullOrWhiteSpace(savedRoot))
        {
            return null;
        }

        try
        {
            string normalized = savedRoot.Trim();
            if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
            {
                normalized = normalized[1..^1].Trim();
            }

            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalized));
        }
        catch (Exception error) when (IsExpectedSourceError(error))
        {
            return null;
        }
    }

    private static bool IsExpectedSourceError(Exception error) =>
        error is ArgumentException or NotSupportedException or PathTooLongException or IOException
            or UnauthorizedAccessException or SecurityException;
}
