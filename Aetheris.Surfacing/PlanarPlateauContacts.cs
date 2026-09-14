using Aetheris.Kernel.Core.Brep.EdgeFinishing;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record PlanarPlateauContactSpan(string SourceSpanId, string BaseContactId, string TopContactId,
    BSpline3Curve BaseContact, BSpline3Curve MiddleContact, BSpline3Curve TopContact,
    IReadOnlyList<BSplineSurfaceWithKnots> Patches, string Exactness = "ExactAffinePolynomial", string ParameterDirection = "SourceForward");
public sealed record PlanarPlateauContactPlan(string StableId, string BaseSupportId, string TopSupportId,
    SectionFrame Frame, SectionPoint2D Center, double Height, double Width, double TopScale, string Seam,
    IReadOnlyList<PlanarPlateauContactSpan> Spans, IReadOnlyList<SectionChainGeometricJoinEvidence> Joins);
public sealed record PlanarPlateauContactResult(PlanarPlateauContactPlan? Plan, IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Plan is not null && Diagnostics.Count == 0;
}

/// <summary>
/// Exact homothetic contact band on parallel planes. Width is the inset on the
/// footprint's shorter bounding-box half-axis; other radial insets scale with it.
/// This is an explicit affine construction, not an approximation to a normal offset.
/// </summary>
public static class PlanarPlateauContacts
{
    public static PlanarPlateauContactResult Build(string name, SectionProfile footprint, SectionFrame frame,
        double height, double width, string baseSupportId, string topSupportId)
    {
        PlanarPlateauContactResult Fail(string reason) => new(null, ["plateau-" + reason]);
        if (!double.IsFinite(width) || width <= 0) return Fail("width-must-be-positive");
        if (!double.IsFinite(height) || height <= 0) return Fail("height-must-be-positive");
        if (footprint.Spans.Count < 3 || footprint.Spans.Select(s => s.SpanId).Distinct().Count() != footprint.Spans.Count ||
            !footprint.Spans.Any(s => s.SpanId == footprint.SeamSpanId)) return Fail("footprint-identity-invalid");
        if (Math.Abs(frame.XAxis.ToVector().Dot(frame.YAxis.ToVector())) > 1e-12 ||
            (frame.XAxis.ToVector().Cross(frame.YAxis.ToVector()) - frame.Normal.ToVector()).Length > 1e-12)
            return Fail("frame-invalid");
        var curves = new List<BSpline3Curve>();
        foreach (var span in footprint.Spans)
        {
            switch (span.Curve)
            {
                case SectionProfileCurve.Line line:
                    curves.Add(new(1, [new(line.Start.X, line.Start.Y, 0), new(line.End.X, line.End.Y, 0)], [2, 2], [0, 1], "UNSPECIFIED", false, false, "UNSPECIFIED")); break;
                case SectionProfileCurve.PolynomialBSpline spline when spline.Degree is >= 1 and <= 3 &&
                    spline.ControlPoints.Count == spline.Degree + 1 && spline.KnotValues.SequenceEqual(new double[] { 0, 1 }) &&
                    spline.KnotMultiplicities.SequenceEqual(new[] { spline.Degree + 1, spline.Degree + 1 }):
                    curves.Add(new(spline.Degree, spline.ControlPoints.Select(p => new Point3D(p.X, p.Y, 0)).ToArray(),
                        spline.KnotMultiplicities, spline.KnotValues, "UNSPECIFIED", false, false, "UNSPECIFIED")); break;
                default: return Fail("footprint-curve-unsupported:" + span.SpanId);
            }
        }
        var controls = curves.SelectMany(c => c.ControlPoints).ToArray();
        if (controls.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return Fail("footprint-nonfinite");
        var cx = (controls.Min(p => p.X) + controls.Max(p => p.X)) / 2;
        var cy = (controls.Min(p => p.Y) + controls.Max(p => p.Y)) / 2;
        var half = Math.Min(controls.Max(p => p.X) - controls.Min(p => p.X), controls.Max(p => p.Y) - controls.Min(p => p.Y)) / 2;
        if (half <= 1e-8) return Fail("footprint-degenerate");
        if (width >= half) return Fail("width-collapses-footprint");
        var center = new Point3D(cx, cy, 0); var winding = 0d;
        for (var index = 0; index < curves.Count; index++)
        {
            var c = curves[index]; var next = curves[(index + 1) % curves.Count];
            if ((c.ControlPoints[^1] - next.ControlPoints[0]).Length > 1e-8) return Fail("footprint-open");
            var a = c.ControlPoints[0] - center; var b = c.ControlPoints[^1] - center;
            var angle = Math.Atan2(a.Cross(b).Z, a.Dot(b));
            if (angle <= 0 || angle >= Math.PI) return Fail("footprint-not-star-shaped");
            winding += angle;
            if (c.ControlPoints.Any(p => a.Cross(p - center).Z < -1e-10 || (p - center).Cross(b).Z < -1e-10))
                return Fail("footprint-control-hull-outside-sector");
            // Bernstein coefficients certify positive angular derivative on the whole
            // span. Together with disjoint angular sectors and one turn this proves
            // a regular simple loop and nested nonintersecting affine contact rings.
            var n = c.Degree;
            for (var k = 0; k <= 2 * n - 1; k++)
            {
                var coefficient = 0d;
                for (var i = 0; i <= n; i++)
                {
                    var j = k - i; if (j < 0 || j >= n) continue;
                    coefficient += Choose(n, i) * Choose(n - 1, j) / Choose(2 * n - 1, k) *
                        (c.ControlPoints[i] - center).Cross((c.ControlPoints[j + 1] - c.ControlPoints[j]) * n).Z;
                }
                if (coefficient <= 1e-12) return Fail("footprint-angular-regularity-unproven:" + footprint.Spans[index].SpanId);
            }
        }
        if (Math.Abs(winding - 2 * Math.PI) > 1e-8) return Fail("footprint-winding-invalid");
        var scale = 1 - width / half;
        // Compose the already-qualified quarter twice: concave entry, convex exit.
        // Affine scaling changes dimensions, not the cross-section law.
        var laws = new[] {
            CurvatureContinuousFilletSection.CreateQuarter(new(0, 0, 0), new(.5, .5, 0), new(0, .5, 0)),
            CurvatureContinuousFilletSection.CreateQuarter(new(.5, .5, 0), new(1, 1, 0), new(1, .5, 0)) };
        var spans = new List<PlanarPlateauContactSpan>();
        Point3D World(Point3D p, double inset, double rise) => frame.Transform(new(cx + (p.X - cx) * (1 - inset * (1 - scale)),
            cy + (p.Y - cy) * (1 - inset * (1 - scale)))) + frame.Normal.ToVector() * (rise * height);
        foreach (var (curve, index) in curves.Select((c, i) => (c, i)))
        {
            BSpline3Curve Contact(double inset, double rise) => new(curve.Degree, curve.ControlPoints.Select(p => World(p, inset, rise)).ToArray(),
                curve.KnotMultiplicities, curve.KnotValues, "UNSPECIFIED", false, false, "UNSPECIFIED");
            var patches = laws.Select(law => new BSplineSurfaceWithKnots(curve.Degree, 5,
                curve.ControlPoints.Select(p => (IReadOnlyList<Point3D>)law.ControlPoints.Select(q => World(p, q.X, q.Y)).ToArray()).ToArray(),
                "UNSPECIFIED", false, false, false, curve.KnotMultiplicities, [6, 6], curve.KnotValues, [0, 1], "UNSPECIFIED")).ToArray();
            var id = footprint.Spans[index].SpanId;
            spans.Add(new(id, name + ".BaseContact." + id, name + ".TopContact." + id, Contact(0, 0), Contact(.5, .5), Contact(1, 1), patches));
        }
        var joins = new List<SectionChainGeometricJoinEvidence>();
        foreach (var (span, index) in spans.Select((s, i) => (s, i)))
        {
            var next = spans[(index + 1) % spans.Count];
            SmoothSectionChainTransitionPatch Patch(BSplineSurfaceWithKnots s) => new(s);
            joins.Add(SectionChainDifferentialInspection.Measure(name + ".Middle." + span.SourceSpanId, "SectionQuarterJoin", Patch(span.Patches[0]), Patch(span.Patches[1]), true));
            for (var stage = 0; stage < 2; stage++)
                joins.Add(SectionChainDifferentialInspection.Measure(name + ".Neighbor." + span.SourceSpanId + "." + stage, "NeighboringFootprintSpans", Patch(span.Patches[stage]), Patch(next.Patches[stage]), false));
            BSplineSurfaceWithKnots PlanePatch(BSpline3Curve c, bool before)
            {
                var worldCenter = frame.Transform(new(cx, cy));
                return new(c.Degree, 1, c.ControlPoints.Select(p => (IReadOnlyList<Point3D>)(before
                    ? new[] { p + (p - (worldCenter + frame.Normal.ToVector() * (p - worldCenter).Dot(frame.Normal.ToVector()))) * .01, p }
                    : new[] { p, p - (p - (worldCenter + frame.Normal.ToVector() * height)) * .01 })).ToArray(),
                    "UNSPECIFIED", false, false, false, c.KnotMultiplicities, [2, 2], c.KnotValues, [0, 1], "UNSPECIFIED");
            }
            joins.Add(SectionChainDifferentialInspection.Measure(span.BaseContactId, "BaseSupport", Patch(PlanePatch(span.BaseContact, true)), Patch(span.Patches[0]), true));
            joins.Add(SectionChainDifferentialInspection.Measure(span.TopContactId, "TopSupport", Patch(span.Patches[1]), Patch(PlanePatch(span.TopContact, false)), true));
        }
        if (joins.Where(j => j.BoundaryKind != "NeighboringFootprintSpans").Any(j => j.Status != "G2WithinSampledTolerance")) return Fail("support-continuity-failed");
        return new(new(name, baseSupportId, topSupportId, frame, new(cx, cy), height, width, scale,
            name + ".Seam." + footprint.SeamSpanId, spans, joins), []);
    }

    private static double Choose(int n, int k)
    {
        var value = 1d; for (var i = 1; i <= k; i++) value = value * (n - i + 1) / i; return value;
    }
}
