using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Jobs;

namespace NxDrawingPdfExporter.Contracts.Tests
{
    [TestClass]
    public sealed class ResultSummaryTests
    {
        private static FileResult Result(FileResultStatus status)
        {
            return new FileResult { SourcePath = "s", FinalOutputPath = "t", Status = status };
        }

        [TestMethod]
        public void From_EmptyResults_AllCountsAreZero()
        {
            var summary = ResultSummary.From(Array.Empty<FileResult>());

            Assert.AreEqual(0, summary.Total);
            Assert.IsFalse(summary.HasFailures);
        }

        [TestMethod]
        public void From_CountsEveryStatusSeparately()
        {
            var results = new List<FileResult>
            {
                Result(FileResultStatus.Success),
                Result(FileResultStatus.Success),
                Result(FileResultStatus.Overwritten),
                Result(FileResultStatus.SkippedExisting),
                Result(FileResultStatus.PureModel),
                Result(FileResultStatus.NoValidSheets),
                Result(FileResultStatus.NameConflict),
                Result(FileResultStatus.Cancelled),
                Result(FileResultStatus.Failed)
            };

            var summary = ResultSummary.From(results);

            Assert.AreEqual(9, summary.Total);
            Assert.AreEqual(2, summary.Succeeded);
            Assert.AreEqual(1, summary.Overwritten);
            Assert.AreEqual(1, summary.SkippedExisting);
            Assert.AreEqual(1, summary.PureModel);
            Assert.AreEqual(1, summary.NoValidSheets);
            Assert.AreEqual(1, summary.NameConflicts);
            Assert.AreEqual(1, summary.Cancelled);
            Assert.AreEqual(1, summary.Failed);
            Assert.IsTrue(summary.HasFailures);
        }

        [TestMethod]
        public void From_OnlySuccessfulResults_HasNoFailures()
        {
            var summary = ResultSummary.From(new[]
            {
                Result(FileResultStatus.Success),
                Result(FileResultStatus.Overwritten),
                Result(FileResultStatus.SkippedExisting)
            });

            Assert.IsFalse(summary.HasFailures);
        }

        [TestMethod]
        public void ToChineseSummary_ContainsAllCounts()
        {
            var summary = ResultSummary.From(new[]
            {
                Result(FileResultStatus.Success),
                Result(FileResultStatus.Failed)
            });

            var text = summary.ToChineseSummary();

            StringAssert.Contains(text, "2");
            StringAssert.Contains(text, "1");
            StringAssert.Contains(text, "失败");
        }
    }
}
