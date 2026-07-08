namespace Aetheris.Forge.Abstractions.FirmamentInterop;

public sealed record ConceptId(string Family, string Concept)
{
    public override string ToString() => $"{Family}<{Concept}>";
}

public enum FirmamentConceptApplicationKind
{
    Manufacturing,
    Feature
}

public sealed record FirmamentConceptApplicationView(
    FirmamentConceptApplicationKind Kind,
    string? Name,
    ConceptId ConceptId,
    IReadOnlyList<FirmamentConceptFieldView> Fields,
    FirmamentSourceSpan? SourceSpan);

public sealed record FirmamentConceptFieldView(
    string Name,
    FirmamentFieldKind Kind,
    FirmamentValue? Value,
    string? TargetSource,
    FirmamentSourceSpan? SourceSpan);

public enum FirmamentFieldKind
{
    Value,
    Target
}
