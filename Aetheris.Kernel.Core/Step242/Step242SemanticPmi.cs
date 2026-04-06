namespace Aetheris.Kernel.Core.Step242;

public abstract record Step242SemanticPmi(string FeatureId);

public sealed record Step242SemanticPmiHole(
    string FeatureId,
    double Diameter,
    double? Depth,
    string? HoleFamily,
    double? TolerancePlus,
    double? ToleranceMinus) : Step242SemanticPmi(FeatureId);

public sealed record Step242SemanticPmiDatum(
    string FeatureId,
    string DatumKind,
    string Label,
    string Target) : Step242SemanticPmi(FeatureId);

public sealed record Step242SemanticPmiNote(
    string FeatureId,
    string Target,
    string Text) : Step242SemanticPmi(FeatureId);
