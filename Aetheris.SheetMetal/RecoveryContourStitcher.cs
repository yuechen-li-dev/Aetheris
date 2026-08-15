using System.Diagnostics;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

public enum RecoveryJunctionKind
{
    ExactEndpointMatch,
    WithinToleranceEndpointMatch,
    PointTangentContinuation,
    TangentContinuation,
    AngularContinuation,
    Ambiguous,
    Rejected
}

public enum RecoveryRepairConfidence { Exact, Strong, Heuristic, Ambiguous }
public enum RecoveredContourAcceptance { Exact, Recovered, RecoveredWithRepairs, NeedsReview, Failed }
public sealed record RecoveryPoint2(double X, double Y);

public sealed record RecoveryJunctionRepair(
    string JunctionId,
    RecoveryJunctionKind Kind,
    RecoveryRepairConfidence Confidence,
    RecoveryPoint2 CanonicalPoint,
    IReadOnlyList<RecoveryPoint2> OriginalEndpoints,
    double MaximumDisplacement,
    IReadOnlyList<string> SourceSegmentIds,
    IReadOnlyList<string> SourceRegionIds,
    string Action,
    string Evidence);

public sealed record RecoveryStitchSummary(
    int RetainedFragmentCount,
    int JunctionCount,
    int AmbiguousJunctionCount,
    int CandidateInterpretationCount,
    int AdmissibleInterpretationCount,
    double MaximumEndpointDisplacement,
    string SelectedInterpretation,
    IReadOnlyList<string> Rejections,
    double EndpointClusteringMilliseconds = 0d,
    double CandidateSelectionMilliseconds = 0d,
    double ContourValidationMilliseconds = 0d);

