using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal sealed class BsplineUvGridScaffoldBuilder
{
    internal BsplineUvGridScaffoldResult Build(BSplineSurfaceWithKnots surface, BsplineUvGridScaffoldBuildRequest request)
    {
        if (request.USegments <= 0 || request.VSegments <= 0)
        {
            return BsplineUvGridScaffoldResult.Rejected(BsplineUvGridScaffoldRejectionReason.UnsupportedInput, BsplineUvGridScaffoldMetrics.Empty);
        }

        var rows = request.USegments + 1;
        var columns = request.VSegments + 1;
        var positions = new Point3D[rows * columns];
        var uvPoints = new UvPoint[rows * columns];

        for (var uIndex = 0; uIndex < rows; uIndex++)
        {
            var tu = (double)uIndex / request.USegments;
            var u = Lerp(surface.DomainStartU, surface.DomainEndU, tu);
            for (var vIndex = 0; vIndex < columns; vIndex++)
            {
                var tv = (double)vIndex / request.VSegments;
                var v = Lerp(surface.DomainStartV, surface.DomainEndV, tv);
                var index = GridIndex(uIndex, vIndex, columns);
                uvPoints[index] = new UvPoint(u, v);
                positions[index] = surface.Evaluate(u, v);
            }
        }

        var triangles = BuildTriangles(request, uvPoints, columns);
        var mesh = new BsplineUvGridScaffoldMesh(positions, triangles, uvPoints);

        if (!HasValidTriangleIndexing(mesh))
        {
            return BsplineUvGridScaffoldResult.Rejected(BsplineUvGridScaffoldRejectionReason.InvalidIndices, BsplineUvGridScaffoldMetrics.Empty, mesh);
        }

        if (HasNonFinite(mesh.Positions))
        {
            return BsplineUvGridScaffoldResult.Rejected(BsplineUvGridScaffoldRejectionReason.NonFiniteGeometry, BsplineUvGridScaffoldMetrics.Empty, mesh);
        }

        if (mesh.TriangleIndices.Count == 0)
        {
            return BsplineUvGridScaffoldResult.Rejected(BsplineUvGridScaffoldRejectionReason.UnsupportedInput, BsplineUvGridScaffoldMetrics.Empty, mesh);
        }

        var degenerateRatio = ComputeDegenerateTriangleRatio(mesh, request.DegenerateTriangleAreaTolerance);
        if (degenerateRatio > request.MaxDegenerateTriangleRatio)
        {
            return BsplineUvGridScaffoldResult.Rejected(
                BsplineUvGridScaffoldRejectionReason.ExcessiveDegenerateTriangles,
                new BsplineUvGridScaffoldMetrics(degenerateRatio, 0, 0d, 0d, 1d),
                mesh);
        }

        var leakageCount = ComputeTrimLeakageCount(mesh, request.TrimMask);
        if (leakageCount > 0)
        {
            return BsplineUvGridScaffoldResult.Rejected(
                BsplineUvGridScaffoldRejectionReason.TrimLeakage,
                new BsplineUvGridScaffoldMetrics(degenerateRatio, leakageCount, 0d, 0d, 1d),
                mesh);
        }

        var boundaryDeviation = ComputeBoundaryDeviation(mesh, request.TrimMask);
        if (boundaryDeviation > request.MaxBoundaryDeviationUv)
        {
            return BsplineUvGridScaffoldResult.Rejected(
                BsplineUvGridScaffoldRejectionReason.BoundaryDeviationTooHigh,
                new BsplineUvGridScaffoldMetrics(degenerateRatio, leakageCount, boundaryDeviation, 0d, 1d),
                mesh);
        }

        var fidelityError = ComputeFidelityError(mesh, request.ReferencePositions);
        if (fidelityError > request.MaxFidelityError)
        {
            return BsplineUvGridScaffoldResult.Rejected(
                BsplineUvGridScaffoldRejectionReason.FidelityTooLow,
                new BsplineUvGridScaffoldMetrics(degenerateRatio, leakageCount, boundaryDeviation, fidelityError, 1d),
                mesh);
        }

        var densityRatio = ComputeDensityRatio(mesh, request.ReferenceTriangleCount);
        if (densityRatio > request.MaxTriangleDensityRatioVsFallback)
        {
            return BsplineUvGridScaffoldResult.Rejected(
                BsplineUvGridScaffoldRejectionReason.TooDenseVsFallback,
                new BsplineUvGridScaffoldMetrics(degenerateRatio, leakageCount, boundaryDeviation, fidelityError, densityRatio),
                mesh);
        }

        return BsplineUvGridScaffoldResult.Accepted(
            mesh,
            new BsplineUvGridScaffoldMetrics(degenerateRatio, leakageCount, boundaryDeviation, fidelityError, densityRatio));
    }

    private static List<int> BuildTriangles(BsplineUvGridScaffoldBuildRequest request, IReadOnlyList<UvPoint> uvPoints, int columns)
    {
        var triangles = new List<int>(request.USegments * request.VSegments * 6);
        for (var uIndex = 0; uIndex < request.USegments; uIndex++)
        {
            for (var vIndex = 0; vIndex < request.VSegments; vIndex++)
            {
                var bottomLeft = GridIndex(uIndex, vIndex, columns);
                var bottomRight = GridIndex(uIndex + 1, vIndex, columns);
                var topLeft = GridIndex(uIndex, vIndex + 1, columns);
                var topRight = GridIndex(uIndex + 1, vIndex + 1, columns);

                if (request.TrimMask is not null
                    && !request.TrimMask.KeepCellByCorners(
                        uvPoints[bottomLeft],
                        uvPoints[bottomRight],
                        uvPoints[topLeft],
                        uvPoints[topRight]))
                {
                    continue;
                }

                triangles.Add(bottomLeft);
                triangles.Add(bottomRight);
                triangles.Add(topRight);

                triangles.Add(bottomLeft);
                triangles.Add(topRight);
                triangles.Add(topLeft);
            }
        }

        return triangles;
    }

    private static bool HasValidTriangleIndexing(BsplineUvGridScaffoldMesh mesh)
        => mesh.TriangleIndices.Count % 3 == 0
            && mesh.TriangleIndices.All(index => index >= 0 && index < mesh.Positions.Count);

    private static bool HasNonFinite(IReadOnlyList<Point3D> positions)
        => positions.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z));

    private static double ComputeDegenerateTriangleRatio(BsplineUvGridScaffoldMesh mesh, double areaTolerance)
    {
        var triangles = mesh.TriangleIndices.Count / 3;
        if (triangles == 0)
        {
            return 1d;
        }

        var degenerate = 0;
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            var a = mesh.Positions[mesh.TriangleIndices[i]];
            var b = mesh.Positions[mesh.TriangleIndices[i + 1]];
            var c = mesh.Positions[mesh.TriangleIndices[i + 2]];
            var area2 = (b - a).Cross(c - a).Length;
            if (area2 <= areaTolerance)
            {
                degenerate++;
            }
        }

        return (double)degenerate / triangles;
    }

    private static int ComputeTrimLeakageCount(BsplineUvGridScaffoldMesh mesh, UvTrimMask? trimMask)
    {
        if (trimMask is null)
        {
            return 0;
        }

        var leakage = 0;
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            var a = mesh.UvPoints[mesh.TriangleIndices[i]];
            var b = mesh.UvPoints[mesh.TriangleIndices[i + 1]];
            var c = mesh.UvPoints[mesh.TriangleIndices[i + 2]];
            var centroid = new UvPoint((a.U + b.U + c.U) / 3d, (a.V + b.V + c.V) / 3d);
            if (!trimMask.Contains(centroid))
            {
                leakage++;
            }
        }

        return leakage;
    }

    private static double ComputeBoundaryDeviation(BsplineUvGridScaffoldMesh mesh, UvTrimMask? trimMask)
    {
        if (trimMask is null)
        {
            return 0d;
        }

        var usedIndices = mesh.TriangleIndices.Distinct().ToArray();
        if (usedIndices.Length == 0)
        {
            return double.PositiveInfinity;
        }

        var usedUv = usedIndices.Select(index => mesh.UvPoints[index]).ToArray();
        var maxDeviation = 0d;

        maxDeviation = double.Max(maxDeviation, ComputeLoopDeviation(trimMask.OuterLoop, usedUv));
        foreach (var hole in trimMask.InnerLoops)
        {
            maxDeviation = double.Max(maxDeviation, ComputeLoopDeviation(hole, usedUv));
        }

        return maxDeviation;
    }

    private static double ComputeLoopDeviation(IReadOnlyList<UvPoint> loop, IReadOnlyList<UvPoint> usedUv)
    {
        var maxDeviation = 0d;
        for (var i = 0; i < loop.Count; i++)
        {
            var start = loop[i];
            var end = loop[(i + 1) % loop.Count];
            var nearest = usedUv.Min(sample => DistancePointToSegment(sample, start, end));
            maxDeviation = double.Max(maxDeviation, nearest);
        }

        return maxDeviation;
    }

    private static double ComputeFidelityError(BsplineUvGridScaffoldMesh mesh, IReadOnlyList<Point3D>? referencePositions)
    {
        if (referencePositions is null || referencePositions.Count == 0)
        {
            return 0d;
        }

        var usedPositions = mesh.TriangleIndices
            .Distinct()
            .Select(index => mesh.Positions[index])
            .ToArray();
        if (usedPositions.Length == 0)
        {
            return double.PositiveInfinity;
        }

        var forward = ComputeMeanNearestDistance(usedPositions, referencePositions);
        var reverse = ComputeMeanNearestDistance(referencePositions, usedPositions);
        return double.Max(forward, reverse);
    }

    private static double ComputeDensityRatio(BsplineUvGridScaffoldMesh mesh, int? referenceTriangleCount)
    {
        if (referenceTriangleCount is null || referenceTriangleCount <= 0)
        {
            return 1d;
        }

        return (double)(mesh.TriangleIndices.Count / 3) / referenceTriangleCount.Value;
    }

    private static double ComputeMeanNearestDistance(IReadOnlyList<Point3D> source, IReadOnlyList<Point3D> target)
    {
        var sum = 0d;
        foreach (var sourcePoint in source)
        {
            var nearest = target.Min(targetPoint => (targetPoint - sourcePoint).Length);
            sum += nearest;
        }

        return source.Count == 0 ? double.PositiveInfinity : sum / source.Count;
    }

    private static double DistancePointToSegment(UvPoint point, UvPoint segmentStart, UvPoint segmentEnd)
    {
        var segmentU = segmentEnd.U - segmentStart.U;
        var segmentV = segmentEnd.V - segmentStart.V;
        var lengthSquared = (segmentU * segmentU) + (segmentV * segmentV);
        if (lengthSquared <= 1e-12d)
        {
            return UvDistance(point, segmentStart);
        }

        var projection = (((point.U - segmentStart.U) * segmentU) + ((point.V - segmentStart.V) * segmentV)) / lengthSquared;
        projection = double.Clamp(projection, 0d, 1d);
        var nearest = new UvPoint(segmentStart.U + (projection * segmentU), segmentStart.V + (projection * segmentV));
        return UvDistance(point, nearest);
    }

    private static double UvDistance(UvPoint left, UvPoint right)
    {
        var du = left.U - right.U;
        var dv = left.V - right.V;
        return double.Sqrt((du * du) + (dv * dv));
    }

    private static int GridIndex(int uIndex, int vIndex, int columns)
        => (uIndex * columns) + vIndex;

    private static double Lerp(double start, double end, double t)
        => start + ((end - start) * t);
}

