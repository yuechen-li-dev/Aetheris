using Aetheris.Kernel.Core.Math;

namespace Aetheris.Reconstruction;

public sealed record FieldRegularizationReport(
    int InputSingularities, int OutputSingularities, int CancelledNet,
    int RetainedFeatureFaces, int RetainedBoundaryFaces, int AcceptedIterations,
    double InputIndexSum, double OutputIndexSum, bool GlobalIndexPreserved,
    string Objective, string DeterministicPolicy);

/// <summary>
/// Bounded global relaxation of a four-fold tangent field. Boundary and high-curvature feature
/// samples are fixed. An iteration is admitted only when the detected global quarter-index sum
/// is preserved and singularity count does not increase.
/// </summary>
public static class CrossFieldRegularizer
{
    public static (IReadOnlyList<DifferentialSample> Field, FieldRegularizationReport Report) Regularize(
        TriangleSurfaceMesh mesh, IReadOnlyList<DifferentialSample> input, int maximumIterations = 8, double featureQuantile = .9)
    {
        ArgumentNullException.ThrowIfNull(mesh); ArgumentNullException.ThrowIfNull(input);
        if (input.Count != mesh.Triangles.Count) throw new ArgumentException("Field sample count must equal face count.", nameof(input));
        if (maximumIterations < 0 || maximumIterations > 100) throw new ArgumentOutOfRangeException(nameof(maximumIterations));
        if (!double.IsFinite(featureQuantile) || featureQuantile <= 0 || featureQuantile >= 1) throw new ArgumentOutOfRangeException(nameof(featureQuantile));
        var adjacency = Adjacency(mesh, out var boundaryFaces); var curvatures = input.Select(x => x.CurvatureProxy).Where(double.IsFinite).Order().ToArray();
        var threshold = curvatures.Length == 0 ? double.PositiveInfinity : curvatures[(int)Math.Clamp(Math.Ceiling(featureQuantile * curvatures.Length) - 1, 0, curvatures.Length - 1)];
        var featureFaces = input.Where(x => x.CurvatureProxy >= threshold && x.CurvatureProxy > 0).Select(x => x.TriangleIndex).ToHashSet();
        var current = input.ToArray(); var initial = QuadAtlasRecovery.DetectSingularitiesForRegularization(mesh, current); var currentSingularities = initial; var initialIndex = initial.Sum(x => x.QuarterIndex); var accepted = 0;
        for (var iteration = 0; iteration < maximumIterations; iteration++)
        {
            var next = (DifferentialSample[])current.Clone();
            for (var face = 0; face < next.Length; face++)
            {
                if (!current[face].DirectionKnown || boundaryFaces.Contains(face) || featureFaces.Contains(face)) continue;
                var normal = current[face].Normal; var anchor = current[face].CrossDirection; var sum = anchor * 2;
                foreach (var neighbor in adjacency[face]) if (current[neighbor].DirectionKnown)
                {
                    var projected = current[neighbor].CrossDirection - normal * current[neighbor].CrossDirection.Dot(normal); if (!projected.TryNormalize(out projected)) continue;
                    var perpendicular = normal.Cross(projected); if (!perpendicular.TryNormalize(out perpendicular)) continue;
                    var representative = new[] { projected, -projected, perpendicular, -perpendicular }.OrderByDescending(x => x.Dot(anchor)).First(); sum += representative;
                }
                sum -= normal * sum.Dot(normal); if (sum.TryNormalize(out sum)) next[face] = next[face] with { CrossDirection = sum, EvidenceClass = "Heuristic: globally relaxed with fixed boundary/feature constraints" };
            }
            var singularities = QuadAtlasRecovery.DetectSingularitiesForRegularization(mesh, next); var index = singularities.Sum(x => x.QuarterIndex);
            if (Math.Abs(index - initialIndex) > .125 || singularities.Count > currentSingularities.Count) break;
            current = next; currentSingularities = singularities; accepted++;
        }
        var outputIndex = currentSingularities.Sum(x => x.QuarterIndex);
        return (current, new(initial.Count, currentSingularities.Count, initial.Count - currentSingularities.Count, featureFaces.Count, boundaryFaces.Count, accepted,
            initialIndex, outputIndex, Math.Abs(initialIndex - outputIndex) <= .125,
            "principal-direction alignment + neighbor smoothness + fixed feature/boundary evidence + singularity sparsity",
            "synchronous quarter-equivalent averaging; admit only non-increasing defect count with preserved global detected index"));
    }

    private static int[][] Adjacency(TriangleSurfaceMesh mesh, out HashSet<int> boundaryFaces)
    {
        var edges = new Dictionary<(int, int), List<int>>();
        for (var f = 0; f < mesh.Triangles.Count; f++) foreach (var edge in mesh.Triangles[f].DirectedEdges())
        { var key = edge.A < edge.B ? (edge.A, edge.B) : (edge.B, edge.A); if (!edges.TryGetValue(key, out var faces)) edges[key] = faces = []; faces.Add(f); }
        var lists = Enumerable.Range(0, mesh.Triangles.Count).Select(_ => new HashSet<int>()).ToArray(); boundaryFaces = [];
        foreach (var faces in edges.Values) if (faces.Count == 1) boundaryFaces.Add(faces[0]); else for (var i = 0; i < faces.Count; i++) for (var j = i + 1; j < faces.Count; j++) { lists[faces[i]].Add(faces[j]); lists[faces[j]].Add(faces[i]); }
        return lists.Select(x => x.Order().ToArray()).ToArray();
    }
}

