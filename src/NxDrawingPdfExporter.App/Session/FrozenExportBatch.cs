using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Output;

namespace NxDrawingPdfExporter.App.Session;

public sealed record FrozenExportBatch(
    NxInstallation Installation,
    IReadOnlyList<PlannedOutputItem> Items,
    OutputRoutingSettings Routing,
    ExistingPdfPolicy Policy);
