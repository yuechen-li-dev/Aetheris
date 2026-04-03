using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal sealed class UvTrimMaskExtractor
{
    internal UvTrimMaskExtractionResult TryExtract(BrepBody body, FaceId faceId, BSplineSurfaceWithKnots surface)
    {
        var loopIds = body.GetLoopIds(faceId).OrderBy(id => id.Value).ToArray();
        if (loopIds.Length != 1)
        {
            return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.UnsupportedLoopTopology);
        }

        var loopVertices = new List<(LoopId LoopId, IReadOnlyList<UvPoint> Vertices)>(loopIds.Length);
        foreach (var loopId in loopIds)
        {
            var loop = body.Topology.GetLoop(loopId);
            if (loop.CoedgeIds.Count < 3)
            {
                return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.InsufficientLoopVertices);
            }

            var uvLoop = new List<UvPoint>(loop.CoedgeIds.Count);
            foreach (var coedgeId in loop.CoedgeIds)
            {
                var coedge = body.Topology.GetCoedge(coedgeId);
                if (!body.Topology.TryGetEdge(coedge.EdgeId, out var edge) || edge is null)
                {
                    return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.MissingTopology);
                }

                if (!body.TryGetEdgeCurveGeometry(coedge.EdgeId, out var curve) || curve is null)
                {
                    return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.MissingEdgeGeometry);
                }

                if (curve.Kind != CurveGeometryKind.Line3)
                {
                    return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.UnsupportedEdgeGeometry);
                }

                var vertexId = coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId;
                if (!body.TryGetVertexPoint(vertexId, out var point))
                {
                    return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.MissingVertexPoint);
                }

                var uv = TryProjectPointToBSplineUv(surface, point);
                if (!uv.HasValue)
                {
                    return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.UvProjectionFailed);
                }

                AppendUniqueUvPoint(uvLoop, new UvPoint(uv.Value.U, uv.Value.V));
            }

            if (uvLoop.Count > 1 && UvPointsAlmostEqual(uvLoop[0], uvLoop[^1]))
            {
                uvLoop.RemoveAt(uvLoop.Count - 1);
            }

            if (uvLoop.Count < 3)
            {
                return UvTrimMaskExtractionResult.Unsupported(UvTrimMaskExtractionFailureReason.InsufficientLoopVertices);
            }

            loopVertices.Add((loopId, uvLoop));
        }

        var orderedByArea = loopVertices
            .OrderByDescending(entry => double.Abs(ComputeSignedArea(entry.Vertices)))
            .ThenBy(entry => entry.LoopId.Value)
            .ToArray();
        var outerLoop = orderedByArea[0].Vertices;
        var innerLoops = orderedByArea.Skip(1).Select(loop => loop.Vertices).ToArray();

        return UvTrimMaskExtractionResult.Success(new UvTrimMask(outerLoop, innerLoops));
    }

    private static void AppendUniqueUvPoint(List<UvPoint> points, UvPoint point)
    {
        if (points.Count == 0 || !UvPointsAlmostEqual(points[^1], point))
        {
            points.Add(point);
        }
    }

    private static bool UvPointsAlmostEqual(UvPoint left, UvPoint right)
        => double.Abs(left.U - right.U) <= 1e-9d
           && double.Abs(left.V - right.V) <= 1e-9d;

    private static double ComputeSignedArea(IReadOnlyList<UvPoint> loop)
    {
        var area = 0d;
        for (var i = 0; i < loop.Count; i++)
        {
            var current = loop[i];
            var next = loop[(i + 1) % loop.Count];
            area += (current.U * next.V) - (next.U * current.V);
        }

        return area * 0.5d;
    }

    private static (double U, double V)? TryProjectPointToBSplineUv(BSplineSurfaceWithKnots surface, Point3D point)
    {
        var uStart = surface.DomainStartU;
        var uEnd = surface.DomainEndU;
        var vStart = surface.DomainStartV;
        var vEnd = surface.DomainEndV;

        const int coarseSegments = 10;
        var bestU = uStart;
        var bestV = vStart;
        var bestDistanceSquared = double.PositiveInfinity;

        for (var iu = 0; iu <= coarseSegments; iu++)
        {
            var tu = (double)iu / coarseSegments;
            var u = uStart + ((uEnd - uStart) * tu);
            for (var iv = 0; iv <= coarseSegments; iv++)
            {
                var tv = (double)iv / coarseSegments;
                var v = vStart + ((vEnd - vStart) * tv);
                var sample = surface.Evaluate(u, v);
                var delta = sample - point;
                var distanceSquared = delta.Dot(delta);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestU = u;
                    bestV = v;
                }
            }
        }

        var uStep = (uEnd - uStart) / coarseSegments;
        var vStep = (vEnd - vStart) / coarseSegments;

        for (var iteration = 0; iteration < 6; iteration++)
        {
            var nextUStep = uStep * 0.5d;
            var nextVStep = vStep * 0.5d;
            var localBestU = bestU;
            var localBestV = bestV;
            var localBestDistance = bestDistanceSquared;

            for (var du = -1; du <= 1; du++)
            {
                var u = System.Math.Clamp(bestU + (du * uStep), uStart, uEnd);
                for (var dv = -1; dv <= 1; dv++)
                {
                    var v = System.Math.Clamp(bestV + (dv * vStep), vStart, vEnd);
                    var sample = surface.Evaluate(u, v);
                    var delta = sample - point;
                    var distanceSquared = delta.Dot(delta);
                    if (distanceSquared < localBestDistance)
                    {
                        localBestDistance = distanceSquared;
                        localBestU = u;
                        localBestV = v;
                    }
                }
            }

            bestU = localBestU;
            bestV = localBestV;
            bestDistanceSquared = localBestDistance;
            uStep = nextUStep;
            vStep = nextVStep;
        }

        var bestSample = surface.Evaluate(bestU, bestV);
        var residual = bestSample - point;
        var residualDistance = System.Math.Sqrt(residual.Dot(residual));
        var tolerance = ComputeBSplineProjectionTolerance(surface);
        if (residualDistance > tolerance)
        {
            return null;
        }

        return (bestU, bestV);
    }

    private static double ComputeBSplineProjectionTolerance(BSplineSurfaceWithKnots surface)
    {
        var points = surface.ControlPoints.SelectMany(row => row).ToArray();
        if (points.Length == 0)
        {
            return 1e-4d;
        }

        var minX = points.Min(point => point.X);
        var minY = points.Min(point => point.Y);
        var minZ = points.Min(point => point.Z);
        var maxX = points.Max(point => point.X);
        var maxY = points.Max(point => point.Y);
        var maxZ = points.Max(point => point.Z);
        var diagonal = new Vector3D(maxX - minX, maxY - minY, maxZ - minZ).Length;
        return System.Math.Max(1e-5d, diagonal * 1e-4d);
    }
}

internal enum UvTrimMaskExtractionFailureReason
{
    None = 0,
    UnsupportedLoopTopology,
    UnsupportedEdgeGeometry,
    MissingEdgeGeometry,
    MissingVertexPoint,
    MissingTopology,
    UvProjectionFailed,
    InsufficientLoopVertices,
}

internal sealed record UvTrimMaskExtractionResult(
    bool IsSuccess,
    UvTrimMask? TrimMask,
    UvTrimMaskExtractionFailureReason FailureReason)
{
    internal static UvTrimMaskExtractionResult Success(UvTrimMask trimMask)
        => new(true, trimMask, UvTrimMaskExtractionFailureReason.None);

    internal static UvTrimMaskExtractionResult Unsupported(UvTrimMaskExtractionFailureReason reason)
        => new(false, null, reason);
}
