using Aetheris.Kernel.Firmament.Materializer;
using System.Text.Json.Serialization;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// A named, non-owning bounded view of an existing geometric guide.  This is deliberately
/// closed over Firmament's current planar guide families; it is not a general generic value.
/// Geometry is evaluated from <see cref="ParentId"/> and the authored constraints each time.
/// </summary>
public sealed record GeometricSpanView(
    string SpanId,
    string SpanType,
    string ParentId,
    string ParentType,
    string Domain,
    string Orientation,
    string? From,
    string? To,
    string? BoundaryProfile,
    double? Length,
    string Provenance,
    [property: JsonIgnore] LineArcProfileCurve2D? Geometry = null);

public sealed record GeometricSpanInspection(
    IReadOnlyList<GeometricSpanView> Spans,
    IReadOnlyList<string> Diagnostics);
