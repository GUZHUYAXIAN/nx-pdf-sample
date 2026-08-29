using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Pdf;
using NxDrawingPdfExporter.Core.Pdf;

namespace NxDrawingPdfExporter.App.Tests
{
    [TestClass]
    public sealed class PdfSharpInspectorTests
    {
        private string directory = "";

        [TestInitialize]
        public void Initialize()
        {
            directory = Path.Combine(Path.GetTempPath(), "nxpdf-inspector-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void Inspect_SinglePagePdf_ReportsHeaderCountAndSize()
        {
            var path = WritePdf((842, 595));

            var inspection = new PdfSharpInspector().Inspect(path);

            Assert.IsTrue(inspection.HasPdfHeader);
            Assert.AreEqual(1, inspection.PageCount);
            Assert.IsGreaterThan(0L, inspection.Length);
            Assert.HasCount(1, inspection.PageSizes);
            Assert.AreEqual(842, inspection.PageSizes[0].WidthPoints, 0.5);
            Assert.AreEqual(595, inspection.PageSizes[0].HeightPoints, 0.5);
        }

        [TestMethod]
        public void Inspect_MultiPagePdf_ReportsEachPageSizeInOrder()
        {
            var path = WritePdf((842, 595), (595, 842));

            var inspection = new PdfSharpInspector().Inspect(path);

            Assert.IsTrue(inspection.HasPdfHeader);
            Assert.AreEqual(2, inspection.PageCount);
            Assert.HasCount(2, inspection.PageSizes);
            Assert.AreEqual(842, inspection.PageSizes[0].WidthPoints, 0.5);
            Assert.AreEqual(595, inspection.PageSizes[0].HeightPoints, 0.5);
            Assert.AreEqual(595, inspection.PageSizes[1].WidthPoints, 0.5);
            Assert.AreEqual(842, inspection.PageSizes[1].HeightPoints, 0.5);
        }

        [TestMethod]
        public void Inspect_NonPdfFile_ReportsMissingHeader()
        {
            var path = Path.Combine(directory, "plain.txt");
            File.WriteAllText(path, "hello world, not a pdf");

            var inspection = new PdfSharpInspector().Inspect(path);

            Assert.IsFalse(inspection.HasPdfHeader);
            Assert.AreEqual(0, inspection.PageCount);
            Assert.IsGreaterThan(0L, inspection.Length);
            Assert.IsEmpty(inspection.PageSizes);
        }

        [TestMethod]
        public void Inspect_TruncatedPdfWithHeader_ThrowsInvalidOperationException()
        {
            var path = Path.Combine(directory, "truncated.pdf");
            File.WriteAllText(path, "%PDF-1.4\n");

            Assert.ThrowsExactly<InvalidOperationException>(() => new PdfSharpInspector().Inspect(path));
        }

        [TestMethod]
        public void Inspect_MissingFile_ThrowsFileNotFoundException()
        {
            var missing = Path.Combine(directory, "missing.pdf");

            Assert.ThrowsExactly<FileNotFoundException>(() => new PdfSharpInspector().Inspect(missing));
        }

        private string WritePdf(params (double Width, double Height)[] pageSizes)
        {
            var path = Path.Combine(directory, "sample-" + Guid.NewGuid().ToString("N") + ".pdf");
            File.WriteAllBytes(path, BuildMinimalPdf(pageSizes));
            return path;
        }

        /// <summary>构造只含页面对象的最小合法 PDF，用于离线测试只读检查。</summary>
        private static byte[] BuildMinimalPdf(params (double Width, double Height)[] pageSizes)
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
