using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Core.Output;
using NxDrawingPdfExporter.Core.Pdf;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class SafeOutputPublisherTests
    {
        private string directory = "";
        private string finalPath = "";
        private string tempPath = "";

        [TestInitialize]
        public void Initialize()
        {
            directory = Path.Combine(Path.GetTempPath(), "nxpdf-publisher-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            finalPath = Path.Combine(directory, "drawing.pdf");
            tempPath = Path.Combine(directory, ".drawing.run1.abc123.tmp.pdf");
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
        public void Publish_MissingTempFile_FailsAndKeepsExistingTarget()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            var publisher = CreatePublisher(new ThrowingInspector());

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 2
            });

            Assert.AreEqual(PublicationOutcomeStatus.ValidationFailed, outcome.Status);
            Assert.IsTrue(outcome.OriginalPreserved);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(finalPath));
            AssertStringContains(outcome.Message, "临时 PDF 不存在");
        }

        [TestMethod]
        public void Publish_EmptyTempFile_FailsAndCreatesNoTarget()
        {
            File.WriteAllBytes(tempPath, Array.Empty<byte>());
            var publisher = CreatePublisher(new ThrowingInspector());

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 1
            });

            Assert.AreEqual(PublicationOutcomeStatus.ValidationFailed, outcome.Status);
            Assert.IsFalse(File.Exists(finalPath));
            Assert.IsFalse(File.Exists(tempPath));
            AssertStringContains(outcome.Message, "空文件");
        }

        [TestMethod]
        public void Publish_NonPdfTempFile_FailsAndKeepsExistingTarget()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllText(tempPath, "this is not a pdf document");
            var publisher = CreatePublisher(CreateStubInspector(hasPdfHeader: false, pageCount: 0));

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 1
            });

            Assert.AreEqual(PublicationOutcomeStatus.ValidationFailed, outcome.Status);
            Assert.IsTrue(outcome.OriginalPreserved);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(finalPath));
            AssertStringContains(outcome.Message, "PDF 文件头");
            Assert.IsFalse(File.Exists(tempPath));
        }

        [TestMethod]
        public void Publish_WrongPageCount_FailsAndKeepsExistingTarget()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllText(tempPath, "%PDF-1.4\n");
            var publisher = CreatePublisher(CreateStubInspector(hasPdfHeader: true, pageCount: 3));

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 2
            });

            Assert.AreEqual(PublicationOutcomeStatus.ValidationFailed, outcome.Status);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(finalPath));
            AssertStringContains(outcome.Message, "页数");
            Assert.IsFalse(File.Exists(tempPath));
        }

        [TestMethod]
        public void Publish_NewFile_MovesValidatedTempIntoFinalPath()
        {
            File.WriteAllText(tempPath, "valid new pdf content");
            var publisher = CreatePublisher(CreateStubInspector(hasPdfHeader: true, pageCount: 2));

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 2
            });

            Assert.AreEqual(PublicationOutcomeStatus.Published, outcome.Status);
            Assert.AreEqual("valid new pdf content", File.ReadAllText(finalPath));
            Assert.IsFalse(File.Exists(tempPath));
        }

        [TestMethod]
        public void Publish_OverwriteExisting_ReplacesContentAndRemovesBackup()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllText(tempPath, "valid replacement pdf content");
            var publisher = CreatePublisher(CreateStubInspector(hasPdfHeader: true, pageCount: 2));

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 2
            });

            Assert.AreEqual(PublicationOutcomeStatus.Replaced, outcome.Status);
            Assert.AreEqual("valid replacement pdf content", File.ReadAllText(finalPath));
            Assert.IsFalse(File.Exists(tempPath));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.bak.pdf"));
        }

        [TestMethod]
        public void Publish_ReplacementInterruption_RestoresOriginalBytes()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllText(tempPath, "valid replacement pdf content");
            var publisher = CreatePublisher(CreateStubInspector(hasPdfHeader: true, pageCount: 2), new InterruptedReplaceReplacer());

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 2
            });

            Assert.AreEqual(PublicationOutcomeStatus.PublishFailed, outcome.Status);
            Assert.IsTrue(outcome.OriginalPreserved);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(finalPath));
            Assert.IsFalse(File.Exists(tempPath));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.bak.pdf"));
        }

        [TestMethod]
        public void Publish_ValidationFailure_DeletesOnlyOwnedTempFiles()
        {
            var otherTemp = Path.Combine(directory, ".drawing.other-run.tmp.pdf");
            var notes = Path.Combine(directory, "notes.txt");
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllText(tempPath, "%PDF-1.4\n");
            File.WriteAllText(otherTemp, "another run's temp file");
            File.WriteAllText(notes, "user notes");
            var publisher = CreatePublisher(CreateStubInspector(hasPdfHeader: true, pageCount: 7));

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 2
            });

            Assert.AreEqual(PublicationOutcomeStatus.ValidationFailed, outcome.Status);
            Assert.IsFalse(File.Exists(tempPath));
            Assert.AreEqual("another run's temp file", File.ReadAllText(otherTemp));
            Assert.AreEqual("user notes", File.ReadAllText(notes));
            Assert.AreEqual("old pdf bytes", File.ReadAllText(finalPath));
        }

        [TestMethod]
        public void Publish_InspectorThrows_ReportsValidationFailureAndKeepsTarget()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllText(tempPath, "%PDF-1.4\n");
            var publisher = CreatePublisher(new ThrowingInspector());

            var outcome = publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 2
            });

            Assert.AreEqual(PublicationOutcomeStatus.ValidationFailed, outcome.Status);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(finalPath));
            AssertStringContains(outcome.Message, "PDF 校验失败");
        }

        [TestMethod]
        public void Publish_RequestWithEmptyPaths_IsRejected()
        {
            var publisher = CreatePublisher(new ThrowingInspector());

            Assert.ThrowsExactly<ArgumentException>(() => publisher.Publish(new PublicationRequest
            {
                TempPdfPath = "",
                FinalOutputPath = finalPath,
                ExpectedPageCount = 1
            }));

            Assert.ThrowsExactly<ArgumentException>(() => publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = "",
                ExpectedPageCount = 1
            }));

            Assert.ThrowsExactly<ArgumentException>(() => publisher.Publish(new PublicationRequest
            {
                TempPdfPath = tempPath,
                FinalOutputPath = finalPath,
                ExpectedPageCount = 0
            }));
        }

        private static SafeOutputPublisher CreatePublisher(IPdfInspector inspector, IFileReplacer? replacer = null)
        {
            return new SafeOutputPublisher(inspector, replacer ?? new FileReplacer());
        }

        private static IPdfInspector CreateStubInspector(bool hasPdfHeader, int pageCount)
        {
            return new StubInspector(new PdfInspection
            {
                HasPdfHeader = hasPdfHeader,
                PageCount = pageCount,
                Length = 1234
            });
        }

        private static void AssertStringContains(string actual, string expected)
        {
            Assert.IsTrue(actual.Contains(expected, StringComparison.Ordinal), $"消息“{actual}”应包含“{expected}”。");
        }

        private sealed class StubInspector : IPdfInspector
        {
            private readonly PdfInspection result;

            public StubInspector(PdfInspection result)
            {
                this.result = result;
            }

            public PdfInspection Inspect(string path) => result;
        }

        private sealed class ThrowingInspector : IPdfInspector
        {
            public PdfInspection Inspect(string path) => throw new InvalidOperationException("该测试场景不应调用 PDF 检查器。");
        }

        private sealed class InterruptedReplaceReplacer : IFileReplacer
        {
            private readonly FileReplacer inner = new();

            public void MoveIntoPlace(string tempPath, string finalPath) => inner.MoveIntoPlace(tempPath, finalPath);

            public void ReplaceWithBackup(string tempPath, string finalPath, string backupPath)
            {
                File.Move(finalPath, backupPath);
                File.Copy(tempPath, finalPath);
                throw new IOException("模拟替换中断");
            }

            public void RestoreBackup(string backupPath, string finalPath) => inner.RestoreBackup(backupPath, finalPath);

            public void DeleteFile(string path) => inner.DeleteFile(path);
        }
    }
}
