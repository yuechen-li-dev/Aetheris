using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record MeshSectionAnchor(string Name, string Section, string Span);
public sealed record MeshSurfaceAnchor(string Name, string Section, string Span, Point3D PositionMm);
public sealed record DisplaceCageBrush(string Id, string Anchor, double RadiusMm, Vector3D OffsetMm) : MeshSculptOperation(Id);

public sealed record MeshBrushEvidence(string Feature, string Anchor, Point3D CenterMm, double RadiusMm,
    Vector3D RequestedOffsetMm, Vector3D AnchorDisplacementMm, double MaximumSurfaceDisplacementMm,
    int SurfaceSampleCount, int MovedControlCount, string ProtectedRim, int RimSampleCount, double MaximumRimMovementMm);

internal static class SubdivisionBrush
{
    internal static IReadOnlyDictionary<string, string>? BindAnchors(IReadOnlyList<MeshSectionAnchor>? definitions,
        IReadOnlyList<Section> sections, string[][] rings, int segments)
    {
        if (definitions is not { Count: > 0 }) return null;
        if (definitions.Count > 64)
            throw new InvalidOperationException("mesh-anchor-budget-exceeded: At most 64 surface anchors are admitted per cage.");

        var anchors = new Dictionary<string, string>(StringComparer.Ordinal);
        var starts = new Dictionary<(string Section, string Span), string>();
        for (var station = 0; station < sections.Count; station++)
            for (var span = 0; span < sections[station].Profile.Spans.Count; span++)
                starts.Add((sections[station].SectionId, sections[station].Profile.Spans[span].SpanId), rings[station * segments][span * segments]);

        foreach (var anchor in definitions)
        {
            if (anchor is null || string.IsNullOrWhiteSpace(anchor.Name) || !starts.TryGetValue((anchor.Section, anchor.Span), out var vertex))
                throw new InvalidOperationException($"mesh-anchor-unresolved: Surface anchor '{anchor?.Name}' must name an authored section and span on the cage.");
            if (!anchors.TryAdd(anchor.Name, vertex))
                throw new InvalidOperationException($"mesh-anchor-duplicate: Surface anchor '{anchor.Name}' has already been declared.");
        }
        return anchors;
    }

    internal static IReadOnlyList<MeshSurfaceAnchor> DescribeAnchors(MeshSculptState state)
    {
        var points = SubdivisionSculptor.LimitVertices(state.Cage).ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        return state.Construction.Anchors!.Select(a => new MeshSurfaceAnchor(a.Name, a.Section, a.Span, points[state.AnchorVertices![a.Name]])).ToArray();
    }

    internal static HashSet<string> ProtectedVertices(MeshSculptState state, MeshSculptOperation operation)
    {
        var kind = operation switch { FairCageRegion => "fair", RecessCageRegion => "recess", _ => "brush" };
        if (state.SourceRim is not { } rim || state.VertexRegions is not { } regions)
            throw new InvalidOperationException($"mesh-{kind}-source-unsupported: The operation requires a SectionChain cage with a qualified rim.");
        var protectedVertices = rim.Vertices.ToHashSet(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in operation.Preserve)
        {
            if (!names.Add(name) || !regions.TryGetValue(name, out var vertices))
                throw new InvalidOperationException($"mesh-{kind}-preserve-unresolved: Use unique current cage region names in preserve; '{name}' is invalid.");
            protectedVertices.UnionWith(vertices);
        }
        return protectedVertices;
    }

    internal static SubdivisionCage Displace(MeshSculptState state, DisplaceCageBrush brush, HashSet<string> protectedVertices)
    {
        if (!double.IsFinite(brush.RadiusMm) || brush.RadiusMm <= 0 || !double.IsFinite(brush.OffsetMm.Length))
            throw new InvalidOperationException("mesh-brush-invalid: Radius must be positive and the radius and offset must be finite.");
        if (state.AnchorVertices is null || !state.AnchorVertices.TryGetValue(brush.Anchor, out var center))
            throw new InvalidOperationException($"mesh-brush-anchor-unresolved: Anchor '{brush.Anchor}' is unavailable on the predecessor.");
        if (protectedVertices.Contains(center))
            throw new InvalidOperationException($"mesh-brush-anchor-protected: Anchor '{brush.Anchor}' belongs to a protected region.");

        var points = SubdivisionSculptor.LimitVertices(state.Cage).ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        var neighbors = points.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in state.Edges)
        {
            if (protectedVertices.Contains(edge.A) || protectedVertices.Contains(edge.B)) continue;
            neighbors[edge.A].Add(edge.B);
            neighbors[edge.B].Add(edge.A);
        }

        // Chord lengths between corresponding limit samples give a bounded,
        // edge-following surface metric, independent of display refinement.
        var distance = new Dictionary<string, double>(StringComparer.Ordinal) { [center] = 0 };
        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(center, 0);
        while (queue.TryDequeue(out var current, out var travel))
        {
            if (distance[current] != travel) continue;
            foreach (var next in neighbors[current])
            {
                var candidate = travel + (points[next] - points[current]).Length;
                if (candidate >= brush.RadiusMm || candidate >= distance.GetValueOrDefault(next, double.PositiveInfinity)) continue;
                distance[next] = candidate;
                queue.Enqueue(next, candidate);
            }
        }

        var positions = state.Cage.Vertices.Select(vertex =>
        {
            if (!distance.TryGetValue(vertex.Id, out var travel)) return vertex;
            var t = travel / brush.RadiusMm;
            var influence = (1 - t) * (1 - t) * (1 + 2 * t);
            return vertex with { Position = vertex.Position + brush.OffsetMm * influence };
        }).ToArray();
        return state.Cage with { Vertices = positions };
    }

    internal static MeshBrushEvidence Measure(MeshSculptState input, SubdivisionCage output, DisplaceCageBrush brush)
    {
        var before = SubdivisionSculptor.SamplePreview(input.Cage, input.Construction.PreviewLevels).Vertices;
        var after = SubdivisionSculptor.SamplePreview(output, input.Construction.PreviewLevels).Vertices;
        var center = input.AnchorVertices![brush.Anchor];
        var rim = input.SourceRim!;
        var rimSamples = rim.SampleIds(input.Construction.PreviewLevels);
        var rimVertices = rimSamples.ToHashSet(StringComparer.Ordinal);
        var maximum = 0d;
        var rimMovement = 0d;
        Point3D centerPoint = default;
        Vector3D centerDisplacement = default;
        for (var index = 0; index < before.Count; index++)
        {
            // Brush displacement retains topology. Both samples use the same
            // refinement order and stencils, giving pointwise correspondence.
            var displacement = after[index].Position - before[index].Position;
            maximum = Math.Max(maximum, displacement.Length);
            if (rimVertices.Contains(before[index].Id)) rimMovement = Math.Max(rimMovement, displacement.Length);
            if (before[index].Id == center)
            {
                centerPoint = before[index].Position;
                centerDisplacement = displacement;
            }
        }
        var controlsMoved = input.Cage.Vertices.Zip(output.Vertices).Count(pair => pair.First.Position != pair.Second.Position);
        return new(brush.Id, brush.Anchor, centerPoint, brush.RadiusMm, brush.OffsetMm, centerDisplacement,
            maximum, before.Count, controlsMoved, rim.Section, rimSamples.Length, rimMovement);
    }
}
