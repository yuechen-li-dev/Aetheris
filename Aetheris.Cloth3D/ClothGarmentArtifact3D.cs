using System.Text.Json;

namespace Aetheris.Cloth3D;

/// <summary>A runtime garment needs the compiled cloth, not the Firmament authoring compiler.
/// Panel names and seam provenance remain in the transport document for inspection.</summary>
public sealed record ClothGarmentArtifact3D(CompiledCloth3D Cloth, ClothSnapshot3D? Settled,
    string SourceHash, string SourceIdentity)
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    public static ClothGarmentArtifact3D Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (new FileInfo(path).Length > 32 * 1024 * 1024)
        {
            throw new InvalidDataException("garment-artifact-budget: Maximum document size is 32 MiB.");
        }
        return Parse(File.ReadAllText(path));
    }

    public static ClothGarmentArtifact3D Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "aetheris.garment.v1"
            || root.GetProperty("units").GetString() != "m"
            || root.GetProperty("upAxis").GetString() != "Z")
        {
            throw new InvalidDataException("garment-artifact-schema: Expected aetheris.garment.v1, metres, Z up.");
        }
        var definition = root.GetProperty("definition").Deserialize<ClothDefinition3D>(Options)
            ?? throw new InvalidDataException("garment-artifact-definition: Missing cloth definition.");
        var cloth = definition.Compile();
        if (root.GetProperty("contentKey").GetString() != cloth.ContentKey)
        {
            throw new InvalidDataException("garment-artifact-content-key: Definition does not match its retained key.");
        }
        ClothSnapshot3D? settled = root.GetProperty("settled").Deserialize<ClothSnapshot3D>(Options);
        if (settled is not null)
        {
            ClothState3D.ValidateSnapshot(cloth, settled);
        }
        string hash = root.GetProperty("sourceHash").GetString() ?? "";
        string identity = root.GetProperty("sourceIdentity").GetString() ?? "";
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)) || string.IsNullOrWhiteSpace(identity))
        {
            throw new InvalidDataException("garment-artifact-provenance: Source identity and SHA-256 are required.");
        }
        return new(cloth, settled, hash, identity);
    }

    public ClothSolver3D CreateSolver()
    {
        var solver = new ClothSolver3D(Cloth);
        if (Settled is not null)
        {
            solver.Restore(Settled);
        }
        return solver;
    }
}
