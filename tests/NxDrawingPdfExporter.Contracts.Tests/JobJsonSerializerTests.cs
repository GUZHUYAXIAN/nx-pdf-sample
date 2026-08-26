using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Contracts.Tests
{
    [TestClass]
    public sealed class JobJsonSerializerTests
    {
        private static JobRequest BuildFullRequest()
        {
            return new JobRequest
            {
                RunId = "run-20260826-01",
                Items = new[]
                {
                    new JobItem
                    {
                        SourcePath = @"E:\图纸 目录\PRIVATE_SAMPLE_A.prt",
                        FinalOutputPath = @"E:\图纸 目录\PRIVATE_SAMPLE_A.pdf",
                        WorkerTempOutputPath = @"E:\图纸 目录\.PRIVATE_SAMPLE_A.run-20260826-01.9f2c.tmp.pdf"
                    },
                    new JobItem
                    {
                        SourcePath = @"E:\图 纸\plain model.prt",
                        FinalOutputPath = @"E:\输 出\plain model.pdf",
                        WorkerTempOutputPath = @"E:\输 出\.plain model.run-20260826-01.aa41.tmp.pdf"
                    }
                },
                OutputMode = OutputMode.UnifiedDirectory,
                UnifiedOutputDirectory = @"E:\统 一 输出",
                ExistingPdfPolicy = ExistingPdfPolicy.Overwrite,
                ResultPath = @"C:\ProgramData\runs\run-20260826-01\result.json",
                CancellationFlagPath = @"C:\ProgramData\runs\run-20260826-01\cancel.flag"
            };
        }

        private static DateTime Ms(DateTime value) => new DateTime(value.Ticks - (value.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);

        private static JobResult BuildFullResult()
        {
            var statuses = new[]
            {
                FileResultStatus.Success,
                FileResultStatus.Overwritten,
                FileResultStatus.SkippedExisting,
                FileResultStatus.PureModel,
                FileResultStatus.NoValidSheets,
                FileResultStatus.NameConflict,
                FileResultStatus.Cancelled,
                FileResultStatus.Failed
            };

            var files = statuses.Select((status, index) => new FileResult
            {
                SourcePath = @"E:\源 目录\文件" + index + ".prt",
                FinalOutputPath = @"E:\目 标\文件" + index + ".pdf",
                Status = status,
                Message = "映射的中文结果消息 " + index,
                ExportedSheets = status == FileResultStatus.Success ? new[] { "页A", "Sheet 02" } : Array.Empty<string>(),
                SkippedSheets = status == FileResultStatus.Success ? new[] { "模板页-图框" } : Array.Empty<string>(),
                ElapsedMilliseconds = 1000 + index
            }).ToArray();

            return new JobResult
            {
                RunId = "run-20260826-01",
                StartedUtc = Ms(DateTime.UtcNow.AddMinutes(-3)),
                EndedUtc = Ms(DateTime.UtcNow),
                Cancelled = true,
                Files = files,
                FatalError = null
            };
        }

        [TestMethod]
        public void Request_RoundTrip_PreservesUnicodeSpacesAndOrder()
        {
            var request = BuildFullRequest();

            var json = JobJsonSerializer.Serialize(request);
            var back = JobJsonSerializer.Deserialize<JobRequest>(json);

            Assert.AreEqual(ProtocolVersion.Current, back.ProtocolVersion);
            Assert.AreEqual(request.RunId, back.RunId);
            Assert.AreEqual(request.OutputMode, back.OutputMode);
            Assert.AreEqual(request.ExistingPdfPolicy, back.ExistingPdfPolicy);
            Assert.AreEqual(request.UnifiedOutputDirectory, back.UnifiedOutputDirectory);
            Assert.AreEqual(request.ResultPath, back.ResultPath);
            Assert.AreEqual(request.CancellationFlagPath, back.CancellationFlagPath);
            Assert.HasCount(request.Items.Length, back.Items);
            for (var i = 0; i < request.Items.Length; i++)
            {
                Assert.AreEqual(request.Items[i].SourcePath, back.Items[i].SourcePath, $"item {i} source");
                Assert.AreEqual(request.Items[i].FinalOutputPath, back.Items[i].FinalOutputPath, $"item {i} output");
                Assert.AreEqual(request.Items[i].WorkerTempOutputPath, back.Items[i].WorkerTempOutputPath, $"item {i} temp");
            }

            CollectionAssert.AreEqual(
                request.Items.Select(i => i.SourcePath).ToArray(),
                back.Items.Select(i => i.SourcePath).ToArray());
        }

        [TestMethod]
        public void Result_RoundTrip_CoversEveryStatusAndKeepsSheetOrder()
        {
            var result = BuildFullResult();

            var json = JobJsonSerializer.Serialize(result);
            var back = JobJsonSerializer.Deserialize<JobResult>(json);

            Assert.AreEqual(result.RunId, back.RunId);
            Assert.AreEqual(result.StartedUtc, back.StartedUtc);
            Assert.AreEqual(result.EndedUtc, back.EndedUtc);
            Assert.AreEqual(result.Cancelled, back.Cancelled);
            Assert.IsNull(back.FatalError);
            Assert.HasCount(8, back.Files);
            CollectionAssert.AreEqual(
                Enum.GetValues(typeof(FileResultStatus)).Cast<FileResultStatus>().ToArray(),
                back.Files.Select(f => f.Status).ToArray());

            var success = back.Files.Single(f => f.Status == FileResultStatus.Success);
            CollectionAssert.AreEqual(new[] { "页A", "Sheet 02" }, success.ExportedSheets);
            CollectionAssert.AreEqual(new[] { "模板页-图框" }, success.SkippedSheets);
        }

        [TestMethod]
        public void Deserialize_MissingRequiredField_ThrowsSafeProtocolError()
        {
            var json = JobJsonSerializer.Serialize(BuildFullRequest());
            // Strip the required ResultPath member from the payload.
            var broken = Regex.Replace(json, "\"ResultPath\":\"[^\"]*\",?", string.Empty);
            Assert.AreNotEqual(json, broken);

            var error = Assert.ThrowsExactly<ProtocolException>(() => JobJsonSerializer.Deserialize<JobRequest>(broken));
            StringAssert.DoesNotMatch(error.Message, NewLinePattern());
            StringAssert.DoesNotMatch(error.Message, StackFramePattern());
        }

        [TestMethod]
        public void Deserialize_UnknownProtocolVersion_Throws()
        {
            var json = JobJsonSerializer.Serialize(BuildFullRequest()).Replace("\"ProtocolVersion\":\"1\"", "\"ProtocolVersion\":\"999\"");

            Assert.ThrowsExactly<ProtocolException>(() => JobJsonSerializer.Deserialize<JobRequest>(json));
        }

        [TestMethod]
        public void Deserialize_UndefinedEnumValue_Throws()
        {
            var json = JobJsonSerializer.Serialize(BuildFullRequest()).Replace("\"ExistingPdfPolicy\":1", "\"ExistingPdfPolicy\":42");

            Assert.ThrowsExactly<ProtocolException>(() => JobJsonSerializer.Deserialize<JobRequest>(json));
        }

        [TestMethod]
        public void Deserialize_RelativeRequiredPath_Throws()
        {
            var request = BuildFullRequest();
            request.ResultPath = "just-result.json";

            var json = JobJsonSerializer.Serialize(request);

            Assert.ThrowsExactly<ProtocolException>(() => JobJsonSerializer.Deserialize<JobRequest>(json));
        }

        [TestMethod]
        public void FileRoundTrip_MalformedJson_ThrowsSafeProtocolError()
        {
            var directory = Path.Combine(Path.GetTempPath(), "nxpdf-proto-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "result.json");
                File.WriteAllText(path, "{ this is not json ");

                var error = Assert.ThrowsExactly<ProtocolException>(() => JobJsonSerializer.ReadFromFile<JobResult>(path));
                StringAssert.DoesNotMatch(error.Message, StackFramePattern());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void WriteThenRead_LeavesNoTemporaryFilesBehind()
        {
            var directory = Path.Combine(Path.GetTempPath(), "nxpdf-proto-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "job.json");
                var request = BuildFullRequest();

                JobJsonSerializer.WriteToFile(request, path);
                var back = JobJsonSerializer.ReadFromFile<JobRequest>(path);

                Assert.AreEqual(request.RunId, back.RunId);
                var leftovers = Directory.GetFiles(directory).Where(f => !string.Equals(Path.GetFullPath(f), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)).ToList();
                Assert.IsEmpty(leftovers, "atomic write must clean up its temporary file");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void WriteToFile_ReplacesExistingContentAtomically()
        {
            var directory = Path.Combine(Path.GetTempPath(), "nxpdf-proto-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "result.json");
                var first = BuildFullResult();
                var second = BuildFullResult();
                second.RunId = "run-second";
                second.Cancelled = false;

                JobJsonSerializer.WriteToFile(first, path);
                JobJsonSerializer.WriteToFile(second, path);
                var back = JobJsonSerializer.ReadFromFile<JobResult>(path);

                Assert.AreEqual("run-second", back.RunId);
                Assert.IsFalse(back.Cancelled);
                var leftovers = Directory.GetFiles(directory);
                Assert.HasCount(1, leftovers, "replace flow must not leave temp/backup files");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void UserFacingMessages_DoNotCarryStackTracesOrTraceProperties()
        {
            var result = BuildFullResult();
            var failed = result.Files.Single(f => f.Status == FileResultStatus.Failed);
            failed.Message = "NX 打开部件失败（错误代码 NXOPEN-OPEN-FAIL）";

            var json = JobJsonSerializer.Serialize(result);

            StringAssert.DoesNotMatch(json, StackFramePattern());
            foreach (var file in result.Files)
            {
                Assert.IsFalse(Regex.IsMatch(file.Message, "\\r?\\n"), "messages must be single-line");
            }

            var traceLikeMembers =
                typeof(FileResult).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Concat(typeof(JobResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    .Where(p => p.Name.IndexOf("StackTrace", StringComparison.OrdinalIgnoreCase) >= 0
                                || p.Name.IndexOf("InnerException", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(p => p.Name)
                    .ToList();

            Assert.IsEmpty(traceLikeMembers, "user-facing results must not expose trace-carrying members");
        }

        [TestMethod]
        public void Deserialize_NullItemsArray_Throws()
        {
            var json = "{\"ProtocolVersion\":\"1\",\"RunId\":\"r\",\"Items\":null,\"OutputMode\":0,\"ExistingPdfPolicy\":0,\"ResultPath\":\"C:\\\\a\\\\result.json\",\"CancellationFlagPath\":\"C:\\\\a\\\\cancel.flag\"}";

            Assert.ThrowsExactly<ProtocolException>(() => JobJsonSerializer.Deserialize<JobRequest>(json));
        }

        private static Regex StackFramePattern() => new Regex("\\r?\\n\\s*at ", RegexOptions.Compiled);

        private static Regex NewLinePattern() => new Regex("\\r?\\n", RegexOptions.Compiled);
    }
}
