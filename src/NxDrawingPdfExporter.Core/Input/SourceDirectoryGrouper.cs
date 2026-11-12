using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NxDrawingPdfExporter.Core.Input;

public sealed class SourceDirectoryGroup
{
    internal SourceDirectoryGroup(string key, string label, string[] paths)
    {
        Key = key;
        Label = label;
        SourcePaths = Array.AsReadOnly(paths);
    }

    public string Key { get; }
    public string Label { get; }
    public IReadOnlyList<string> SourcePaths { get; }
}

public static class SourceDirectoryGrouper
{
    public static IReadOnlyList<SourceDirectoryGroup> Group(IReadOnlyList<string> sources,
        IReadOnlyDictionary<string, string> knownLabels)
    {
        var labels = knownLabels.ToDictionary(pair => NormalizeDirectory(pair.Key), pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        var next = labels.Count;
        var groups = sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .GroupBy(ParentDirectory, StringComparer.OrdinalIgnoreCase);
        return Array.AsReadOnly(groups.Select(group => new SourceDirectoryGroup(group.Key,
            labels.TryGetValue(group.Key, out var label) ? label : NextLabel(ref next, labels), group.ToArray())).ToArray());
    }

    public static string ParentDirectory(string path) => NormalizeDirectory(
        Path.GetDirectoryName(Path.GetFullPath(path)) ?? throw new ArgumentException("Missing parent directory.", nameof(path)));

    public static string NormalizeDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("An absolute directory is required.", nameof(directory));

        directory = directory.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var root = Path.GetPathRoot(directory);
        if (string.IsNullOrEmpty(root) || root == "\\" || (root.Length == 2 && root[1] == ':'))
            throw new ArgumentException("An absolute directory is required.", nameof(directory));

        var full = Path.GetFullPath(directory);
        var fullRoot = Path.GetPathRoot(full)!;
        return full.Length == fullRoot.Length ? fullRoot : full.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string NextLabel(ref int index, Dictionary<string, string> existing)
    {
        string label;
        do
        {
            var ordinal = ++index;
            label = "";
            while (ordinal > 0)
            {
                ordinal--;
                label = (char)('A' + ordinal % 26) + label;
                ordinal /= 26;
            }
        } while (existing.Values.Contains(label, StringComparer.OrdinalIgnoreCase));
        return label;
    }
}
