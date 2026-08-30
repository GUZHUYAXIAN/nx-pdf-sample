using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Core.Input;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class InputDiscoveryServiceTests
    {
        private sealed class FakeFileSystem : IFileSystem
        {
            public HashSet<string> Directories { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> Files { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public bool DirectoryExists(string path) => Directories.Contains(Normalize(path));

            public bool FileExists(string path) => Files.Contains(Normalize(path));

            public string GetFullPath(string path) => Normalize(path);

            public IReadOnlyList<string> EnumeratePrtFiles(string folder, bool includeSubfolders)
            {
                var root = Normalize(folder);
                var result = new List<string>();
                foreach (var file in Files)
                {
                    var isUnderRoot = file.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
                    if (!isUnderRoot)
                    {
                        continue;
                    }

                    var relative = file.Substring(root.Length + 1);
                    var hasSeparator = relative.Contains("\\");
                    if (!includeSubfolders && hasSeparator)
                    {
                        continue;
                    }

                    if (relative.EndsWith(".prt", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(file);
                    }
                }

                return result;
            }

            internal static string Normalize(string path)
            {
                var fullPath = Path.GetFullPath(path);
                return fullPath.Length > 3 && fullPath.EndsWith(":\\", StringComparison.Ordinal)
                    ? fullPath
                    : fullPath.TrimEnd('\\');
            }
        }

        private static FakeFileSystem CreateFake()
        {
            var fs = new FakeFileSystem();
            fs.Directories.Add(@"E:\图纸库");
            fs.Directories.Add(@"E:\图纸库\子目录");
            fs.Directories.Add(@"E:\图纸库\子目录\更深");

            fs.Files.Add(@"E:\图纸库\DWG_主装配.prt");
            fs.Files.Add(@"E:\图纸库\plain MODEL.Prt");
            fs.Files.Add(@"E:\图纸库\多 点.号 图.prt");
            fs.Files.Add(@"E:\图纸库\not-a-drawing.txt");
            fs.Files.Add(@"E:\图纸库\子目录\嵌套一.prt");
            fs.Files.Add(@"E:\图纸库\子目录\更深\嵌套二.PRT");
            return fs;
        }

        [TestMethod]
        public void ScanFolder_DefaultListsOnlyTopLevel()
        {
            var service = new InputDiscoveryService(CreateFake());

            var found = service.ScanFolder(@"E:\图纸库", includeSubfolders: false);

            CollectionAssert.AreEquivalent(
                new[] { @"E:\图纸库\DWG_主装配.prt", @"E:\图纸库\plain MODEL.Prt", @"E:\图纸库\多 点.号 图.prt" },
                found.ToArray());
        }

        [TestMethod]
        public void ScanFolder_RecursiveOptInIncludesNested()
        {
            var service = new InputDiscoveryService(CreateFake());

            var found = service.ScanFolder(@"E:\图纸库", includeSubfolders: true);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    @"E:\图纸库\DWG_主装配.prt",
                    @"E:\图纸库\plain MODEL.Prt",
                    @"E:\图纸库\多 点.号 图.prt",
                    @"E:\图纸库\子目录\嵌套一.prt",
                    @"E:\图纸库\子目录\更深\嵌套二.PRT"
                },
                found.ToArray());
        }

        [TestMethod]
        public void ScanFolder_MatchesExtensionCaseInsensitively()
        {
            var service = new InputDiscoveryService(CreateFake());

            var found = service.ScanFolder(@"E:\图纸库", includeSubfolders: true);

            Assert.IsTrue(found.Any(p => p.EndsWith(".PRT", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(found.All(p => p.EndsWith(".prt", StringComparison.OrdinalIgnoreCase)),
                "every result keeps its original casing");
        }

        [TestMethod]
        public void ScanFolder_MissingFolder_ThrowsClearError()
        {
            var service = new InputDiscoveryService(CreateFake());

            var error = Assert.ThrowsExactly<InvalidOperationException>(
                () => service.ScanFolder(@"E:\不存在 目录", includeSubfolders: false));

            StringAssert.Contains(error.Message, "不存在");
        }

        [TestMethod]
        public void NormalizeManualSelection_PreservesUserOrderAndDedupesCanonically()
        {
            var service = new InputDiscoveryService(CreateFake());

            var selection = service.NormalizeManualSelection(new[]
            {
                @"E:\图纸库\子目录\嵌套一.prt",
                @"E:\图纸库\Dwg_主装配.prt".Replace("Dwg_", "DWG_"),
                @"e:\图纸库\dwg_主装配.prt",
                @"E:\图纸库\..\图纸库\多 点.号 图.prt"
            });

            CollectionAssert.AreEqual(
                new[]
                {
                    @"E:\图纸库\子目录\嵌套一.prt",
                    @"E:\图纸库\DWG_主装配.prt",
                    @"E:\图纸库\多 点.号 图.prt"
                },
                selection.ToArray());
        }

        [TestMethod]
        public void NormalizeManualSelection_MissingFile_ThrowsWithAllMissingListed()
        {
            var service = new InputDiscoveryService(CreateFake());

            var error = Assert.ThrowsExactly<InvalidOperationException>(
                () => service.NormalizeManualSelection(new[]
                {
                    @"E:\图纸库\DWG_主装配.prt",
                    @"E:\图纸库\丢失.prt",
                    @"E:\图纸库\也没有.prt"
                }));

            StringAssert.Contains(error.Message, "丢失.prt");
            StringAssert.Contains(error.Message, "也没有.prt");
            StringAssert.DoesNotMatch(error.Message, NewLineAtEnd());
        }

        [TestMethod]
        public void NormalizeManualSelection_EmptyInput_ReturnsEmpty()
        {
            var service = new InputDiscoveryService(CreateFake());

            var result = service.NormalizeManualSelection(Array.Empty<string>());

            Assert.HasCount(0, result);
        }

        private static Regex NewLineAtEnd() => new Regex("\\r?\\n\\s*$", RegexOptions.Compiled);
    }
}
