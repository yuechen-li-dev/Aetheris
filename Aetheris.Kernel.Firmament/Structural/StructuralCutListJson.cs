using System.Text.Json.Serialization;

namespace Aetheris.Kernel.Firmament.Structural;

internal sealed record StructuralCutListUnits(string Length, string Mass);
internal sealed record StructuralCutListDocument(
    string Schema, string Structure, StructuralCutListUnits Units,
    IReadOnlyList<StructuralCutListEntry> Entries);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(StructuralCutListDocument))]
internal partial class StructuralJsonContext : JsonSerializerContext;
