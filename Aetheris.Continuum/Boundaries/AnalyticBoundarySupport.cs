using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Math;
using System.Diagnostics;

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

public sealed class BoundaryMapBuildCosts
{
    public double LocalFrameMilliseconds { get; internal set; }
    public double RuntimeCertificateMilliseconds { get; internal set; }
    public double ExactQueryCacheMilliseconds { get; internal set; }
    public double MapConstructionMilliseconds { get; internal set; }
}

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
        var map = RuntimeBoundaryMapBuild.Build(cellIndex, reference, frame, domain, resolution, resolution, policy,
            exactSample, key, cache, certificate: null);
        return CertifiedBoundaryMapValidation.Validate(map, exactValidation, policy);
    }
}

/// <summary>Production-like map construction. Independent oracle evaluation is deliberately excluded.</summary>
public static class RuntimeBoundaryMapBuild
{
    public static SampledBoundaryOffsetMap Build(
        CellIndex cellIndex,
        BoundaryReference reference,
        BoundaryLocalFrame frame,
        BoundaryMapDomain domain,
        int resolutionU,
        int resolutionV,
        BoundaryOffsetMapErrorPolicy policy,
        Func<double, double, ExactBoundaryEvaluation> exactSample,
        Func<double, double, BoundaryEvaluationKey> key,
        BoundaryEvaluationCache? cache,
        EngineeringBoundaryMapCertificate? certificate,
        BoundaryMapBuildCosts? costs = null)
    {
        if (resolutionU < 2 || resolutionV < 2 || resolutionU > policy.MaximumResolution || resolutionV > policy.MaximumResolution)
        {
            throw new ArgumentOutOfRangeException(nameof(resolutionU));
        }

        var mapStart = Stopwatch.GetTimestamp();
        var grid = new BoundaryOffsetSample[resolutionU, resolutionV];
        for (var j = 0; j < resolutionV; j++)
        for (var i = 0; i < resolutionU; i++)
        {
            var u = Lerp(domain.MinimumU, domain.MaximumU, i / (double)(resolutionU - 1));
            var v = Lerp(domain.MinimumV, domain.MaximumV, j / (double)(resolutionV - 1));
            var queryStart = Stopwatch.GetTimestamp();
            var evaluation = cache is null ? exactSample(u, v) : cache.GetOrAdd(key(u, v), () => exactSample(u, v));
            if (costs is not null) costs.ExactQueryCacheMilliseconds += Stopwatch.GetElapsedTime(queryStart).TotalMilliseconds;
            grid[i, j] = new BoundaryOffsetSample(u, v, evaluation.Offset, evaluation.Normal);
        }

        var accepted = certificate?.Decision == BoundaryMapCertificateDecision.Acceptable;
        var metadata = new BoundaryApproximationMetadata(0d, 0d, 0d, 0d, 0d,
            "bilinear-offset-linear-normal", resolutionU, resolutionV, 0, accepted, 2, certificate);
        var map = new SampledBoundaryOffsetMap(cellIndex, reference, frame, domain, grid, metadata);
        if (costs is not null) costs.MapConstructionMilliseconds += Stopwatch.GetElapsedTime(mapStart).TotalMilliseconds;
        return map;
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}

/// <summary>Independent experimental/debug oracle. This is never required by runtime map construction.</summary>
public static class CertifiedBoundaryMapValidation
{
    public static SampledBoundaryOffsetMap Validate(
        SampledBoundaryOffsetMap map,
        Func<double, double, ExactBoundaryEvaluation> exact,
        BoundaryOffsetMapErrorPolicy policy,
        int? countU = null,
        int? countV = null)
    {
        var validation = ValidateIndependent(map, exact,
            countU ?? ((map.Approximation.ResolutionU * 2) + 1),
            countV ?? ((map.Approximation.ResolutionV * 2) + 1));
        var accepted = validation.MaximumPositionError <= policy.MaximumPositionError
            && validation.MaximumNormalAngleDegrees <= policy.MaximumNormalAngleDegrees;
        var metadata = new BoundaryApproximationMetadata(
            validation.MaximumPositionError,
            validation.RmsPositionError,
            validation.MeanPositionError,
            validation.MaximumNormalAngleDegrees,
            validation.RmsNormalAngleDegrees,
            "bilinear-offset-linear-normal",
            map.Approximation.ResolutionU,
            map.Approximation.ResolutionV,
            validation.Count,
            accepted,
            2,
            map.Approximation.RuntimeCertificate);
        return map.WithApproximation(metadata);
    }

    private static ValidationMetrics ValidateIndependent(
        SampledBoundaryOffsetMap map,
        Func<double, double, ExactBoundaryEvaluation> exact,
        int countU,
        int countV)
    {
        var positionSum = 0d;
        var positionSquareSum = 0d;
        var angleSquareSum = 0d;
        var maximumPosition = 0d;
        var maximumAngle = 0d;
        var count = 0;
        for (var j = 0; j < countV; j++)
        for (var i = 0; i < countU; i++)
        {
            // Half-stride locations are independent of the map's uniformly spaced nodes.
            var u = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, (i + 0.5d) / countU);
            var v = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, (j + 0.5d) / countV);
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
