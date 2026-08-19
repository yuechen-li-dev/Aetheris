using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Ordered analytic geometry emitted by a domain-neutral Firmament Concept Path.</summary>
public sealed record ResolvedConceptPath2D(
    string Name,
    IReadOnlyList<ResolvedConceptPathSegment2D> Segments,
    string StableId);

public sealed record ResolvedConceptPathSegment2D(
    string Name,
    string SemanticKind,
    LineArcProfileCurve2D Geometry,
    string StableId);
