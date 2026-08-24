using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

internal sealed record SectionChainValidationPatch(int TransitionIndex, string TransitionId, string SpanId, ISectionChainTransitionPatch Patch);
internal sealed record SectionChainSelfIntersectionValidationResult(bool Passed, SectionChainSelfIntersectionEvidence Evidence, string Detail);

/// <summary>
/// Deterministic validation-only proxy. It detects proper/coplanar crossings between
/// non-neighbouring transitions and between terminal caps and remote transitions.
/// The authoritative product remains the exact structured BRep.
/// </summary>
internal static class SectionChainSelfIntersectionValidator
{
    private const double Tolerance = 1e-6;
    private const int UDivisions = 24;
    private const int VDivisions = 6;

    public static SectionChainSelfIntersectionValidationResult Validate(SectionChain chain, IReadOnlyList<SectionChainValidationPatch> patches)
    {
        var proxies = patches.SelectMany(Triangulate).ToList();
        if (chain.StartTermination == SectionTermination.Cap) proxies.AddRange(Cap(chain.Sections[0], -1, "StartTermination"));
        if (chain.EndTermination == SectionTermination.Cap) proxies.AddRange(Cap(chain.Sections[^1], chain.Sections.Count - 1, "EndTermination"));
        var candidates = 0; var qualified = 0;
        for (var left = 0; left < proxies.Count; left++) for (var right = left + 1; right < proxies.Count; right++)
        {
            var a = proxies[left]; var b = proxies[right];
            if (SameOrAuthorizedNeighbor(a, b)) continue;
            if (!a.Bounds.Expanded(Tolerance).Intersects(b.Bounds.Expanded(Tolerance))) continue;
            candidates++;
            if (!TrianglesIntersect(a, b, Tolerance)) { qualified++; continue; }
            var detail = $"{a.Owner}:{a.Span} intersects {b.Owner}:{b.Span}; validation-only proxy={UDivisions}x{VDivisions}, tolerance={Tolerance:R}.";
            return Result(false, candidates, qualified, detail);
        }
        return Result(true, candidates, qualified,
            $"No non-neighbour transition or remote cap crossings were detected by the deterministic {UDivisions}x{VDivisions} surface proxy.");

        static bool SameOrAuthorizedNeighbor(Triangle a, Triangle b)
        {
            if (a.Owner == b.Owner) return true;
            if (a.IsCap || b.IsCap)
            {
                var cap = a.IsCap ? a : b; var transition = a.IsCap ? b : a;
                return cap.TransitionIndex == -1 ? transition.TransitionIndex == 0 : transition.TransitionIndex == cap.TransitionIndex - 1;
            }
            return Math.Abs(a.TransitionIndex - b.TransitionIndex) <= 1;
        }
    }

    private static SectionChainSelfIntersectionValidationResult Result(bool passed, int candidates, int qualified, string detail) =>
        new(passed, new("DeterministicBroadphasePlusTriangleProxy", passed, Tolerance,
            "Closed single-loop X3 chains; profile crossings and adjacent foldover are checked separately; tessellation is validation-only and is not a global proof.",
            candidates, qualified), detail);

    private static IEnumerable<Triangle> Triangulate(SectionChainValidationPatch patch)
    {
        for (var u = 0; u < UDivisions; u++) for (var v = 0; v < VDivisions; v++)
        {
            var u0 = u / (double)UDivisions; var u1 = (u + 1d) / UDivisions;
            var v0 = v / (double)VDivisions; var v1 = (v + 1d) / VDivisions;
            var a = patch.Patch.Evaluate(u0, v0); var b = patch.Patch.Evaluate(u1, v0);
            var c = patch.Patch.Evaluate(u1, v1); var d = patch.Patch.Evaluate(u0, v1);
            yield return Triangle.Create(patch.TransitionIndex, patch.TransitionId, patch.SpanId, false, a, b, c);
            yield return Triangle.Create(patch.TransitionIndex, patch.TransitionId, patch.SpanId, false, a, c, d);
        }
    }

    private static IEnumerable<Triangle> Cap(Section section, int transitionIndex, string owner)
    {
        var points = SampleProfile(section).ToArray();
        var center = new Point3D(points.Average(point => point.X), points.Average(point => point.Y), points.Average(point => point.Z));
        for (var index = 0; index < points.Length; index++)
            yield return Triangle.Create(transitionIndex, owner, section.SectionId, true, center, points[index], points[(index + 1) % points.Length]);
    }

    private static IEnumerable<Point3D> SampleProfile(Section section)
    {
        foreach (var span in section.Profile.Spans)
        {
            var count = span.Curve is SectionProfileCurve.Line ? 1 : 24;
            for (var index = 0; index < count; index++) yield return section.Frame.Transform(Evaluate(span.Curve, index / (double)count));
        }
    }

