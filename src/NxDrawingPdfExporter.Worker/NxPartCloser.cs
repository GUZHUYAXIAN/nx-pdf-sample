using NXOpen;

namespace NxDrawingPdfExporter.Worker
{
    internal sealed class NxPartCloser
    {
        // View updates mark the drawing part modified in memory. Closing with
        // CloseModified discards those changes and never saves; DontCloseModified
        // would throw "Modified part not saved" in NX 10 for the same state.
        public void CloseWithoutSaving(BasePart part)
        {
            part.Close(BasePart.CloseWholeTree.True, BasePart.CloseModified.CloseModified, null);
        }
    }
}
