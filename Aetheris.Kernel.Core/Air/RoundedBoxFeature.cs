namespace Aetheris.Kernel.Core.Air;

/// <summary>
/// Semantic primitive intent for a history-known rounded rectangular prism.  The
/// corner radius belongs to the silhouette; it is deliberately not an edge finish.
/// </summary>
internal sealed record RoundedBoxFeature(
    string BodyId,
    string FeatureId,
    string FeatureName,
    double Width,
    double Depth,
    double Height,
    double CornerRadius,
    string Frame,
    AirSourceSpan Provenance,
    IReadOnlyList<string> SemanticRoles,
    string Admission = "Admitted");

internal sealed record RoundedRectangleProfile(
    double Width,
    double Depth,
    double CornerRadius,
    IReadOnlyList<RoundedRectangleProfileSegment> Segments,
    string Frame,
    AirSourceSpan Provenance);

internal sealed record RoundedRectangleProfileSegment(string StableId, string Kind, string StartRole, string EndRole, double Radius = 0d);

internal sealed record LinearSweep(RoundedRectangleProfile Profile, string Vector, AirSourceSpan Provenance);

/// <summary>Feature AIR for the post-primitive, complete-top-boundary edge finish.</summary>
internal sealed record RoundedBoxTopBoundaryFilletFeature(
    string FeatureId,
    string BodyId,
    double Radius,
    string Face,
    string Target,
    string Kind,
    AirSourceSpan Provenance,
    string Admission = "Admitted");
