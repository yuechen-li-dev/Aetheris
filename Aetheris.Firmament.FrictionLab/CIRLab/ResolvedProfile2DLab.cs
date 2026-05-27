using System.Globalization;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum LabProfileStatus { Succeeded, Failed, Deferred }
public enum LabProfileRecommendation { profile2d_valid, profile2d_invalid, profile2d_deferred_topology, profile2d_needs_normalization_lab }

public abstract record LabAirCurve2D;
public sealed record LabAirLineSegment2D((double X, double Y) Start, (double X, double Y) End) : LabAirCurve2D;
public sealed record LabAirCircularArc2D((double X, double Y) Center, double Radius, double StartAngleRadians, double SweepAngleRadians) : LabAirCurve2D;
public sealed record LabAirFullCircle2D((double X, double Y) Center, double Radius, bool CounterClockwise = true) : LabAirCurve2D;

public sealed record LabAirLoop2D(IReadOnlyList<LabAirCurve2D> Curves, string Role);
public sealed record LabResolvedProfile2D(IReadOnlyList<LabAirLoop2D> Loops);
public sealed record LabResolvedProfile2DArtifact(string CaseName, LabProfileStatus Status, int CurveCount, int LoopCount, int HoleCount,
    string BoundingBox, IReadOnlyList<double> LoopSignedAreas, string NormalizedOrientation, IReadOnlyList<string> Diagnostics, LabProfileRecommendation Recommendation);

public static class ResolvedProfile2DLab
{
    private const double Tol = 1e-6;

    public static IReadOnlyList<LabResolvedProfile2DArtifact> RunAll() =>
    [
        Evaluate("valid-rectangle", Rectangle()),
        Evaluate("valid-circle", FullCircleOuter()),
        Evaluate("valid-rectangle-one-hole", RectangleWithHoles(1)),
        Evaluate("valid-rectangle-two-holes", RectangleWithHoles(2)),
        Evaluate("valid-orientation-reversed-input", RectangleWithHoles(1, reverseOrientation: true)),
        Evaluate("invalid-open-loop", OpenRectangle()),
        Evaluate("invalid-endpoint-mismatch", EndpointMismatch()),
        Evaluate("invalid-zero-length-line", ZeroLengthLine()),
        Evaluate("invalid-arc-radius", InvalidArcRadius()),
        Evaluate("invalid-arc-zero-sweep", InvalidArcZeroSweep()),
        Evaluate("invalid-self-intersecting-bowtie", BowTie()),
        Evaluate("invalid-hole-outside", HoleOutside()),
        Evaluate("invalid-hole-touches-boundary", HoleTouchesBoundary()),
        Evaluate("invalid-hole-overlap", OverlappingHoles()),
        Evaluate("deferred-multiple-outers", MultipleOuters()),
        Evaluate("deferred-nested-island", NestedIsland())
    ];

