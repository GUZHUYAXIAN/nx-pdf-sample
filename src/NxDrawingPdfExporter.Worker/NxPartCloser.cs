using NXOpen;

namespace NxDrawingPdfExporter.Worker
{
    internal sealed class NxPartCloser
    {
        public void CloseWithoutSaving(BasePart part)
        {
            part.Close(BasePart.CloseWholeTree.True, BasePart.CloseModified.DontCloseModified, null);
        }
    }
}
