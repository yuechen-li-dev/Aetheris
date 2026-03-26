using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal static class TrimmedSurfaceTessellator
{
    private const double PointOnSegmentTolerance = 1e-9d;
    private const double SignedAreaTolerance = 1e-10d;
    private const int BoundaryRefinementDepth = 1;
    private const int MaxPointClassificationCacheEntries = 65_536;

    public static KernelResult<DisplayFaceMeshPatch> Tessellate(
        FaceId faceId,
        IReadOnlyList<IReadOnlyList<(double U, double V)>> uvLoops,
        Func<double, double, Point3D> evaluate,
        Func<double, double, Vector3D> evaluateNormal,
        DisplayTessellationOptions options,
        double uDomainStart,
        double uDomainEnd,
        double vDomainStart,
        double vDomainEnd,
        Func<string, string, KernelDiagnostic> createWarning)
    {
        if (uvLoops.Count == 0)
        {
            return KernelResult<DisplayFaceMeshPatch>.Success(CreateEmptyPatch(faceId));
        }

        var normalizedLoops = new List<List<(double U, double V)>>(uvLoops.Count);
        foreach (var loop in uvLoops)
        {
            var normalized = NormalizeLoop(loop);
            if (normalized.Count < 3)
            {
                return KernelResult<DisplayFaceMeshPatch>.Success(
                    CreateEmptyPatch(faceId),
                    [createWarning(
                        $"Face {faceId.Value} parametric trim loop evaluation produced fewer than three unique UV points; skipping face patch.",
                        "Viewer.Tessellation.TrimEvaluationFailed")]);
            }

            normalizedLoops.Add(normalized);
        }

        var outerLoopIndex = SelectOuterLoop(normalizedLoops);
        var trimContext = TrimEvaluationContext.Create(normalizedLoops, outerLoopIndex);

        var (uStartRaw, uEndRaw, vStartRaw, vEndRaw) = trimContext.OuterLoop.Bounds;
        var uStart = System.Math.Max(uDomainStart, uStartRaw);
        var uEnd = System.Math.Min(uDomainEnd, uEndRaw);
        var vStart = System.Math.Max(vDomainStart, vStartRaw);
        var vEnd = System.Math.Min(vDomainEnd, vEndRaw);

        if ((uEnd - uStart) <= SignedAreaTolerance || (vEnd - vStart) <= SignedAreaTolerance)
        {
            return KernelResult<DisplayFaceMeshPatch>.Success(
                CreateEmptyPatch(faceId),
                [createWarning(
                    $"Face {faceId.Value} parametric trim evaluation derived a degenerate UV domain; skipping face patch.",
                    "Viewer.Tessellation.TrimEvaluationFailed")]);
        }

        var uSegments = ResolveSegmentCount(uEnd - uStart, options);
        var vSegments = ResolveSegmentCount(vEnd - vStart, options);

        var positions = new List<Point3D>((uSegments + 1) * (vSegments + 1));
        var normals = new List<Vector3D>((uSegments + 1) * (vSegments + 1));
        var uvGrid = new List<(double U, double V)>((uSegments + 1) * (vSegments + 1));
        var indices = new List<int>(uSegments * vSegments * 6);

        for (var iv = 0; iv <= vSegments; iv++)
        {
            var tv = (double)iv / vSegments;
            var v = vStart + ((vEnd - vStart) * tv);
            for (var iu = 0; iu <= uSegments; iu++)
            {
                var tu = (double)iu / uSegments;
                var u = uStart + ((uEnd - uStart) * tu);
                uvGrid.Add((u, v));
                positions.Add(evaluate(u, v));
                normals.Add(evaluateNormal(u, v));
            }
        }

        var rowWidth = uSegments + 1;
        for (var iv = 0; iv < vSegments; iv++)
        {
            for (var iu = 0; iu < uSegments; iu++)
            {
                var i0 = (iv * rowWidth) + iu;
                var i1 = i0 + 1;
                var i2 = i0 + rowWidth;
                var i3 = i2 + 1;

                TryAppendTriangle(i0, i2, i1);
                TryAppendTriangle(i1, i2, i3);
            }
        }

        return KernelResult<DisplayFaceMeshPatch>.Success(new DisplayFaceMeshPatch(faceId, positions, normals, indices));

        void TryAppendTriangle(int ia, int ib, int ic)
        {
            AppendTriangleWithBoundaryRefinement(ia, ib, ic, remainingDepth: BoundaryRefinementDepth);
        }

        void AppendTriangleWithBoundaryRefinement(int ia, int ib, int ic, int remainingDepth)
        {
            var classification = ClassifyTriangle(uvGrid[ia], uvGrid[ib], uvGrid[ic], trimContext);
            switch (classification)
            {
                case TriangleTrimClassification.ConfidentlyInside:
                    indices.Add(ia);
                    indices.Add(ib);
                    indices.Add(ic);
                    return;
                case TriangleTrimClassification.ConfidentlyOutside:
                    return;
                default:
                    if (remainingDepth <= 0)
                    {
                        return;
                    }

                    var iab = AppendVertexAtUv(Interpolate(uvGrid[ia], uvGrid[ib]));
                    var ibc = AppendVertexAtUv(Interpolate(uvGrid[ib], uvGrid[ic]));
                    var ica = AppendVertexAtUv(Interpolate(uvGrid[ic], uvGrid[ia]));

                    AppendTriangleWithBoundaryRefinement(ia, iab, ica, remainingDepth - 1);
                    AppendTriangleWithBoundaryRefinement(iab, ib, ibc, remainingDepth - 1);
                    AppendTriangleWithBoundaryRefinement(ica, ibc, ic, remainingDepth - 1);
                    AppendTriangleWithBoundaryRefinement(iab, ibc, ica, remainingDepth - 1);
                    return;
            }
        }

        int AppendVertexAtUv((double U, double V) uv)
        {
            var index = uvGrid.Count;
            uvGrid.Add(uv);
            positions.Add(evaluate(uv.U, uv.V));
            normals.Add(evaluateNormal(uv.U, uv.V));
            return index;
        }
    }

    private static TriangleTrimClassification ClassifyTriangle(
        (double U, double V) a,
        (double U, double V) b,
        (double U, double V) c,
        TrimEvaluationContext trimContext)
    {
        var samples = new[]
        {
            a,
            b,
            c,
            Interpolate(a, b),
            Interpolate(b, c),
            Interpolate(c, a),
            ((a.U + b.U + c.U) / 3d, (a.V + b.V + c.V) / 3d),
        };

        var insideCount = 0;
        foreach (var sample in samples)
        {
            if (trimContext.IsInsideTrimRegion(sample))
            {
                insideCount++;
            }
        }

        if (insideCount == samples.Length)
        {
            return TriangleTrimClassification.ConfidentlyInside;
        }

        if (insideCount == 0)
        {
            return TriangleTrimClassification.ConfidentlyOutside;
        }

        return TriangleTrimClassification.BoundaryStraddling;
    }

    private static int SelectOuterLoop(IReadOnlyList<IReadOnlyList<(double U, double V)>> loops)
    {
        var bestIndex = 0;
        var bestArea = double.NegativeInfinity;
        for (var i = 0; i < loops.Count; i++)
        {
            var area = System.Math.Abs(ComputeSignedArea(loops[i]));
            if (area > bestArea + SignedAreaTolerance
                || (System.Math.Abs(area - bestArea) <= SignedAreaTolerance && i < bestIndex))
            {
                bestArea = area;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static (double UMin, double UMax, double VMin, double VMax) ComputeBounds(IReadOnlyList<(double U, double V)> loop)
        => (
            loop.Min(point => point.U),
            loop.Max(point => point.U),
            loop.Min(point => point.V),
            loop.Max(point => point.V));

    internal static TrimEvaluationMetrics EvaluatePointContainmentMetricsForTest(
        IReadOnlyList<IReadOnlyList<(double U, double V)>> normalizedLoops,
        int outerLoopIndex,
        IReadOnlyList<(double U, double V)> points)
    {
        var metrics = new TrimEvaluationMetrics();
        var context = TrimEvaluationContext.Create(normalizedLoops, outerLoopIndex, metrics);
        foreach (var point in points)
        {
            context.IsInsideTrimRegion(point);
        }

        return metrics;
    }

    private static List<(double U, double V)> NormalizeLoop(IReadOnlyList<(double U, double V)> loop)
    {
        var normalized = new List<(double U, double V)>(loop.Count);
        foreach (var point in loop)
        {
            if (normalized.Count == 0 || !AlmostEqual(normalized[^1], point))
            {
                normalized.Add(point);
            }
        }

        if (normalized.Count > 1 && AlmostEqual(normalized[0], normalized[^1]))
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return normalized;
    }

    private static int ResolveSegmentCount(double span, DisplayTessellationOptions options)
    {
        var ratio = span / System.Math.Max(1e-6d, options.ChordTolerance);
        var segmentCount = (int)double.Ceiling(ratio);
        segmentCount = System.Math.Max(options.MinimumSegments, segmentCount);
        return System.Math.Clamp(segmentCount, options.MinimumSegments, options.MaximumSegments);
    }

    private static bool ContainsPoint(IReadOnlyList<(double U, double V)> polygon, (double U, double V) point)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];

            if (IsPointOnSegment(point, a, b))
            {
                return true;
            }

            var crosses = ((a.V > point.V) != (b.V > point.V))
                && (point.U < (((b.U - a.U) * (point.V - a.V)) / (b.V - a.V)) + a.U);
            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsPointOnSegment((double U, double V) point, (double U, double V) a, (double U, double V) b)
    {
        var cross = ((point.U - a.U) * (b.V - a.V)) - ((point.V - a.V) * (b.U - a.U));
        if (System.Math.Abs(cross) > PointOnSegmentTolerance)
        {
            return false;
        }

        var dot = ((point.U - a.U) * (b.U - a.U)) + ((point.V - a.V) * (b.V - a.V));
        if (dot < -PointOnSegmentTolerance)
        {
            return false;
        }

        var lengthSquared = ((b.U - a.U) * (b.U - a.U)) + ((b.V - a.V) * (b.V - a.V));
        return dot <= lengthSquared + PointOnSegmentTolerance;
    }

    private static double ComputeSignedArea(IReadOnlyList<(double U, double V)> polygon)
    {
        var area = 0d;
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            area += (current.U * next.V) - (next.U * current.V);
        }

        return area * 0.5d;
    }

    private static bool AlmostEqual((double U, double V) left, (double U, double V) right)
        => System.Math.Abs(left.U - right.U) <= PointOnSegmentTolerance
            && System.Math.Abs(left.V - right.V) <= PointOnSegmentTolerance;

    private static (double U, double V) Interpolate((double U, double V) left, (double U, double V) right)
        => ((left.U + right.U) * 0.5d, (left.V + right.V) * 0.5d);

    private static DisplayFaceMeshPatch CreateEmptyPatch(FaceId faceId)
        => new(faceId, Array.Empty<Point3D>(), Array.Empty<Vector3D>(), Array.Empty<int>());

    internal sealed class TrimEvaluationMetrics
    {
        public int FullContainsPointEvaluations { get; private set; }

        public int CacheHits { get; private set; }

        internal void IncrementContainsPointEvaluations() => FullContainsPointEvaluations++;

        internal void IncrementCacheHits() => CacheHits++;
    }

    private sealed class TrimEvaluationContext
    {
        private readonly Dictionary<UvPointCacheKey, bool> pointInsideCache = new();
        private readonly TrimEvaluationMetrics? metrics;

        private TrimEvaluationContext(TrimLoopData outerLoop, IReadOnlyList<TrimLoopData> innerLoops, TrimEvaluationMetrics? metrics)
        {
            OuterLoop = outerLoop;
            InnerLoops = innerLoops;
            this.metrics = metrics;
        }

        public TrimLoopData OuterLoop { get; }

        public IReadOnlyList<TrimLoopData> InnerLoops { get; }

        public static TrimEvaluationContext Create(
            IReadOnlyList<IReadOnlyList<(double U, double V)>> loops,
            int outerLoopIndex,
            TrimEvaluationMetrics? metrics = null)
        {
            var loopData = new List<TrimLoopData>(loops.Count);
            for (var i = 0; i < loops.Count; i++)
            {
                var polygon = loops[i];
                var bounds = ComputeBounds(polygon);
                loopData.Add(new TrimLoopData(i == outerLoopIndex, polygon, bounds));
            }

            var outer = loopData[outerLoopIndex];
            var inner = loopData.Where(static loop => !loop.IsOuterLoop).ToArray();
            return new TrimEvaluationContext(outer, inner, metrics);
        }

        public bool IsInsideTrimRegion((double U, double V) point)
        {
            var key = new UvPointCacheKey(point.U, point.V);
            if (pointInsideCache.TryGetValue(key, out var isInsideCached))
            {
                metrics?.IncrementCacheHits();
                return isInsideCached;
            }

            var isInside = IsInsideTrimRegionUncached(point);
            if (pointInsideCache.Count < MaxPointClassificationCacheEntries)
            {
                pointInsideCache.Add(key, isInside);
            }

            return isInside;
        }

        private bool IsInsideTrimRegionUncached((double U, double V) point)
        {
            if (!ContainsPoint(OuterLoop, point, metrics))
            {
                return false;
            }

            foreach (var hole in InnerLoops)
            {
                if (ContainsPoint(hole, point, metrics))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private readonly record struct TrimLoopData(
        bool IsOuterLoop,
        IReadOnlyList<(double U, double V)> Polygon,
        (double UMin, double UMax, double VMin, double VMax) Bounds);

    private readonly record struct UvPointCacheKey(ulong UBits, ulong VBits)
    {
        public UvPointCacheKey(double u, double v)
            : this(BitConverter.DoubleToUInt64Bits(u), BitConverter.DoubleToUInt64Bits(v))
        {
        }
    }

    private static bool ContainsPoint(
        TrimLoopData loop,
        (double U, double V) point,
        TrimEvaluationMetrics? metrics)
    {
        if (!IsWithinBounds(loop.Bounds, point))
        {
            return false;
        }

        metrics?.IncrementContainsPointEvaluations();
        return ContainsPoint(loop.Polygon, point);
    }

    private static bool IsWithinBounds((double UMin, double UMax, double VMin, double VMax) bounds, (double U, double V) point)
        => point.U >= (bounds.UMin - PointOnSegmentTolerance)
            && point.U <= (bounds.UMax + PointOnSegmentTolerance)
            && point.V >= (bounds.VMin - PointOnSegmentTolerance)
            && point.V <= (bounds.VMax + PointOnSegmentTolerance);

    private enum TriangleTrimClassification
    {
        ConfidentlyOutside = 0,
        BoundaryStraddling = 1,
        ConfidentlyInside = 2,
    }
}
