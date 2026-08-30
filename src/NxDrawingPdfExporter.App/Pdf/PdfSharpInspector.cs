using System;
using System.IO;
using System.Text;
using NxDrawingPdfExporter.Core.Pdf;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace NxDrawingPdfExporter.App.Pdf
{
    /// <summary>
    /// 基于 PDFsharp（MIT）的只读检查实现：只读取文件头、页数与 MediaBox 尺寸，
    /// 不渲染、不修改文件、不参与 NX 输出。
    /// </summary>
    public sealed class PdfSharpInspector : IPdfInspector
    {
        public PdfInspection Inspect(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("PDF 路径不能为空。", nameof(path));
            }

            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("PDF 文件不存在。", path);
            }

            if (!HasPdfHeader(path))
            {
                return new PdfInspection { HasPdfHeader = false, Length = fileInfo.Length };
            }

            try
            {
                using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
                var pageSizes = new PdfPageSize[document.PageCount];
                for (var i = 0; i < document.PageCount; i++)
                {
                    var mediaBox = document.Pages[i].MediaBox;
                    pageSizes[i] = new PdfPageSize
                    {
                        WidthPoints = mediaBox.Width,
                        HeightPoints = mediaBox.Height
                    };
                }

                return new PdfInspection
                {
                    HasPdfHeader = true,
                    Length = fileInfo.Length,
                    PageCount = document.PageCount,
                    PageSizes = pageSizes
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("PDF 解析失败: " + ex.Message, ex);
            }
        }

        private static bool HasPdfHeader(string path)
        {
            var buffer = new byte[5];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var read = 0;
            while (read < buffer.Length)
            {
                var count = stream.Read(buffer, read, buffer.Length - read);
                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            return read == buffer.Length && Encoding.ASCII.GetString(buffer) == "%PDF-";
        }
    }
}
