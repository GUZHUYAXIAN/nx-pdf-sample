using System.Threading;
using System.Threading.Tasks;

namespace NxDrawingPdfExporter.App.Configuration;

public interface INxSettingsStore
{
    Task<NxSettingsLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(NxSettings settings, CancellationToken cancellationToken);
}
