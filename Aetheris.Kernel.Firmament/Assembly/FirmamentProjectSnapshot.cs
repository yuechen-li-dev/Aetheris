using System.Collections.Frozen;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>An immutable, project-relative source set for an assembly build.</summary>
public sealed class FirmamentProjectSnapshot
{
    private readonly IReadOnlyDictionary<string, string> documents;

    public string RootDocument { get; }
    public IReadOnlyDictionary<string, string> Documents => documents;

    public FirmamentProjectSnapshot(string rootDocument, IEnumerable<KeyValuePair<string, string>> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        RootDocument = NormalizePath(rootDocument);
        if (!RootDocument.EndsWith(".firmasm", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The project root must be a .firmasm document.", nameof(rootDocument));
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, source) in documents)
        {
            var key = NormalizePath(path);
            if (!key.EndsWith(".firmasm", StringComparison.OrdinalIgnoreCase) &&
                !key.EndsWith(".firmament", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Unsupported project document kind: '{key}'.", nameof(documents));
            if (!normalized.TryAdd(key, source ?? throw new ArgumentException($"Null source for '{key}'.", nameof(documents))))
                throw new ArgumentException($"Duplicate normalized project path: '{key}'.", nameof(documents));
        }
        this.documents = normalized.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.StartsWith('\\') ||
            path.Contains(':') || path.Contains('?') || path.Contains('#'))
            throw new ArgumentException($"Invalid project-relative path: '{path}'.", nameof(path));
        var parts = new List<string>();
        foreach (var part in path.Replace('\\', '/').Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (parts.Count == 0) throw new ArgumentException($"Path escapes project root: '{path}'.", nameof(path));
                parts.RemoveAt(parts.Count - 1);
            }
            else parts.Add(part);
        }
        if (parts.Count == 0) throw new ArgumentException($"Empty project path: '{path}'.", nameof(path));
        return string.Join('/', parts);
    }

    public bool TryResolve(string path, out string source) => documents.TryGetValue(NormalizePath(path), out source!);
}
