using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App.Runtime;

internal static class WorkerResultValidator
{
    public static bool IsTrusted(JobRequest request, JobResult result, DateTime launchedUtc, DateTime returnedUtc)
    {
        // JSON timestamps have millisecond precision. Missing/old snapshots are never terminal success.
        if (!result.Completed || !string.Equals(result.RunId, request.RunId, StringComparison.Ordinal)
            || result.StartedUtc < launchedUtc.AddSeconds(-1) || result.EndedUtc < result.StartedUtc
            || result.EndedUtc > returnedUtc.AddSeconds(1)) return false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in result.Files)
        {
            var item = request.Items.FirstOrDefault(i => string.Equals(i.SourcePath, file.SourcePath, StringComparison.OrdinalIgnoreCase));
            if (item is null || !seen.Add(file.SourcePath)
                || !string.Equals(file.FinalOutputPath, item.FinalOutputPath, StringComparison.OrdinalIgnoreCase)
                || !Enum.IsDefined(file.Status) || file.Status == FileResultStatus.Overwritten
                || file.ExportedSheets is null || file.SkippedSheets is null) return false;
        }
        // Missing entries are reported as failures by the controller; completed entries can be recovered.
        return true;
    }
}
