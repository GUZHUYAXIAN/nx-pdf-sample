using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Runtime.Detection;

public interface INxInstallationCandidateSource
{
    NxCandidateSource? Source => null;

    Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken);
}

public interface INxInstallationDiscoveryService
{
    Task<NxInstallationDiscoveryResult> DetectAsync(string? savedRoot, CancellationToken cancellationToken);

    NxInstallationDiscoveryResult ValidateManual(string rootDirectory);
}
