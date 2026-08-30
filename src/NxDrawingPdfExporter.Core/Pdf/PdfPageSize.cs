namespace NxDrawingPdfExporter.Core.Pdf
{
    /// <summary>PDF 页面 MediaBox 尺寸，单位为 PDF 点（1/72 英寸）。</summary>
    public sealed class PdfPageSize
    {
        public double WidthPoints { get; set; }

        public double HeightPoints { get; set; }
    }
}
