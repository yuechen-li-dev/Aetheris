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

    public PmiModel CreatePlanarDatum(string label, PmiPlanarFaceReference reference)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Datum label is required.", nameof(label));
        }

        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.IsValid)
        {
            throw new ArgumentException("Planar datum reference must include non-empty feature id and selector.", nameof(reference));
        }

        if (DatumFeatures.Any(existing => string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Datum label '{label}' already exists in this PMI model.");
        }

        return AddDatum(new PmiDatumFeature(
            $"datum:{label}",
            label,
            PmiDatumFeatureKind.Planar,
            reference));
    }

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
public sealed record PmiPlanarFaceReference(string FeatureId, string Selector) : PmiSemanticReference
{
    public bool IsValid => !string.IsNullOrWhiteSpace(FeatureId) && !string.IsNullOrWhiteSpace(Selector);

    public static PmiPlanarFaceReference FromSelector(string selectorTarget)
    {
        if (string.IsNullOrWhiteSpace(selectorTarget))
        {
            throw new ArgumentException("Selector target must be non-empty.", nameof(selectorTarget));
        }

        var separatorIndex = selectorTarget.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= selectorTarget.Length - 1)
        {
            throw new ArgumentException(
                "Selector target must be shaped as 'feature.port'.",
                nameof(selectorTarget));
        }

        var featureId = selectorTarget[..separatorIndex];
        return new PmiPlanarFaceReference(featureId, selectorTarget);
    }
}

/// <summary>
/// Semantic reference to a cylindrical/hole-like feature.
/// </summary>
public sealed record PmiCylindricalFeatureReference(string FeatureId, string? Family = null) : PmiSemanticReference;

/// <summary>
/// Semantic reference to an already declared datum feature.
/// </summary>
public sealed record PmiDatumReference(string DatumId) : PmiSemanticReference;
