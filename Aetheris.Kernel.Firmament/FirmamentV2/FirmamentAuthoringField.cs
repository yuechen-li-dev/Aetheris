namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Compiler-owned, JSON-safe field help for a bounded authoring construct.</summary>
public sealed record FirmamentAuthoringField(
    string Name, string Type, bool Required, string Meaning,
    string? Default = null, IReadOnlyList<string>? Choices = null);

public static class FirmamentSchemaAuthoringFields
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<FirmamentAuthoringField>> Cache =
        FirmamentSemanticSchemas.All.ToDictionary(schema => schema.Name,
            schema => (IReadOnlyList<FirmamentAuthoringField>)schema.Fields.Select(field => new FirmamentAuthoringField(
            field.Name, field.Kind switch
            {
                FirmamentSchemaValueKind.Choice => "Enum",
                FirmamentSchemaValueKind.Vector => "Vector3",
                _ => field.Kind.ToString()
            }, field.Required, field.Description ?? string.Empty, field.Default,
            field.Choices.Count == 0 ? null : field.Choices)).ToArray(), StringComparer.Ordinal);

    public static IReadOnlyList<FirmamentAuthoringField> For(string construct) =>
        Cache.TryGetValue(construct, out var fields) ? fields : [];
}
