using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Core.Drawing;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class ExportReportRulesTests
    {
        [TestMethod]
        public void Validate_NullReport_ReportsIssue()
        {
            var issues = ExportReportRules.Validate(null);

            Assert.HasCount(1, issues);
        }

        [TestMethod]
        public void Validate_FailedReportWithoutReason_ReportsIssue()
        {
            var report = new ExportReport { Success = false };

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_FailedReportWithReason_HasNoIssues()
        {
            var issues = ExportReportRules.Validate(ExportReport.Failed("制图视图更新失败。"));

            Assert.IsEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportWithEmptySheets_ReportsIssue()
        {
            var report = BuildValidReport(0);

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportWithoutSourceUnchanged_ReportsIssue()
        {
            var report = BuildValidReport(1);
            report.SourceUnchanged = false;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportWithSelectedCountMismatch_ReportsIssue()
        {
            var report = BuildValidReport(2);
            report.SelectedCount = 1;
            report.SkippedCount = report.Sheets.Length - report.SelectedCount;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportWithSkippedCountMismatch_ReportsIssue()
        {
            var report = BuildValidReport(2, 1);
            report.SkippedCount = 0;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportWithIncoherentOrderIndices_ReportsIssues()
        {
            var report = BuildValidReport(2);
            report.Sheets[1].ExportOrderIndex = 5;
            report.Sheets[1].NativeIndex = 5;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportWithInvalidToken_ReportsIssue()
        {
            var report = BuildValidReport(1);
            report.Sheets[0].NameToken = "not-a-token";

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportWithDuplicateTokens_ReportsIssue()
        {
            var report = BuildValidReport(2);
            report.Sheets[1].NameToken = report.Sheets[0].NameToken;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SelectedSheetWithoutDraftingViews_ReportsIssue()
        {
            var report = BuildValidReport(1);
            report.Sheets[0].DraftingViewCount = 0;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SkippedSheetWithDraftingViews_ReportsIssue()
        {
            var report = BuildValidReport(2, 1);
            report.Sheets[1].DraftingViewCount = 3;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_SelectedSheetWithInvalidSize_ReportsIssue()
        {
            var report = BuildValidReport(1);
            report.Sheets[0].Length = 0;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        [TestMethod]
        public void Validate_WellFormedReport_HasNoIssues()
        {
            var report = BuildValidReport(3);

            var issues = ExportReportRules.Validate(report);

            Assert.IsEmpty(issues);
        }

        [TestMethod]
        public void Validate_SuccessfulReportMissingLoadDiagnostics_ReportsIssue()
        {
            var report = BuildValidReport(1);
            report.LoadDiagnostics = null!;

            var issues = ExportReportRules.Validate(report);

            Assert.IsNotEmpty(issues);
        }

        /// <summary>构造 selectedCount 张有效页 + 其余为模板页的规则有效报告。</summary>
        private static ExportReport BuildValidReport(int totalSheets, int? selectedCount = null)
        {
            var selected = selectedCount ?? totalSheets;
            var sheets = new List<ExportSheetFact>();
            for (var index = 0; index < totalSheets; index++)
            {
                var isSelected = index < selected;
                sheets.Add(new ExportSheetFact
                {
                    ExportOrderIndex = index,
                    NativeIndex = index,
                    DraftingViewCount = isSelected ? 1 : 0,
                    Selected = isSelected,
                    Length = 420,
                    Height = 594,
                    Units = "Millimeters",
                    NameToken = index.ToString("x16")
                });
            }

            return new ExportReport
            {
                Success = true,
                LoadDiagnostics = Array.Empty<string>(),
                Sheets = sheets.ToArray(),
                SelectedCount = selected,
                SkippedCount = totalSheets - selected,
                SourceUnchanged = true,
                ElapsedMilliseconds = 1234
            };
        }
    }
}
