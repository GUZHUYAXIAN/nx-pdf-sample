using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Runtime.Detection;

public sealed class RegistryNxCandidateSource : INxInstallationCandidateSource
{
    public NxCandidateSource? Source => NxCandidateSource.Registry;

    private const string NxKeyPath = @"SOFTWARE\Unigraphics Solutions\NX";
    private readonly Func<RegistryView, IReadOnlyList<string>> rootsReader;

    public RegistryNxCandidateSource()
        : this(ReadRoots)
    {
    }

    internal RegistryNxCandidateSource(Func<RegistryView, IReadOnlyList<string>> rootsReader)
    {
        this.rootsReader = rootsReader ?? throw new ArgumentNullException(nameof(rootsReader));
    }

    public Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<NxInstallationCandidate>();
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.AddRange(rootsReader(view)
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Select(root => new NxInstallationCandidate(root, NxCandidateSource.Registry)));
        }

        return Task.FromResult<IReadOnlyList<NxInstallationCandidate>>(candidates);
    }

    private static IReadOnlyList<string> ReadRoots(RegistryView view)
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using RegistryKey? nxKey = baseKey.OpenSubKey(NxKeyPath, writable: false);
        if (nxKey is null)
        {
            return Array.Empty<string>();
        }

        var roots = new List<string>();
        foreach (string subKeyName in nxKey.GetSubKeyNames())
        {
            using RegistryKey? versionKey = nxKey.OpenSubKey(subKeyName, writable: false);
            if (versionKey?.GetValue("UGII_BASE_DIR") is string root && !string.IsNullOrWhiteSpace(root))
            {
                roots.Add(root);
            }
        }

        return roots;
    }
}
