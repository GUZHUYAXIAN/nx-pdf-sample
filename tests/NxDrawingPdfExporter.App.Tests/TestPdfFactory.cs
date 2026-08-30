using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NxDrawingPdfExporter.App.Tests
{
    /// <summary>构造只含页面对象的最小合法 PDF，用于离线测试。</summary>
    internal static class TestPdfFactory
    {
        public static byte[] CreateSinglePage() => CreatePages((842, 595));

        public static byte[] CreatePages(params (double Width, double Height)[] pageSizes)
        {
            using var body = new MemoryStream();
            var offsets = new List<long>();
            var culture = CultureInfo.InvariantCulture;

            void Write(string text)
            {
                var bytes = Encoding.ASCII.GetBytes(text);
                body.Write(bytes, 0, bytes.Length);
            }

            Write("%PDF-1.4\n");

            var pageCount = pageSizes.Length;
            var kids = new StringBuilder();
            for (var i = 0; i < pageCount; i++)
            {
                if (i > 0)
                {
                    kids.Append(' ');
                }

                kids.Append((3 + i).ToString(culture)).Append(" 0 R");
            }

            offsets.Add(body.Position);
            Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            offsets.Add(body.Position);
            Write(string.Create(
                culture,
                $"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n"));

            for (var i = 0; i < pageCount; i++)
            {
                offsets.Add(body.Position);
                Write(string.Create(
                    culture,
                    $"{3 + i} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageSizes[i].Width.ToString(culture)} {pageSizes[i].Height.ToString(culture)}] >>\nendobj\n"));
            }

            var xrefPosition = body.Position;
            var xref = new StringBuilder();
            xref.Append("xref\n0 ").Append((pageCount + 3).ToString(culture)).Append('\n');
            xref.Append("0000000000 65535 f \n");
            foreach (var offset in offsets)
            {
                xref.Append(offset.ToString("D10", culture)).Append(" 00000 n \n");
            }

            xref.Append("trailer\n<< /Size ").Append((pageCount + 3).ToString(culture))
                .Append(" /Root 1 0 R >>\nstartxref\n").Append(xrefPosition.ToString(culture)).Append("\n%%EOF\n");
            Write(xref.ToString());

            return body.ToArray();
        }
    }
}
