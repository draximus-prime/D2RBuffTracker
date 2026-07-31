using System.IO;

namespace D2RBuffTracker.Services;

/// <summary>
/// Resolves buff icon paths (relative catalog paths or absolute custom paths)
/// to absolute file paths on disk, and exposes common asset locations.
/// </summary>
public static class AssetPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string AssetsRoot => Path.Combine(BaseDirectory, "Assets");

    public static string SoundsRoot => Path.Combine(AssetsRoot, "Sounds");

    public static string AlarmSound => Path.Combine(SoundsRoot, "expire.wav");

    /// <summary>Resolve a sound id to an absolute .wav path. Rooted ids are custom user files.</summary>
    public static string Sound(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Path.Combine(SoundsRoot, "expire.wav");
        if (Path.IsPathRooted(id))
            return id;
        var rel = id.Replace('/', Path.DirectorySeparatorChar) + ".wav";
        return Path.Combine(SoundsRoot, rel);
    }

    /// <summary>A selectable sound in the gallery.</summary>
    public sealed record SoundEntry(string Id, string Category, string DisplayName);

    /// <summary>List the available sounds (recursively) grouped-ready with category + display name.</summary>
    public static IReadOnlyList<SoundEntry> AvailableSounds()
    {
        if (!Directory.Exists(SoundsRoot))
            return Array.Empty<SoundEntry>();

        var list = new List<SoundEntry>();
        foreach (var file in Directory.GetFiles(SoundsRoot, "*.wav", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(SoundsRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            var id = rel[..^4]; // strip ".wav"
            var slash = id.IndexOf('/');
            var category = slash < 0 ? "Classic" : id[..slash];
            var display = Humanize(Path.GetFileNameWithoutExtension(file));
            list.Add(new SoundEntry(id, category, display));
        }
        return list
            .OrderBy(s => s.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Human-friendly label for a sound id (falls back to the file name).</summary>
    public static string SoundDisplayName(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "Default";
        if (Path.IsPathRooted(id))
            return Humanize(Path.GetFileNameWithoutExtension(id));
        var name = id.Contains('/') ? id[(id.LastIndexOf('/') + 1)..] : id;
        return Humanize(name);
    }

    private static string Humanize(string raw)
    {
        var words = raw.Replace('_', ' ').Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    /// <summary>
    /// Resolve a stored icon path to an absolute path. Absolute paths are
    /// returned as-is; relative paths are rooted under the Assets folder.
    /// </summary>
    public static string? Resolve(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return null;

        if (Path.IsPathRooted(storedPath) && File.Exists(storedPath))
            return storedPath;

        var combined = Path.Combine(AssetsRoot, storedPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(combined) ? combined : null;
    }
}