    private static SectionPoint2D Evaluate(SectionProfileCurve curve, double t) => curve switch
    {
        SectionProfileCurve.Line line => new(line.Start.X + (line.End.X - line.Start.X) * t, line.Start.Y + (line.End.Y - line.Start.Y) * t),
        SectionProfileCurve.Arc arc => new(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + t * arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + t * arc.SweepAngleRadians)),
        SectionProfileCurve.PolynomialBSpline spline => Spline(spline, t),
        _ => default
    };

    private static SectionPoint2D Spline(SectionProfileCurve.PolynomialBSpline spline, double t)
    {
        var curve = new BSpline3Curve(spline.Degree, spline.ControlPoints.Select(point => new Point3D(point.X, point.Y, 0)).ToArray(), spline.KnotMultiplicities, spline.KnotValues, "UNSPECIFIED", false, false, "UNSPECIFIED");
        var point = curve.Evaluate(curve.DomainStart + t * (curve.DomainEnd - curve.DomainStart)); return new(point.X, point.Y);
    }

    private static bool TrianglesIntersect(Triangle a, Triangle b, double tolerance)
    {
        var ae = new[] { (a.A, a.B), (a.B, a.C), (a.C, a.A) }; var be = new[] { (b.A, b.B), (b.B, b.C), (b.C, b.A) };
        if (ae.Any(edge => SegmentTriangle(edge.Item1, edge.Item2, b, tolerance)) || be.Any(edge => SegmentTriangle(edge.Item1, edge.Item2, a, tolerance))) return true;
        var an = (a.B - a.A).Cross(a.C - a.A); var bn = (b.B - b.A).Cross(b.C - b.A);
        return an.TryNormalize(out var au) && bn.TryNormalize(out var bu) && Math.Abs(au.Dot(bu)) > 1d - 1e-8
            && Math.Abs((b.A - a.A).Dot(au)) <= tolerance && CoplanarOverlap(a, b, au);
    }

    private static bool SegmentTriangle(Point3D start, Point3D end, Triangle triangle, double tolerance)
    {
        var direction = end - start; var e1 = triangle.B - triangle.A; var e2 = triangle.C - triangle.A;
        var p = direction.Cross(e2); var determinant = e1.Dot(p);
        if (Math.Abs(determinant) <= tolerance) return false;
        var inverse = 1d / determinant; var s = start - triangle.A; var u = s.Dot(p) * inverse;
        if (u < -tolerance || u > 1d + tolerance) return false;
        var q = s.Cross(e1); var v = direction.Dot(q) * inverse;
        if (v < -tolerance || u + v > 1d + tolerance) return false;
        var t = e2.Dot(q) * inverse; return t >= -tolerance && t <= 1d + tolerance;
    }

    private static bool CoplanarOverlap(Triangle a, Triangle b, Vector3D normal)
    {
        var axis = Math.Abs(normal.X) > Math.Abs(normal.Y) ? (Math.Abs(normal.X) > Math.Abs(normal.Z) ? 0 : 2) : (Math.Abs(normal.Y) > Math.Abs(normal.Z) ? 1 : 2);
        var ap = new[] { Project(a.A, axis), Project(a.B, axis), Project(a.C, axis) }; var bp = new[] { Project(b.A, axis), Project(b.B, axis), Project(b.C, axis) };
        for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) if (Segments2(ap[i], ap[(i + 1) % 3], bp[j], bp[(j + 1) % 3])) return true;
        return Inside(ap[0], bp) || Inside(bp[0], ap);
    }

    private static (double X, double Y) Project(Point3D point, int drop) => drop switch { 0 => (point.Y, point.Z), 1 => (point.X, point.Z), _ => (point.X, point.Y) };
    private static bool Segments2((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        static double O((double X, double Y) p, (double X, double Y) q, (double X, double Y) r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        return O(a, b, c) * O(a, b, d) <= 0 && O(c, d, a) * O(c, d, b) <= 0;
    }
    private static bool Inside((double X, double Y) p, IReadOnlyList<(double X, double Y)> t)
    {
        var a = Sign(p, t[0], t[1]); var b = Sign(p, t[1], t[2]); var c = Sign(p, t[2], t[0]);
        return !(a < 0 || b < 0 || c < 0) || !(a > 0 || b > 0 || c > 0);
        static double Sign((double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3) => (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    private readonly record struct Bounds(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
    {
        public Bounds Expanded(double amount) => new(MinX - amount, MinY - amount, MinZ - amount, MaxX + amount, MaxY + amount, MaxZ + amount);
        public bool Intersects(Bounds other) => MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY && MinZ <= other.MaxZ && MaxZ >= other.MinZ;
    }
    private sealed record Triangle(int TransitionIndex, string Owner, string Span, bool IsCap, Point3D A, Point3D B, Point3D C, Bounds Bounds)
    {
        public static Triangle Create(int index, string owner, string span, bool cap, Point3D a, Point3D b, Point3D c) => new(index, owner, span, cap, a, b, c,
            new(Math.Min(a.X, Math.Min(b.X, c.X)), Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Min(a.Z, Math.Min(b.Z, c.Z)), Math.Max(a.X, Math.Max(b.X, c.X)), Math.Max(a.Y, Math.Max(b.Y, c.Y)), Math.Max(a.Z, Math.Max(b.Z, c.Z))));
    }
}