    public static LabResolvedProfile2DArtifact Evaluate(string caseName, LabResolvedProfile2D profile)
    {
        var diagnostics = new List<string>();
        var curveCount = profile.Loops.Sum(x => x.Curves.Count);
        if (profile.Loops.Count == 0)
        {
            diagnostics.Add("profile-region-missing-outer-loop");
            return BuildArtifact(caseName, LabProfileStatus.Failed, profile, curveCount, [], diagnostics, LabProfileRecommendation.profile2d_invalid);
        }

        var loopSamples = new List<List<(double X, double Y)>>();
        foreach (var loop in profile.Loops)
        {
            ValidateCurvePrimitives(loop, diagnostics);
            var sample = ValidateLoop(loop, diagnostics);
            loopSamples.Add(sample);
        }

        var areas = loopSamples.Select(SignedArea).ToArray();
        var material = areas.Select((a, i) => (Area: a, Index: i)).Where(x => x.Area > Tol).ToArray();
        var outerIndex = -1;
        if (material.Length == 0)
        {
            if (areas.Length > 0)
            {
                outerIndex = Enumerable.Range(0, areas.Length).OrderByDescending(i => Math.Abs(areas[i])).First();
                diagnostics.Add("profile-normalized-orientation");
            }
            else diagnostics.Add("profile-region-missing-outer-loop");
        }
        else outerIndex = material.OrderByDescending(m => Math.Abs(m.Area)).First().Index;
        if (material.Length > 1)
            diagnostics.Add("profile-region-multiple-outer-loops-deferred");

        if (!diagnostics.Contains("profile-region-multiple-outer-loops-deferred", StringComparer.Ordinal) && outerIndex >= 0)
        {
            var outer = loopSamples[outerIndex];
            var holes = areas.Select((a, i) => (Area: a, Index: i)).Where(x => x.Index != outerIndex).ToArray();
            for (var h = 0; h < holes.Length; h++)
            {
                var hole = loopSamples[holes[h].Index];
                if (hole.Any(p => !PointInPolygon(p, outer))) diagnostics.Add("profile-region-hole-outside-outer");
                if (TouchesBoundary(hole, outer)) diagnostics.Add("profile-region-hole-touches-boundary");
                for (var j = h + 1; j < holes.Length; j++)
                {
                    if (LoopsOverlap(hole, loopSamples[holes[j].Index])) diagnostics.Add("profile-region-hole-overlaps-hole");
                }
            }
        }

        if (outerIndex >= 0 && Enumerable.Range(0, areas.Length).Any(i => i != outerIndex && Math.Sign(areas[i]) == Math.Sign(areas[outerIndex])))
            diagnostics.Add("profile-normalized-orientation");

        if (areas.Count(x => x > Tol) > 1 && areas.Count(x => x < -Tol) > 0)
            diagnostics.Add("profile-region-nested-island-deferred");

        diagnostics = diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var blockers = diagnostics.Where(d => d is not "profile-normalized-orientation").ToArray();
        var status = blockers.Any(d => d.Contains("deferred", StringComparison.Ordinal)) ? LabProfileStatus.Deferred
            : blockers.Length > 0 ? LabProfileStatus.Failed : LabProfileStatus.Succeeded;
        var recommendation = status switch
        {
            LabProfileStatus.Succeeded => LabProfileRecommendation.profile2d_valid,
            LabProfileStatus.Deferred => LabProfileRecommendation.profile2d_deferred_topology,
            _ => LabProfileRecommendation.profile2d_invalid
        };
        return BuildArtifact(caseName, status, profile, curveCount, areas, diagnostics, recommendation);
    }

    private static LabResolvedProfile2DArtifact BuildArtifact(string caseName, LabProfileStatus status, LabResolvedProfile2D profile, int curveCount, IReadOnlyList<double> areas, IReadOnlyList<string> diagnostics, LabProfileRecommendation recommendation)
    {
        var points = profile.Loops.SelectMany(ExpandLoopPoints).ToArray();
        var bbox = points.Length == 0 ? "empty" : FormattableString.Invariant($"[{points.Min(p => p.X):0.###},{points.Min(p => p.Y):0.###}]..[{points.Max(p => p.X):0.###},{points.Max(p => p.Y):0.###}]");
        var orient = areas.Count == 0 ? "none" : string.Join(",", areas.Select(x => x >= 0 ? "ccw" : "cw"));
        var holeCount = areas.Count(x => x < -Tol);
        return new(caseName, status, curveCount, profile.Loops.Count, holeCount, bbox, areas.Select(a => Math.Round(a, 6)).ToArray(), orient, diagnostics, recommendation);
    }

