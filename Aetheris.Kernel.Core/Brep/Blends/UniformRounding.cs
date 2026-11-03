using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Blends;

/// <summary>
/// "Fillet everything": one constant radius on every edge of a body. Rounding a convex body's edges with a ball is
/// a morphological opening (erode by the radius, then sweep the ball over what is left), so it has one answer,
/// with no corner cases to choose: corners come out as sphere patches by construction, whatever their valence.
/// Built exactly from the erosion (the faces' planes moved in by the radius) by
/// <see cref="ConvexStockPrimitives"/>' ball sweep. Admits convex bodies bounded by planes; everything else is
/// refused with what it found and where, and a radius that does not fit is refused with the largest that does.
/// </summary>
public static class UniformRounding
{
    public const string Source = "UniformRounding";

    public static KernelResult<BrepBody> RoundAllEdges(BrepBody body, double radius)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!double.IsFinite(radius) || radius <= 0) return Failure("uniform-rounding-radius-invalid", "radius must be finite and positive");

        // Edges first, so the message names what the body is made of, not only that a face is curved. Spline and
        // elliptical edges are named before circles: they are the ones this construction has no plan for yet.
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
        if (curved.Any(x => x.Kind == CurveGeometryKind.Circle3))
            return Failure("uniform-rounding-circular-edge-unsupported", $"rounding every edge does not support circular edges yet: this body has {Some(CurveGeometryKind.Circle3, "circular")}. Bodies bounded by planes only for now");
        if (curved.Length > 0)
            return Failure("uniform-rounding-curved-edge-unsupported", $"rounding every edge does not support {curved[0].Kind} edges (edge {curved[0].Edge.Value}); bodies bounded by planes only for now");
        var planes = new List<(FaceId Face, Vector3D Normal, double Offset)>();
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException($"vertex {v.Id.Value} has no point")).ToArray();
        var extent = points.Max(p => (p - Point3D.Origin).Length);
        var tolerance = double.Max(1e-9, (extent + 1) * 1e-10) * 1e3;
        var centroid = Point3D.Origin + points.Aggregate(Vector3D.Zero, (s, p) => s + (p - Point3D.Origin)) * (1d / points.Length);
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface?.Plane is not { } plane)
                return Failure("uniform-rounding-curved-face-unsupported", $"rounding every edge needs a body bounded by planes for now; face {face.Id.Value} is {surface?.Kind.ToString() ?? "unbound"}");
            var n = plane.Normal.ToVector();
            if (n.Dot(plane.Origin - centroid) < 0) n = n * -1;
            planes.Add((face.Id, n, n.Dot(plane.Origin - Point3D.Origin)));
        }
        // Convex: every vertex on the inner side of every face. A vertex outside names a concave edge's region.
        foreach (var (face, n, d) in planes)
        {
            var outside = points.Select((p, i) => (Point: p, Index: i)).Where(x => n.Dot(x.Point - Point3D.Origin) > d + tolerance).ToArray();
            if (outside.Length > 0)
                return Failure("uniform-rounding-concave-unsupported", $"rounding every edge supports convex bodies only for now: the body reaches {outside.Max(x => n.Dot(x.Point - Point3D.Origin) - d):G4} mm beyond face {face.Value}'s plane, so it has a concave edge (closing concave edges with the ball is not built yet)");
        }

        var eroded = Erode(planes, radius, tolerance);
        if (eroded is null || !FullDimensional(eroded, tolerance))
            return Failure("uniform-rounding-radius-too-large", $"radius {radius:G6} mm leaves nothing of the body to round (the largest that fits is under {Largest(planes, tolerance, keepEveryFace: false, radius):G6} mm)");
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
