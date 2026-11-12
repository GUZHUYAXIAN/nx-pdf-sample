using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NxDrawingPdfExporter.Core.Input;

namespace NxDrawingPdfExporter.Core.Output;

public interface IOutputDirectoryReader
{
    IReadOnlyList<string> ReadFiles(string directory);
}

public sealed class PhysicalOutputDirectoryReader : IOutputDirectoryReader
{
    public IReadOnlyList<string> ReadFiles(string directory) => Directory.GetFiles(directory);
}

public sealed class RevisionCatalogSnapshot
{
    internal RevisionCatalogSnapshot(string sourceDirectory, string targetDirectory, int? sourceHighest, int? targetHighest,
        string[] facts, string[] aliases)
    {
        SourceDirectory = sourceDirectory;
        TargetDirectory = targetDirectory;
        SourceHighest = sourceHighest;
        TargetHighest = targetHighest;
        Facts = Array.AsReadOnly(facts);
        Aliases = Array.AsReadOnly(aliases);
    }

    public string SourceDirectory { get; }
    public string TargetDirectory { get; }
    public int? SourceHighest { get; }
    public int? TargetHighest { get; }
    public IReadOnlyList<string> Facts { get; }
    public IReadOnlyList<string> Aliases { get; }

    public bool Matches(RevisionCatalogSnapshot other) =>
        StringComparer.OrdinalIgnoreCase.Equals(SourceDirectory, other.SourceDirectory)
        && StringComparer.OrdinalIgnoreCase.Equals(TargetDirectory, other.TargetDirectory)
        && Facts.SequenceEqual(other.Facts, StringComparer.Ordinal);
}

public sealed class RevisionCatalog
{
    private readonly IOutputDirectoryReader directories;

    public RevisionCatalog(IOutputDirectoryReader directories) => this.directories = directories;

    public RevisionCatalogSnapshot Read(string sourcePath, string targetDirectory)
    {
        var sourceDirectory = SourceDirectoryGrouper.ParentDirectory(sourcePath);
        var basename = Path.GetFileNameWithoutExtension(sourcePath);
        var facts = new List<string>();
        var aliases = new List<string>();
        var sourceHighest = ReadDirectory(sourceDirectory, basename, facts, aliases);
        var targetHighest = StringComparer.OrdinalIgnoreCase.Equals(sourceDirectory, targetDirectory)
            ? sourceHighest : ReadDirectory(targetDirectory, basename, facts, aliases);
        return new RevisionCatalogSnapshot(sourceDirectory, targetDirectory, sourceHighest, targetHighest,
            facts.OrderBy(x => x, StringComparer.Ordinal).ToArray(), aliases.ToArray());
    }

    public void CheckDirectory(string directory) => directories.ReadFiles(directory);

    private int? ReadDirectory(string directory, string basename, List<string> facts, List<string> aliases)
    {
        int? maximum = null;
        var seen = new Dictionary<int, string>();
        foreach (var path in directories.ReadFiles(directory))
        {
            var match = RevisionFileName.Parse(basename, Path.GetFileName(path));
            if (!match.IsMatch)
                continue;
            facts.Add(path);
            if (!match.IsCanonical)
                aliases.Add(path);
            if (match.Revision is int number)
            {
                if (seen.TryGetValue(number, out var previous))
                {
                    aliases.Add(previous);
                    aliases.Add(path);
                }
                seen[number] = path;
                maximum = Math.Max(maximum ?? number, number);
            }
        }
        return maximum;
    }
}