    private static List<(double X, double Y)> ValidateLoop(LabAirLoop2D loop, List<string> diagnostics)
    {
        var points = BuildChainPoints(loop);
        if (loop.Curves.Count == 0) diagnostics.Add("profile-loop-empty");
        if (points.Count < 3) diagnostics.Add("profile-loop-open");

        for (var i = 1; i < points.Count; i++)
        {
            if (Distance(points[i - 1], points[i]) <= Tol) diagnostics.Add("profile-loop-zero-length-segment");
        }
        if (points.Count >= 2 && Distance(points.First(), points.Last()) > 1e-4) diagnostics.Add("profile-loop-open");

        if (SelfIntersects(points)) diagnostics.Add("profile-loop-self-intersection");
        if (Math.Abs(SignedArea(points)) <= Tol) diagnostics.Add("profile-loop-ambiguous-orientation");
        return points;
    }

    private static IEnumerable<(double X, double Y)> ExpandLoopPoints(LabAirLoop2D loop)
    {
        foreach (var curve in loop.Curves)
        {
            foreach (var p in ExpandCurvePoints(curve)) yield return p;
        }
    }

    private static List<(double X, double Y)> BuildChainPoints(LabAirLoop2D loop)
    {
        var chain = new List<(double X, double Y)>();
        foreach (var curve in loop.Curves)
        {
            var pts = ExpandCurvePoints(curve).ToList();
            if (pts.Count == 0) continue;
            if (chain.Count > 0 && Distance(chain[^1], pts[0]) <= Tol) pts.RemoveAt(0);
            chain.AddRange(pts);
        }
        return chain;
    }

    private static void ValidateCurvePrimitives(LabAirLoop2D loop, List<string> diagnostics)
    {
        foreach (var curve in loop.Curves)
        {
            switch (curve)
            {
                case LabAirLineSegment2D line when (!Finite(line.Start) || !Finite(line.End)):
                    diagnostics.Add("profile-curve-non-finite");
                    break;
                case LabAirLineSegment2D line when Distance(line.Start, line.End) <= Tol:
                    diagnostics.Add("profile-curve-zero-length-line");
                    diagnostics.Add("profile-loop-zero-length-segment");
                    break;
                case LabAirCircularArc2D arc when (!Finite(arc.Center) || !double.IsFinite(arc.Radius) || !double.IsFinite(arc.StartAngleRadians) || !double.IsFinite(arc.SweepAngleRadians)):
                    diagnostics.Add("profile-curve-non-finite");
                    break;
                case LabAirCircularArc2D arc when arc.Radius <= Tol:
                    diagnostics.Add("profile-curve-invalid-radius");
                    break;
                case LabAirCircularArc2D arc when Math.Abs(arc.SweepAngleRadians) <= Tol:
                    diagnostics.Add("profile-curve-zero-sweep-arc");
                    break;
                case LabAirFullCircle2D circle when (!Finite(circle.Center) || !double.IsFinite(circle.Radius)):
                    diagnostics.Add("profile-curve-non-finite");
                    break;
                case LabAirFullCircle2D circle when circle.Radius <= Tol:
                    diagnostics.Add("profile-curve-invalid-radius");
                    break;
            }
        }
    }

    private static IEnumerable<(double X, double Y)> ExpandCurvePoints(LabAirCurve2D curve)
    {
        switch (curve)
        {
            case LabAirLineSegment2D line:
                if (!Finite(line.Start) || !Finite(line.End)) yield break;
                if (Distance(line.Start, line.End) <= Tol) yield break;
                yield return line.Start;
                yield return line.End;
                break;
            case LabAirCircularArc2D arc:
                if (!Finite(arc.Center) || !double.IsFinite(arc.Radius) || arc.Radius <= Tol || Math.Abs(arc.SweepAngleRadians) <= Tol) yield break;
                var n = Math.Max(8, (int)Math.Ceiling(Math.Abs(arc.SweepAngleRadians) / (Math.PI / 8)));
                for (var i = 0; i <= n; i++)
                {
                    var t = arc.StartAngleRadians + arc.SweepAngleRadians * (i / (double)n);
                    yield return (arc.Center.X + arc.Radius * Math.Cos(t), arc.Center.Y + arc.Radius * Math.Sin(t));
                }
                break;
            case LabAirFullCircle2D circle:
                if (!Finite(circle.Center) || !double.IsFinite(circle.Radius) || circle.Radius <= Tol) yield break;
                for (var i = 0; i <= 32; i++)
                {
                    var t = (circle.CounterClockwise ? 1 : -1) * i * 2 * Math.PI / 32;
                    yield return (circle.Center.X + circle.Radius * Math.Cos(t), circle.Center.Y + circle.Radius * Math.Sin(t));
                }
                break;
        }
    }

