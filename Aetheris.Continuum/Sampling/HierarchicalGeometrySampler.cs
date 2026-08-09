using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Sampling;

public readonly record struct GeometryQueryKey(long X, long Y, long Z)
{
    public static GeometryQueryKey From(Point3D point) => new(
        BitConverter.DoubleToInt64Bits(point.X),
        BitConverter.DoubleToInt64Bits(point.Y),
        BitConverter.DoubleToInt64Bits(point.Z));
}

public sealed class GeometryQueryCache
{
    private readonly Dictionary<GeometryQueryKey, ContinuumPointClassification> _entries = [];

    public long Requests { get; private set; }
    public long Hits { get; private set; }
    public long Misses => Requests - Hits;
    public double HitRate => Requests == 0 ? 0d : (double)Hits / Requests;

    public ContinuumPointClassification Classify(IContinuumRegion region, Point3D point)
    {
        Requests++;
        var key = GeometryQueryKey.From(point);
        if (_entries.TryGetValue(key, out var classification))
        {
            Hits++;
            return classification;
        }

        classification = region.Classify(point);
        _entries.Add(key, classification);
        return classification;
    }
}

public sealed record GeometrySamplingPass(
    GeometrySamplePlan Plan,
    long RawRequestedSamples,
    long UniqueExactQueries,
    long ReusedSamples)
{
    public double ReuseRatio => RawRequestedSamples == 0 ? 0d : (double)ReusedSamples / RawRequestedSamples;
}

public static class HierarchicalGeometrySampler
{
    private static readonly double[] Regular2 = [0.25d, 0.75d];
    private static readonly double[] Regular4 = [0.125d, 0.375d, 0.625d, 0.875d];
    private static readonly double[] NestedBase2 = [0.375d, 0.625d];

    public static GeometrySamplingPass SampleRegular(
        IContinuumRegion region,
        BoundingBox3D bounds,
        int samplesPerAxis,
        GeometryQueryCache? cache = null) =>
        Sample(region, bounds, samplesPerAxis switch
        {
            2 => Regular2,
            4 => Regular4,
            _ => throw new ArgumentOutOfRangeException(nameof(samplesPerAxis), "M1 standardizes regular 2x2x2 and 4x4x4 patterns."),
        }, samplesPerAxis, cache);

    /// <summary>
    /// A central 2x2x2 base whose coordinates are exactly the middle half of the regular 4x4x4 pattern.
    /// Refinement therefore requests the full regular pattern while reusing all eight base samples.
    /// </summary>
    public static GeometrySamplingPass SampleNestedBase2(
        IContinuumRegion region,
        BoundingBox3D bounds,
        GeometryQueryCache cache) => Sample(region, bounds, NestedBase2, 2, cache);

    public static GeometrySamplingPass RefineToRegular4(
        IContinuumRegion region,
        BoundingBox3D bounds,
        GeometryQueryCache cache) => Sample(region, bounds, Regular4, 4, cache);

    private static GeometrySamplingPass Sample(
        IContinuumRegion region,
        BoundingBox3D bounds,
        IReadOnlyList<double> coordinates,
        int samplesPerAxis,
        GeometryQueryCache? cache)
    {
        ArgumentNullException.ThrowIfNull(region);
        var beforeRequests = cache?.Requests ?? 0;
        var beforeMisses = cache?.Misses ?? 0;
        var samples = new List<GeometrySample>(coordinates.Count * coordinates.Count * coordinates.Count);
        var occupied = 0d;
        foreach (var z in coordinates)
        foreach (var y in coordinates)
        foreach (var x in coordinates)
        {
            var point = new Point3D(
                Lerp(bounds.Min.X, bounds.Max.X, x),
                Lerp(bounds.Min.Y, bounds.Max.Y, y),
                Lerp(bounds.Min.Z, bounds.Max.Z, z));
            var classification = cache?.Classify(region, point) ?? region.Classify(point);
            occupied += classification switch
            {
                ContinuumPointClassification.Inside => 1d,
                ContinuumPointClassification.Boundary => 0.5d,
                _ => 0d,
            };
            samples.Add(new GeometrySample(point, classification));
        }

        var boundaries = region is Boundaries.IBoundaryReferenceCapability capability
            ? capability.BoundaryCandidates(bounds)
            : [];
        var plan = new GeometrySamplePlan(
            GeometrySamplePattern.SubcellCenters,
            samplesPerAxis,
            samples,
            boundaries,
            occupied / samples.Count);
        var requested = samples.Count;
        var misses = cache is null ? requested : cache.Misses - beforeMisses;
        var requests = cache is null ? requested : cache.Requests - beforeRequests;
        return new GeometrySamplingPass(plan, requests, misses, requests - misses);
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
