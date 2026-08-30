using System;
using System.Diagnostics;
using System.IO;

namespace NxDrawingPdfExporter.App.Runtime
{
    public sealed class NxInstallationDetection
    {
        public bool IsSupported { get; set; }

        public string RootDirectory { get; set; } = "";

        public string LauncherPath { get; set; } = "";

        public string DetectedVersion { get; set; } = "";

        /// <summary>面向用户的中文状态说明；受支持时为空。</summary>
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Fail-closed NX 检测：只接受计划核验过的本机 NX 10 根目录与
    /// NXOpen.dll 文件版本 10.0.0.24；其他版本显示为不受支持且不能启动。
    /// </summary>
    public sealed class NxInstallationDetector
    {
        public const string VerifiedRoot = @"D:\Program Files\Siemens\NX 10.0";
        public const string VerifiedFileVersion = "10.0.0.24";

        private readonly Func<string, string?> fileVersionReader;

        public NxInstallationDetector(Func<string, string?>? fileVersionReader = null)
        {
            this.fileVersionReader = fileVersionReader ?? ReadFileVersion;
        }

        public NxInstallationDetection Detect() => Detect(VerifiedRoot);

        public NxInstallationDetection Detect(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                return new NxInstallationDetection
                {
                    Message = $"未检测到 NX 安装目录（期望: {VerifiedRoot}）。"
                };
            }

            var launcherPath = Path.Combine(rootDirectory, "UGII", "run_managed.exe");
            var nxOpenPath = Path.Combine(rootDirectory, "UGII", "managed", "NXOpen.dll");
            if (!File.Exists(launcherPath))
            {
                return new NxInstallationDetection
                {
                    RootDirectory = rootDirectory,
                    Message = "NX 目录中缺少已验证的启动器 UGII\\run_managed.exe。"
                };
            }

            if (!File.Exists(nxOpenPath))
            {
                return new NxInstallationDetection
                {
                    RootDirectory = rootDirectory,
                    LauncherPath = launcherPath,
                    Message = "NX 目录中缺少 UGII\\managed\\NXOpen.dll。"
                };
            }

            var version = fileVersionReader(nxOpenPath);
            if (string.IsNullOrWhiteSpace(version))
            {
                return new NxInstallationDetection
                {
                    RootDirectory = rootDirectory,
                    LauncherPath = launcherPath,
                    Message = "无法读取 NXOpen.dll 的文件版本。"
                };
            }

            if (!string.Equals(version, VerifiedFileVersion, StringComparison.OrdinalIgnoreCase))
            {
                return new NxInstallationDetection
                {
                    IsSupported = false,
                    RootDirectory = rootDirectory,
                    LauncherPath = launcherPath,
                    DetectedVersion = version,
                    Message = $"检测到不受支持的 NX 版本 {version}；本工具仅支持 NX 10.0.0.24。"
                };
            }

            return new NxInstallationDetection
            {
                IsSupported = true,
                RootDirectory = rootDirectory,
                LauncherPath = launcherPath,
                DetectedVersion = version
            };
        }

        private static string? ReadFileVersion(string path)
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
    }
}