internal sealed record BsplineUvGridScaffoldBuildRequest(
    int USegments,
    int VSegments,
    UvTrimMask? TrimMask = null,
    IReadOnlyList<Point3D>? ReferencePositions = null,
    int? ReferenceTriangleCount = null,
    double MaxDegenerateTriangleRatio = 0.15d,
    double DegenerateTriangleAreaTolerance = 1e-12d,
    double MaxBoundaryDeviationUv = 0.06d,
    double MaxFidelityError = 0.10d,
    double MaxTriangleDensityRatioVsFallback = 1.10d);

internal sealed record BsplineUvGridScaffoldMesh(
    IReadOnlyList<Point3D> Positions,
    IReadOnlyList<int> TriangleIndices,
    IReadOnlyList<UvPoint> UvPoints);

internal sealed record BsplineUvGridScaffoldMetrics(
    double DegenerateTriangleRatio,
    int LeakageTriangleCount,
    double BoundaryDeviationUv,
    double FidelityError,
    double TriangleDensityRatio)
{
    internal static BsplineUvGridScaffoldMetrics Empty { get; } = new(0d, 0, 0d, 0d, 1d);
}

internal enum BsplineUvGridScaffoldAcceptance
{
    Accepted,
    Rejected,
}

internal enum BsplineUvGridScaffoldRejectionReason
{
    None,
    UnsupportedInput,
    InvalidIndices,
    NonFiniteGeometry,
    ExcessiveDegenerateTriangles,
    TrimLeakage,
    BoundaryDeviationTooHigh,
    FidelityTooLow,
    TooDenseVsFallback,
}

internal sealed record BsplineUvGridScaffoldResult(
    BsplineUvGridScaffoldAcceptance Acceptance,
    BsplineUvGridScaffoldMesh? Mesh,
    BsplineUvGridScaffoldMetrics Metrics,
    BsplineUvGridScaffoldRejectionReason RejectionReason)
{
    internal static BsplineUvGridScaffoldResult Accepted(BsplineUvGridScaffoldMesh mesh, BsplineUvGridScaffoldMetrics metrics)
        => new(BsplineUvGridScaffoldAcceptance.Accepted, mesh, metrics, BsplineUvGridScaffoldRejectionReason.None);

    internal static BsplineUvGridScaffoldResult Rejected(BsplineUvGridScaffoldRejectionReason reason, BsplineUvGridScaffoldMetrics metrics, BsplineUvGridScaffoldMesh? mesh = null)
        => new(BsplineUvGridScaffoldAcceptance.Rejected, mesh, metrics, reason);
}

internal readonly record struct UvPoint(double U, double V);
