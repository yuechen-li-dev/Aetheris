using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Blends;

/// <summary>
/// "Fillet everything": one constant radius on every edge of a body. Rounding a convex body's edges with a ball is
/// a morphological opening (erode by the radius, then sweep the ball over what is left). Convex bodies bounded by
/// planes are built exactly that way, from the erosion, by <see cref="ConvexStockPrimitives"/>' ball sweep, which
/// also opens corners of any valence. Other bodies of plane, cylinder and cone faces with convex edges are built by
/// the local rolling ball on their own faces (<see cref="LocalRounding"/>): cylinders along lines, tori around
/// circles, spheres at three-edge corners. Everything else is refused with what it found and where, and a radius
/// that does not fit is refused with the largest that does.
/// </summary>
public static class UniformRounding
{
    public const string Source = "UniformRounding";

    internal const string TooLargeCode = "uniform-rounding-radius-too-large";

    /// <summary>The local route, with a radius that does not fit refused alongside the largest that does (by
    /// bisection: every check that refuses a radius also refuses every larger one).</summary>
    private static KernelResult<BrepBody> Local(BrepBody body, double radius)
    {
        var outcome = LocalRounding.RoundAll(body, radius);
        if (outcome.Body is not null) return KernelResult<BrepBody>.Success(outcome.Body);
        var refusal = outcome.Refusal!;
        if (refusal.Code != TooLargeCode) return Failure(refusal.Code, refusal.Message);
        var (lo, hi) = (0d, radius);
        for (var i = 0; i < 40; i++) { var mid = 0.5 * (lo + hi); if (LocalRounding.RoundAll(body, mid).Body is not null) lo = mid; else hi = mid; }
        return Failure(refusal.Code, $"{refusal.Message}; the largest that fits is under {hi:G6} mm");
    }

