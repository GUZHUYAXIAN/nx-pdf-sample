using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NxDrawingPdfExporter.Core.Output;

public readonly struct RevisionNameMatch
{
    public RevisionNameMatch(int? revision, bool canonical)
    {
        IsMatch = true;
        Revision = revision;
        IsCanonical = canonical;
    }

    public bool IsMatch { get; }
    public int? Revision { get; }
    public bool IsCanonical { get; }
}

public static class RevisionFileName
{
    public static RevisionNameMatch Parse(string sourceBaseName, string fileName)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return default;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.Equals(stem, sourceBaseName, StringComparison.OrdinalIgnoreCase))
            return new RevisionNameMatch(0, true);

        var prefix = sourceBaseName + "-R";
        if (!stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return default;
        var digits = stem.Substring(prefix.Length);
        if (digits.Length == 0 || digits.Any(c => c < '0' || c > '9'))
            return default;
        return RevisionNumber.TryParse(digits, out var revision)
            ? new RevisionNameMatch(revision, revision != 0 && digits[0] != '0')
            : new RevisionNameMatch(null, false);
    }

    public static string Format(string sourcePath, int revision)
    {
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision));
        var basename = Path.GetFileNameWithoutExtension(sourcePath);
        return basename + (revision == 0 ? "" : "-R" + revision.ToString(CultureInfo.InvariantCulture)) + ".pdf";
    }
}
