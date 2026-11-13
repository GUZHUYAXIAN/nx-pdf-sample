using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Jobs;

public sealed class BatchItemProgress
{
    public BatchItemProgress(int itemIndex, FileResult? result)
    {
        ItemIndex = itemIndex;
        Result = result;
    }

    public int ItemIndex { get; }
    public bool Completed => Result is not null;
    public FileResult? Result { get; }
}
