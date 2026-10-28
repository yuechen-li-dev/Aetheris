using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

public sealed record RibPlanningCandidate(string Name, bool Admitted, double Utility, string Reason);
public sealed record RibPlanningReport(string Strategy, IReadOnlyList<RibPlanningCandidate> Candidates,
    int CheckedEventIntervals, string ContactPolicy = "ExactPlanarInterface");
internal sealed record RibNetworkPlan(string Strategy, BrepBody? Addition,
    IReadOnlyDictionary<FaceId, string> FaceOwners, PathFeatureBody? Isolated, RibPlanningReport Report);

/// <summary>Choose a bounded representation, then let the existing planar
/// arrangement own intersections. There is no 3D Boolean search or source rewrite.</summary>
internal static class RibNetworkPlanner
{
    private sealed record Context(bool Isolated, bool Prismatic, bool Affine, int Count);
    internal static KernelResult<RibNetworkPlan> Plan(double x, double y, double z, IReadOnlyList<FirmamentV2PathFeature> declarations)
    {
        KernelResult<RibNetworkPlan> Fail(string message) => Failure<RibNetworkPlan>(message);
        var features = declarations.Select(d => d.Feature).ToArray();
        if (features.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != features.Length) return Fail("duplicate feature names");
        foreach (var f in features)
        {
            var admitted = BrepPlanarPathFeature.ValidateFeature(f, z);
            if (!admitted.IsSuccess) return KernelResult<RibNetworkPlan>.Failure(admitted.Diagnostics);
            try
            {
                var bounds = Bounds(Profile(f, 0));
                if (bounds.MinX <= -x / 2 + 1e-7 || bounds.MaxX >= x / 2 - 1e-7 || bounds.MinY <= -y / 2 + 1e-7 || bounds.MaxY >= y / 2 - 1e-7)
                    return Fail(f.Name + ": footprint leaves its Box support");
            }
            catch (ArgumentException e) { return Fail(f.Name + ": " + e.Message); }
        }
        var isolated = BrepPlanarPathFeature.BuildBox(x, y, z, features);
        var ribs = features.Where(f => f.Add).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        var context = new Context(isolated?.IsSuccess == true,
            ribs.Length > 0 && ribs.All(f => f.DraftDegrees == 0),
            ribs.Length > 0 && ribs.All(f => f.Guide.Kind == PlanarGuideKind.Line && !f.RoundEnds && Math.Abs(f.Extent - ribs[0].Extent) < 1e-8), ribs.Length);
        JudgmentCandidate<Context>[] candidates = [
            new("ExistingAnalyticPatch",c=>c.Isolated,_=>1,_=>"Feature envelopes interact; an arrangement is required.",0),
            new("PrismaticSectionArrangement",c=>c.Prismatic,c=>Utility.Weighted((1,.8),(1/(1+c.Count/64d),.2)),_=>"Connected constant-section networks require zero side draft.",1),
            new("AffineDraftArrangement",c=>c.Affine,c=>Utility.Weighted((.9,.8),(1/(1+c.Count/64d),.2)),_=>"Drafted junctions require Flat-ended straight ribs of one common height.",2)];
        var decision = new JudgmentEngine<Context>().Evaluate(context, candidates);
        var evidence = candidates.Select(c => new RibPlanningCandidate(c.Name, c.IsAdmissible(context), c.IsAdmissible(context) ? c.Score(context) : 0,
            c.IsAdmissible(context) ? "Eligible bounded construction." : c.RejectionReason!(context))).ToArray();
        if (!decision.IsSuccess) return Fail("no admitted rib plan: " + string.Join("; ", evidence.Select(e => e.Name + ": " + e.Reason)));
        var strategy = decision.Selection!.Value.Candidate.Name;
        if (strategy == "ExistingAnalyticPatch") return KernelResult<RibNetworkPlan>.Success(new(strategy, null, new Dictionary<FaceId, string>(), isolated!.Value, new(strategy, evidence, 0)));
        if (strategy == "PrismaticSectionArrangement")
        {
            var profiles = ribs.ToDictionary(f => f.Name, f => Profile(f, 0), StringComparer.Ordinal);
            var dummy = "__attachment_base"; while (profiles.ContainsKey(dummy)) dummy += "_";
            profiles[dummy] = Rectangle(dummy, x, y);
            var operations = new List<PrismaticProfileOperation> { new(dummy, PrismaticProfileIntent.Base, dummy, -1, 0, "AttachmentBase", "generated:attachment") };
            operations.AddRange(ribs.Select(f => new PrismaticProfileOperation(f.Name, PrismaticProfileIntent.Add, f.Name, 0, f.Extent, "Rib", "feature:" + f.Name)));
            var feature = new PrismaticProfileCompositionFeature("RibNetwork", "XY", "+Z", new("WorldXY", 0, 0, 0, "XY", "+Z", "+X", false), operations,
                operations.SelectMany(o => new[] { o.From, o.To }).Distinct().Order().ToArray(), "ManualRibArrangement");
            var stack = PrismaticSectionStackCompiler.Normalize(new(feature, profiles, []), out var errors);
            if (stack is null) return Fail(string.Join("; ", errors));
            var emitted = PrismaticSectionStackEmitter.Emit(stack);
            if (emitted.Body is null) return Fail(string.Join("; ", emitted.Diagnostics));
            var faceOwners = new Dictionary<FaceId, string>();
            foreach (var descendant in emitted.Correspondence?.Descendants ?? [])
                if (descendant.Face is { } face)
                {
                    var owner = ribs.FirstOrDefault(r => descendant.SourceStableId.StartsWith("profile:" + r.Name + ".", StringComparison.Ordinal));
                    if (owner is not null) faceOwners[face] = owner.Name;
                }
            return KernelResult<RibNetworkPlan>.Success(new(strategy, emitted.Body, faceOwners, null, new(strategy, evidence, stack.Slabs.Count)));
        }
        var h = ribs[0].Extent; var lower = Arrange(ribs, 0); var upper = Arrange(ribs, h);
        if (lower.Arrangement.Diagnostics.Any(d => d.StartsWith("arrangement-rejected", StringComparison.Ordinal))
            || upper.Arrangement.Diagnostics.Any(d => d.StartsWith("arrangement-rejected", StringComparison.Ordinal)))
            return Fail(string.Join("; ", lower.Arrangement.Diagnostics.Concat(upper.Arrangement.Diagnostics)));
        var lo = Canonical(lower.Arrangement); var hi = Canonical(upper.Arrangement);
        if (!lo.Keys.Order().SequenceEqual(hi.Keys.Order())) return Fail("draft changes junction topology between the base and top; reduce height/draft or adjust the guides");
        var stations = EventStations(ribs, h); var intervals = 0;
        foreach (var pair in stations.Zip(stations.Skip(1)))
        {
            var station = (pair.First + pair.Second) / 2; var section = Arrange(ribs, station);
            if (section.Arrangement.Diagnostics.Any(d => d.StartsWith("arrangement-rejected", StringComparison.Ordinal))) return Fail("unqualified junction at height " + station + ": " + string.Join("; ", section.Arrangement.Diagnostics));
            var actual = Canonical(section.Arrangement);
            if (!lo.Keys.Order().SequenceEqual(actual.Keys.Order())) return Fail("draft crosses a junction event near height " + station);
            foreach (var key in lo.Keys)
                for (var i = 0; i < lo[key].Length; i++)
                {
                    var a = ((LineArcLineSegment2D)lo[key][i].Geometry).Start; var b = ((LineArcLineSegment2D)hi[key][i].Geometry).Start;
                    var p = ((LineArcLineSegment2D)actual[key][i].Geometry).Start; var t = station / h;
                    if (Math.Abs(p.X - (a.X + (b.X - a.X) * t)) + Math.Abs(p.Y - (a.Y + (b.Y - a.Y) * t)) > 1e-6)
                        return Fail("a drafted boundary changes its supporting planes near height " + station);
                }
            intervals++;
        }
        var regions = new List<RibLoftRegion>();
        foreach (var outer in lower.Arrangement.ResultLoops.Where(l => l.IsOuter))
        {
            var exterior = LoopProfile(outer);
            var loops = new[] { outer }.Concat(lower.Arrangement.ResultLoops.Where(l => !l.IsOuter
                && ProfileArrangementBuilder.PointInProfile(exterior, ((LineArcLineSegment2D)l.Fragments[0].Geometry).Start) == ArrangementPointLocation.Inside)).ToArray();
            regions.Add(new(loops.Select(l =>
            {
                var key = Key(l); var a = lo[key]; var b = hi[key];
                return new RibLoftLoop(a.Select(f => { var p = ((LineArcLineSegment2D)f.Geometry).Start; return new ProfilePoint2D(p.X, p.Y); }).ToArray(),
                    b.Select(f => { var p = ((LineArcLineSegment2D)f.Geometry).Start; return new ProfilePoint2D(p.X, p.Y); }).ToArray(), a.Select(f => f.Source.Operation).ToArray());
            }).ToArray()));
        }
        var loft = BrepRuledRibNetwork.Build(regions, h);
        return loft.IsSuccess ? KernelResult<RibNetworkPlan>.Success(new(strategy, loft.Value.Body, loft.Value.SideOwners, null, new(strategy, evidence, intervals)))
            : KernelResult<RibNetworkPlan>.Failure(loft.Diagnostics);
    }

