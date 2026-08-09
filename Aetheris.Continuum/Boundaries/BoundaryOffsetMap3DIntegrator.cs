using System.Diagnostics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Boundaries;

public readonly record struct BoundaryFootprintVertex(double U, double V);

public sealed record ClippedBoundaryFootprint(
    IReadOnlyList<BoundaryFootprintVertex> Vertices,
    double Area,
    string Orientation,
    string ClippingSource,
    bool IsDegenerate);

public sealed record BoundaryIntegrationDiagnostics(
    string Method,
    int FootprintVertices,
    int MapCellsVisited,
    int ClippedPolygons,
    int Triangles,
    int ThicknessEvaluations,
    int AdaptiveSubdivisions,
    int MaximumAdaptiveDepth,
    int SurfaceTriangles,
    long EstimatedWorkBufferBytes);

public sealed record StructuredBoundaryMapCellEstimate(
    BoundaryMapCellEstimate Estimate,
    ClippedBoundaryFootprint Footprint,
    BoundaryIntegrationDiagnostics Diagnostics);

public sealed record DenseBoundaryIntegrationAudit(
    BoundaryMapCellEstimate Estimate,
    int FootprintVertices,
    int FootprintTriangles,
    long ThicknessEvaluations,
    long AreaMapEvaluations,
    double FootprintConstructionMilliseconds,
    double VolumeSamplingMilliseconds,
    double AreaSamplingMilliseconds,
    long AllocatedBytes);

public readonly record struct StructuredIntegrationPolicy(
    double RelativeVolumeTolerance = 2e-5d,
    double AbsoluteVolumeTolerance = 1e-12d,
    int MaximumAdaptiveDepth = 8);

/// <summary>
/// Deterministic fixed-cell integration of a derived local graph. The production path clips the
/// compact projected box footprint against map intervals, integrates small polygon triangles, and
/// adaptively refines only where thickness is non-smooth. The lattice itself is never subdivided.
/// </summary>
public static class BoundaryOffsetMap3DIntegrator
{
    private readonly record struct Point2(double U, double V);
    private readonly record struct SurfaceVertex(Point3D Position, Vector3D Normal);
    private sealed class Counters
    {
        public int MapCells, Polygons, Triangles, Evaluations, Subdivisions, MaximumDepth, SurfaceTriangles;
    }

    public static BoundaryMapCellEstimate Integrate(IBoundaryOffsetMap map, BoundingBox3D bounds,
        int volumeSamplesPerAxis = 10, int areaSubdivisionsPerMapInterval = 6) =>
        IntegrateStructured(map, bounds).Estimate;

