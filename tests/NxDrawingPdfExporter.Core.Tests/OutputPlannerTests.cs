using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Output;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class OutputPlannerTests
    {
        private sealed class FakeExistingFiles : ITargetProbe
        {
            public HashSet<string> Existing { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public bool TargetExists(string path) => Existing.Contains(Normalize(path));

            internal static string Normalize(string path) => path.TrimEnd('\\');
        }

        // 合成样例名：真实私有样例名不得进入受跟踪文件。
        private const string Drawing1 = @"E:\图纸库\DWG_示例组件A1.prt";
        private const string Drawing2 = @"E:\图纸库\plain model.prt";
        private const string MultiDot = @"E:\图纸库\多 点.号 图.prt";

        private const string Drawing1Pdf = @"E:\图纸库\DWG_示例组件A1.pdf";
        private const string Drawing1UnifiedPdf = @"E:\统一 输出\DWG_示例组件A1.pdf";

        [TestMethod]
        public void BesideSource_PreservesCompleteBasename_ReplacingOnlyLastExtension()
        {
            var planner = new OutputPlanner(new FakeExistingFiles());

            var plan = planner.Plan(
                new[] { MultiDot },
                OutputMode.BesideSource,
                unifiedDirectory: null,
                ExistingPdfPolicy.Skip);

            Assert.HasCount(1, plan);
            Assert.AreEqual(@"E:\图纸库\多 点.号 图.pdf", plan[0].FinalOutputPath);
            Assert.AreEqual(OutputPlanStatus.Ready, plan[0].Status);
        }

        [TestMethod]
        public void UnifiedDirectory_WritesAllOutputsIntoUnifiedRoot()
        {
            var planner = new OutputPlanner(new FakeExistingFiles());

            var plan = planner.Plan(
                new[] { Drawing1, Drawing2 },
                OutputMode.UnifiedDirectory,
                unifiedDirectory: @"E:\统一 输出",
                ExistingPdfPolicy.Skip);

            CollectionAssert.AreEqual(
                new[] { Drawing1UnifiedPdf, @"E:\统一 输出\plain model.pdf" },
                plan.Select(p => p.FinalOutputPath).ToArray());
        }

        [TestMethod]
        public void SkipPolicy_MarksExistingTargetsAsSkipped()
        {
            var probe = new FakeExistingFiles();
            probe.Existing.Add(Drawing1Pdf);
            var planner = new OutputPlanner(probe);

            var plan = planner.Plan(
                new[] { Drawing1 },
                OutputMode.BesideSource,
                null,
                ExistingPdfPolicy.Skip);

            Assert.AreEqual(OutputPlanStatus.SkippedExisting, plan[0].Status);
        }

        [TestMethod]
        public void OverwritePolicy_KeepsExistingTargetsReady()
        {
            var probe = new FakeExistingFiles();
            probe.Existing.Add(Drawing1Pdf);
            var planner = new OutputPlanner(probe);

            var plan = planner.Plan(
                new[] { Drawing1 },
                OutputMode.BesideSource,
                null,
                ExistingPdfPolicy.Overwrite);

            Assert.AreEqual(OutputPlanStatus.Ready, plan[0].Status);
        }

        [TestMethod]
        public void UnifiedNameCollision_MarksEveryInvolvedItemAndNoneReady()
        {
            var planner = new OutputPlanner(new FakeExistingFiles());

            var plan = planner.Plan(
                new[]
                {
                    @"E:\目录A\同名 图纸.prt",
                    @"E:\目录B\子\同名 图纸.PRT",
                    Drawing1
                },
                OutputMode.UnifiedDirectory,
                @"E:\统一 输出",
                ExistingPdfPolicy.Skip);

            var conflicted = plan.Where(p => p.Status == OutputPlanStatus.NameConflict).ToList();
            Assert.HasCount(2, conflicted);
            StringAssert.Matches(conflicted[0].Message, new System.Text.RegularExpressions.Regex("输出名称冲突"));
            StringAssert.Matches(conflicted[1].Message, new System.Text.RegularExpressions.Regex("输出名称冲突"));
            Assert.IsTrue(plan.Where(p => p.SourcePath.Contains("同名")).All(p => p.Status == OutputPlanStatus.NameConflict),
                "every colliding item is blocked");
        }

        [TestMethod]
        public void BesideSource_SameBasenamesInDifferentFolders_DoNotCollide()
        {
            var planner = new OutputPlanner(new FakeExistingFiles());

            var plan = planner.Plan(
                new[]
                {
                    @"E:\目录A\同名 图纸.prt",
                    @"E:\目录B\同名 图纸.prt"
                },
                OutputMode.BesideSource,
                null,
                ExistingPdfPolicy.Skip);

            Assert.IsTrue(plan.All(p => p.Status == OutputPlanStatus.Ready));
        }

        [TestMethod]
        public void UnifiedMode_RelativeUnifiedDirectory_ThrowsBeforeAnyPlanning()
        {
            var planner = new OutputPlanner(new FakeExistingFiles());

            Assert.ThrowsExactly<InvalidOperationException>(
                () => planner.Plan(new[] { Drawing1 }, OutputMode.UnifiedDirectory, "relative-dir", ExistingPdfPolicy.Skip));
        }

        [TestMethod]
        public void Plan_PreservesInputOrder()
        {
            var planner = new OutputPlanner(new FakeExistingFiles());

            var plan = planner.Plan(
                new[] { Drawing2, MultiDot, Drawing1 },
                OutputMode.UnifiedDirectory,
                @"E:\out",
                ExistingPdfPolicy.Skip);

            CollectionAssert.AreEqual(
                new[] { Drawing2, MultiDot, Drawing1 },
                plan.Select(p => p.SourcePath).ToArray());
        }
    }
}
