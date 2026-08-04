namespace Aetheris.Kernel.Core.Air;

/// <summary>Construction intent, deliberately distinct from a post-construction shell operation.</summary>
public sealed record HollowBodyFeature(
    string PrimitiveKind,
    IReadOnlyDictionary<string, double> PrimitiveParameters,
    double WallThickness,
    IReadOnlyList<string> Openings,
    string ThicknessPolicy,
    HollowConstructionWitness Witness,
    string Provenance);

/// <summary>Primitive-owned proof that its inner boundary can be derived exactly.</summary>
public sealed record HollowConstructionWitness(
    string Kind,
    bool Exact,
    string InnerBoundaryDerivation,
    IReadOnlyList<string> SurfaceCorrespondence,
    IReadOnlyList<string> AdmissibilityEvidence);

/// <summary>Shared paired-boundary construction AIR consumed by the vessel B-rep planner.</summary>
public sealed record ThinWalledBodyConstruction(
    HollowBodyFeature Feature,
    IReadOnlyList<string> OuterBoundaryRoles,
    IReadOnlyList<string> InnerBoundaryRoles,
    IReadOnlyList<string> RimRoles,
    IReadOnlyList<string> ClosedRegionRoles,
    IReadOnlyList<ThinWallThicknessWitness> ThicknessWitnesses,
    string MaterialSide = "between outer and inner boundaries");

public sealed record ThinWallThicknessWitness(
    string Role,
    string OuterSupport,
    string InnerSupport,
    double Distance,
    string OffsetDirection,
    bool Exact = true);
