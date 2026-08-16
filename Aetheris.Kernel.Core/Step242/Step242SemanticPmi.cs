namespace Aetheris.Kernel.Core.Step242;

public abstract record Step242SemanticPmi(string FeatureId)
{
    /// <summary>Optional one-based BRep face ids resolved from the semantic target before STEP emission.</summary>
    public IReadOnlyList<int> GeometricFaceIds { get; init; } = [];
}

public sealed record Step242SemanticPmiHole(
    string FeatureId,
    double Diameter,
    double? Depth,
    string? HoleFamily,
    double? TolerancePlus,
    double? ToleranceMinus,
    int? Quantity = null) : Step242SemanticPmi(FeatureId);

public sealed record Step242SemanticPmiDimension(
    string FeatureId,
    string Name,
    string DimensionKind,
    double Value,
    double? TolerancePlus,
    double? ToleranceMinus,
    int? Quantity = null) : Step242SemanticPmi(FeatureId);

public sealed record Step242SemanticPmiDatum(
    string FeatureId,
    string DatumKind,
    string Label,
    string Target) : Step242SemanticPmi(FeatureId);

public sealed record Step242SemanticPmiNote(
    string FeatureId,
    string Target,
    string Text) : Step242SemanticPmi(FeatureId);

public sealed record Step242SemanticPmiGeometricTolerance(
    string FeatureId,
    string Name,
    string ToleranceKind,
    double Value,
    IReadOnlyList<string> DatumReferences,
    int? Quantity = null) : Step242SemanticPmi(FeatureId);