    private static double SignedArea(List<(double X, double Y)> pts)
    {
        if (pts.Count < 3) return 0;
        double a = 0;
        for (var i = 0; i < pts.Count - 1; i++) a += pts[i].X * pts[i + 1].Y - pts[i + 1].X * pts[i].Y;
        return 0.5 * a;
    }
    private static bool Finite((double X, double Y) p) => double.IsFinite(p.X) && double.IsFinite(p.Y);
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X)*(a.X - b.X)+(a.Y - b.Y)*(a.Y - b.Y));
    private static bool SelfIntersects(List<(double X, double Y)> pts)
    {
        for (var i = 0; i < pts.Count - 1; i++) for (var j = i + 2; j < pts.Count - 1; j++) if (!(i == 0 && j == pts.Count - 2) && SegmentsIntersect(pts[i], pts[i + 1], pts[j], pts[j + 1])) return true;
        return false;
    }
    private static bool SegmentsIntersect((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    { static double O((double X,double Y)p,(double X,double Y)q,(double X,double Y)r)=> (q.X-p.X)*(r.Y-p.Y)-(q.Y-p.Y)*(r.X-p.X); var o1=O(a,b,c);var o2=O(a,b,d);var o3=O(c,d,a);var o4=O(c,d,b); return o1*o2 < -Tol && o3*o4 < -Tol; }
    private static bool PointInPolygon((double X,double Y)p, List<(double X,double Y)> poly)
    { var inside=false; for(int i=0,j=poly.Count-1;i<poly.Count;j=i++) { var pi=poly[i]; var pj=poly[j]; if (((pi.Y>p.Y)!=(pj.Y>p.Y)) && (p.X < (pj.X-pi.X)*(p.Y-pi.Y)/(pj.Y-pi.Y+1e-20)+pi.X)) inside=!inside; } return inside; }
    private static bool TouchesBoundary(List<(double X,double Y)> a, List<(double X,double Y)> b)
    {
        for (var i = 0; i < b.Count - 1; i++)
        {
            var s = b[i];
            var e = b[i + 1];
            if (a.Any(p => PointSegmentDistance(p, s, e) <= 1e-4)) return true;
        }
        return false;
    }
    private static double PointSegmentDistance((double X,double Y) p, (double X,double Y) a, (double X,double Y) b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var len2 = dx*dx + dy*dy;
        if (len2 <= Tol) return Distance(p,a);
        var t = Math.Clamp(((p.X-a.X)*dx + (p.Y-a.Y)*dy)/len2, 0, 1);
        var proj = (a.X + t*dx, a.Y + t*dy);
        return Distance(p, proj);
    }
    private static bool LoopsOverlap(List<(double X,double Y)> a, List<(double X,double Y)> b) => a.Any(p => PointInPolygon(p,b)) || b.Any(p => PointInPolygon(p,a));

    private static LabResolvedProfile2D Rectangle() => new([new([new LabAirLineSegment2D((0,0),(10,0)), new LabAirLineSegment2D((10,0),(10,6)), new LabAirLineSegment2D((10,6),(0,6)), new LabAirLineSegment2D((0,6),(0,0))], "outer")]);
    private static LabResolvedProfile2D FullCircleOuter() => new([new([new LabAirFullCircle2D((0,0), 4)], "outer")]);
    private static LabResolvedProfile2D RectangleWithHoles(int holes, bool reverseOrientation=false)
    {
        var outer = reverseOrientation
            ? new LabAirLoop2D([new LabAirLineSegment2D((0,0),(0,8)), new LabAirLineSegment2D((0,8),(12,8)), new LabAirLineSegment2D((12,8),(12,0)), new LabAirLineSegment2D((12,0),(0,0))],"outer")
            : new LabAirLoop2D([new LabAirLineSegment2D((0,0),(12,0)), new LabAirLineSegment2D((12,0),(12,8)), new LabAirLineSegment2D((12,8),(0,8)), new LabAirLineSegment2D((0,8),(0,0))],"outer");
        var loops = new List<LabAirLoop2D> { outer };
        if (holes >= 1) loops.Add(new([new LabAirFullCircle2D((3,4), 1, CounterClockwise: false)], "hole"));
        if (holes >= 2) loops.Add(new([new LabAirFullCircle2D((8,4), 1, CounterClockwise: false)], "hole"));
        return new(loops);
    }
    private static LabResolvedProfile2D OpenRectangle() => new([new([new LabAirLineSegment2D((0,0),(10,0)), new LabAirLineSegment2D((10,0),(10,5)), new LabAirLineSegment2D((10,5),(0,5))], "outer")]);
    private static LabResolvedProfile2D EndpointMismatch() => new([new([new LabAirLineSegment2D((0,0),(4,0)), new LabAirLineSegment2D((4,0),(4,4)), new LabAirLineSegment2D((4,4),(0,4)), new LabAirLineSegment2D((0,4),(0.2,0))], "outer")]);
    private static LabResolvedProfile2D ZeroLengthLine() => new([new([new LabAirLineSegment2D((0,0),(0,0))], "outer")]);
    private static LabResolvedProfile2D InvalidArcRadius() => new([new([new LabAirCircularArc2D((0,0), 0, 0, Math.PI)], "outer")]);
    private static LabResolvedProfile2D InvalidArcZeroSweep() => new([new([new LabAirCircularArc2D((0,0), 1, 0, 0)], "outer")]);
    private static LabResolvedProfile2D BowTie() => new([new([new LabAirLineSegment2D((0,0),(4,4)), new LabAirLineSegment2D((4,4),(0,4)), new LabAirLineSegment2D((0,4),(4,0)), new LabAirLineSegment2D((4,0),(0,0))], "outer")]);
    private static LabResolvedProfile2D HoleOutside() => new([Rectangle().Loops[0], new LabAirLoop2D([new LabAirFullCircle2D((20,20),1,false)], "hole")]);
    private static LabResolvedProfile2D HoleTouchesBoundary() => new([Rectangle().Loops[0], new LabAirLoop2D([new LabAirFullCircle2D((1,3),1,false)], "hole")]);
    private static LabResolvedProfile2D OverlappingHoles() => new([Rectangle().Loops[0], new LabAirLoop2D([new LabAirFullCircle2D((3,3),1,false)], "hole"), new LabAirLoop2D([new LabAirFullCircle2D((3.5,3),1,false)], "hole")]);
    private static LabResolvedProfile2D MultipleOuters() => new([Rectangle().Loops[0], new LabAirLoop2D([new LabAirLineSegment2D((20,0),(24,0)), new LabAirLineSegment2D((24,0),(24,4)), new LabAirLineSegment2D((24,4),(20,4)), new LabAirLineSegment2D((20,4),(20,0))], "outer2")]);
    private static LabResolvedProfile2D NestedIsland() => new([Rectangle().Loops[0], new LabAirLoop2D([new LabAirFullCircle2D((3,3),1,false)], "hole"), new LabAirLoop2D([new LabAirFullCircle2D((3,3),0.5,true)], "island")]);
}
