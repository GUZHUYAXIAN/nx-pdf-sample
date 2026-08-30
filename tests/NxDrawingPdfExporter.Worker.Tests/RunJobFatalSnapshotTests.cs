using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Jobs;
using NxDrawingPdfExporter.Worker;

namespace NxDrawingPdfExporter.Worker.Tests
{
    /// <summary>
    /// CR-04 回归（二审发现 5）：走真实 Program.Main → NxBatchRunner →
    /// BatchStateMachine → JobJsonSerializer 路径，注入"第一份快照成功
    /// 写入、后续快照写入抛错"的持久化故障，验证持久快照恢复。
    /// 测试操纵进程级静态测试缝隙，必须串行执行。
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class RunJobFatalSnapshotTests
    {
    private string scratch = "";
    private Task? pendingLockTask;

    [TestInitialize]
    public void Initialize()
    {
        scratch = Path.Combine(Path.GetTempPath(), "nxpdf-worker-fatal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
    }

    [TestCleanup]
    public void Cleanup()
    {
        NxBatchRunner.ProcessorOverride = null;
        NxBatchRunner.SnapshotWriterOverride = null;
        try
        {
            // 故障注入的独占锁可能仍在 800ms 窗口内；等它释放后再清理。
            pendingLockTask?.Wait(5000);
        }
        catch (Exception)
        {
        }

        if (Directory.Exists(scratch))
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

        private (string JobPath, string ResultPath) WriteJob(int itemCount)
        {
            var items = new JobItem[itemCount];
            for (var index = 0; index < itemCount; index++)
            {
                var source = Path.Combine(scratch, "source" + index + ".prt");
                items[index] = new JobItem
                {
                    SourcePath = source,
                    FinalOutputPath = Path.Combine(scratch, "source" + index + ".pdf"),
                    WorkerTempOutputPath = Path.Combine(scratch, ".source" + index + ".tmp.pdf")
                };
            }

            var request = new JobRequest
            {
                ProtocolVersion = ProtocolVersion.Current,
                RunId = "run-fatal-test",
                Items = items,
                OutputMode = OutputMode.BesideSource,
                ExistingPdfPolicy = ExistingPdfPolicy.Skip,
                ResultPath = Path.Combine(scratch, "result.json"),
                CancellationFlagPath = Path.Combine(scratch, "cancel.flag")
            };
            var jobPath = Path.Combine(scratch, "job.json");
            JobJsonSerializer.WriteToFile(request, jobPath);
            return (jobPath, request.ResultPath);
        }

        /// <summary>条目处理器替换：快速失败，不触碰 NX；记录已处理的条目。</summary>
        private static Core.Jobs.IBatchItemProcessor FailingProcessor()
        {
            return new FakeProcessor();
        }

        private sealed class FakeProcessor : Core.Jobs.IBatchItemProcessor
        {
            public FileResult Process(JobItem item)
            {
                return new FileResult
                {
                    SourcePath = item.SourcePath,
                    FinalOutputPath = item.FinalOutputPath,
                    Status = FileResultStatus.Failed,
                    Message = "处理器模拟失败（无 NX 环境）"
                };
            }
        }

        [TestMethod]
        public void RunJob_FatalAfterFirstSnapshot_RetainsSnapshotAndFailsUnresolvedItems()
        {
            var (jobPath, resultPath) = WriteJob(itemCount: 2);
            var snapshotWrites = 0;
            NxBatchRunner.ProcessorOverride = FailingProcessor;
            NxBatchRunner.SnapshotWriterOverride = (result, path) =>
            {
                snapshotWrites++;
                if (snapshotWrites == 1)
                {
                    // 第一份快照走真实的原子协议写入并持久化。
                    JobJsonSerializer.WriteToFile(result, path);
                    var persisted = JobJsonSerializer.ReadFromFile<JobResult>(path);
                    Assert.HasCount(1, persisted.Files);
                    return;
                }

                throw new IOException("模拟第二份快照写入失败");
            };

            var exit = Program.Main(new[] { "--run-job", jobPath });

            Assert.AreEqual(22, exit);
            Assert.IsGreaterThanOrEqualTo(2, snapshotWrites);
            var final = JobJsonSerializer.ReadFromFile<JobResult>(resultPath);
            Assert.HasCount(2, final.Files);
            Assert.AreEqual(FileResultStatus.Failed, final.Files[0].Status);
            StringAssert.Contains(final.Files[0].Message, "处理器模拟失败");
            Assert.AreEqual(FileResultStatus.Failed, final.Files[1].Status);
            StringAssert.Contains(final.Files[1].Message, "致命");
            Assert.IsNotNull(final.FatalError);
            Assert.IsGreaterThan(0, final.FatalError!.Length);
            Assert.DoesNotContain('\n', final.FatalError);
            Assert.DoesNotContain('\r', final.FatalError);
        }

        [TestMethod]
        public void RunJob_FatalWithMergeWriteFailure_KeepsDurableSnapshotValid()
        {
            var (jobPath, resultPath) = WriteJob(itemCount: 2);
            var snapshotWrites = 0;
            NxBatchRunner.ProcessorOverride = FailingProcessor;
            NxBatchRunner.SnapshotWriterOverride = (result, path) =>
            {
                snapshotWrites++;
                if (snapshotWrites == 1)
                {
                    JobJsonSerializer.WriteToFile(result, path);
                    return;
                }

                // 真实写入路径注入持久 I/O 故障：目标文件被允许读取、
                // 拒绝写入的独占句柄锁定 800ms，随后致命合并的写出也会失败。
                var locked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                pendingLockTask = Task.Run(() =>
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    locked.TrySetResult(true);
                    Thread.Sleep(800);
                });
                Assert.IsTrue(locked.Task.Wait(5000), "独占锁未在时限内取得。");
                JobJsonSerializer.WriteToFile(result, path);
            };

            var exit = Program.Main(new[] { "--run-job", jobPath });

            Assert.AreEqual(21, exit);
            Assert.IsGreaterThanOrEqualTo(2, snapshotWrites);
            // 等独占锁释放后再读取磁盘结果。
            pendingLockTask!.Wait(5000);
            // 磁盘上的 result.json 仍是第一份快照：合法 JSON，仅含已完成的条目 1。
            var onDisk = JobJsonSerializer.ReadFromFile<JobResult>(resultPath);
            Assert.HasCount(1, onDisk.Files);
            Assert.AreEqual(FileResultStatus.Failed, onDisk.Files[0].Status);
        }
    }
}
