using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NxDrawingPdfExporter.Core.Drawing;
using NXOpen;
using NXOpen.Drawings;

namespace NxDrawingPdfExporter.Worker
{
    internal sealed class NxSheetInventory
    {
        public SheetInventoryReport Collect(Part workPart, IReadOnlyList<string> loadDiagnostics, out SheetNameMap nameMap)
        {
            var salt = SheetNameTokenizer.NewSalt();
            var nativeOrder = new List<DrawingSheet>();
            foreach (DrawingSheet sheet in workPart.DrawingSheets)
            {
                nativeOrder.Add(sheet);
            }

            var sheets = new List<SheetInventoryItem>();
            var entries = new List<SheetNameMapEntry>();
            for (var nativeIndex = 0; nativeIndex < nativeOrder.Count; nativeIndex++)
            {
                var sheet = nativeOrder[nativeIndex];
                var token = SheetNameTokenizer.Tokenize(sheet.Name, salt);
                sheets.Add(new SheetInventoryItem
                {
                    ExportOrderIndex = nativeIndex,
                    DraftingViewCount = sheet.GetDraftingViews().Length,
                    Length = sheet.Length,
                    Height = sheet.Height,
                    Units = sheet.Units.ToString(),
                    NameToken = token,
                    NativeIndex = nativeIndex
                });
                entries.Add(new SheetNameMapEntry { Token = token, Name = sheet.Name });
            }

            nameMap = new SheetNameMap
            {
                SaltBase64 = Convert.ToBase64String(salt),
                Entries = entries.ToArray()
            };

            return new SheetInventoryReport
            {
                Success = true,
                LoadDiagnostics = loadDiagnostics.ToArray(),
                Sheets = sheets.ToArray()
            };
        }
    }

    // Real sheet names must exist only in this map file, which the harness
    // keeps inside gitignored run artifacts. It never enters the report, the
    // console, or tracked evidence.
    [DataContract]
    public sealed class SheetNameMap
    {
        [DataMember(Order = 1)]
        public string SaltBase64 { get; set; } = "";

        [DataMember(Order = 2)]
        public SheetNameMapEntry[] Entries { get; set; } = Array.Empty<SheetNameMapEntry>();
    }

    [DataContract]
    public sealed class SheetNameMapEntry
    {
        [DataMember(Order = 1)]
        public string Token { get; set; } = "";

        [DataMember(Order = 2)]
        public string Name { get; set; } = "";
    }
}
