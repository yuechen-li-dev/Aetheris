namespace Aetheris.Kernel.Core.Pmi;

/// <summary>
/// Kernel-owned semantic PMI container scoped to one logical body/model export context.
/// </summary>
public sealed record PmiModel(
    string BodyFeatureId,
    IReadOnlyList<PmiDatumFeature> DatumFeatures,
    IReadOnlyList<PmiDimension> Dimensions)
{
    public static PmiModel Empty(string bodyFeatureId)
        => new(bodyFeatureId, [], []);

    public PmiModel AddDatum(PmiDatumFeature datum)
        => this with { DatumFeatures = DatumFeatures.Concat([datum]).ToArray() };

    public PmiModel AddDimension(PmiDimension dimension)
        => this with { Dimensions = Dimensions.Concat([dimension]).ToArray() };
}

public enum PmiDatumFeatureKind
{
    Planar
}

public sealed record PmiDatumFeature(
    string DatumId,
    string Label,
    PmiDatumFeatureKind Kind,
    PmiSemanticReference Target);

public enum PmiDimensionKind
{
    Diameter,
    LinearDistanceToDatum
}

public sealed record PmiDimension(
    string DimensionId,
    PmiDimensionKind Kind,
    PmiSemanticReference PrimaryReference,
    PmiSemanticReference? SecondaryReference,
    double NominalValue,
    string? SourceTag = null);

public abstract record PmiSemanticReference;

/// <summary>
/// Semantic reference to a planar target selected from a feature context.
/// </summary>
public sealed record PmiPlanarFaceReference(string FeatureId, string Selector) : PmiSemanticReference;

/// <summary>
/// Semantic reference to a cylindrical/hole-like feature.
/// </summary>
public sealed record PmiCylindricalFeatureReference(string FeatureId, string? Family = null) : PmiSemanticReference;

/// <summary>
/// Semantic reference to an already declared datum feature.
/// </summary>
public sealed record PmiDatumReference(string DatumId) : PmiSemanticReference;
