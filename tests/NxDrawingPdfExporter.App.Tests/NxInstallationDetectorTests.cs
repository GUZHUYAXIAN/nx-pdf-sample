using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Tests
{
    [TestClass]
    public sealed class NxInstallationDetectorTests
    {
        private static string TempDir()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nxpdf-nxdetect-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }

        [TestMethod]
        public void Detect_MissingRoot_FailsClosed()
        {
            var missingRoot = System.IO.Path.Combine(TempDir(), "nx-10.0");

            var detection = new NxInstallationDetector(_ => NxInstallationDetector.VerifiedFileVersion).Detect(missingRoot);

            Assert.IsFalse(detection.IsSupported);
            Assert.IsNotEmpty(detection.Message);
        }

        [TestMethod]
        public void Detect_MissingNxOpenAssembly_FailsClosed()
        {
            var root = TempDir();
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "UGII"));
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, "UGII", "run_managed.exe"), Array.Empty<byte>());

            var detection = new NxInstallationDetector(_ => NxInstallationDetector.VerifiedFileVersion).Detect(root);

            Assert.IsFalse(detection.IsSupported);
            Assert.IsNotEmpty(detection.Message);
        }

        [TestMethod]
        public void Detect_WrongVersion_IsUnsupported()
        {
            var root = TempDir();
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "UGII", "managed"));
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, "UGII", "run_managed.exe"), Array.Empty<byte>());
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, "UGII", "managed", "NXOpen.dll"), Array.Empty<byte>());

            var detection = new NxInstallationDetector(_ => "12.0.0.1").Detect(root);

            Assert.IsFalse(detection.IsSupported);
            StringAssert.Contains(detection.Message, "10.0.0.24");
        }

        [TestMethod]
        public void Detect_UnreadableVersion_FailsClosed()
        {
            var root = TempDir();
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "UGII", "managed"));
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, "UGII", "run_managed.exe"), Array.Empty<byte>());
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, "UGII", "managed", "NXOpen.dll"), Array.Empty<byte>());

            var detection = new NxInstallationDetector(_ => null).Detect(root);

            Assert.IsFalse(detection.IsSupported);
        }

        [TestMethod]
        public void Detect_VerifiedInstallation_IsSupported()
        {
            var root = TempDir();
            var ugii = System.IO.Path.Combine(root, "UGII");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(ugii, "managed"));
            var launcher = System.IO.Path.Combine(ugii, "run_managed.exe");
            System.IO.File.WriteAllBytes(launcher, Array.Empty<byte>());
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(ugii, "managed", "NXOpen.dll"), Array.Empty<byte>());

            var detection = new NxInstallationDetector(_ => NxInstallationDetector.VerifiedFileVersion).Detect(root);

            Assert.IsTrue(detection.IsSupported);
            Assert.AreEqual(launcher, detection.LauncherPath);
            Assert.AreEqual("10.0.0.24", detection.DetectedVersion);
            Assert.IsEmpty(detection.Message);
        }
    }
}