    internal static ResolvedProfile2D Profile(PlanarPathFeature feature, double z)
    {
        var loops = BrepPlanarPathFeature.Footprint(feature, z);
        return new(feature.Name, "XY", loops.Select((loop, k) => new ResolvedProfileLoop2D(k == 0 ? "Outer" : "Inner" + k, k == 0,
            // Section-arrangement source holes use CCW orientation.
            (k == 0 ? loop : loop.Reverse().Select(Reverse).ToArray()).Select((e, i) => new ResolvedProfileSegment2D("Side" + i,
                Curve(e), new("profile:" + feature.Name + "." + (k == 0 ? "Outer" : "Inner" + k) + ".Side" + i, "guide:" + feature.Name, "feature:" + feature.Name, "RibFootprint", "XY"))).ToArray())).ToArray());
    }
    private static PlanarBandSpan Reverse(PlanarBandSpan e) => e with { Start = e.End, End = e.Start, StartAngle = e.StartAngle + e.Sweep, Sweep = -e.Sweep };
    private static LineArcProfileCurve2D Curve(PlanarBandSpan e) => e.Radius == 0 ? new LineArcLineSegment2D((e.Start.X, e.Start.Y), (e.End.X, e.End.Y))
        : new LineArcCircularArc2D((e.Center.X, e.Center.Y), e.Radius, e.StartAngle, e.Sweep);
    private static ResolvedProfile2D Rectangle(string name, double x, double y)
    {
        (double X, double Y)[] p = [(-x / 2, -y / 2), (x / 2, -y / 2), (x / 2, y / 2), (-x / 2, y / 2)];
        return new(name, "XY", [new("Outer",true,p.Select((a,i)=>new ResolvedProfileSegment2D("Side"+i,new LineArcLineSegment2D(a,p[(i+1)%4]),
            new("profile:"+name+".Outer.Side"+i,name,"generated","AttachmentBase","XY"))).ToArray())]);
    }
    internal static (double MinX, double MinY, double MaxX, double MaxY) Bounds(ResolvedProfile2D profile)
    {
        var points = profile.Loops.SelectMany(l => l.Segments).SelectMany(s => Extrema(s.Geometry)).ToArray();
        return (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    }
    private static IEnumerable<(double X, double Y)> Extrema(LineArcProfileCurve2D curve)
    {
        if (curve is LineArcLineSegment2D l) { yield return l.Start; yield return l.End; yield break; }
        var a = (LineArcCircularArc2D)curve; var from = Math.Min(a.StartAngleRadians, a.StartAngleRadians + a.SweepAngleRadians); var to = Math.Max(a.StartAngleRadians, a.StartAngleRadians + a.SweepAngleRadians);
        yield return (a.Center.X + a.Radius * Math.Cos(from), a.Center.Y + a.Radius * Math.Sin(from)); yield return (a.Center.X + a.Radius * Math.Cos(to), a.Center.Y + a.Radius * Math.Sin(to));
        for (var i = Math.Ceiling(from / (Math.PI / 2)); i <= Math.Floor(to / (Math.PI / 2)); i++) yield return (a.Center.X + a.Radius * Math.Cos(i * Math.PI / 2), a.Center.Y + a.Radius * Math.Sin(i * Math.PI / 2));
    }
    private static ProfileArrangementResult Arrange(IReadOnlyList<PlanarPathFeature> ribs, double z)
    {
        var profiles = ribs.ToDictionary(r => r.Name, r => Profile(r, z), StringComparer.Ordinal);
        return ProfileArrangementBuilder.Compose("XY", ribs.Select(r => new PrismaticProfileOperation(r.Name, PrismaticProfileIntent.Add, r.Name, 0, r.Extent, "Rib", "feature:" + r.Name)).ToArray(), profiles, "RibNetwork@" + z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }
    private static ResolvedProfile2D LoopProfile(ArrangementLoop2D loop) => new("loop", "XY", [new("Outer", true, loop.Fragments.Select((f, i) => new ResolvedProfileSegment2D("s" + i, f.Geometry, f.Source.Provenance)).ToArray())]);
    private static ArrangementFragment2D[] Rotate(ArrangementLoop2D loop)
    {
        var f = loop.Fragments; var starts = Enumerable.Range(0, f.Count).OrderBy(i => string.Join("|", Enumerable.Range(0, f.Count).Select(j => f[(i + j) % f.Count].Source.StableId)), StringComparer.Ordinal).ToArray();
        return Enumerable.Range(0, f.Count).Select(j => f[(starts[0] + j) % f.Count]).ToArray();
    }
    private static string Key(ArrangementLoop2D loop) => (loop.IsOuter ? "outer:" : "inner:") + string.Join("|", Rotate(loop).Select(f => f.Source.StableId));
    private static Dictionary<string, ArrangementFragment2D[]> Canonical(ProfileArrangement2D arrangement) => arrangement.ResultLoops.ToDictionary(Key, Rotate, StringComparer.Ordinal);

    // All section supports are n.x = c0+c1*z. Their pair/triple coincidences
    // enumerate every height at which the finite polygon arrangement can change.
    private static double[] EventStations(IReadOnlyList<PlanarPathFeature> ribs, double h)
    {
        var planes = new List<(double X, double Y, double C, double Rate)>();
        foreach (var r in ribs)
        {
            var a = Profile(r, 0).Loops[0].Segments; var b = Profile(r, h).Loops[0].Segments;
            for (var i = 0; i < a.Count; i++)
            {
                var l = (LineArcLineSegment2D)a[i].Geometry; var u = (LineArcLineSegment2D)b[i].Geometry;
                var dx = l.End.X - l.Start.X; var dy = l.End.Y - l.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
                var nx = dy / length; var ny = -dx / length; var c = nx * l.Start.X + ny * l.Start.Y;
                planes.Add((nx, ny, c, (nx * u.Start.X + ny * u.Start.Y - c) / h));
            }
        }
        var values = new List<double> { 0, h };
        void Add(double z) { if (double.IsFinite(z) && z > 1e-7 && z < h - 1e-7) values.Add(z); }
        for (var i = 0; i < planes.Count; i++) for (var j = i + 1; j < planes.Count; j++)
        {
            var a = planes[i]; var b = planes[j]; var det = a.X * b.Y - a.Y * b.X;
            if (Math.Abs(det) < 1e-10)
            {
                var sign = a.X * b.X + a.Y * b.Y >= 0 ? 1d : -1d; var rate = a.Rate - sign * b.Rate;
                if (Math.Abs(rate) > 1e-12) Add((sign * b.C - a.C) / rate);
                continue;
            }
            var x = (a.C * b.Y - a.Y * b.C) / det; var y = (a.X * b.C - a.C * b.X) / det;
            var vx = (a.Rate * b.Y - a.Y * b.Rate) / det; var vy = (a.X * b.Rate - a.Rate * b.X) / det;
            for (var k = j + 1; k < planes.Count; k++)
            {
                var c = planes[k]; var rate = c.X * vx + c.Y * vy - c.Rate;
                if (Math.Abs(rate) > 1e-12) Add((c.C - c.X * x - c.Y * y) / rate);
            }
        }
        return values.Order().Aggregate(new List<double>(), (list, value) => { if (list.Count == 0 || value - list[^1] > 1e-7) list.Add(value); return list; }).ToArray();
    }
    internal static KernelResult<T> Failure<T>(string message) => KernelResult<T>.Failure([
        new(Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,Core.Diagnostics.KernelDiagnosticSeverity.Error,"rib-network-rejected: "+message,"Firmament.RibNetwork")]);
}
