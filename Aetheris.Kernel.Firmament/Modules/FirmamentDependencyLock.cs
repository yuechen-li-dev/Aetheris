using System.Security.Cryptography;
using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Modules;

public sealed record FirmamentLockedDependency(string Path, string Format, string HashKind, string Sha256);

/// <summary>Dependency identity, never import policy or geometric admission.</summary>
public sealed record FirmamentDependencyLock(string Root, IReadOnlyList<FirmamentLockedDependency> Dependencies)
{
    public const string FileName = "firmament.lock.toml";
    public static FirmamentDependencyLock Parse(string text)
    {
        try
        {
            var table = TomlSerializer.Deserialize<TomlTable>(text) ?? throw new ArgumentException("Empty lock document.");
            Keys(table, "version", "root", "dependencies");
            if (table["version"] is not long version || version != 1) throw new ArgumentException("Unsupported lock version; expected 1.");
            var root = FirmamentProjectSnapshot.NormalizePath((string)table["root"]!);
            var items = table["dependencies"] as TomlTableArray;
            if (items is null && table["dependencies"] is TomlArray { Count: 0 }) items = new();
            if (items is null) throw new ArgumentException("Lock dependencies must be an array of tables.");
            var dependencies = new List<FirmamentLockedDependency>();
            foreach (var item in items)
            {
                Keys(item, "path", "format", "hashKind", "sha256");
                var path = FirmamentProjectSnapshot.NormalizePath((string)item["path"]!);
                var format = (string)item["format"]!; var kind = (string)item["hashKind"]!; var hash = (string)item["sha256"]!;
                if (format is not ("step" or "firmament") || kind != (format == "step" ? "sha256-bytes" : "sha256-utf8")
                    || hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c))) throw new ArgumentException("Invalid lock dependency format, hash kind or SHA-256.");
                if (dependencies.Any(d => d.Path == path)) throw new ArgumentException("Duplicate locked dependency: " + path);
                dependencies.Add(new(path, format, kind, hash.ToLowerInvariant()));
            }
            return new(root, dependencies);
        }
        catch (Exception error) when (error is TomlException or InvalidCastException or KeyNotFoundException or NullReferenceException)
        { throw new ArgumentException("Malformed Firmament lock: " + error.Message, error); }
    }

    public string ToToml()
    {
        var items = new TomlTableArray();
        foreach (var dependency in Dependencies.OrderBy(d => d.Path, StringComparer.Ordinal))
            items.Add(new TomlTable { ["path"] = dependency.Path, ["format"] = dependency.Format,
                ["hashKind"] = dependency.HashKind, ["sha256"] = dependency.Sha256.ToLowerInvariant() });
        var table = new TomlTable { ["version"] = 1L, ["root"] = Root, ["dependencies"] = items };
        // An empty array of tables has no TOML table declarations to emit.
        // Write an ordinary empty array so the required field remains present.
        if (items.Count == 0) table["dependencies"] = new TomlArray();
        return TomlSerializer.Serialize(table);
    }

    static void Keys(TomlTable table, params string[] names)
    {
        if (table.Keys.Except(names, StringComparer.Ordinal).Any() || names.Any(n => !table.ContainsKey(n)))
            throw new ArgumentException("Lock fields must be exactly: " + string.Join(", ", names));
    }
    internal static FirmamentLockedDependency? StepPin(string? directory, IReadOnlyDictionary<string, string>? resources, string path)
    {
        string? text; string relative;
        if (resources is not null)
        {
            var prefix = string.IsNullOrEmpty(directory) ? "" : directory.Replace('\\', '/') + "/";
            if (!resources.TryGetValue(prefix + FileName, out text) && !resources.TryGetValue(FileName, out text)) return null;
            relative = FirmamentProjectSnapshot.NormalizePath(prefix + path);
        }
        else
        {
            var folder = Path.GetFullPath(directory ?? Directory.GetCurrentDirectory());
            var lockPath = System.IO.Path.Combine(folder, FileName);
            if (!File.Exists(lockPath)) return null;
            text = File.ReadAllText(lockPath);
            var root = AssemblyM0Parser.FindAllowedSourceRoot(System.IO.Path.Combine(folder, "__provided_source.firmament"));
            relative = System.IO.Path.GetRelativePath(root, System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, path))).Replace('\\', '/');
        }
        var manifest = Parse(text);
        var entry = manifest.Dependencies.SingleOrDefault(d => d.Path == relative);
        if (entry is not { Format: "step", HashKind: "sha256-bytes" }) throw new ArgumentException("STEP resource is missing an exact-byte lock entry: " + relative);
        return entry;
    }
    internal static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
