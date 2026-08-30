using System;
using System.Collections.Generic;

namespace NxDrawingPdfExporter.Core.Pdf
{
    /// <summary>只读 PDF 检查结果：文件头、页数、长度与每页尺寸。</summary>
    public sealed class PdfInspection
    {
        public bool HasPdfHeader { get; set; }

        public int PageCount { get; set; }

        public long Length { get; set; }

        public IReadOnlyList<PdfPageSize> PageSizes { get; set; } = Array.Empty<PdfPageSize>();
    }
}
