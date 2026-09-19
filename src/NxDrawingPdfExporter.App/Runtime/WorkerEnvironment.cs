namespace NxDrawingPdfExporter.App.Runtime;

/// <summary>Build a private child environment; never set process/user/machine variables.</summary>
internal static class WorkerEnvironment
{
    public static Dictionary<string, string?> Create(NxInstallation installation,
        IEnumerable<KeyValuePair<string, string?>> parent, IEnumerable<string> knownRoots)
    {
        var child = new Dictionary<string, string?>(parent, StringComparer.OrdinalIgnoreCase);
        var roots = knownRoots.Append(installation.RootDirectory).ToList();
        if (child.TryGetValue("UGII_BASE_DIR", out string? baseRoot) && !string.IsNullOrWhiteSpace(baseRoot))
            roots.Add(baseRoot);
        if (child.TryGetValue("UGII_ROOT_DIR", out string? binRoot) && !string.IsNullOrWhiteSpace(binRoot))
        {
            string value = Clean(binRoot);
            string leaf = Path.GetFileName(value);
            roots.Add(leaf.Equals("UGII", StringComparison.OrdinalIgnoreCase) || leaf.Equals("NXBIN", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(value)! : value);
        }
        string[] normalizedRoots = roots.Where(r => !string.IsNullOrWhiteSpace(r)).Select(Clean).ToArray();
        // Version-specific initialization/customization overrides are not carried between generations.
        // Preserve license and locale configuration verbatim; neither binds DLL paths.
        // Never log license values. Clearing the locale would change inherited NX behavior.
        foreach (string key in child.Keys.ToArray())
            if (key.StartsWith("UGII_", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("UGII_LICENSE_FILE", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("UGII_LANG", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("UGII_UTF8_MODE", StringComparison.OrdinalIgnoreCase))
                child.Remove(key);

        string bin = Path.Combine(installation.RootDirectory, installation.Profile.BinDirectoryName);
        var paths = (child.GetValueOrDefault("PATH") ?? "").Split(';').Select(Clean)
            .Where(p => p.Length > 0 && !normalizedRoots.Any(r => IsWithin(p, r)));
        child["UGII_BASE_DIR"] = installation.RootDirectory;
        child["UGII_ROOT_DIR"] = bin + Path.DirectorySeparatorChar;
        child["PATH"] = string.Join(";", new[] { bin, installation.ManagedDirectory }.Concat(paths).Distinct(StringComparer.OrdinalIgnoreCase));
        return child;
    }

    private static string Clean(string value) => value.Trim().Trim('"').TrimEnd('\\', '/');
    private static bool IsWithin(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
}