    public static KernelResult<BrepBody> RoundAllEdges(BrepBody body, double radius)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!double.IsFinite(radius) || radius <= 0) return Failure("uniform-rounding-radius-invalid", "radius must be finite and positive");

        // Edges first, so the message names what the body is made of, not only that a face is curved: spline and
        // elliptical edges are the ones this construction has no plan for yet.
        var curved = body.Topology.Edges.OrderBy(e => e.Id.Value)
            .Select(e => (Edge: e.Id, Kind: body.Bindings.TryGetEdgeBinding(e.Id, out var b) && body.Geometry.TryGetCurve(b.CurveGeometryId, out var c) && c is not null ? c.Kind : (CurveGeometryKind?)null))
            .Where(x => x.Kind is not null && x.Kind != CurveGeometryKind.Line3).ToArray();
        string Some(CurveGeometryKind kind, string adjective)
        {
            var ids = curved.Where(x => x.Kind == kind).Select(x => x.Edge.Value).ToArray();
            return $"{ids.Length} {adjective} edge{(ids.Length == 1 ? "" : "s")} ({string.Join(", ", ids.Take(5))}{(ids.Length > 5 ? ", ..." : "")})";
        }
        var splines = curved.Any(x => x.Kind == CurveGeometryKind.BSpline3);
        var ellipses = curved.Any(x => x.Kind == CurveGeometryKind.Ellipse3);
        if (splines || ellipses)
            return Failure(splines ? "uniform-rounding-spline-edge-unsupported" : "uniform-rounding-elliptical-edge-unsupported",
                "rounding every edge does not support spline or elliptical edges yet: this body has "
                + string.Join(" and ", new[] { splines ? Some(CurveGeometryKind.BSpline3, "spline") : null, ellipses ? Some(CurveGeometryKind.Ellipse3, "elliptical") : null }.Where(x => x is not null))
                + ". Round a body bounded by planes, or finish those edges with a local EdgeFinish");
        if (curved.Any(x => x.Kind != CurveGeometryKind.Circle3))
        {
            var other = curved.First(x => x.Kind != CurveGeometryKind.Circle3);
            return Failure("uniform-rounding-curved-edge-unsupported", $"rounding every edge does not support {other.Kind} edges (edge {other.Edge.Value}); lines and circles only for now");
        }

        // Convex and bounded by planes: the opening, erode then sweep, which also opens corners where four or more
        // faces meet unevenly. Anything else: the local rolling ball on the body's own faces.
        var planar = curved.Length == 0 && body.Topology.Faces.All(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) && g?.Plane is not null);
        var planes = new List<(FaceId Face, Vector3D Normal, double Offset)>();
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : (Point3D?)null).Where(p => p is not null).Select(p => p!.Value).ToArray();
        if (points.Length == 0) return Local(body, radius);
        var extent = points.Max(p => (p - Point3D.Origin).Length);
        var tolerance = double.Max(1e-9, (extent + 1) * 1e-10) * 1e3;
        if (planar)
        {
            var centroid = Point3D.Origin + points.Aggregate(Vector3D.Zero, (s, p) => s + (p - Point3D.Origin)) * (1d / points.Length);
            foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
            {
                body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
                var plane = surface!.Plane!.Value; var n = plane.Normal.ToVector();
                if (n.Dot(plane.Origin - centroid) < 0) n = n * -1;
                planes.Add((face.Id, n, n.Dot(plane.Origin - Point3D.Origin)));
            }
            planar = planes.All(q => points.All(p => q.Normal.Dot(p - Point3D.Origin) <= q.Offset + tolerance));
        }
        if (!planar) return Local(body, radius);

        var eroded = Erode(planes, radius, tolerance);
        if (eroded is null || !FullDimensional(eroded, tolerance))
            return Failure(TooLargeCode, $"radius {radius:G6} mm leaves nothing of the body to round (the largest that fits is under {Largest(planes, tolerance, keepEveryFace: false, radius):G6} mm)");
        var kept = KeptFaces(planes, eroded, radius, tolerance);
        var distinct = Distinct(planes).Length;
        if (kept < distinct)
            return Failure("uniform-rounding-radius-consumes-faces", $"radius {radius:G6} mm consumes {distinct - kept} face(s) of the body whole; the largest radius that keeps every face is {Largest(planes, tolerance, keepEveryFace: true, radius):G6} mm");
        var swept = ConvexStockPrimitives.Dilate(eroded, radius);
        return swept.IsSuccess ? swept : KernelResult<BrepBody>.Failure(swept.Diagnostics);
    }

    /// <summary>The vertices of the faces' planes moved in by the radius: their half-spaces' intersection.</summary>
    private static Point3D[]? Erode(IReadOnlyList<(FaceId Face, Vector3D Normal, double Offset)> planes, double radius, double tolerance)
    {
        var found = new List<Point3D>();
        for (var i = 0; i < planes.Count; i++)
            for (var j = i + 1; j < planes.Count; j++)
                for (var k = j + 1; k < planes.Count; k++)
                {
                    var (a, b, c) = (planes[i], planes[j], planes[k]);
                    var det = a.Normal.Dot(b.Normal.Cross(c.Normal));
                    if (double.Abs(det) < 1e-12) continue;
                    var p = Point3D.Origin + (b.Normal.Cross(c.Normal) * (a.Offset - radius) + c.Normal.Cross(a.Normal) * (b.Offset - radius) + a.Normal.Cross(b.Normal) * (c.Offset - radius)) * (1 / det);
                    if (planes.All(q => q.Normal.Dot(p - Point3D.Origin) <= q.Offset - radius + tolerance) && found.All(f => (f - p).Length > tolerance))
                        found.Add(p);
                }
        return found.Count == 0 ? null : found.ToArray();
    }

    private static bool FullDimensional(Point3D[] points, double tolerance)
    {
        if (points.Length < 4) return false;
        var o = points[0];
        var far = points.MaxBy(p => (p - o).Length);
        var line = far - o;
        if (line.Length <= tolerance) return false;
        var off = points.MaxBy(p => line.Cross(p - o).Length);
        var normal = line.Cross(off - o);
        if (normal.Length <= tolerance * line.Length) return false;
        return points.Any(p => double.Abs(normal.Dot(p - o)) / normal.Length > tolerance);
    }

    /// <summary>How many distinct planes still bound the eroded body with an area (three non-collinear of its
    /// vertices on the plane moved in by the radius).</summary>
    private static int KeptFaces(IReadOnlyList<(FaceId Face, Vector3D Normal, double Offset)> planes, Point3D[] eroded, double radius, double tolerance) =>
        Distinct(planes).Count(p => FaceHasArea(eroded.Where(x => double.Abs(p.Normal.Dot(x - Point3D.Origin) - (p.Offset - radius)) <= tolerance).ToArray(), tolerance));

    private static (FaceId Face, Vector3D Normal, double Offset)[] Distinct(IReadOnlyList<(FaceId Face, Vector3D Normal, double Offset)> planes) =>
        planes.GroupBy(p => (double.Round(p.Normal.X, 9), double.Round(p.Normal.Y, 9), double.Round(p.Normal.Z, 9), double.Round(p.Offset, 6))).Select(g => g.First()).ToArray();

    private static bool FaceHasArea(Point3D[] onFace, double tolerance)
    {
        if (onFace.Length < 3) return false;
        var o = onFace[0];
        var far = onFace.MaxBy(p => (p - o).Length);
        return onFace.Any(p => (far - o).Cross(p - o).Length > tolerance * double.Max(1, (far - o).Length));
    }

    /// <summary>The largest radius that leaves the body something to round (or keeps every face), by bisection:
    /// erosion only shrinks as the radius grows, so both conditions are monotone.</summary>
    private static double Largest(IReadOnlyList<(FaceId Face, Vector3D Normal, double Offset)> planes, double tolerance, bool keepEveryFace, double above)
    {
        var faces = Distinct(planes).Length;
        bool Fits(double r)
        {
            var e = Erode(planes, r, tolerance);
            if (e is null || !FullDimensional(e, tolerance)) return false;
            return !keepEveryFace || KeptFaces(planes, e, r, tolerance) >= faces;
        }
        var (lo, hi) = (0d, above);
        for (var i = 0; i < 60; i++) { var mid = 0.5 * (lo + hi); if (Fits(mid)) lo = mid; else hi = mid; }
        return lo;
    }

    private static KernelResult<BrepBody> Failure(string code, string message) => KernelResult<BrepBody>.Failure([
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"{code}: {message}", Source)]);
}
