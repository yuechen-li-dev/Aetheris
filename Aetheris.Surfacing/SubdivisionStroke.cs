using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record StrokeMirror(string Feature, string Across, Point3D OriginMm, Vector3D Normal);
public sealed record DisplaceCageStroke(string Id, IReadOnlyList<string> Path, double RadiusMm, Vector3D OffsetMm, StrokeMirror? Mirror = null)
    : MeshSculptOperation(Id);

public sealed record MeshStrokeEvidence(string Feature, IReadOnlyList<string> Path, double RadiusMm, Vector3D RequestedOffsetMm,
    int PathControls, int MovedControls, MeshComparisonEvidence Movement, MeshStrokeMirrorEvidence? Mirror);
public sealed record MeshStrokeMirrorEvidence(string Feature, string Across, Vector3D ReflectedOffsetMm, int SharedFootprintControls,
    int MatchedSamples, double MaximumSampledSymmetryErrorMm);

internal static class SubdivisionStroke
{
    internal static (SubdivisionCage Cage, MeshStrokeEvidence Evidence) Apply(MeshSculptState state, DisplaceCageStroke stroke, HashSet<string> pinned)
    {
        if (stroke.Path is not { Count: >= 2 and <= 32 } || stroke.Path.Distinct(StringComparer.Ordinal).Count() != stroke.Path.Count
            || !double.IsFinite(stroke.RadiusMm) || stroke.RadiusMm <= 0 || !double.IsFinite(stroke.OffsetMm.Length))
            throw new InvalidOperationException("mesh-stroke-invalid: Use 2..32 unique anchors, a finite positive radius, and a finite offset.");
        var anchors = stroke.Path.Select(name =>
        {
            if (state.AnchorVertices is null || !state.AnchorVertices.TryGetValue(name, out var id))
                throw new InvalidOperationException($"mesh-stroke-anchor-unresolved: Anchor '{name}' is unavailable on the predecessor.");
            if (pinned.Contains(id))
                throw new InvalidOperationException($"mesh-stroke-anchor-protected: Anchor '{name}' is in a protected region.");
            return id;
        }).ToArray();

        var edges = state.Edges;
        var graph = new CageSurfaceGraph(state.Cage, edges, pinned);
        var route = graph.Trace(anchors);
        var influence = graph.Influences(route, stroke.RadiusMm);
        Plane3? across = null;
        Dictionary<string, string>? partners = null;
        var reflectedOffset = Vector3D.Zero;
        var shared = 0;
        if (stroke.Mirror is { } mirror)
        {
            if (!Direction3D.TryCreate(mirror.Normal, out var normal) || !double.IsFinite((mirror.OriginMm - Point3D.Origin).Length))
                throw new InvalidOperationException("mesh-stroke-mirror-plane-invalid: The mirror needs a finite origin and a nonzero normal.");
            across = new(mirror.OriginMm, normal);
            partners = SubdivisionStrokeReflection.MatchCage(state.Cage, edges, pinned, across.Value);
            reflectedOffset = stroke.OffsetMm - normal.ToVector() * (2 * stroke.OffsetMm.Dot(normal.ToVector()));
            shared = influence.Keys.Count(id => influence.ContainsKey(partners[id]));
        }

        // Both footprints come from the same input. A smooth union caps their
        // combined strength at one, and the weighted offsets cancel a normal
        // component at a control on the mirror plane.
        var vertices = state.Cage.Vertices.Select(v =>
        {
            var a = influence.GetValueOrDefault(v.Id);
            var b = partners is null ? 0 : influence.GetValueOrDefault(partners[v.Id]);
            var total = a + b;
            if (total == 0) return v;
            var offset = (stroke.OffsetMm * a + reflectedOffset * b) * ((total - a * b) / total);
            return v with { Position = v.Position + offset };
        }).ToArray();
        var output = state.Cage with { Vertices = vertices };

        var levels = state.Construction.PreviewLevels;
        var before = SubdivisionSculptor.SamplePreview(state.Cage, levels).Vertices;
        var after = SubdivisionSculptor.SamplePreview(output, levels).Vertices;
        var movement = SubdivisionComparison.MeasureSamples(before, after, state.SourceRim!.SampleIds(levels).ToHashSet(StringComparer.Ordinal));
        MeshStrokeMirrorEvidence? mirrorEvidence = null;
        if (across is { } plane)
        {
            var samplePartners = SubdivisionStrokeReflection.Match(before, plane);
            var positions = after.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
            var error = after.Max(v => (SubdivisionStrokeReflection.Reflect(v.Position, plane) - positions[samplePartners[v.Id]]).Length);
            mirrorEvidence = new(stroke.Mirror!.Feature, stroke.Mirror.Across, reflectedOffset, shared, after.Count, error);
        }
        var moved = state.Cage.Vertices.Zip(vertices).Count(pair => pair.First.Position != pair.Second.Position);
        return (output, new(stroke.Id, stroke.Path, stroke.RadiusMm, stroke.OffsetMm, route.Count, moved, movement, mirrorEvidence));
    }
}
