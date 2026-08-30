using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Core.Drawing;

namespace NxDrawingPdfExporter.Core.Tests
{
    [TestClass]
    public sealed class SourceFileGuardTests
    {
        [TestMethod]
        public void Capture_UnchangedFile_RemainsUnchanged()
        {
            using var fixture = new TemporaryFileFixture("original drawing bytes");
            var guard = new SourceFileGuard();

            var snapshot = guard.Capture(fixture.Path);

            Assert.IsFalse(guard.HasChanged(snapshot));
            Assert.AreEqual(Path.GetFullPath(fixture.Path), snapshot.FullPath);
            Assert.IsGreaterThan(0L, snapshot.Length);
            Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.Sha256));
        }

        [TestMethod]
        public void HasChanged_RewrittenFile_ReturnsTrue()
        {
            using var fixture = new TemporaryFileFixture("original drawing bytes");
            var guard = new SourceFileGuard();
            var snapshot = guard.Capture(fixture.Path);

            File.WriteAllText(fixture.Path, "changed drawing bytes with a different length");

            Assert.IsTrue(guard.HasChanged(snapshot));
        }

        private sealed class TemporaryFileFixture : IDisposable
        {
            private readonly string directory;

            public TemporaryFileFixture(string contents)
            {
                directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nxpdf-source-guard-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                Path = System.IO.Path.Combine(directory, "drawing.prt");
                File.WriteAllText(Path, contents);
            }

            public string Path { get; }

            public void Dispose()
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
