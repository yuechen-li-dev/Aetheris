using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record FairCageRegion(string Id, IReadOnlyList<string> Regions, double MaximumMovementMm, int Iterations) : MeshSculptOperation(Id);
public sealed record MeshFairEvidence(string Feature, int MovedControls, double MaximumAllowedMovementMm,
    MeshComparisonEvidence Movement, double BeforeNormalTurnRmsDegrees, double AfterNormalTurnRmsDegrees);
public sealed record MeshComparisonEvidence(int Samples, double MaximumMovementMm, double RmsMovementMm,
    Point3D PeakPositionBeforeMm, int RimSamples, double? MaximumRimMovementMm);

public static class SubdivisionComparison
{
    public static MeshComparisonEvidence Measure(MeshSculptState before, MeshSculptState after)
    {
        var levels = before.Construction.PreviewLevels;
        if (levels != after.Construction.PreviewLevels || before.SourceBodyStateId != after.SourceBodyStateId ||
            before.Cage.Faces.Count != after.Cage.Faces.Count ||
            before.Cage.Faces.Zip(after.Cage.Faces).Any(pair =>
                pair.First.Id != pair.Second.Id || !pair.First.Vertices.SequenceEqual(pair.Second.Vertices)))
            throw new InvalidOperationException("mesh-compare-correspondence-unsupported: Comparison requires a common source body, matching cage faces, and matching subdivision preview levels.");

        var first = SubdivisionSculptor.SamplePreview(before.Cage, levels).Vertices;
        var second = SubdivisionSculptor.SamplePreview(after.Cage, levels).Vertices;
        var rim = before.SourceRim is { } source && after.SourceRim is { } target && source.Section == target.Section
            && source.Vertices.SequenceEqual(target.Vertices) ? source.SampleIds(levels).ToHashSet(StringComparer.Ordinal) : null;
        return MeasureSamples(first, second, rim);
    }

    internal static MeshComparisonEvidence MeasureSamples(IReadOnlyList<CageVertex> first, IReadOnlyList<CageVertex> second,
        IReadOnlySet<string>? rim)
    {
        if (!first.Select(v => v.Id).SequenceEqual(second.Select(v => v.Id)))
            throw new InvalidOperationException("mesh-compare-correspondence-unsupported: Limit sample identities do not match.");
        double maximum = 0, squareSum = 0, rimMovement = 0;
        var peak = first[0].Position;
        for (var i = 0; i < first.Count; i++)
        {
            var displacement = (second[i].Position - first[i].Position).Length;
            squareSum += displacement * displacement;
            if (displacement > maximum)
            {
                maximum = displacement;
                peak = first[i].Position;
            }
            if (rim?.Contains(first[i].Id) == true)
                rimMovement = Math.Max(rimMovement, displacement);
        }
        return new(first.Count, maximum, Math.Sqrt(squareSum / first.Count), peak, rim?.Count ?? 0, rim is null ? null : rimMovement);
    }
}

internal static class SubdivisionFairing
{
    private static HashSet<string> Selected(MeshSculptState state, FairCageRegion fair)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in fair.Regions)
        {
            if (!names.Add(region) || !state.VertexRegions!.TryGetValue(region, out var vertices))
                throw new InvalidOperationException($"mesh-fair-region-unresolved: Use unique named cage regions; '{region}' is invalid.");
            result.UnionWith(vertices);
        }
        return result;
    }

    internal static SubdivisionCage Apply(MeshSculptState state, FairCageRegion fair, HashSet<string> pinned)
    {
        if (fair.Iterations is < 1 or > 16 || !double.IsFinite(fair.MaximumMovementMm) || fair.MaximumMovementMm <= 0)
            throw new InvalidOperationException("mesh-fair-budget-invalid: Use 1..16 iterations and a finite positive maximumMovement.");
        var selected = Selected(state, fair);
        // Keep both the declared rim and any existing sharp features.
        var sharp = state.Cage.Creases.ToHashSet(StringComparer.Ordinal);
        var edges = state.Edges;
        foreach (var edge in edges.Where(edge => sharp.Contains(edge.Id)))
        {
            pinned.Add(edge.A);
            pinned.Add(edge.B);
        }
        selected.ExceptWith(pinned);
        if (selected.Count == 0)
            throw new InvalidOperationException("mesh-fair-region-empty: The region has no unpinned controls.");

        var original = state.Cage.Vertices.ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        var neighbors = selected.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (neighbors.TryGetValue(edge.A, out var a)) a.Add(edge.B);
            if (neighbors.TryGetValue(edge.B, out var b)) b.Add(edge.A);
        }

        var positions = new Dictionary<string, Point3D>(original, StringComparer.Ordinal);
        for (var iteration = 0; iteration < fair.Iterations; iteration++)
        {
            var updated = new Dictionary<string, Point3D>(StringComparer.Ordinal);
            foreach (var (id, adjacent) in neighbors)
            {
                var p = positions[id];
                var delta = Vector3D.Zero;
                foreach (var neighbor in adjacent) delta += positions[neighbor] - p;
                updated[id] = p + delta * (0.5 / adjacent.Count);
            }
            foreach (var (id, value) in updated) positions[id] = value;
        }

        // Positive subdivision weights preserve the convex displacement bound.
        // The export contract is also checked on the evaluated limit samples.
        var maximum = selected.Max(id => (positions[id] - original[id]).Length);
        var scale = maximum > fair.MaximumMovementMm ? fair.MaximumMovementMm / maximum : 1;
        return state.Cage with
        {
            Vertices = state.Cage.Vertices.Select(v => selected.Contains(v.Id)
                ? v with { Position = v.Position + (positions[v.Id] - v.Position) * scale } : v).ToArray()
        };
    }

    internal static MeshFairEvidence Measure(MeshSculptState before, SubdivisionCage output, FairCageRegion fair)
    {
        var movement = SubdivisionComparison.Measure(before, before with { Cage = output });
        if (movement.MaximumMovementMm > fair.MaximumMovementMm + 1e-9)
            throw new InvalidOperationException("mesh-fair-budget-exceeded: Evaluated surface movement exceeds maximumMovement.");
        var selected = Selected(before, fair);
        var sharp = before.Cage.Creases.ToHashSet(StringComparer.Ordinal);
        var edges = before.Edges.Where(edge => !sharp.Contains(edge.Id)
            && (selected.Contains(edge.A) || selected.Contains(edge.B))).ToArray();
        var moved = before.Cage.Vertices.Zip(output.Vertices).Count(pair => pair.First.Position != pair.Second.Position);
        return new(fair.Id, moved, fair.MaximumMovementMm, movement, NormalTurn(before.Cage, edges), NormalTurn(output, edges));
    }

    private static double NormalTurn(SubdivisionCage cage, IReadOnlyList<CageEdge> edges)
    {
        var points = SubdivisionSculptor.LimitVertices(cage).ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        var normals = cage.Faces.Select(face =>
        {
            var diagonal = points[face.Vertices[2]] - points[face.Vertices[0]];
            var across = points[face.Vertices[3]] - points[face.Vertices[1]];
            return diagonal.Cross(across);
        }).ToArray();
        if (edges.Count == 0) return 0;
        return Math.Sqrt(edges.Average(edge =>
        {
            var a = normals[edge.Faces[0]];
            var b = normals[edge.Faces[1]];
            var angle = Math.Atan2(a.Cross(b).Length, a.Dot(b)) * (180 / Math.PI);
            return angle * angle;
        }));
    }
}
