using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public sealed record SectionProfileNormalizationEvidence(string SourceIdentity, string SourceCurveType,
    string Representation, int Degree, int PolynomialSpanCount, double RequestedTolerance,
    double CertifiedDeviationBound, double MaximumSampledDeviation, double EndpointPositionError,
    double EndpointTangentAngleDegrees, string ControlHash, string ErrorBoundMethod);
public sealed record SectionProfileNormalizationDecision(string SpanIdentity, int SelectedSegmentCount,
    IReadOnlyList<int> RejectedSegmentCounts);
public sealed record SectionProfileNormalizationResult([property: System.Text.Json.Serialization.JsonIgnore] SectionChain? NormalizedChain,
    IReadOnlyList<SectionProfileNormalizationEvidence> Curves,
    IReadOnlyList<SectionProfileNormalizationDecision> Decisions,
    IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{
    public bool IsSuccess => NormalizedChain is not null && Diagnostics.Count == 0;
}

/// <summary>Surfacing-only quintic Hermite normalization. Analytic source profiles remain immutable.</summary>
public static class SectionProfileNormalizer
{
    private static readonly int[] SegmentCounts = [1, 2, 4, 8, 16];
    public const int Degree = 5;
    public const int MaximumSegments = 16;

    public static SectionProfileNormalizationResult Normalize(SectionChain chain)
    {
        var diagnostics = new List<SurfacingDiagnostic>();
        var evidence = new List<SectionProfileNormalizationEvidence>();
        var decisions = new List<SectionProfileNormalizationDecision>();
        if (!double.IsFinite(chain.ProfileApproximationTolerance) || chain.ProfileApproximationTolerance <= 0)
            return Fail("section-chain-normalization-tolerance-invalid", "Profile approximation tolerance must be finite and positive.");
        if (chain.Sections.SelectMany(s => s.Profile.Spans).Any(s => s.Curve is not
            (SectionProfileCurve.Line or SectionProfileCurve.Arc or SectionProfileCurve.PolynomialBSpline)))
            return Fail("section-chain-normalization-curve-unsupported", "Only line, circular arc, and polynomial B-spline profiles are admitted.");
        var sections = chain.Sections.ToArray();
        if (sections.Length == 0) return new(chain, [], [], []);
        var count = sections[0].Profile.Spans.Count;
        if (sections.Any(s => s.Profile.Spans.Count != count))
            return Fail("section-chain-correspondence-topology-mismatch", "Normalization requires equal ordered semantic span counts.");
        // The owning SectionChain correspondence validator establishes seam-relative ordering first.
        for (var span = 0; span < count; span++)
        {
            var sourceCurves = sections.Select(s => s.Profile.Spans[span].Curve).ToArray();
            var arcs = sourceCurves.OfType<SectionProfileCurve.Arc>().ToArray();
            if (arcs.Length == 0) continue; // Existing polynomial and line-only G1 geometry is unchanged.
            if (arcs.Any(a => !double.IsFinite(a.Radius) || a.Radius <= 1e-8 ||
                !double.IsFinite(a.StartAngleRadians) || !double.IsFinite(a.SweepAngleRadians) ||
                Math.Abs(a.SweepAngleRadians) <= 1e-10 || Math.Abs(a.SweepAngleRadians) > 2*Math.PI ||
                !double.IsFinite(a.Center.X) || !double.IsFinite(a.Center.Y)))
                return Fail("section-chain-normalization-arc-invalid", "Arc radius, sweep and center must be finite and nondegenerate, with |sweep| <= 2*pi.");
            var judged = new JudgmentEngine<SectionProfileCurve.Arc[]>().Evaluate(arcs,
                SegmentCounts.Select(n => new JudgmentCandidate<SectionProfileCurve.Arc[]>("Segments" + n,
                    a => a.All(arc => Bound(arc,n) <= chain.ProfileApproximationTolerance),
                    _ => 1d/n, _ => "Certified Hermite remainder exceeds the requested tolerance.",
                    TieBreakerPriority: n)).ToArray());
            if (!judged.IsSuccess)
                return Fail("section-chain-normalization-tolerance-unmet", $"Span '{sections[0].Profile.Spans[span].SpanId}' cannot meet {chain.ProfileApproximationTolerance:R} mm with degree 5 and at most 16 segments.");
            var segments = int.Parse(judged.Selection!.Value.Candidate.Name[8..], CultureInfo.InvariantCulture);
            decisions.Add(new(sections[0].Profile.Spans[span].SpanId, segments,
                SegmentCounts.Where(n => arcs.Any(a => Bound(a,n) > chain.ProfileApproximationTolerance)).ToArray()));
            for (var section = 0; section < sections.Length; section++)
            {
                var source = sourceCurves[section];
                SectionProfileCurve.PolynomialBSpline curve;
                if (source is SectionProfileCurve.Arc arc) curve = Arc(arc, segments);
                else if (source is SectionProfileCurve.Line line) curve = Line(line, segments);
                else if (source is SectionProfileCurve.PolynomialBSpline polynomial && polynomial.Degree == Degree &&
                    polynomial.KnotValues.SequenceEqual(Knots(segments)) && polynomial.KnotMultiplicities.SequenceEqual(Multiplicities(segments))) curve = polynomial;
                else return Fail("section-chain-normalization-correspondence-incompatible", "An arc track accepts lines, arcs, or an already-compatible degree-5 polynomial; arbitrary degree/knot conversion is not inferred.");
                var identity = sections[section].SectionId + "." + sections[section].Profile.Spans[span].SpanId;
                var sampled = 0d; var endpoint = 0d; var tangent = 0d;
                var spline = Spline(curve);
                if (source is SectionProfileCurve.Arc exact)
                {
                    for (var i=0; i<=segments*32; i++)
                    {
                        var t=i/(double)(segments*32);
                        sampled=Math.Max(sampled,(spline.Evaluate(t)-Point(exact,t)).Length);
                    }
                    endpoint=Math.Max((spline.Evaluate(0)-Point(exact,0)).Length,(spline.Evaluate(1)-Point(exact,1)).Length);
                    var first = spline.ControlPoints[1]-spline.ControlPoints[0];
                    var last = spline.ControlPoints[^1]-spline.ControlPoints[^2];
                    tangent=Math.Max(Angle(first,Derivative(exact,0)),Angle(last,Derivative(exact,1)));
                }
                if (sampled > chain.ProfileApproximationTolerance || endpoint > chain.ProfileApproximationTolerance || tangent > 1e-7)
                    return Fail("section-chain-normalization-numerical-error", $"{identity}: numeric endpoint/tangent or sampled approximation check exceeds policy.");
                evidence.Add(new(identity,source.GetType().Name,"NonRationalQuinticHermiteBSpline",Degree,segments,
                    chain.ProfileApproximationTolerance,source is SectionProfileCurve.Arc a ? Bound(a,segments) : 0,
                    sampled,endpoint,tangent,Hash(curve),
                    "sqrt(2)*R*abs(segmentSweep)^6/(6!*64), componentwise Hermite remainder plus conservative coordinate-scale roundoff allowance; sampled error is separate"));
                var spans=sections[section].Profile.Spans.ToArray();
                spans[span]=spans[span] with { Curve=curve };
                sections[section]=sections[section] with { Profile=sections[section].Profile with { Spans=spans } };
            }
        }
        return new(chain with { Sections=sections },evidence,decisions,diagnostics);

        SectionProfileNormalizationResult Fail(string code,string message) => new(null,evidence,decisions,[new(code,message)]);
    }

    private static double Bound(SectionProfileCurve.Arc arc,int segments) =>
        Math.Sqrt(2)*arc.Radius*Math.Pow(Math.Abs(arc.SweepAngleRadians)/segments,6)/(720*64)
        + 128 * 2.2204460492503131e-16 * (Math.Abs(arc.Center.X)+Math.Abs(arc.Center.Y)+arc.Radius+1);
    private static double[] Knots(int segments) => Enumerable.Range(0,segments+1).Select(i=>i/(double)segments).ToArray();
    private static int[] Multiplicities(int segments) => Enumerable.Range(0,segments+1).Select(i=>i==0||i==segments?6:5).ToArray();
    private static SectionProfileCurve.PolynomialBSpline Arc(SectionProfileCurve.Arc arc,int segments)
    {
        var controls=new List<SectionPoint2D>(); var h=1d/segments;
        for(var i=0;i<segments;i++)
        {
            var a=i*h; var b=(i+1)*h; var p=Point(arc,a);var q=Point(arc,b);
            var da=Derivative(arc,a)*h;var db=Derivative(arc,b)*h;
            var aa=Second(arc,a)*(h*h);var ab=Second(arc,b)*(h*h);
            Point3D[] local=[p,p+da/5,p+da*(2d/5)+aa/20,q-db*(2d/5)+ab/20,q-db/5,q];
            controls.AddRange(local.Skip(i==0?0:1).Select(v=>new SectionPoint2D(v.X,v.Y)));
        }
        return new(Degree,controls,Multiplicities(segments),Knots(segments));
    }
    private static SectionProfileCurve.PolynomialBSpline Line(SectionProfileCurve.Line line,int segments) =>
        new(Degree,Enumerable.Range(0,5*segments+1).Select(i=>new SectionPoint2D(
            line.Start.X+(line.End.X-line.Start.X)*i/(5*segments),
            line.Start.Y+(line.End.Y-line.Start.Y)*i/(5*segments))).ToArray(),Multiplicities(segments),Knots(segments));
    private static Point3D Point(SectionProfileCurve.Arc a,double t) => new(a.Center.X+a.Radius*Math.Cos(a.StartAngleRadians+t*a.SweepAngleRadians),a.Center.Y+a.Radius*Math.Sin(a.StartAngleRadians+t*a.SweepAngleRadians),0);
    private static Vector3D Derivative(SectionProfileCurve.Arc a,double t) => new(-a.Radius*a.SweepAngleRadians*Math.Sin(a.StartAngleRadians+t*a.SweepAngleRadians),a.Radius*a.SweepAngleRadians*Math.Cos(a.StartAngleRadians+t*a.SweepAngleRadians),0);
    private static Vector3D Second(SectionProfileCurve.Arc a,double t) => new(-a.Radius*a.SweepAngleRadians*a.SweepAngleRadians*Math.Cos(a.StartAngleRadians+t*a.SweepAngleRadians),-a.Radius*a.SweepAngleRadians*a.SweepAngleRadians*Math.Sin(a.StartAngleRadians+t*a.SweepAngleRadians),0);
    private static BSpline3Curve Spline(SectionProfileCurve.PolynomialBSpline c) => new(c.Degree,c.ControlPoints.Select(p=>new Point3D(p.X,p.Y,0)).ToArray(),c.KnotMultiplicities,c.KnotValues,"UNSPECIFIED",false,false,"UNSPECIFIED");
    private static double Angle(Vector3D a,Vector3D b) => a.TryNormalize(out var x)&&b.TryNormalize(out var y)?Math.Atan2(x.Cross(y).Length,x.Dot(y))*180/Math.PI:double.PositiveInfinity;
    private static string Hash(SectionProfileCurve.PolynomialBSpline c) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";",c.ControlPoints.Select(p=>p.X.ToString("R",CultureInfo.InvariantCulture)+","+p.Y.ToString("R",CultureInfo.InvariantCulture)))+"|"+string.Join(",",c.KnotValues.Select(k=>k.ToString("R",CultureInfo.InvariantCulture))))));
}
