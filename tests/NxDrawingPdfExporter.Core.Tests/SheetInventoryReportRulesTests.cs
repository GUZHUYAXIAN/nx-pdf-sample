using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Core.Drawing;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class SheetInventoryReportRulesTests
    {
        [TestMethod]
        public void Validate_SuccessReportWithUsableFacts_ReturnsNoIssues()
        {
            var report = ValidSuccessReport();

            Assert.IsEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_NullReport_ReturnsIssue()
        {
            var issues = SheetInventoryReportRules.Validate(null!);

            Assert.HasCount(1, issues);
        }

        [TestMethod]
        public void Validate_SuccessWithEmptySheets_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets = Array.Empty<SheetInventoryItem>();

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_SuccessWithoutSheetArray_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets = null!;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_NonSequentialExportOrderIndices_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[1].ExportOrderIndex = 5;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_DuplicateExportOrderIndices_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[2].ExportOrderIndex = report.Sheets[1].ExportOrderIndex;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_NativeIndexNotMatchingArrayPosition_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[0].NativeIndex = 7;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_SuccessWithFailureText_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Failure = "unexpected failure text";

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_SuccessWithEmptyLoadDiagnosticsArray_IsUsable()
        {
            var report = ValidSuccessReport();

            Assert.IsEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_SuccessWithNullLoadDiagnostics_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.LoadDiagnostics = null!;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_ZeroSheetSize_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[2].Length = 0;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_EmptyUnits_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[0].Units = "";

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_NegativeDraftingViewCount_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[0].DraftingViewCount = -1;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_EmptyNameToken_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[1].NameToken = "";

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_NonHexNameToken_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[1].NameToken = "not-a-hex-token!!!!";

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_DuplicateNameTokens_ReturnsIssue()
        {
            var report = ValidSuccessReport();
            report.Sheets[2].NameToken = report.Sheets[0].NameToken;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_FailedReportWithReason_IsStructurallyValid()
        {
            var report = SheetInventoryReport.Failed("NX 制图页清点失败: 测试原因");

            Assert.IsEmpty(SheetInventoryReportRules.Validate(report));
        }

        [TestMethod]
        public void Validate_FailedReportWithoutReason_ReturnsIssue()
        {
            var report = SheetInventoryReport.Failed("");
            report.Failure = null;

            Assert.IsNotEmpty(SheetInventoryReportRules.Validate(report));
        }

        private static SheetInventoryReport ValidSuccessReport()
        {
            return new SheetInventoryReport
            {
                Success = true,
                LoadDiagnostics = Array.Empty<string>(),
                Sheets = new[]
                {
                    new SheetInventoryItem
                    {
                        ExportOrderIndex = 0,
                        NativeIndex = 0,
                        DraftingViewCount = 2,
                        Length = 420,
                        Height = 594,
                        Units = "Millimeters",
                        NameToken = "0123456789abcdef"
                    },
                    new SheetInventoryItem
                    {
                        ExportOrderIndex = 1,
                        NativeIndex = 1,
                        DraftingViewCount = 0,
                        Length = 420,
                        Height = 594,
                        Units = "Millimeters",
                        NameToken = "fedcba9876543210"
                    },
                    new SheetInventoryItem
                    {
                        ExportOrderIndex = 2,
                        NativeIndex = 2,
                        DraftingViewCount = 1,
                        Length = 297,
                        Height = 420,
                        Units = "Millimeters",
                        NameToken = "abcdef0123456789"
                    }
                }
            };
        }
    }
}
