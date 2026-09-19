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

            // Inspect layouts without guessing from the root folder's name.
            var profiles = NxRuntimeProfile.Supported.Where(p =>
                Directory.Exists(Path.Combine(rootDirectory, p.BinDirectoryName, "managed"))).ToArray();
            if (profiles.Length == 0) profiles = new[] { NxRuntimeProfile.Nx10 };
            if (profiles.Length > 1)
                return ValidationAttempt.FromIssue(Issue(NxDetectionIssueCode.UnsupportedVersion,
                    "安装目录同时含有两代 managed 布局，无法确认组件来源，请选择独立的 NX 根目录。", candidate.Source));
            NxRuntimeProfile profile = profiles[0];
            string launcherPath = Path.Combine(rootDirectory, profile.BinDirectoryName, profile.LauncherName);
            if (!File.Exists(launcherPath))
                return ValidationAttempt.FromIssue(Issue(NxDetectionIssueCode.LauncherMissing,
                    $"{profile.Label} 目录中缺少 {profile.BinDirectoryName}\\{profile.LauncherName}。", candidate.Source));

            string managed = Path.Combine(rootDirectory, profile.BinDirectoryName, "managed");
            string[] names = { "NXOpen.dll", "NXOpen.UF.dll", "NXOpen.Utilities.dll" };
            string[] expected = { profile.NxOpenVersion, profile.UfVersion, profile.UtilitiesVersion };
            var versions = new string[3];
            for (int i = 0; i < names.Length; i++)
            {
                string path = Path.Combine(managed, names[i]);
                if (!File.Exists(path))
                    return ValidationAttempt.FromIssue(Issue(NxDetectionIssueCode.NxOpenMissing,
                        $"{profile.Label} 安装缺少 {names[i]}，请检查安装完整性。", candidate.Source));
                string? version = fileVersionReader(path);
                if (string.IsNullOrWhiteSpace(version))
                    return ValidationAttempt.FromIssue(Issue(NxDetectionIssueCode.VersionUnreadable,
                        $"无法读取 {names[i]} 的文件版本。", candidate.Source));
                if (!string.Equals(version, expected[i], StringComparison.Ordinal))
                    return ValidationAttempt.FromIssue(Issue(NxDetectionIssueCode.UnsupportedVersion,
                        $"{profile.Label} 的 {names[i]} 版本不受支持（要求 {expected[i]}）。当前支持 NX 10.0.0.24 和已验证的 NX 2506 组件组合；不支持混搭或未知补丁。", candidate.Source));
                versions[i] = version;
            }
            return ValidationAttempt.FromInstallation(new NxInstallation(rootDirectory, launcherPath,
                Path.Combine(managed, names[0]), versions[0], new[] { candidate.Source })
            { Profile = profile, UfVersion = versions[1], UtilitiesVersion = versions[2] });
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
        private readonly NxInstallation installation;

        private MutableInstallation(NxInstallation installation)
        {
            this.installation = installation;
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

        public NxInstallation ToImmutable() => installation with { Sources = sources.ToArray() };
    }
}
