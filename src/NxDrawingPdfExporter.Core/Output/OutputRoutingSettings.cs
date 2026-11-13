using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Input;

namespace NxDrawingPdfExporter.Core.Output;

public sealed class OutputRoutingSettings
{
    public OutputRoutingSettings(OutputMode mode, string unifiedDirectory, IReadOnlyDictionary<string, string> groupTargets)
    {
        Mode = mode;
        UnifiedDirectory = unifiedDirectory;
        GroupTargets = new ReadOnlyDictionary<string, string>(groupTargets.ToDictionary(
            pair => SourceDirectoryGrouper.NormalizeDirectory(pair.Key), pair => pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    public OutputMode Mode { get; }
    public string UnifiedDirectory { get; }
    public IReadOnlyDictionary<string, string> GroupTargets { get; }

    public string ResolveDirectory(string sourcePath)
    {
        var parent = SourceDirectoryGrouper.ParentDirectory(sourcePath);
        return Mode switch
        {
            OutputMode.UnifiedDirectory => SourceDirectoryGrouper.NormalizeDirectory(UnifiedDirectory),
            OutputMode.GroupedDirectories when GroupTargets.TryGetValue(parent, out var target)
                => SourceDirectoryGrouper.NormalizeDirectory(target),
            OutputMode.GroupedDirectories or OutputMode.BesideSource => parent,
            _ => throw new ArgumentOutOfRangeException(nameof(Mode))
        };
    }
}
