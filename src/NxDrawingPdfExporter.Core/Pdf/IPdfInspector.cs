namespace NxDrawingPdfExporter.Core.Pdf
{
    /// <summary>只读 PDF 检查器。实现不得修改被检查的文件，也不参与 PDF 生成。</summary>
    public interface IPdfInspector
    {
        PdfInspection Inspect(string path);
    }
}
