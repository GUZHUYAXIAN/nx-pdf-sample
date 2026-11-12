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
            directory = Path.Combine(ProjectScratch(), "nxpdf-publisher-" + Guid.NewGuid().ToString("N"));
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

            var outcome = publisher.Publish(Plan(2));

            Assert.AreEqual(PublicationOutcomeStatus.ValidationFailed, outcome.Status);
            Assert.IsTrue(outcome.OriginalPreserved);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(finalPath));
            AssertStringContains(outcome.Message, "临时 PDF 不存在");
        }

        [TestMethod]
        public void PlannedCreateNeverOverwritesNewTarget()
        {
            var request = new PublicationRequest
            {
                TempPdfPath = tempPath, FinalOutputPath = finalPath, ExpectedPageCount = 1,
                Intent = PlannedPublishIntent.CreateNew, ExpectedTarget = TargetSnapshot.Missing
            };
            File.WriteAllBytes(tempPath, TestPdfFactory.CreatePages(1));
            File.WriteAllText(finalPath, "third party");
            var outcome = CreatePublisher(new ContentInspector()).Publish(request);
            Assert.AreEqual(PublicationOutcomeStatus.TargetChanged, outcome.Status);
            Assert.AreEqual("third party", File.ReadAllText(finalPath));
        }

        [TestMethod]
        public void PlannedReplacementRejectsSameLengthChangeOrDisappearance()
        {
            File.WriteAllText(finalPath, "old");
            var expected = new PhysicalTargetStateReader().Capture(finalPath);
            var request = new PublicationRequest
            {
                TempPdfPath = tempPath, FinalOutputPath = finalPath, ExpectedPageCount = 1,
                Intent = PlannedPublishIntent.ReplaceExpected, ExpectedTarget = expected
            };
            var publisher = CreatePublisher(new ContentInspector());
            File.WriteAllBytes(tempPath, TestPdfFactory.CreatePages(1));
            File.WriteAllText(finalPath, "new");
            Assert.AreEqual(PublicationOutcomeStatus.TargetChanged, publisher.Publish(request).Status);
            Assert.AreEqual("new", File.ReadAllText(finalPath));
            File.Delete(finalPath);
            File.WriteAllBytes(tempPath, TestPdfFactory.CreatePages(1));
            Assert.AreEqual(PublicationOutcomeStatus.TargetChanged, publisher.Publish(request).Status);
            Assert.IsFalse(File.Exists(finalPath));
        }

        [TestMethod]
        public void Publish_EmptyTempFile_FailsAndCreatesNoTarget()
        {
            File.WriteAllBytes(tempPath, Array.Empty<byte>());
            var publisher = CreatePublisher(new ThrowingInspector());

            var outcome = publisher.Publish(Plan(1));

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

            var outcome = publisher.Publish(Plan(1));

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

            var outcome = publisher.Publish(Plan(2));

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

            var outcome = publisher.Publish(Plan(2));

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

            var outcome = publisher.Publish(Plan(2));

            Assert.AreEqual(PublicationOutcomeStatus.Replaced, outcome.Status);
            Assert.AreEqual("valid replacement pdf content", File.ReadAllText(finalPath));
            Assert.IsFalse(File.Exists(tempPath));
            Assert.IsEmpty(Directory.GetFiles(directory, "*.bak.pdf"));
        }

        [TestMethod]
        public void Publish_ReplacementInterruption_RetainsOriginalBackup()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllText(tempPath, "valid replacement pdf content");
            var publisher = CreatePublisher(CreateStubInspector(hasPdfHeader: true, pageCount: 2), new InterruptedReplaceReplacer());

            var outcome = publisher.Publish(Plan(2));

            Assert.AreEqual(PublicationOutcomeStatus.PublishFailed, outcome.Status);
            Assert.IsFalse(outcome.OriginalPreserved);
            Assert.IsNotNull(outcome.BackupPath);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(outcome.BackupPath));
            Assert.IsFalse(File.Exists(tempPath));
            Assert.HasCount(1, Directory.GetFiles(directory, "*.bak.pdf"));
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

            var outcome = publisher.Publish(Plan(2));

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

            var outcome = publisher.Publish(Plan(2));

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

        [TestMethod]
        public void Publish_ReplacementWritesNonPdfBytes_RetainsOriginalBackup()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllBytes(tempPath, TestPdfFactory.CreatePages(1));
            var publisher = CreatePublisher(new ContentInspector(), new MutatingReplacer("corrupted non-pdf payload"));

            var outcome = publisher.Publish(Plan(1));

            Assert.AreEqual(PublicationOutcomeStatus.PublishFailed, outcome.Status);
            Assert.IsFalse(outcome.OriginalPreserved);
            Assert.IsNotNull(outcome.BackupPath);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(outcome.BackupPath));
            Assert.HasCount(1, Directory.GetFiles(directory, "*.bak.pdf"));
            Assert.IsFalse(File.Exists(tempPath));
        }

        [TestMethod]
        public void Publish_ReplacementWritesWrongPageCount_RetainsOriginalBackup()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllBytes(tempPath, TestPdfFactory.CreatePages(2));
            var publisher = CreatePublisher(new ContentInspector(), new MutatingReplacer(Encoding.ASCII.GetString(TestPdfFactory.CreatePages(1))));

            var outcome = publisher.Publish(Plan(2));

            Assert.AreEqual(PublicationOutcomeStatus.PublishFailed, outcome.Status);
            Assert.IsFalse(outcome.OriginalPreserved);
            Assert.IsNotNull(outcome.BackupPath);
            Assert.AreEqual("old pdf bytes", File.ReadAllText(outcome.BackupPath));
            StringAssert.Contains(outcome.Message, "页数");
            Assert.HasCount(1, Directory.GetFiles(directory, "*.bak.pdf"));
        }

        [TestMethod]
        public void Publish_OverwriteExisting_DeletesBackupOnlyAfterFinalInspection()
        {
            File.WriteAllText(finalPath, "old pdf bytes");
            File.WriteAllBytes(tempPath, TestPdfFactory.CreatePages(1));
            var timeline = new List<string>();
            var publisher = CreatePublisher(new RecordingInspector(timeline), new RecordingReplacer(timeline));

            var outcome = publisher.Publish(Plan(1));

            Assert.AreEqual(PublicationOutcomeStatus.Replaced, outcome.Status);
            var finalInspection = timeline.LastIndexOf("inspect:" + finalPath);
            var backupDeletion = timeline.FindIndex(entry => entry.StartsWith("delete:", StringComparison.Ordinal) && entry.EndsWith(".bak.pdf", StringComparison.Ordinal));
            Assert.IsGreaterThanOrEqualTo(0, finalInspection);
            Assert.IsGreaterThan(finalInspection, backupDeletion);
        }

        private PublicationRequest Plan(int pages)
        {
            var expected = new PhysicalTargetStateReader().Capture(finalPath);
            return new PublicationRequest
            {
                TempPdfPath = tempPath, FinalOutputPath = finalPath, ExpectedPageCount = pages,
                ExpectedTarget = expected,
                Intent = expected.Exists ? PlannedPublishIntent.ReplaceExpected : PlannedPublishIntent.CreateNew
            };
        }

        private static string ProjectScratch()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "NxDrawingPdfExporter.slnx")))
                root = root.Parent ?? throw new DirectoryNotFoundException("Repository root");
            return Path.Combine(root.FullName, "artifacts", "publication-tests");
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


            public void DeleteFile(string path) => inner.DeleteFile(path);
        }

        /// <summary>按真实字节判定文件头与页数（/Type /Page 计数），可区分替换前后的内容变化。</summary>
        private sealed class ContentInspector : IPdfInspector
        {
            public PdfInspection Inspect(string path)
            {
                var bytes = File.ReadAllBytes(path);
                var text = Encoding.ASCII.GetString(bytes);
                var pageCount = 0;
                var position = 0;
                while ((position = text.IndexOf("/Type /Page ", position, StringComparison.Ordinal)) >= 0)
                {
                    pageCount++;
                    position += "/Type /Page ".Length;
                }

                return new PdfInspection
                {
                    HasPdfHeader = text.StartsWith("%PDF-", StringComparison.Ordinal),
                    PageCount = pageCount,
                    Length = bytes.Length
                };
            }
        }

        /// <summary>替换成功返回，但把最终文件写成另一个载荷——模拟损坏/不符的替换结果。</summary>
        private sealed class MutatingReplacer : IFileReplacer
        {
            private readonly FileReplacer inner = new();
            private readonly string finalPayload;

            public MutatingReplacer(string finalPayload) => this.finalPayload = finalPayload;

            public void MoveIntoPlace(string tempPath, string finalPath) => inner.MoveIntoPlace(tempPath, finalPath);

            public void ReplaceWithBackup(string tempPath, string finalPath, string backupPath)
            {
                File.Replace(tempPath, finalPath, backupPath);
                File.WriteAllText(finalPath, finalPayload);
            }


            public void DeleteFile(string path) => inner.DeleteFile(path);
        }

        private sealed class RecordingInspector : IPdfInspector
        {
            private readonly ContentInspector inner = new();
            private readonly List<string> timeline;

            public RecordingInspector(List<string> timeline) => this.timeline = timeline;

            public PdfInspection Inspect(string path)
            {
                timeline.Add("inspect:" + path);
                return inner.Inspect(path);
            }
        }

        private sealed class RecordingReplacer : IFileReplacer
        {
            private readonly FileReplacer inner = new();
            private readonly List<string> timeline;

            public RecordingReplacer(List<string> timeline) => this.timeline = timeline;

            public void MoveIntoPlace(string tempPath, string finalPath)
            {
                inner.MoveIntoPlace(tempPath, finalPath);
                timeline.Add("move:" + finalPath);
            }

            public void ReplaceWithBackup(string tempPath, string finalPath, string backupPath)
            {
                inner.ReplaceWithBackup(tempPath, finalPath, backupPath);
                timeline.Add("replace:" + finalPath);
            }

            public void DeleteFile(string path)
            {
                inner.DeleteFile(path);
                timeline.Add("delete:" + path);
            }
        }
    }

    /// <summary>
    /// 仅供桩检查器做字节级断言的最小 PDF 构造；与 App.Tests 的完整工厂不同，
    /// 这里只保证文件头与 /Type /Page 计数正确。
    /// </summary>
    internal static class TestPdfFactory
    {
        public static byte[] CreatePages(int pageCount)
        {
            var body = "%PDF-1.4\n";
            for (var index = 0; index < pageCount; index++)
            {
                body += (3 + index) + " 0 obj\n<< /Type /Page /Parent 2 0 R >>\nendobj\n";
            }

            body += "%%EOF\n";
            return Encoding.ASCII.GetBytes(body);
        }
    }
}
