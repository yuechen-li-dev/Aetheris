using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public sealed record VertexDeltaStatistics(
    int Count,
    double RmsMm,
    double P50Mm,
    double P95Mm,
    double P99Mm,
    double MaximumMm,
    Vector3D MeanSignedDeltaMm);

public sealed record RegionVertexDeltaStatistics(HumanoidRegionKind Region, VertexDeltaStatistics Statistics);

public sealed record EdgeDistortionStatistics(
    int Edges,
    double MinimumLengthRatio,
    double P50BidirectionalRatio,
    double P95BidirectionalRatio,
    double P99BidirectionalRatio,
    double MaximumBidirectionalRatio,
    int OrientationReversals);

public sealed record EdgeWitness(
    string VertexA,
    string VertexB,
    double RestLengthMm,
    double SourceLengthMm,
    double AetherisLengthMm);

public sealed record HumanoidSourcePoseDiff(
    string TopologyId,
    string ConnectivityHash,
    VertexDeltaStatistics Vertices,
    IReadOnlyList<RegionVertexDeltaStatistics> Regions,
    EdgeDistortionStatistics SourceEdges,
    EdgeDistortionStatistics AetherisEdges,
    EdgeWitness? KnownHipEdge);

/// <summary>
/// Corresponding-index comparison only. This code never performs surface matching or
/// silently accepts a different topology; source extraction owns and hashes any mapping.
/// </summary>
public static class HumanoidSourcePoseComparison
{
    public const string KnownHipEdgeVertexA = "antonia:v:008100";
    public const string KnownHipEdgeVertexB = "antonia:v:031783";

    public static HumanoidSourcePoseDiff Compare(
        HumanoidSurface surface,
        IReadOnlyList<Point3D> sourcePositions,
        IReadOnlyList<Point3D> aetherisPositions)
    {
        if (sourcePositions.Count != surface.Vertices.Count || aetherisPositions.Count != surface.Vertices.Count)
            throw new InvalidDataException("HUM400: Source/Aetheris pose arrays must use the canonical vertex count and order.");
        if (!sourcePositions.All(Finite) || !aetherisPositions.All(Finite))
            throw new InvalidDataException("HUM401: Source/Aetheris pose arrays must be finite.");

        var all = Enumerable.Range(0, surface.Vertices.Count).ToArray();
        var regions = surface.Vertices
            .Select((vertex, index) => (vertex.Region, index))
            .GroupBy(item => item.Region)
            .OrderBy(group => group.Key)
            .Select(group => new RegionVertexDeltaStatistics(group.Key, Statistics(group.Select(item => item.index))))
            .ToArray();

        var indexById = surface.Vertices.Select((vertex, index) => (vertex.Id, index))
            .ToDictionary(item => item.Id, item => item.index, StringComparer.Ordinal);
        EdgeWitness? witness = null;
        if (indexById.TryGetValue(KnownHipEdgeVertexA, out var a) && indexById.TryGetValue(KnownHipEdgeVertexB, out var b))
            witness = new(KnownHipEdgeVertexA, KnownHipEdgeVertexB,
                Length(surface.Vertices[a].Position, surface.Vertices[b].Position),
                Length(sourcePositions[a], sourcePositions[b]), Length(aetherisPositions[a], aetherisPositions[b]));

        return new(surface.TopologyId, surface.ConnectivityHash, Statistics(all), regions,
            EdgeStatistics(sourcePositions), EdgeStatistics(aetherisPositions), witness);

        VertexDeltaStatistics Statistics(IEnumerable<int> indices)
        {
            var selected = indices.ToArray();
            var magnitudes = selected.Select(index => Length(sourcePositions[index], aetherisPositions[index])).Order().ToArray();
            if (magnitudes.Length == 0) throw new InvalidDataException("HUM402: Empty semantic region in pose comparison.");
            var signed = selected.Select(index => aetherisPositions[index] - sourcePositions[index]).ToArray();
            return new(magnitudes.Length, Math.Sqrt(magnitudes.Sum(value => value * value) / magnitudes.Length),
                Percentile(magnitudes, .5), Percentile(magnitudes, .95), Percentile(magnitudes, .99), magnitudes[^1],
                new(signed.Average(value => value.X), signed.Average(value => value.Y), signed.Average(value => value.Z)));
        }

        EdgeDistortionStatistics EdgeStatistics(IReadOnlyList<Point3D> posed)
        {
            var edges = new SortedSet<(int A, int B)>();
            var reversals = 0;
            foreach (var face in surface.Faces)
            {
                Add(face.A, face.B); Add(face.B, face.C); Add(face.C, face.A);
                var restNormal = (surface.Vertices[face.B].Position - surface.Vertices[face.A].Position)
                    .Cross(surface.Vertices[face.C].Position - surface.Vertices[face.A].Position);
                var posedNormal = (posed[face.B] - posed[face.A]).Cross(posed[face.C] - posed[face.A]);
                if (posedNormal.LengthSquared < 4e-8 || posedNormal.Dot(restNormal) <= 0) reversals++;
            }
            var lengthRatios = edges.Select(edge =>
            {
                var rest = Length(surface.Vertices[edge.A].Position, surface.Vertices[edge.B].Position);
                var current = Length(posed[edge.A], posed[edge.B]);
                return current / rest;
            }).Order().ToArray();
            var bidirectional = lengthRatios.Select(ratio => Math.Max(ratio, 1 / ratio)).Order().ToArray();
            return new(edges.Count, lengthRatios[0], Percentile(bidirectional, .5), Percentile(bidirectional, .95),
                Percentile(bidirectional, .99), bidirectional[^1], reversals);

            void Add(int first, int second) => edges.Add(first < second ? (first, second) : (second, first));
        }
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile) =>
        sorted[Math.Max(0, (int)Math.Ceiling(percentile * sorted.Count) - 1)];

    private static double Length(Point3D first, Point3D second) => (first - second).Length;
    private static bool Finite(Point3D point) => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
}
