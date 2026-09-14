using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NxDrawingPdfExporter.App.Runtime;

public sealed class NxInstallationValidator
{
    public const string SupportedNxOpenFileVersion = "10.0.0.24";

    private readonly Func<string, string?> fileVersionReader;

    public NxInstallationValidator(Func<string, string?>? fileVersionReader = null)
    {
        this.fileVersionReader = fileVersionReader ??
            (path => FileVersionInfo.GetVersionInfo(path).FileVersion);
    }

    public NxInstallationDiscoveryResult ValidateAndDeduplicate(
        IEnumerable<NxInstallationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var installations = new Dictionary<string, MutableInstallation>(StringComparer.OrdinalIgnoreCase);
        var issues = new List<NxDetectionIssue>();

        foreach (NxInstallationCandidate candidate in candidates)
        {
            ValidationAttempt attempt = ValidateOne(candidate);
            if (attempt.Issue is not null)
            {
                issues.Add(attempt.Issue);
                continue;
            }

            NxInstallation installation = attempt.Installation!;
            if (!installations.TryGetValue(installation.RootDirectory, out MutableInstallation? existing))
            {
                installations.Add(installation.RootDirectory, MutableInstallation.From(installation));
            }
            else
            {
                existing.AddSource(candidate.Source);
            }
        }

        return new NxInstallationDiscoveryResult(
            installations.Values.Select(value => value.ToImmutable()).ToArray(),
            issues,
            null);
    }

    private ValidationAttempt ValidateOne(NxInstallationCandidate candidate)
    {
        try
        {
            string rootDirectory = NormalizeRoot(candidate.RootDirectory);
            if (!Directory.Exists(rootDirectory))
            {
                return ValidationAttempt.FromIssue(Issue(
                    NxDetectionIssueCode.RootMissing,
                    "NX 安装目录不存在。",
                    candidate.Source));
            }

            string launcherPath = Path.Combine(rootDirectory, "UGII", "run_managed.exe");
            if (!File.Exists(launcherPath))
            {
                return ValidationAttempt.FromIssue(Issue(
                    NxDetectionIssueCode.LauncherMissing,
                    "NX 目录中缺少 UGII\\run_managed.exe。",
                    candidate.Source));
            }

            string nxOpenPath = Path.Combine(rootDirectory, "UGII", "managed", "NXOpen.dll");
            if (!File.Exists(nxOpenPath))
            {
                return ValidationAttempt.FromIssue(Issue(
                    NxDetectionIssueCode.NxOpenMissing,
                    "NX 目录中缺少 UGII\\managed\\NXOpen.dll。",
                    candidate.Source));
            }

            string? version = fileVersionReader(nxOpenPath);
            if (string.IsNullOrWhiteSpace(version))
            {
                return ValidationAttempt.FromIssue(Issue(
                    NxDetectionIssueCode.VersionUnreadable,
                    "无法读取 NXOpen.dll 的文件版本。",
                    candidate.Source));
            }

            if (!string.Equals(version, SupportedNxOpenFileVersion, StringComparison.Ordinal))
            {
                return ValidationAttempt.FromIssue(Issue(
                    NxDetectionIssueCode.UnsupportedVersion,
                    $"检测到不受支持的 NX 版本；本工具仅支持 NX {SupportedNxOpenFileVersion}。",
                    candidate.Source));
            }

            return ValidationAttempt.FromInstallation(new NxInstallation(
                rootDirectory,
                launcherPath,
                nxOpenPath,
                version,
                new[] { candidate.Source }));
        }
        catch (Exception error) when (IsExpectedPathOrVersionError(error))
        {
            return ValidationAttempt.FromIssue(Issue(
                NxDetectionIssueCode.InvalidPath,
                "NX 安装路径无法访问或格式无效。",
                candidate.Source,
                error.GetType().Name));
        }
    }

    private static string NormalizeRoot(string rootDirectory)
    {
        string normalized = rootDirectory.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = normalized[1..^1].Trim();
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("NX root directory is required.", nameof(rootDirectory));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalized));
    }

    private static bool IsExpectedPathOrVersionError(Exception error) =>
        error is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException;

    private static NxDetectionIssue Issue(
        NxDetectionIssueCode code,
        string message,
        NxCandidateSource source,
        string? diagnostic = null) =>
        new(code, message, source, diagnostic);

    private sealed record ValidationAttempt(NxInstallation? Installation, NxDetectionIssue? Issue)
    {
        public static ValidationAttempt FromInstallation(NxInstallation installation) => new(installation, null);

        public static ValidationAttempt FromIssue(NxDetectionIssue issue) => new(null, issue);
    }

    private sealed class MutableInstallation
    {
        private readonly List<NxCandidateSource> sources;

        private MutableInstallation(NxInstallation installation)
        {
            RootDirectory = installation.RootDirectory;
            LauncherPath = installation.LauncherPath;
            NxOpenPath = installation.NxOpenPath;
            DetectedVersion = installation.DetectedVersion;
            sources = installation.Sources.ToList();
        }

        public string RootDirectory { get; }

        public string LauncherPath { get; }

        public string NxOpenPath { get; }

        public string DetectedVersion { get; }

        public static MutableInstallation From(NxInstallation installation) => new(installation);

        public void AddSource(NxCandidateSource source)
        {
            if (!sources.Contains(source))
            {
                sources.Add(source);
            }
        }

        public NxInstallation ToImmutable() => new(
            RootDirectory,
            LauncherPath,
            NxOpenPath,
            DetectedVersion,
            sources.ToArray());
    }
}
