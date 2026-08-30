using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Jobs;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class BatchStateMachineTests
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
                ResultPath = "result.json",
                CancellationFlagPath = "cancel.flag"
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

        private sealed class FakeCancellation : ICancellationFlag
        {
            public bool IsRequested { get; set; }
        }

        private sealed class FakeProcessor : IBatchItemProcessor
        {
            private readonly Dictionary<string, FileResult> results;
            public List<string> Processed { get; } = new List<string>();

            public FakeProcessor(params FileResult[] results)
            {
                this.results = results.ToDictionary(r => r.SourcePath);
            }

            public FileResult Process(JobItem item)
            {
                Processed.Add(item.SourcePath);
                return results[item.SourcePath];
            }
        }

        private sealed class FakeProbe : IBatchTargetProbe
        {
            private readonly HashSet<string> existing;
            public FakeProbe(params string[] existingPaths) => existing = new HashSet<string>(existingPaths);
            public bool TargetExists(string finalOutputPath) => existing.Contains(finalOutputPath);
        }

        private static FileResult StatusResult(JobItem item, FileResultStatus status, string message = "")
        {
            return new FileResult { SourcePath = item.SourcePath, FinalOutputPath = item.FinalOutputPath, Status = status, Message = message };
        }

        [TestMethod]
        public void Run_ProcessesAllItemsInOrder_WhenEverythingSucceeds()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var processor = new FakeProcessor(
                StatusResult(item1, FileResultStatus.Success, "完成"),
                StatusResult(item2, FileResultStatus.Success, "完成"));
            var machine = new BatchStateMachine(processor, new FakeProbe(), new FakeCancellation());

            var result = machine.Run(NewRequest(item1, item2));

            Assert.IsFalse(result.Cancelled);
            Assert.HasCount(2, result.Files);
            Assert.AreEqual(FileResultStatus.Success, result.Files[0].Status);
            Assert.AreEqual(FileResultStatus.Success, result.Files[1].Status);
            CollectionAssert.AreEqual(new[] { item1.SourcePath, item2.SourcePath }, processor.Processed);
        }

        [TestMethod]
        public void Run_SingleFileFailure_ContinuesToNextFile()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var processor = new FakeProcessor(
                StatusResult(item1, FileResultStatus.Failed, "缺失模型依赖。"),
                StatusResult(item2, FileResultStatus.Success, "完成"));
            var machine = new BatchStateMachine(processor, new FakeProbe(), new FakeCancellation());

            var result = machine.Run(NewRequest(item1, item2));

            Assert.HasCount(2, processor.Processed);
            Assert.AreEqual(FileResultStatus.Failed, result.Files[0].Status);
            Assert.AreEqual(FileResultStatus.Success, result.Files[1].Status);
            Assert.IsNull(result.FatalError);
        }

        [TestMethod]
        public void Run_ProcessorThrows_ItemFailsAndNextFileContinues()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var processor = new ThrowingProcessor(item1.SourcePath);
            var machine = new BatchStateMachine(processor, new FakeProbe(), new FakeCancellation());

            var result = machine.Run(NewRequest(item1, item2));

            Assert.AreEqual(FileResultStatus.Failed, result.Files[0].Status);
            Assert.IsNotEmpty(result.Files[0].Message);
            Assert.AreEqual(FileResultStatus.Success, result.Files[1].Status);
        }

        [TestMethod]
        public void Run_SkipPolicyWithExistingTarget_DoesNotStartWorkerExport()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var processor = new FakeProcessor(StatusResult(item2, FileResultStatus.Success));
            var machine = new BatchStateMachine(processor, new FakeProbe(item1.FinalOutputPath), new FakeCancellation());
            var request = NewRequest(item1, item2);
            request.ExistingPdfPolicy = ExistingPdfPolicy.Skip;

            var result = machine.Run(request);

            CollectionAssert.AreEqual(new[] { item2.SourcePath }, processor.Processed);
            Assert.AreEqual(FileResultStatus.SkippedExisting, result.Files[0].Status);
            Assert.AreEqual(FileResultStatus.Success, result.Files[1].Status);
        }

        [TestMethod]
        public void Run_OverwritePolicyWithExistingTarget_StartsWorkerExport()
        {
            var item1 = NewItem("a");
            var processor = new FakeProcessor(StatusResult(item1, FileResultStatus.Success));
            var machine = new BatchStateMachine(processor, new FakeProbe(item1.FinalOutputPath), new FakeCancellation());
            var request = NewRequest(item1);
            request.ExistingPdfPolicy = ExistingPdfPolicy.Overwrite;

            var result = machine.Run(request);

            Assert.HasCount(1, processor.Processed);
            Assert.AreEqual(FileResultStatus.Success, result.Files[0].Status);
        }

        [TestMethod]
        public void Run_NameConflictItems_AreNeverStarted()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var processor = new FakeProcessor(StatusResult(item1, FileResultStatus.Success));
            var machine = new BatchStateMachine(processor, new FakeProbe(), new FakeCancellation());
            var preflight = new[]
            {
                new BatchItemPreflight { Item = item1, NameConflict = false },
                new BatchItemPreflight { Item = item2, NameConflict = true }
            };

            var result = machine.Run(NewRequest(item1, item2), preflight);

            CollectionAssert.AreEqual(new[] { item1.SourcePath }, processor.Processed);
            Assert.AreEqual(FileResultStatus.NameConflict, result.Files[1].Status);
        }

        [TestMethod]
        public void Run_CancellationBeforeFirstFile_MarksEverythingCancelledWithoutProcessing()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var processor = new FakeProcessor();
            var cancellation = new FakeCancellation { IsRequested = true };
            var machine = new BatchStateMachine(processor, new FakeProbe(), cancellation);

            var result = machine.Run(NewRequest(item1, item2));

            Assert.IsEmpty(processor.Processed);
            Assert.IsTrue(result.Cancelled);
            Assert.HasCount(2, result.Files);
            Assert.IsTrue(result.Files.All(f => f.Status == FileResultStatus.Cancelled));
        }

        [TestMethod]
        public void Run_CancellationDuringItem_CompletesItemThenCancelsRemainder()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var item3 = NewItem("c");
            var cancellation = new FakeCancellation();
            var processor = new CancellingProcessor(cancellation, item2.SourcePath);
            var machine = new BatchStateMachine(processor, new FakeProbe(), cancellation);

            var result = machine.Run(NewRequest(item1, item2, item3));

            CollectionAssert.AreEqual(new[] { item1.SourcePath, item2.SourcePath }, processor.Processed);
            Assert.IsTrue(result.Cancelled);
            Assert.AreEqual(FileResultStatus.Success, result.Files[0].Status);
            Assert.AreEqual(FileResultStatus.Success, result.Files[1].Status);
            Assert.AreEqual(FileResultStatus.Cancelled, result.Files[2].Status);
        }

        [TestMethod]
        public void Run_WritesAuthoritativeSnapshotAfterEachFile()
        {
            var item1 = NewItem("a");
            var item2 = NewItem("b");
            var processor = new FakeProcessor(
                StatusResult(item1, FileResultStatus.Success),
                StatusResult(item2, FileResultStatus.Failed, "失败"));
            var machine = new BatchStateMachine(processor, new FakeProbe(), new FakeCancellation());
            var snapshots = new List<int>();
            var request = NewRequest(item1, item2);

            var result = machine.Run(request, null, snapshot => snapshots.Add(snapshot.Files.Length));

            Assert.HasCount(2, snapshots);
            CollectionAssert.AreEqual(new[] { 1, 2 }, snapshots);
            Assert.HasCount(2, result.Files);
        }

        [TestMethod]
        public void Run_EverySnapshotCarriesSerializableTimestamps()
        {
            // Regression: mid-run snapshots once carried default(DateTime),
            // which cannot be serialized to JSON (UTC conversion overflow).
            var item1 = NewItem("a");
            var processor = new FakeProcessor(StatusResult(item1, FileResultStatus.Success));
            var machine = new BatchStateMachine(processor, new FakeProbe(), new FakeCancellation());
            var snapshots = new List<JobResult>();

            machine.Run(NewRequest(item1), null, snapshots.Add);

            Assert.HasCount(1, snapshots);
            Assert.AreNotEqual(default(DateTime), snapshots[0].StartedUtc);
            Assert.AreNotEqual(default(DateTime), snapshots[0].EndedUtc);
        }

        private sealed class ThrowingProcessor : IBatchItemProcessor
        {
            private readonly HashSet<string> throwingFor;

            public ThrowingProcessor(params string[] throwingFor) => this.throwingFor = new HashSet<string>(throwingFor);

            public FileResult Process(JobItem item)
            {
                if (throwingFor.Contains(item.SourcePath))
                {
                    throw new InvalidOperationException("模拟 NX 崩溃。");
                }

                return new FileResult { SourcePath = item.SourcePath, FinalOutputPath = item.FinalOutputPath, Status = FileResultStatus.Success };
            }
        }

        private sealed class CancellingProcessor : IBatchItemProcessor
        {
            private readonly FakeCancellation cancellation;
            private readonly string cancelDuring;

            public CancellingProcessor(FakeCancellation cancellation, string cancelDuring)
            {
                this.cancellation = cancellation;
                this.cancelDuring = cancelDuring;
            }

            public List<string> Processed { get; } = new List<string>();

            public FileResult Process(JobItem item)
            {
                Processed.Add(item.SourcePath);
                if (item.SourcePath == cancelDuring)
                {
                    cancellation.IsRequested = true;
                }

                return new FileResult { SourcePath = item.SourcePath, FinalOutputPath = item.FinalOutputPath, Status = FileResultStatus.Success };
            }
        }
    }
}