    public static StructuredBoundaryMapCellEstimate IntegrateStructured(
        IBoundaryOffsetMap map,
        BoundingBox3D bounds,
        StructuredIntegrationPolicy policy = default)
    {
        if (policy.RelativeVolumeTolerance <= 0d) policy = new StructuredIntegrationPolicy();
        var footprint = CreateFootprint(map, bounds);
        if (footprint.IsDegenerate)
        {
            return new(new(0d, 0d, Vector3D.Zero, Vector3D.Zero), footprint,
                new("structured-polygon-adaptive", footprint.Vertices.Count, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        var counters = new Counters();
        var polygon = footprint.Vertices.Select(p => new Point2(p.U, p.V)).ToArray();
        var uNodes = map.Samples.Select(s => s.U).Distinct().Order().ToArray();
        var vNodes = map.Samples.Select(s => s.V).Distinct().Order().ToArray();
        var volume = 0d;
        var cellVolume = Volume(bounds);
        var perAreaTolerance = double.Max(policy.AbsoluteVolumeTolerance, policy.RelativeVolumeTolerance * cellVolume) / footprint.Area;

        for (var j = 0; j < vNodes.Length - 1; j++)
        for (var i = 0; i < uNodes.Length - 1; i++)
        {
            counters.MapCells++;
            var clipped = ClipRectangle(polygon, uNodes[i], uNodes[i + 1], vNodes[j], vNodes[j + 1]);
            if (clipped.Count < 3) continue;
            counters.Polygons++;
            for (var k = 1; k < clipped.Count - 1; k++)
            {
                counters.Triangles++;
                volume += IntegrateTriangle(map, bounds, clipped[0], clipped[k], clipped[k + 1],
                    perAreaTolerance, policy.MaximumAdaptiveDepth, 0, counters);
            }
        }

        var (area, moment, normal) = IntegrateSurface(map, bounds, uNodes, vNodes, counters);
        var estimate = new BoundaryMapCellEstimate(double.Clamp(volume / cellVolume, 0d, 1d), area, moment, normal);
        var diagnostics = new BoundaryIntegrationDiagnostics("structured-polygon-adaptive", footprint.Vertices.Count,
            counters.MapCells, counters.Polygons, counters.Triangles, counters.Evaluations, counters.Subdivisions,
            counters.MaximumDepth, counters.SurfaceTriangles,
            (footprint.Vertices.Count * 16L) + ((uNodes.Length + vNodes.Length) * 8L) + 512L);
        return new(estimate, footprint, diagnostics);
    }

    /// <summary>Dense M2 control/oracle retained for regression and independent cost comparison.</summary>
    public static BoundaryMapCellEstimate IntegrateDenseOracle(IBoundaryOffsetMap map, BoundingBox3D bounds,
        int volumeSamplesPerAxis = 64, int areaSubdivisionsPerMapInterval = 6)
    {
        if (volumeSamplesPerAxis < 2) throw new ArgumentOutOfRangeException(nameof(volumeSamplesPerAxis));
        var occupiedVolume = IntegrateDenseProjectedFootprint(map, bounds, volumeSamplesPerAxis);
        var area = 0d;
        var moment = Vector3D.Zero;
        var normalIntegral = Vector3D.Zero;
        var nu = int.Max(12, areaSubdivisionsPerMapInterval * 4);
        var nv = int.Max(12, areaSubdivisionsPerMapInterval * 4);
        for (var j = 0; j < nv; j++)
        for (var i = 0; i < nu; i++)
        {
            var u0 = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, i / (double)nu);
            var u1 = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, (i + 1d) / nu);
            var v0 = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, j / (double)nv);
            var v1 = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, (j + 1d) / nv);
            var a = map.Evaluate(u0, v0); var b = map.Evaluate(u1, v0);
            var c = map.Evaluate(u0, v1); var d = map.Evaluate(u1, v1);
            AddDenseTriangle(a, b, d, bounds, ref area, ref moment, ref normalIntegral);
            AddDenseTriangle(a, d, c, bounds, ref area, ref moment, ref normalIntegral);
        }
        return new(double.Clamp(occupiedVolume / Volume(bounds), 0d, 1d), area, moment, normalIntegral);
    }

    public static DenseBoundaryIntegrationAudit AuditDense(IBoundaryOffsetMap map, BoundingBox3D bounds,
        int volumeSamplesPerAxis = 64, int areaSubdivisionsPerMapInterval = 6)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stage = Stopwatch.GetTimestamp();
        var footprint = CreateFootprint(map, bounds);
        var hull = footprint.Vertices.Select(p => new Point2(p.U, p.V)).ToArray();
        var footprintMilliseconds = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        stage = Stopwatch.GetTimestamp();
        var occupiedVolume = IntegrateDenseHull(map, bounds, volumeSamplesPerAxis, hull);
        var volumeMilliseconds = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        stage = Stopwatch.GetTimestamp();
        var area = 0d; var moment = Vector3D.Zero; var normal = Vector3D.Zero;
        var nu = int.Max(12, areaSubdivisionsPerMapInterval * 4); var nv = int.Max(12, areaSubdivisionsPerMapInterval * 4);
        for (var j = 0; j < nv; j++)
        for (var i = 0; i < nu; i++)
        {
            var u0 = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, i / (double)nu); var u1 = Lerp(map.Domain.MinimumU, map.Domain.MaximumU, (i + 1d) / nu);
            var v0 = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, j / (double)nv); var v1 = Lerp(map.Domain.MinimumV, map.Domain.MaximumV, (j + 1d) / nv);
            var a = map.Evaluate(u0, v0); var b = map.Evaluate(u1, v0); var c = map.Evaluate(u0, v1); var d = map.Evaluate(u1, v1);
            AddDenseTriangle(a, b, d, bounds, ref area, ref moment, ref normal); AddDenseTriangle(a, d, c, bounds, ref area, ref moment, ref normal);
        }
        var areaMilliseconds = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        var estimate = new BoundaryMapCellEstimate(double.Clamp(occupiedVolume / Volume(bounds), 0d, 1d), area, moment, normal);
        return new(estimate, hull.Length, int.Max(0, hull.Length - 2), (long)int.Max(0, hull.Length - 2) * volumeSamplesPerAxis * volumeSamplesPerAxis,
            4L * nu * nv, footprintMilliseconds, volumeMilliseconds, areaMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    }

    public static ClippedBoundaryFootprint CreateFootprint(IBoundaryOffsetMap map, BoundingBox3D bounds)
    {
        var projected = BoxCorners(bounds).Select(point =>
        {
            var delta = point - map.LocalFrame.Origin;
            return new Point2(delta.Dot(map.LocalFrame.TangentU), delta.Dot(map.LocalFrame.TangentV));
        });
        var hull = ConvexHull(projected);
        var signed = SignedArea(hull);
        return new(hull.Select(p => new BoundaryFootprintVertex(p.U, p.V)).ToArray(), double.Abs(signed),
            signed >= 0d ? "counter-clockwise" : "clockwise", "orthographic projection of eight Cartesian cell corners",
            hull.Count < 3 || double.Abs(signed) <= 1e-18d);
    }

    private static double IntegrateTriangle(IBoundaryOffsetMap map, BoundingBox3D bounds,
        Point2 a, Point2 b, Point2 c, double perAreaTolerance, int maximumDepth, int depth, Counters counters)
    {
        counters.MaximumDepth = int.Max(counters.MaximumDepth, depth);
        var area = double.Abs(Cross(a, b, c)) * 0.5d;
        if (area <= 1e-20d) return 0d;
        var centroid = new Point2((a.U + b.U + c.U) / 3d, (a.V + b.V + c.V) / 3d);
        var coarse = Thickness(map, bounds, centroid); counters.Evaluations++;
        var p0 = Barycentric(a, b, c, 1d / 6d, 1d / 6d);
        var p1 = Barycentric(a, b, c, 2d / 3d, 1d / 6d);
        var p2 = Barycentric(a, b, c, 1d / 6d, 2d / 3d);
        var f0 = Thickness(map, bounds, p0); var f1 = Thickness(map, bounds, p1); var f2 = Thickness(map, bounds, p2);
        counters.Evaluations += 3;
        var fine = (f0 + f1 + f2) / 3d;
        var error = double.Abs(fine - coarse);
        var variation = double.Max(f0, double.Max(f1, f2)) - double.Min(f0, double.Min(f1, f2));
        if (depth >= maximumDepth || (error <= perAreaTolerance && variation * area <= perAreaTolerance * area * 8d))
            return fine * area;

        counters.Subdivisions++;
        var ab = Midpoint(a, b); var bc = Midpoint(b, c); var ca = Midpoint(c, a);
        return IntegrateTriangle(map, bounds, a, ab, ca, perAreaTolerance, maximumDepth, depth + 1, counters)
            + IntegrateTriangle(map, bounds, ab, b, bc, perAreaTolerance, maximumDepth, depth + 1, counters)
            + IntegrateTriangle(map, bounds, ca, bc, c, perAreaTolerance, maximumDepth, depth + 1, counters)
            + IntegrateTriangle(map, bounds, ab, bc, ca, perAreaTolerance, maximumDepth, depth + 1, counters);
    }

    private static (double Area, Vector3D Moment, Vector3D Normal) IntegrateSurface(IBoundaryOffsetMap map,
        BoundingBox3D bounds, double[] uNodes, double[] vNodes, Counters counters)
    {
        var nodes = new SurfaceVertex[uNodes.Length, vNodes.Length];
        for (var j = 0; j < vNodes.Length; j++)
        for (var i = 0; i < uNodes.Length; i++)
        {
            var evaluation = map.Evaluate(uNodes[i], vNodes[j]);
            nodes[i, j] = new(evaluation.Position, evaluation.Normal);
        }
        var area = 0d; var moment = Vector3D.Zero; var normal = Vector3D.Zero;
        for (var j = 0; j < vNodes.Length - 1; j++)
        for (var i = 0; i < uNodes.Length - 1; i++)
        {
            AddClippedSurfaceTriangle(map, nodes[i, j], nodes[i + 1, j], nodes[i + 1, j + 1], bounds, ref area, ref moment, ref normal, counters);
            AddClippedSurfaceTriangle(map, nodes[i, j], nodes[i + 1, j + 1], nodes[i, j + 1], bounds, ref area, ref moment, ref normal, counters);
        }
        return (area, moment, normal);
    }

    private static void AddClippedSurfaceTriangle(IBoundaryOffsetMap map, SurfaceVertex a, SurfaceVertex b, SurfaceVertex c, BoundingBox3D bounds,
        ref double area, ref Vector3D moment, ref Vector3D normal, Counters counters)
    {
        List<SurfaceVertex> polygon = [a, b, c];
        polygon = ClipSurface(polygon, p => p.Position.X - bounds.Min.X);
        polygon = ClipSurface(polygon, p => bounds.Max.X - p.Position.X);
        polygon = ClipSurface(polygon, p => p.Position.Y - bounds.Min.Y);
        polygon = ClipSurface(polygon, p => bounds.Max.Y - p.Position.Y);
        polygon = ClipSurface(polygon, p => p.Position.Z - bounds.Min.Z);
        polygon = ClipSurface(polygon, p => bounds.Max.Z - p.Position.Z);
        polygon = ClipSurface(polygon, p => map.SourceTrimSignedDistance(p.Position));
        for (var i = 1; i < polygon.Count - 1; i++)
        {
            counters.SurfaceTriangles++;
            var p0 = polygon[0]; var p1 = polygon[i]; var p2 = polygon[i + 1];
            var cross = (p1.Position - p0.Position).Cross(p2.Position - p0.Position);
            var triangleArea = cross.Length * 0.5d;
            if (triangleArea <= 1e-20d) continue;
            var centroid = new Point3D((p0.Position.X + p1.Position.X + p2.Position.X) / 3d,
                (p0.Position.Y + p1.Position.Y + p2.Position.Y) / 3d,
                (p0.Position.Z + p1.Position.Z + p2.Position.Z) / 3d);
            area += triangleArea;
            moment += new Vector3D(centroid.X, centroid.Y, centroid.Z) * triangleArea;
            var n = p0.Normal + p1.Normal + p2.Normal;
            if (n.TryNormalize(out n)) normal += n * triangleArea;
        }
    }

    private static List<SurfaceVertex> ClipSurface(IReadOnlyList<SurfaceVertex> input, Func<SurfaceVertex, double> distance)
    {
        if (input.Count == 0) return [];
        var output = new List<SurfaceVertex>(input.Count + 1);
        var previous = input[^1]; var previousDistance = distance(previous);
        foreach (var current in input)
        {
            var currentDistance = distance(current);
            var previousInside = previousDistance >= -1e-12d; var currentInside = currentDistance >= -1e-12d;
            if (previousInside != currentInside)
            {
                var t = previousDistance / (previousDistance - currentDistance);
                var position = previous.Position + ((current.Position - previous.Position) * t);
                var n = previous.Normal + ((current.Normal - previous.Normal) * t); n.TryNormalize(out n);
                output.Add(new(position, n));
            }
            if (currentInside) output.Add(current);
            previous = current; previousDistance = currentDistance;
        }
        return output;
    }

    private static List<Point2> ClipRectangle(IReadOnlyList<Point2> polygon, double u0, double u1, double v0, double v1)
    {
        var result = Clip(polygon, p => p.U - u0);
        result = Clip(result, p => u1 - p.U);
        result = Clip(result, p => p.V - v0);
        return Clip(result, p => v1 - p.V);
    }

    private static List<Point2> Clip(IReadOnlyList<Point2> input, Func<Point2, double> distance)
    {
        if (input.Count == 0) return [];
        var output = new List<Point2>(input.Count + 1);
        var previous = input[^1]; var previousDistance = distance(previous);
        foreach (var current in input)
        {
            var currentDistance = distance(current);
            var previousInside = previousDistance >= -1e-14d; var currentInside = currentDistance >= -1e-14d;
            if (previousInside != currentInside)
            {
                var t = previousDistance / (previousDistance - currentDistance);
                output.Add(new(previous.U + ((current.U - previous.U) * t), previous.V + ((current.V - previous.V) * t)));
            }
            if (currentInside) output.Add(current);
            previous = current; previousDistance = currentDistance;
        }
        return output;
    }

    private static double IntegrateDenseProjectedFootprint(IBoundaryOffsetMap map, BoundingBox3D bounds, int subdivisions)
    {
        var hull = ConvexHull(BoxCorners(bounds).Select(point =>
        {
            var delta = point - map.LocalFrame.Origin;
            return new Point2(delta.Dot(map.LocalFrame.TangentU), delta.Dot(map.LocalFrame.TangentV));
        }));
        return IntegrateDenseHull(map, bounds, subdivisions, hull);
    }

    private static double IntegrateDenseHull(IBoundaryOffsetMap map, BoundingBox3D bounds, int subdivisions, IReadOnlyList<Point2> hull)
    {
        var volume = 0d;
        for (var h = 1; h < hull.Count - 1; h++)
        {
            var a = hull[0]; var b = hull[h]; var c = hull[h + 1];
            var microArea = double.Abs(Cross(a, b, c)) * 0.5d / (subdivisions * subdivisions);
            for (var j = 0; j < subdivisions; j++)
            for (var i = 0; i < subdivisions - j; i++)
            {
                var p00 = Barycentric(a, b, c, i / (double)subdivisions, j / (double)subdivisions);
                var p10 = Barycentric(a, b, c, (i + 1d) / subdivisions, j / (double)subdivisions);
                var p01 = Barycentric(a, b, c, i / (double)subdivisions, (j + 1d) / subdivisions);
                volume += Thickness(map, bounds, new((p00.U + p10.U + p01.U) / 3d, (p00.V + p10.V + p01.V) / 3d)) * microArea;
                if (i + j < subdivisions - 1)
                {
                    var p11 = Barycentric(a, b, c, (i + 1d) / subdivisions, (j + 1d) / subdivisions);
                    volume += Thickness(map, bounds, new((p10.U + p11.U + p01.U) / 3d, (p10.V + p11.V + p01.V) / 3d)) * microArea;
                }
            }
        }
        return volume;
    }

    private static double Thickness(IBoundaryOffsetMap map, BoundingBox3D bounds, Point2 uv)
    {
        var lineOrigin = map.LocalFrame.Origin + (map.LocalFrame.TangentU * uv.U) + (map.LocalFrame.TangentV * uv.V);
        if (!ClipLineToBox(lineOrigin, map.LocalFrame.Normal, bounds, out var minimumW, out var maximumW)) return 0d;
        return double.Max(0d, maximumW - double.Max(minimumW, map.Evaluate(uv.U, uv.V).Offset));
    }

    private static IReadOnlyList<Point2> ConvexHull(IEnumerable<Point2> points)
    {
        var values = points.Distinct().OrderBy(p => p.U).ThenBy(p => p.V).ToArray();
        if (values.Length <= 2) return values;
        var lower = new List<Point2>();
        foreach (var p in values) { while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 1e-15d) lower.RemoveAt(lower.Count - 1); lower.Add(p); }
        var upper = new List<Point2>();
        for (var i = values.Length - 1; i >= 0; i--) { var p = values[i]; while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 1e-15d) upper.RemoveAt(upper.Count - 1); upper.Add(p); }
        lower.RemoveAt(lower.Count - 1); upper.RemoveAt(upper.Count - 1); lower.AddRange(upper); return lower;
    }

    private static double SignedArea(IReadOnlyList<Point2> p)
    {
        var twice = 0d; for (var i = 0; i < p.Count; i++) { var n = p[(i + 1) % p.Count]; twice += (p[i].U * n.V) - (n.U * p[i].V); }
        return twice * 0.5d;
    }
    private static Point2 Barycentric(Point2 a, Point2 b, Point2 c, double s, double t) => new(a.U + (s * (b.U - a.U)) + (t * (c.U - a.U)), a.V + (s * (b.V - a.V)) + (t * (c.V - a.V)));
    private static Point2 Midpoint(Point2 a, Point2 b) => new((a.U + b.U) * 0.5d, (a.V + b.V) * 0.5d);
    private static double Cross(Point2 a, Point2 b, Point2 c) => ((b.U - a.U) * (c.V - a.V)) - ((b.V - a.V) * (c.U - a.U));
    private static Point3D[] BoxCorners(BoundingBox3D b) => [new(b.Min.X,b.Min.Y,b.Min.Z), new(b.Max.X,b.Min.Y,b.Min.Z), new(b.Min.X,b.Max.Y,b.Min.Z), new(b.Max.X,b.Max.Y,b.Min.Z), new(b.Min.X,b.Min.Y,b.Max.Z), new(b.Max.X,b.Min.Y,b.Max.Z), new(b.Min.X,b.Max.Y,b.Max.Z), new(b.Max.X,b.Max.Y,b.Max.Z)];

    private static bool ClipLineToBox(Point3D origin, Vector3D direction, BoundingBox3D bounds, out double minimum, out double maximum)
    {
        minimum = double.NegativeInfinity; maximum = double.PositiveInfinity;
        return ClipAxis(origin.X, direction.X, bounds.Min.X, bounds.Max.X, ref minimum, ref maximum)
            && ClipAxis(origin.Y, direction.Y, bounds.Min.Y, bounds.Max.Y, ref minimum, ref maximum)
            && ClipAxis(origin.Z, direction.Z, bounds.Min.Z, bounds.Max.Z, ref minimum, ref maximum) && maximum >= minimum;
    }
    private static bool ClipAxis(double origin, double direction, double low, double high, ref double minimum, ref double maximum)
    {
        if (double.Abs(direction) <= 1e-14d) return origin >= low && origin <= high;
        var a = (low - origin) / direction; var b = (high - origin) / direction; if (a > b) (a, b) = (b, a);
        minimum = double.Max(minimum, a); maximum = double.Min(maximum, b); return maximum >= minimum;
    }

    private static void AddDenseTriangle(BoundaryMapEvaluation a, BoundaryMapEvaluation b, BoundaryMapEvaluation c, BoundingBox3D bounds, ref double area, ref Vector3D moment, ref Vector3D normalIntegral)
    {
        var centroid = new Point3D((a.Position.X + b.Position.X + c.Position.X) / 3d, (a.Position.Y + b.Position.Y + c.Position.Y) / 3d, (a.Position.Z + b.Position.Z + c.Position.Z) / 3d);
        if (centroid.X < bounds.Min.X - 1e-12d || centroid.X > bounds.Max.X + 1e-12d || centroid.Y < bounds.Min.Y - 1e-12d || centroid.Y > bounds.Max.Y + 1e-12d || centroid.Z < bounds.Min.Z - 1e-12d || centroid.Z > bounds.Max.Z + 1e-12d) return;
        var triangleArea = (b.Position - a.Position).Cross(c.Position - a.Position).Length * 0.5d; if (triangleArea <= 0d) return;
        area += triangleArea; moment += new Vector3D(centroid.X, centroid.Y, centroid.Z) * triangleArea;
        var normal = a.Normal + b.Normal + c.Normal; if (normal.TryNormalize(out normal)) normalIntegral += normal * triangleArea;
    }
    private static double Volume(BoundingBox3D b) => (b.Max.X - b.Min.X) * (b.Max.Y - b.Min.Y) * (b.Max.Z - b.Min.Z);
    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
