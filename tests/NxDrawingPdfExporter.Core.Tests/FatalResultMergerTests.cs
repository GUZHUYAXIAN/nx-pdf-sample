using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Jobs;

namespace NxDrawingPdfExporter.Core.Tests
{
    /// <summary>
    /// CR-04 回归：批处理致命错误绝不把已完成文件的持久快照替换为空结果。
    /// </summary>
    [TestClass]
    public sealed class FatalResultMergerTests
    {
        private static JobRequest NewRequest(params JobItem[] items)
        {
            return new JobRequest
            {
                ProtocolVersion = ProtocolVersion.Current,
                RunId = "run-1",
                Items = items,
                OutputMode = OutputMode.BesideSource,
                ExistingPdfPolicy = ExistingPdfPolicy.Skip,
                ResultPath = @"C:\runs\run-1\result.json",
                CancellationFlagPath = @"C:\runs\run-1\cancel.flag"
            };
        }

        private static JobItem NewItem(string name)
        {
            return new JobItem
            {
                SourcePath = @"C:\in\" + name + ".prt",
                FinalOutputPath = @"C:\out\" + name + ".pdf",
                WorkerTempOutputPath = @"C:\out\." + name + ".tmp.pdf"
            };
        }

        private static FileResult ResultFor(JobItem item, FileResultStatus status)
        {
            return new FileResult
            {
                SourcePath = item.SourcePath,
                FinalOutputPath = item.FinalOutputPath,
                Status = status,
                Message = status == FileResultStatus.Success ? "完成" : "失败"
            };
        }

        [TestMethod]
        public void Merge_RetainsSnapshotFiles_AndAppendsFailedForUnresolvedItems()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var item3 = NewItem("c");
            var request = NewRequest(item1, item2, item3);
            var snapshot = new JobResult
            {
                ProtocolVersion = ProtocolVersion.Current,
                RunId = "run-1",
                StartedUtc = DateTime.UtcNow.AddMinutes(-5),
                EndedUtc = DateTime.UtcNow.AddMinutes(-4),
                Files = new[] { ResultFor(item1, FileResultStatus.Success), ResultFor(item2, FileResultStatus.Failed) }
            };
            var endedUtc = DateTime.UtcNow;

            var merged = FatalResultMerger.Merge(request, snapshot, "快照写入失败\r\n第二行", endedUtc);

            Assert.HasCount(3, merged.Files);
            Assert.AreEqual(FileResultStatus.Success, merged.Files[0].Status);
            Assert.AreEqual(FileResultStatus.Failed, merged.Files[1].Status);
            Assert.AreEqual(item3.SourcePath, merged.Files[2].SourcePath);
            Assert.AreEqual(FileResultStatus.Failed, merged.Files[2].Status);
            StringAssert.Contains(merged.Files[2].Message, "致命");
            Assert.IsNotNull(merged.FatalError);
            Assert.AreEqual("快照写入失败 第二行", merged.FatalError);
            Assert.AreEqual("run-1", merged.RunId);
            Assert.AreEqual(snapshot.StartedUtc, merged.StartedUtc);
            Assert.AreEqual(endedUtc, merged.EndedUtc);
        }

        [TestMethod]
        public void Merge_WithoutSnapshot_MarksEveryItemFailed()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var request = NewRequest(item1, item2);

            var merged = FatalResultMerger.Merge(request, null, "启动即崩溃", DateTime.UtcNow);

            Assert.HasCount(2, merged.Files);
            Assert.IsTrue(merged.Files.All(f => f.Status == FileResultStatus.Failed));
            Assert.AreEqual("启动即崩溃", merged.FatalError);
        }

        [TestMethod]
        public void Merge_SnapshotFromDifferentRun_IsDiscarded()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var request = NewRequest(item1, item2);
            var snapshot = new JobResult
            {
                ProtocolVersion = ProtocolVersion.Current,
                RunId = "some-other-run",
                StartedUtc = DateTime.UtcNow,
                EndedUtc = DateTime.UtcNow,
                Files = new[] { ResultFor(item1, FileResultStatus.Success) }
            };

            var merged = FatalResultMerger.Merge(request, snapshot, "崩溃", DateTime.UtcNow);

            Assert.HasCount(2, merged.Files);
            Assert.IsTrue(merged.Files.All(f => f.Status == FileResultStatus.Failed));
            Assert.AreEqual(item2.SourcePath, merged.Files[1].SourcePath);
        }

        [TestMethod]
        public void Merge_SnapshotWithWrongProtocol_IsDiscarded()
        {
            var item1 = NewItem("a");
            var request = NewRequest(item1);
            var snapshot = new JobResult
            {
                ProtocolVersion = "999",
                RunId = "run-1",
                StartedUtc = DateTime.UtcNow,
                EndedUtc = DateTime.UtcNow,
                Files = new[] { ResultFor(item1, FileResultStatus.Success) }
            };

            var merged = FatalResultMerger.Merge(request, snapshot, "崩溃", DateTime.UtcNow);

            Assert.HasCount(1, merged.Files);
            Assert.AreEqual(FileResultStatus.Failed, merged.Files[0].Status);
        }

        [TestMethod]
        public void Merge_SnapshotWithMismatchedEntryOrder_IsDiscarded()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var request = NewRequest(item1, item2);
            var snapshot = new JobResult
            {
                ProtocolVersion = ProtocolVersion.Current,
                RunId = "run-1",
                StartedUtc = DateTime.UtcNow,
                EndedUtc = DateTime.UtcNow,
                // 快照声称第二个条目先完成——违反任务顺序前缀性质，不可信。
                Files = new[] { ResultFor(item2, FileResultStatus.Success) }
            };

            var merged = FatalResultMerger.Merge(request, snapshot, "崩溃", DateTime.UtcNow);

            Assert.HasCount(2, merged.Files);
            Assert.IsTrue(merged.Files.All(f => f.Status == FileResultStatus.Failed));
        }

        [TestMethod]
        public void Merge_SnapshotLongerThanRequest_IsDiscarded()
        {
            var item1 = NewItem("a");
            var request = NewRequest(item1);
            var snapshot = new JobResult
            {
                ProtocolVersion = ProtocolVersion.Current,
                RunId = "run-1",
                StartedUtc = DateTime.UtcNow,
                EndedUtc = DateTime.UtcNow,
                Files = new[] { ResultFor(item1, FileResultStatus.Success), ResultFor(NewItem("ghost"), FileResultStatus.Success) }
            };

            var merged = FatalResultMerger.Merge(request, snapshot, "崩溃", DateTime.UtcNow);

            Assert.HasCount(1, merged.Files);
            Assert.AreEqual(FileResultStatus.Failed, merged.Files[0].Status);
        }

        [TestMethod]
        public void Merge_EmptyFatalMessage_UsesPlaceholder()
        {
            var request = NewRequest(NewItem("a"));

            var merged = FatalResultMerger.Merge(request, null, "   \r\n  ", DateTime.UtcNow);

            Assert.IsNotNull(merged.FatalError);
            Assert.IsGreaterThan(0, merged.FatalError.Length);
        }
    }
}
