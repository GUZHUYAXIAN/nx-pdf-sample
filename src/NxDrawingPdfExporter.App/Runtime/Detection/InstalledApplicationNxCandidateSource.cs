using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Runtime.Detection;

public sealed record InstalledApplicationEntry(string? DisplayName, string? InstallLocation);

public sealed class InstalledApplicationNxCandidateSource : INxInstallationCandidateSource
{
    public NxCandidateSource? Source => NxCandidateSource.InstalledApplication;

    private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private readonly Func<RegistryView, IReadOnlyList<InstalledApplicationEntry>> entriesReader;

    public InstalledApplicationNxCandidateSource()
        : this(ReadEntries)
    {
    }

    internal InstalledApplicationNxCandidateSource(
        Func<RegistryView, IReadOnlyList<InstalledApplicationEntry>> entriesReader)
    {
        this.entriesReader = entriesReader ?? throw new ArgumentNullException(nameof(entriesReader));
    }

    public Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<NxInstallationCandidate>();
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.AddRange(entriesReader(view)
                .Where(entry => IsSiemensNx10(entry.DisplayName) && !string.IsNullOrWhiteSpace(entry.InstallLocation))
                .Select(entry => new NxInstallationCandidate(entry.InstallLocation!, NxCandidateSource.InstalledApplication)));
        }

        return Task.FromResult<IReadOnlyList<NxInstallationCandidate>>(candidates);
    }

    private static bool IsSiemensNx10(string? displayName) =>
        !string.IsNullOrWhiteSpace(displayName)
        && displayName.Contains("Siemens NX 10.0", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<InstalledApplicationEntry> ReadEntries(RegistryView view)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using RegistryKey? uninstallKey = baseKey.OpenSubKey(UninstallKeyPath, writable: false);
        if (uninstallKey is null)
        {
            return Array.Empty<InstalledApplicationEntry>();
        }

        var entries = new List<InstalledApplicationEntry>();
        foreach (string subKeyName in uninstallKey.GetSubKeyNames())
        {
            using RegistryKey? entryKey = uninstallKey.OpenSubKey(subKeyName, writable: false);
            if (entryKey is null)
            {
                continue;
            }

            entries.Add(new InstalledApplicationEntry(
                entryKey.GetValue("DisplayName") as string,
                entryKey.GetValue("InstallLocation") as string));
        }

        return entries;
    }
}
