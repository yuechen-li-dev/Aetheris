using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Boundaries;

public interface IBoundaryOffsetMapCapability
{
    IReadOnlyList<IAnalyticBoundarySupport> BoundarySupports(BoundingBox3D cellBounds);
}

/// <summary>Exact fixture/backend support used to derive and independently validate local maps.</summary>
public interface IAnalyticBoundarySupport
{
    BoundaryReference Reference { get; }
    double ExactArea { get; }
    Point3D Project(Point3D point);
    Vector3D MaterialSideNormal(Point3D boundaryPoint);
    IBoundaryOffsetMap CreateOffsetMap(
        CellIndex cellIndex,
        BoundingBox3D cellBounds,
        int resolution,
        BoundaryOffsetMapErrorPolicy policy,
        BoundaryEvaluationCache? cache = null);
}

public readonly record struct BoundaryEvaluationKey(
    string BoundaryId,
    long FrameX,
    long FrameY,
    long U,
    long V);

public readonly record struct ExactBoundaryEvaluation(double Offset, Vector3D Normal);

/// <summary>Small deterministic cache for repeated exact support evaluations during map construction.</summary>
public sealed class BoundaryEvaluationCache
{
    private readonly Dictionary<BoundaryEvaluationKey, ExactBoundaryEvaluation> _entries = [];

    public long Requests { get; private set; }
    public long Hits { get; private set; }
    public long Misses => Requests - Hits;
    public double HitRate => Requests == 0 ? 0d : (double)Hits / Requests;

    public ExactBoundaryEvaluation GetOrAdd(BoundaryEvaluationKey key, Func<ExactBoundaryEvaluation> factory)
    {
        Requests++;
        if (_entries.TryGetValue(key, out var value))
        {
            Hits++;
            return value;
        }

        value = factory();
        _entries.Add(key, value);
        return value;
    }
}

internal static class BoundaryMapBuilder
{
    public static SampledBoundaryOffsetMap Build(
        CellIndex cellIndex,
        BoundaryReference reference,
        BoundaryLocalFrame frame,
        BoundaryMapDomain domain,
        int resolution,
        BoundaryOffsetMapErrorPolicy policy,
        Func<double, double, ExactBoundaryEvaluation> exactSample,
        Func<double, double, ExactBoundaryEvaluation> exactValidation,
        Func<double, double, BoundaryEvaluationKey> key,
        BoundaryEvaluationCache? cache)
    {
        if (resolution < 2 || resolution > policy.MaximumResolution)
        {
            throw new ArgumentOutOfRangeException(nameof(resolution));
        }

        var grid = new BoundaryOffsetSample[resolution, resolution];
        for (var j = 0; j < resolution; j++)
        for (var i = 0; i < resolution; i++)
        {
            var u = Lerp(domain.MinimumU, domain.MaximumU, i / (double)(resolution - 1));
            var v = Lerp(domain.MinimumV, domain.MaximumV, j / (double)(resolution - 1));
            var evaluation = cache is null ? exactSample(u, v) : cache.GetOrAdd(key(u, v), () => exactSample(u, v));
            grid[i, j] = new BoundaryOffsetSample(u, v, evaluation.Offset, evaluation.Normal);
        }

        // Build once with provisional metadata so validation exercises interpolation, never sample nodes only.
        var provisional = new BoundaryApproximationMetadata(0d, 0d, 0d, 0d, 0d, "bilinear-offset-linear-normal", resolution, resolution, 0, true);
        var map = new SampledBoundaryOffsetMap(cellIndex, reference, frame, domain, grid, provisional);
        var validation = ValidateIndependent(map, exactValidation, resolution);
        var accepted = validation.MaximumPositionError <= policy.MaximumPositionError
            && validation.MaximumNormalAngleDegrees <= policy.MaximumNormalAngleDegrees;
        var metadata = new BoundaryApproximationMetadata(
            validation.MaximumPositionError,
            validation.RmsPositionError,
            validation.MeanPositionError,
            validation.MaximumNormalAngleDegrees,
            validation.RmsNormalAngleDegrees,
            "bilinear-offset-linear-normal",
            resolution,
            resolution,
            validation.Count,
            accepted);
        return new SampledBoundaryOffsetMap(cellIndex, reference, frame, domain, grid, metadata);
    }

    private static ValidationMetrics ValidateIndependent(
        SampledBoundaryOffsetMap map,
        Func<double, double, ExactBoundaryEvaluation> exact,
        int resolution)
    {
        var countPerAxis = (resolution * 2) + 1;
        var positionSum = 0d;
        var positionSquareSum = 0d;
        var angleSquareSum = 0d;
        var maximumPosition = 0d;
        var maximumAngle = 0d;
        var count = 0;
        for (var j = 0; j < countPerAxis; j++)
        for (var i = 0; i < countPerAxis; i++)
        {
            // Half-stride locations are independent of the map's uniformly spaced nodes.
            var u = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, (i + 0.5d) / countPerAxis);
            var v = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, (j + 0.5d) / countPerAxis);
            var truth = exact(u, v);
            var approximate = map.Evaluate(u, v);
            var exactPosition = map.LocalFrame.Origin
                + (map.LocalFrame.TangentU * u)
                + (map.LocalFrame.TangentV * v)
                + (map.LocalFrame.Normal * truth.Offset);
            var positionError = (approximate.Position - exactPosition).Length;
            var dot = double.Clamp(approximate.Normal.Dot(truth.Normal), -1d, 1d);
            var angle = double.Acos(dot) * 180d / double.Pi;
            maximumPosition = double.Max(maximumPosition, positionError);
            maximumAngle = double.Max(maximumAngle, angle);
            positionSum += positionError;
            positionSquareSum += positionError * positionError;
            angleSquareSum += angle * angle;
            count++;
        }

        return new ValidationMetrics(
            maximumPosition,
            double.Sqrt(positionSquareSum / count),
            positionSum / count,
            maximumAngle,
            double.Sqrt(angleSquareSum / count),
            count);
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private readonly record struct ValidationMetrics(
        double MaximumPositionError,
        double RmsPositionError,
        double MeanPositionError,
        double MaximumNormalAngleDegrees,
        double RmsNormalAngleDegrees,
        int Count);
}