public sealed record RecoveryContourStitchResult(
    PlanarContour2? Contour,
    RecoveredContourAcceptance Acceptance,
    IReadOnlyList<RecoveryJunctionRepair> Repairs,
    RecoveryStitchSummary Summary,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Recovery-only graph repair over material-classified arrangement fragments. It does
/// not alter the general profile arrangement kernel and never performs a global Boolean.
/// </summary>
public static class RecoveryContourStitcher
{
    public const double ExactEndpointTolerance = 1e-7;
    public const double JunctionTolerance = 0.02;
    public const double MaximumRepairDisplacement = 0.05;
    private const int MaximumInterpretations = 256;

    public static RecoveryContourStitchResult Stitch(ProfileArrangement2D arrangement, string stableId)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        var retained = arrangement.AtomicFragments.Where(x => x.Retained).OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray();
        var collapsed = retained.Where(IsMicroClosure).ToArray();
        var fragments = retained.Except(collapsed).ToArray();
        if (fragments.Length == 0) return Failed("No retained material boundary fragments were available.");

        var clusterClock=Stopwatch.StartNew();
        var endpoints = fragments.SelectMany(x => new[]
        {
            new Endpoint(x, true, Ends(x.Geometry).Start),
            new Endpoint(x, false, Ends(x.Geometry).End)
        }).OrderBy(x => x.Fragment.StableId, StringComparer.Ordinal).ThenBy(x => x.IsStart ? 0 : 1).ToArray();
        var clusters = Cluster(endpoints);
        clusterClock.Stop();
        if (clusters.Any(x => x.MaximumDisplacement > MaximumRepairDisplacement))
            return Failed("A recovery junction exceeded the maximum local displacement.", clusters.Count);

        var nodeByEndpoint = clusters.SelectMany((cluster, index) => cluster.Endpoints.Select(x => (x, index)))
            .ToDictionary(x => EndpointKey(x.x), x => x.index, StringComparer.Ordinal);
        var incoming = new Dictionary<int, ArrangementFragment2D[]>();
        var outgoing = new Dictionary<int, ArrangementFragment2D[]>();
        for (var i = 0; i < clusters.Count; i++)
        {
            incoming[i] = clusters[i].Endpoints.Where(x => !x.IsStart).Select(x => x.Fragment).ToArray();
            outgoing[i] = clusters[i].Endpoints.Where(x => x.IsStart).Select(x => x.Fragment).ToArray();
            if (incoming[i].Length != outgoing[i].Length || incoming[i].Length is < 1 or > 2)
                return Failed($"Junction {i} has unsupported incidence {incoming[i].Length}-in/{outgoing[i].Length}-out.", clusters.Count);
        }

        var ambiguousNodes = Enumerable.Range(0, clusters.Count).Where(i => incoming[i].Length == 2).ToArray();
        var interpretationCount = 1 << ambiguousNodes.Length;
        if (ambiguousNodes.Length >= 31 || interpretationCount > MaximumInterpretations)
            return Failed($"The bounded recovery cap was exceeded ({ambiguousNodes.Length} ambiguous junctions).", clusters.Count);

        var candidateClock=Stopwatch.StartNew();var candidates = new List<Interpretation>();
        for (var mask = 0; mask < interpretationCount; mask++)
        {
            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            var score = 0d;
            for (var node = 0; node < clusters.Count; node++)
            {
                var ins = incoming[node]; var outs = outgoing[node];
                if (ins.Length == 1) { next[ins[0].StableId] = outs[0].StableId; score += PairScore(ins[0], outs[0]); continue; }
                var bit = Array.IndexOf(ambiguousNodes, node);
                var crossed = ((mask >> bit) & 1) != 0;
                next[ins[0].StableId] = outs[crossed ? 1 : 0].StableId;
                next[ins[1].StableId] = outs[crossed ? 0 : 1].StableId;
                score += PairScore(ins[0], outs[crossed ? 1 : 0]) + PairScore(ins[1], outs[crossed ? 0 : 1]);
            }
            var loops = Walk(fragments, next);
            var validation = ValidateInterpretation(loops, fragments.Length, clusters, nodeByEndpoint);
            candidates.Add(new($"pairing-{mask:D3}", mask, loops, validation.IsAdmissible, score, validation.Reason));
        }

        var engine = new JudgmentEngine<IReadOnlyList<Interpretation>>();
        var judgmentCandidates = candidates.Select(x => new JudgmentCandidate<IReadOnlyList<Interpretation>>(
            x.Name, _ => x.IsAdmissible, _ => x.Score, _ => x.RejectionReason)).ToArray();
        var judgment = engine.Evaluate(candidates, judgmentCandidates);
        candidateClock.Stop();
        if (!judgment.IsSuccess)
            return new(null, RecoveredContourAcceptance.Failed, [],
                new(fragments.Length, clusters.Count, ambiguousNodes.Length, interpretationCount, 0,
                    clusters.Max(x => x.MaximumDisplacement), "none", judgment.Rejections.Select(x => $"{x.CandidateName}: {x.Reason}").ToArray()),
                ["No bounded topology/provenance interpretation produced one valid material loop."]);

        var selected = candidates.Single(x => x.Name == judgment.Selection!.Value.Candidate.Name);
        var loop = selected.Loops.Single();
        var repairedGeometry = loop.Select(fragment => Snap(fragment.Geometry,
            clusters[nodeByEndpoint[EndpointKey(new(fragment, true, Ends(fragment.Geometry).Start))]].Canonical,
            clusters[nodeByEndpoint[EndpointKey(new(fragment, false, Ends(fragment.Geometry).End))]].Canonical)).ToArray();
        var segments = loop.Select((fragment, index) => new PlanarContourSegment2(
            $"{fragment.Source.Provenance.StableId}.recovery.{index:D4}", repairedGeometry[index],
            fragment.Source.Provenance with { StableId = $"{fragment.Source.Provenance.StableId}.recovery.{index:D4}", Derivation = $"recovery-stitch:{fragment.StableId}:{fragment.FromParameter:R}..{fragment.ToParameter:R}" })).ToArray();
        var contour = new PlanarContour2(stableId, arrangement.Frame,
            new($"{stableId}.outer", true, segments), [], "topology-guided recovered source boundary");
        var validationClock=Stopwatch.StartNew();var contourValidation = PlanarContourKernel.Validate(contour);validationClock.Stop();
        if (!contourValidation.IsValid)
            return new(null, RecoveredContourAcceptance.Failed, [],
                new(fragments.Length, clusters.Count, ambiguousNodes.Length, interpretationCount, candidates.Count(x => x.IsAdmissible), clusters.Max(x => x.MaximumDisplacement), selected.Name,
                    candidates.Where(x => !x.IsAdmissible).Select(x => $"{x.Name}: {x.RejectionReason}").ToArray()),
                contourValidation.Diagnostics.Select(x => $"{x.Code}: {x.Message}").ToArray());

        var repairs = ambiguousNodes.Select(node => Repair(node, clusters[node], incoming[node], outgoing[node], selected.Mask, ambiguousNodes)).ToArray();
        var collapsedRepairs = collapsed.Select((fragment, index) =>
        {
            var point=Ends(fragment.Geometry).Start;var key=VertexBucketKey(point);var incident=retained.Where(x=>VertexBucketKey(Ends(x.Geometry).Start)==key||VertexBucketKey(Ends(x.Geometry).End)==key).ToArray();
            return new RecoveryJunctionRepair(
            $"junction-micro-closure-{index:D4}", RecoveryJunctionKind.PointTangentContinuation, RecoveryRepairConfidence.Strong,
            new(point.X, point.Y), incident.SelectMany(x=>new[]{Ends(x.Geometry).Start,Ends(x.Geometry).End}).Where(x=>VertexBucketKey(x)==key).Select(x=>new RecoveryPoint2(x.X,x.Y)).ToArray(),
            Length(fragment.Geometry), incident.Select(x=>x.Source.Segment).Distinct().Order().ToArray(), incident.Select(x=>x.Source.Profile).Distinct().Order().ToArray(),
            "collapse bounded single-fragment micro-closure and continue through its tangent junction",
            $"retained analytic split remnant length {Length(fragment.Geometry):G12} mm and area {Math.Abs(Area([fragment])):G12} mm^2; incident directions {string.Join(", ",incident.Select(DirectionEvidence))}; source provenance retained" );
        }).ToArray();
        var collapsedKeys = collapsed.SelectMany(x => new[] { VertexBucketKey(Ends(x.Geometry).Start), VertexBucketKey(Ends(x.Geometry).End) }).ToHashSet(StringComparer.Ordinal);
        var displaced = clusters.Where(x => x.MaximumDisplacement > ExactEndpointTolerance && !collapsedKeys.Contains(VertexBucketKey(x.Canonical))).Select((cluster, index) => new RecoveryJunctionRepair(
            $"junction-snap-{index:D4}", RecoveryJunctionKind.WithinToleranceEndpointMatch, RecoveryRepairConfidence.Strong,
            new(cluster.Canonical.X, cluster.Canonical.Y), cluster.Endpoints.Select(x=>new RecoveryPoint2(x.Point.X,x.Point.Y)).ToArray(), cluster.MaximumDisplacement, cluster.Endpoints.Select(x => x.Fragment.Source.Segment).Distinct().Order().ToArray(),
            cluster.Endpoints.Select(x => x.Fragment.Source.Profile).Distinct().Order().ToArray(), "snap bounded endpoints to a source-supported canonical point",
            $"maximum displacement {cluster.MaximumDisplacement:G12} mm <= {JunctionTolerance:G12} mm recovery tolerance")).ToArray();
        var allRepairs = collapsedRepairs.Concat(repairs).Concat(displaced).OrderBy(x => x.JunctionId, StringComparer.Ordinal).ToArray();
        var acceptance = allRepairs.Length == 0 ? RecoveredContourAcceptance.Recovered : RecoveredContourAcceptance.RecoveredWithRepairs;
        return new(contour, acceptance, allRepairs,
            new(retained.Length, clusters.Count, ambiguousNodes.Length + collapsed.Length, interpretationCount, candidates.Count(x => x.IsAdmissible), Math.Max(clusters.Max(x => x.MaximumDisplacement), collapsed.DefaultIfEmpty().Max(x => x is null ? 0d : Length(x.Geometry))), selected.Name,
                candidates.Where(x => !x.IsAdmissible).Select(x => $"{x.Name}: {x.RejectionReason}").ToArray(),clusterClock.Elapsed.TotalMilliseconds,candidateClock.Elapsed.TotalMilliseconds,validationClock.Elapsed.TotalMilliseconds), []);
    }

    private static RecoveryJunctionRepair Repair(int node, JunctionCluster cluster, ArrangementFragment2D[] incoming, ArrangementFragment2D[] outgoing, int mask, int[] ambiguousNodes)
    {
        var bit = Array.IndexOf(ambiguousNodes, node); var crossed = ((mask >> bit) & 1) != 0;
        var pairs = new[] { (incoming[0], outgoing[crossed ? 1 : 0]), (incoming[1], outgoing[crossed ? 0 : 1]) };
        var tangent = pairs.All(x => TangentAlignment(x.Item1.Geometry, x.Item2.Geometry) >= .999999);
        return new($"junction-{node:D4}", tangent ? RecoveryJunctionKind.PointTangentContinuation : RecoveryJunctionKind.AngularContinuation,
            tangent ? RecoveryRepairConfidence.Strong : RecoveryRepairConfidence.Heuristic, new(cluster.Canonical.X, cluster.Canonical.Y), cluster.Endpoints.Select(x=>new RecoveryPoint2(x.Point.X,x.Point.Y)).ToArray(), cluster.MaximumDisplacement,
            cluster.Endpoints.Select(x => x.Fragment.Source.Segment).Distinct().Order().ToArray(), cluster.Endpoints.Select(x => x.Fragment.Source.Profile).Distinct().Order().ToArray(),
            $"resolve 2-in/2-out angular order with {pairs[0].Item1.Source.Segment}->{pairs[0].Item2.Source.Segment} and {pairs[1].Item1.Source.Segment}->{pairs[1].Item2.Source.Segment}",
            $"bounded global interpretation {mask}; one simple material loop; provenance/tangent utility {pairs.Sum(x => PairScore(x.Item1, x.Item2)):G12}");
    }

    private static (bool IsAdmissible, string Reason) ValidateInterpretation(IReadOnlyList<IReadOnlyList<ArrangementFragment2D>> loops, int fragmentCount,
        IReadOnlyList<JunctionCluster> clusters, IReadOnlyDictionary<string,int> nodeByEndpoint)
    {
        if (loops.Sum(x => x.Count) != fragmentCount) return (false, "not every retained fragment was consumed exactly once");
        if (loops.Count != 1) return (false, $"produced {loops.Count} closed loops [{string.Join(", ", loops.Select(x => $"{Area(x):G12}/{x.Count}seg"))}] instead of one source blank outer loop");
        var segments=loops[0].Select((fragment,index)=>new PlanarContourSegment2($"candidate-{index:D4}",Snap(fragment.Geometry,
            clusters[nodeByEndpoint[EndpointKey(new(fragment,true,Ends(fragment.Geometry).Start))]].Canonical,
            clusters[nodeByEndpoint[EndpointKey(new(fragment,false,Ends(fragment.Geometry).End))]].Canonical),fragment.Source.Provenance with{StableId=$"candidate-{index:D4}"})).ToArray();
        var validation=PlanarContourKernel.Validate(new("candidate","XY",new("candidate.outer",true,segments),[],"recovery candidate"));
        return validation.IsValid?(true,"strictly valid canonical contour"):(false,string.Join("; ",validation.Diagnostics.Select(x=>x.Code).Distinct()));
    }

    private static IReadOnlyList<IReadOnlyList<ArrangementFragment2D>> Walk(ArrangementFragment2D[] fragments, IReadOnlyDictionary<string, string> next)
    {
        var byId = fragments.ToDictionary(x => x.StableId, StringComparer.Ordinal); var used = new HashSet<string>(StringComparer.Ordinal); var loops = new List<IReadOnlyList<ArrangementFragment2D>>();
        foreach (var seed in fragments)
        {
            if (used.Contains(seed.StableId)) continue;
            var chain = new List<ArrangementFragment2D>(); var current = seed;
            for (var guard = 0; guard <= fragments.Length; guard++)
            {
                if (!used.Add(current.StableId)) break;
                chain.Add(current);
                if (!next.TryGetValue(current.StableId, out var nextId) || !byId.TryGetValue(nextId, out current!)) break;
                if (current.StableId == seed.StableId) break;
            }
            loops.Add(chain);
        }
        return loops;
    }

    private static List<JunctionCluster> Cluster(IReadOnlyList<Endpoint> endpoints)
    {
        static string StrictKey(Endpoint x)
        {
            return VertexBucketKey(x.Point);
        }
        var clusters = endpoints.GroupBy(StrictKey, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.ToList()).ToList();
        // Preserve all balanced strict nodes. The recovery tolerance may only close a
        // complementary end/start gap owned by the same imported source profile.
        for (var i = 0; i < clusters.Count; i++)
        {
            if (Balanced(clusters[i])) continue;
            var matches = Enumerable.Range(i + 1, clusters.Count - i - 1)
                .Where(j => !Balanced(clusters[j]) && Complementary(clusters[i], clusters[j]))
                .Select(j => (Index: j, Distance: clusters[i].Min(a => clusters[j].Min(b => Distance(a.Point, b.Point)))))
                .Where(x => x.Distance <= JunctionTolerance).OrderBy(x => x.Distance).ThenBy(x => x.Index).ToArray();
            if (matches.Length == 0) continue;
            var match = matches[0];
            clusters[i].AddRange(clusters[match.Index]); clusters.RemoveAt(match.Index); i--;
        }
        return clusters.Select(x =>
        {
            // Prefer an analytic arc endpoint: lines can be snapped to it without moving circular support.
            var canonical = x.OrderByDescending(y => y.Fragment.Geometry is LineArcCircularArc2D).ThenBy(y => y.Fragment.StableId, StringComparer.Ordinal).ThenBy(y => y.IsStart ? 0 : 1).First().Point;
            return new JunctionCluster(x, canonical, x.Max(y => Distance(y.Point, canonical)));
        }).ToList();
    }

    private static bool Balanced(IReadOnlyList<Endpoint> cluster) => cluster.Count(x => x.IsStart) == cluster.Count(x => !x.IsStart);
    private static bool Complementary(IReadOnlyList<Endpoint> a, IReadOnlyList<Endpoint> b) =>
        a.Concat(b).Select(x => x.Fragment.Source.Profile).Distinct(StringComparer.Ordinal).Count() == 1 &&
        a.Count(x => x.IsStart) + b.Count(x => x.IsStart) == a.Count(x => !x.IsStart) + b.Count(x => !x.IsStart);

    private static double PairScore(ArrangementFragment2D incoming, ArrangementFragment2D outgoing) =>
        (incoming.Source.Profile == outgoing.Source.Profile ? 100d : 0d) +
        (incoming.Source.Operation == outgoing.Source.Operation ? 25d : 0d) + 10d * TangentAlignment(incoming.Geometry, outgoing.Geometry);

    private static bool IsMicroClosure(ArrangementFragment2D fragment) =>
        VertexBucketKey(Ends(fragment.Geometry).Start) == VertexBucketKey(Ends(fragment.Geometry).End) && Length(fragment.Geometry) <= JunctionTolerance;

    private static string VertexBucketKey((double X, double Y) point)
    {
        const double strictVertexBucket = 1e-5;
        static double Bucket(double value) { var rounded = Math.Round(value / strictVertexBucket); return rounded == 0d ? 0d : rounded; }
        return $"{Bucket(point.X):F0},{Bucket(point.Y):F0}";
    }

    private static double Length(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => Distance(line.Start, line.End),
        LineArcCircularArc2D arc => Math.Abs(arc.Radius * arc.SweepAngleRadians),
        _ => double.PositiveInfinity
    };

    private static double Area(IReadOnlyList<ArrangementFragment2D> loop) => loop.Sum(x => x.Geometry switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => ArcArea(arc),
        _ => 0d
    }) / 2d;
    private static double ArcArea(LineArcCircularArc2D arc)
    {
        var a = arc.StartAngleRadians; var b = a + arc.SweepAngleRadians;
        return arc.Center.X * arc.Radius * (Math.Sin(b) - Math.Sin(a)) - arc.Center.Y * arc.Radius * (Math.Cos(b) - Math.Cos(a)) + arc.Radius * arc.Radius * (b - a);
    }

    private static double TangentAlignment(LineArcProfileCurve2D incoming, LineArcProfileCurve2D outgoing)
    {
        var a = Tangent(incoming, false); var b = Tangent(outgoing, true); var al = Math.Sqrt(a.X * a.X + a.Y * a.Y); var bl = Math.Sqrt(b.X * b.X + b.Y * b.Y);
        return al <= ExactEndpointTolerance || bl <= ExactEndpointTolerance ? -1d : (a.X * b.X + a.Y * b.Y) / (al * bl);
    }
    private static string DirectionEvidence(ArrangementFragment2D fragment)
    {
        var a=Tangent(fragment.Geometry,true);var b=Tangent(fragment.Geometry,false);
        return $"{fragment.Source.Segment}:start=({a.X:G6},{a.Y:G6}),end=({b.X:G6},{b.Y:G6})";
    }

    private static LineArcProfileCurve2D Snap(LineArcProfileCurve2D geometry, (double X, double Y) start, (double X, double Y) end) => geometry switch
    {
        LineArcLineSegment2D => new LineArcLineSegment2D(start, end),
        LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius, Math.Atan2(start.Y - arc.Center.Y, start.X - arc.Center.X), ArcSweep(arc, start, end)),
        _ => geometry
    };

    private static double ArcSweep(LineArcCircularArc2D original, (double X, double Y) start, (double X, double Y) end)
    {
        var a = Math.Atan2(start.Y - original.Center.Y, start.X - original.Center.X); var b = Math.Atan2(end.Y - original.Center.Y, end.X - original.Center.X); var sweep = b - a;
        if (original.SweepAngleRadians >= 0d) while (sweep <= 0d) sweep += 2d * Math.PI; else while (sweep >= 0d) sweep -= 2d * Math.PI;
        return sweep;
    }

    private static ((double X, double Y) Start, (double X, double Y) End) Ends(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => (line.Start, line.End),
        LineArcCircularArc2D arc => (At(arc, 0d), At(arc, 1d)),
        _ => throw new NotSupportedException("Recovery stitching supports bounded lines and circular arcs.")
    };
    private static (double X, double Y) At(LineArcCircularArc2D arc, double t) => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians * t), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians * t));
    private static (double X, double Y) Tangent(LineArcProfileCurve2D curve, bool atStart) => curve switch
    {
        LineArcLineSegment2D line => (line.End.X - line.Start.X, line.End.Y - line.Start.Y),
        LineArcCircularArc2D arc => (-Math.Sin(arc.StartAngleRadians + (atStart ? 0d : arc.SweepAngleRadians)) * Math.Sign(arc.SweepAngleRadians), Math.Cos(arc.StartAngleRadians + (atStart ? 0d : arc.SweepAngleRadians)) * Math.Sign(arc.SweepAngleRadians)),
        _ => (0d, 0d)
    };
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static string EndpointKey(Endpoint endpoint) => $"{endpoint.Fragment.StableId}:{(endpoint.IsStart ? 's' : 'e')}";
    private static RecoveryContourStitchResult Failed(string message, int junctionCount = 0) => new(null, RecoveredContourAcceptance.Failed, [], new(0, junctionCount, 0, 0, 0, 0d, "none", [message]), [message]);

    private sealed record Endpoint(ArrangementFragment2D Fragment, bool IsStart, (double X, double Y) Point);
    private sealed record JunctionCluster(IReadOnlyList<Endpoint> Endpoints, (double X, double Y) Canonical, double MaximumDisplacement);
    private sealed record Interpretation(string Name, int Mask, IReadOnlyList<IReadOnlyList<ArrangementFragment2D>> Loops, bool IsAdmissible, double Score, string RejectionReason);
}
