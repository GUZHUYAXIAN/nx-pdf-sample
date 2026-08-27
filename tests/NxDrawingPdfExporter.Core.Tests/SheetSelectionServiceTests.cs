using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Core.Drawing;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class SheetSelectionServiceTests
    {
        [TestMethod]
        public void Select_NoSheets_ClassifiesPureModel()
        {
            var selection = new SheetSelectionService().Select(new List<SheetFacts>());

            Assert.AreEqual(SheetSelectionKind.PureModel, selection.Kind);
            Assert.IsEmpty(selection.ExportIndices);
            Assert.IsEmpty(selection.SkipIndices);
        }

        [TestMethod]
        public void Select_OnlyZeroDraftingViews_ClassifiesNoValidSheets()
        {
            var selection = new SheetSelectionService().Select(new[]
            {
                new SheetFacts { NavigatorIndex = 0, Name = "模板页", DraftingViewCount = 0 },
                new SheetFacts { NavigatorIndex = 1, Name = "标题栏页", DraftingViewCount = 0 }
            });

            Assert.AreEqual(SheetSelectionKind.NoValidSheets, selection.Kind);
            CollectionAssert.AreEqual(new[] { 0, 1 }, selection.SkipIndices.ToArray());
        }

        [TestMethod]
        public void Select_DraftingViews_ExportsInNavigatorOrder()
        {
            var selection = new SheetSelectionService().Select(new[]
            {
                new SheetFacts { NavigatorIndex = 8, Name = "第二页", DraftingViewCount = 1 },
                new SheetFacts { NavigatorIndex = 3, Name = "模板页", DraftingViewCount = 0 },
                new SheetFacts { NavigatorIndex = 1, Name = "第一页", DraftingViewCount = 2 }
            });

            Assert.AreEqual(SheetSelectionKind.Exportable, selection.Kind);
            CollectionAssert.AreEqual(new[] { 8, 1 }, selection.ExportIndices.ToArray());
            CollectionAssert.AreEqual(new[] { 3 }, selection.SkipIndices.ToArray());
        }

        [TestMethod]
        public void Select_InspectionFailure_ReturnsFatalFailureRatherThanBlankClassification()
        {
            var selection = new SheetSelectionService().Select(new[]
            {
                new SheetFacts { NavigatorIndex = 0, Name = "视图更新失败", DraftingViewCount = 0, InspectionFailure = "关联模型缺失" }
            });

            Assert.AreEqual(SheetSelectionKind.Failed, selection.Kind);
            StringAssert.Contains(selection.FatalFailure!, "关联模型缺失");
            Assert.IsEmpty(selection.ExportIndices);
        }
    }
}
