using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Blends;

public enum BlendSection { Round, Chamfer }

public enum BlendFaceRole { Primary, Strip, Joiner }

/// <summary>One reading of a face as a blend: the two faces it spans, its section, and whether the edge it rounds
/// is convex (a round: the ball centre is inside the material; a chamfer: measured from its rails).</summary>
public sealed record BlendHypothesis(FaceId Face, FaceId AnchorA, FaceId AnchorB, BlendSection Section, bool Convex)
{
    public string Id => $"{Section.ToString().ToLowerInvariant()}{(Section == BlendSection.Round ? Convex ? "-convex" : "-concave" : "")}:{AnchorA.Value}+{AnchorB.Value}";
}

/// <summary>
/// What was measured for a hypothesis, before any judgment. A round: the radius whose ball, centred a radius
/// behind each sample of the face along its normal, touches both anchors; the residual is the worst miss.
/// A chamfer: each rail's constant distance to the other anchor; the residual is the worst spread.
/// </summary>
public sealed record BlendMeasurement(
    BlendHypothesis Hypothesis,
    bool AnchorsAnalytic,
    double Size,
    double SecondSize,
    double Residual,
    double Coverage,
    double CollapseMargin,
    double SpineExtent,
    double AnchorTurnDegrees,
    bool BothRailsAlongEdges,
    int Samples,
    double Lean = 1);

public sealed record BlendRecognitionOptions
{
    /// <summary>Largest round radius or chamfer setback read as a blend (a policy, not geometry).</summary>
    public double MaxSize { get; init; } = 5;
    /// <summary>Certificate tolerance: an approximating net (another kernel's B-spline fillet) misses by this.</summary>
    public double Tolerance { get; init; } = 1e-3;
    public double TangentDegrees { get; init; } = 3;
    public int MaxSamples { get; init; } = 64;

    public double Limit(double size) => double.Max(Tolerance, 1e-4 * size);
}

public sealed record FaceBlendRecognition(FaceId Face, BlendFaceRole Role, BlendMeasurement? Blend, JudgmentModelResult<BlendMeasurement>? Judgment, string Reason);

/// <summary>
/// Recognizes blend faces by the rolling-ball certificate, independent of how the face is represented: an
/// analytic cylinder or torus, or another kernel's B-spline net, certifies the same way. Which neighbours a
/// face spans is a judgment: every pair is measured first, then a <see cref="JudgmentModel{TCandidate,TContext}"/>
/// admits the readings whose measurements pass and ranks them. Nothing is built.
/// </summary>
public static class BlendRecognition
{
    public const string PolicyVersion = "blend-recognition/1";

    private const string JoinerReason = "smooth all round and meets a strip";

    /// <summary>"Not bounded" as a finite number: the judgment rejects non-finite evidence by design.</summary>
    private const double Unbounded = 1e300;

    public static IReadOnlyList<FaceBlendRecognition> Recognize(BrepBody body, BlendRecognitionOptions? options = null)
    {
        options ??= new();
        var tess = BrepDisplayTessellator.Tessellate(body);
        if (!tess.IsSuccess) throw new InvalidOperationException("blend-recognition: tessellation failed: " + string.Join("; ", tess.Diagnostics.Select(d => d.Message)));
        var context = new Context(body, tess.Value, options);
        var results = body.Topology.Faces.OrderBy(f => f.Id.Value).Select(f => RecognizeStrip(context, f.Id, new HashSet<FaceId>())).ToDictionary(r => r.Face);
        // An anchor is a face that stays. Joiners (corner patches: smooth all round, meeting a strip, spanning no
        // two faces of their own) are decided from the strips; a strip read against a joiner or another strip (a
        // transition patch rolling on a fillet of its own radius) is judged again without them, until nothing changes.
        for (var pass = 0; ; pass++)
        {
            foreach (var r in results.Values.Where(r => r.Role == BlendFaceRole.Joiner).ToArray())
                results[r.Face] = r with { Role = BlendFaceRole.Primary, Reason = r.Reason.Replace(JoinerReason, "").TrimStart(';', ' ') };
            // A face some strip spans stays: it is an anchor, never a corner patch, however smooth it is.
            var spanned = results.Values.Where(r => r.Blend is not null).SelectMany(r => new[] { r.Blend!.Hypothesis.AnchorA, r.Blend.Hypothesis.AnchorB }).ToHashSet();
            foreach (var face in results.Values.Where(r => r.Role == BlendFaceRole.Primary).ToArray())
            {
                if (context.Kind(face.Face) == SurfaceGeometryKind.Plane || spanned.Contains(face.Face)) continue;
                var edges = context.Edges(face.Face);
                if (edges.Length == 0 || !edges.All(e => context.Tangent(e, face.Face))) continue;
                if (!edges.Any(e => results[context.Other(e, face.Face)].Role == BlendFaceRole.Strip)) continue;
                results[face.Face] = face with { Role = BlendFaceRole.Joiner, Reason = JoinerReason + (face.Reason.Length > 0 ? $"; {face.Reason}" : "") };
            }
            var blends = results.Values.Where(r => r.Role != BlendFaceRole.Primary).Select(r => r.Face).ToHashSet();
            var stale = results.Values.Where(r => r.Blend is { } b && (blends.Contains(b.Hypothesis.AnchorA) || blends.Contains(b.Hypothesis.AnchorB))).ToArray();
            if (stale.Length == 0) break;
            if (pass == 8) throw new InvalidOperationException($"blend-recognition: anchors did not settle after {pass} passes (faces {string.Join(',', stale.Select(r => r.Face.Value))})");
            foreach (var r in stale) results[r.Face] = RecognizeStrip(context, r.Face, blends);
        }
        return results.Values.OrderBy(r => r.Face.Value).ToArray();
    }

    private static readonly JudgmentModel<BlendMeasurement, BlendRecognitionOptions> Model = new(PolicyVersion,
    [
        new("anchors-analytic", (m, _) => new(m.AnchorsAnalytic, "blend-anchor-not-analytic", m.AnchorsAnalytic ? 1 : 0, 1, "both anchors have a distance function")),
        new("coverage", (m, _) => new(m.Coverage >= 0.8, "blend-certificate-coverage", m.Coverage, 0.8, "fraction of samples whose ball touches an anchor")),
        new("size", (m, o) => new(m.Size > 0 && double.Max(m.Size, m.SecondSize) <= o.MaxSize + o.Limit(o.MaxSize), "blend-size-over-limit", double.Max(m.Size, m.SecondSize), o.MaxSize, "radius or setback within the policy limit (a measured size is known to the certificate's tolerance)")),
        new("residual", (m, o) => new(m.Residual <= o.Limit(m.Size), "blend-certificate-residual", m.Residual, o.Limit(m.Size), "worst miss of the certificate, mm")),
        new("offset-not-collapsed", (m, o) => new(m.CollapseMargin > CollapseLimit(m, o), "blend-anchor-offset-collapsed", m.CollapseMargin, CollapseLimit(m, o), "an anchor of the ball's own radius (within 1%: approximating nets differ) is a sibling blend, not a face the ball rolls on")),
        new("spine-is-a-curve", (m, o) => new(m.Hypothesis.Section == BlendSection.Chamfer || m.SpineExtent > 10 * o.Limit(m.Size), "blend-spine-is-a-point", m.SpineExtent, 10 * o.Limit(m.Size), "a ball that does not move is a corner, not a strip")),
        new("chamfer-leans", (m, _) => new(m.Lean > 0.05, "blend-chamfer-does-not-lean", m.Lean, 0.05, "a chamfer's outward normal leans towards both anchors' (cosine); a wall square to them is not one")),
        new("anchors-turn-at-all", (m, _) => new(m.AnchorTurnDegrees > 1, "blend-anchors-do-not-turn", m.AnchorTurnDegrees, 1, "degrees between the anchors: tangent anchors have no edge to blend")),
        new("anchors-turn", (m, _) => new(m.AnchorTurnDegrees < 179, "blend-full-round", m.AnchorTurnDegrees, 179, "section sweep (round) or anchor turn (chamfer), degrees: a full round between facing walls has no sharp edge")),
    ],
    [
        new("certificate", 3, (m, o) => new(Utility.Clamp01(1 - m.Residual / o.Limit(m.Size)), m.Residual, "mm", "residual against its limit")),
        new("rails-along-edges", 2, (m, _) => new(m.BothRailsAlongEdges ? 1 : 0.5, m.BothRailsAlongEdges ? 1 : 0, "", "both rails are edges (else a point contact)")),
        new("coverage", 1, (m, _) => new(m.Coverage, m.Coverage, "", "samples certified")),
    ]);

    private static double CollapseLimit(BlendMeasurement m, BlendRecognitionOptions o) => double.Max(o.Limit(m.Size), 0.01 * m.Size);

    private static FaceBlendRecognition RecognizeStrip(Context c, FaceId face, IReadOnlySet<FaceId> notAnchors)
    {
        SurfaceGeometryKind? kind = c.Kind(face);
        var edges = c.Edges(face);
        var edgeNeighbours = edges.Select(e => c.Other(e, face)).Where(n => n != face).Distinct().ToArray();
        var hypotheses = new List<BlendHypothesis>();
        if (kind is { } k && k != SurfaceGeometryKind.Plane)
        {
            // A round's first anchor is a face it is tangent to along an edge; the second may touch it only at a
            // vertex (a ball whose rail on that side has shrunk to a point).
            var tangent = edges.Where(e => c.Tangent(e, face)).Select(e => c.Other(e, face)).Where(n => n != face).Distinct().ToArray();
            var others = edgeNeighbours.Concat(c.VertexNeighbours(face)).Distinct().ToArray();
            foreach (var a in tangent)
                foreach (var b in others.Where(b => b != a && (!tangent.Contains(b) || a.Value < b.Value)))
                    foreach (var convex in new[] { true, false })
                        hypotheses.Add(new(face, a, b, BlendSection.Round, convex));
        }
        if (kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cone && edgeNeighbours.Length <= 12)
            foreach (var a in edgeNeighbours)
                foreach (var b in edgeNeighbours.Where(b => b.Value > a.Value))
                    hypotheses.Add(new(face, a, b, BlendSection.Chamfer, true));
        hypotheses.RemoveAll(h => notAnchors.Contains(h.AnchorA) || notAnchors.Contains(h.AnchorB));
        if (hypotheses.Count == 0)
            return new(face, BlendFaceRole.Primary, null, null, kind == SurfaceGeometryKind.Plane || edges.Length == 0 ? ""
                : $"no tangent neighbour (closest {edges.Min(e => c.Angle(e, face)):F1}° over a {c.Options.TangentDegrees:F1}° limit)");
        var measured = hypotheses.Select(h => Measure(c, h)).ToArray();
        var judgment = Model.Evaluate(c.Options, measured.Select(m => new JudgmentOption<BlendMeasurement>(m.Hypothesis.Id, m)).ToArray());
        return judgment.Winner is { } winner
            ? new(face, BlendFaceRole.Strip, winner.Value, judgment, winner.Value.Hypothesis.Id)
            : new(face, BlendFaceRole.Primary, null, judgment, "no reading admitted");
    }

    /// <summary>Measure one hypothesis. Rounds use the face's own samples (points and normals); chamfers its rails.</summary>
    public static BlendMeasurement Measure(BrepBody body, BlendHypothesis hypothesis, BlendRecognitionOptions? options = null)
    {
        var tess = BrepDisplayTessellator.Tessellate(body);
        if (!tess.IsSuccess) throw new InvalidOperationException("blend-recognition: tessellation failed");
        return Measure(new Context(body, tess.Value, options ?? new()), hypothesis);
    }

    private static BlendMeasurement Measure(Context c, BlendHypothesis h)
    {
        var a = BlendSupport.Of(c.Body, h.AnchorA);
        var b = BlendSupport.Of(c.Body, h.AnchorB);
        var railsOnEdges = c.SharesEdge(h.Face, h.AnchorA) && c.SharesEdge(h.Face, h.AnchorB);
        if (a is null || b is null) return new(h, false, 0, 0, Unbounded, 0, Unbounded, 0, 0, railsOnEdges, 0);
        return h.Section == BlendSection.Round ? MeasureRound(c, h, a, b, railsOnEdges) : MeasureChamfer(c, h, a, b);
    }

    private static BlendMeasurement MeasureRound(Context c, BlendHypothesis h, BlendSupport a, BlendSupport b, bool railsOnEdges)
    {
        var samples = c.Samples(h.Face);
        var side = h.Convex ? 1d : -1d;
        var reach = 2 * c.Options.MaxSize;
        // A coarse look first: most pairs are nowhere near a ball, and need not be measured in full.
        var coarse = samples.Where((_, i) => i % double.Max(1, samples.Length / 8) == 0).ToArray();
        var (r0, cov0) = Radius(coarse);
        if (r0 is { } guess && Residual(coarse, guess) > 50 * c.Options.Limit(guess))
            return Result(guess, Residual(coarse, guess), cov0, coarse);
        var (r, coverage) = Radius(samples);
        if (r is not { } radius) return new(h, true, 0, 0, Unbounded, coverage, Unbounded, 0, 0, railsOnEdges, samples.Length);
        return Result(radius, Residual(samples, radius), coverage, samples);

        BlendMeasurement Result(double radius, double residual, double coverage, (Point3D P, Vector3D N)[] used)
        {
            var collapse = new[] { a.ConstantRadius, b.ConstantRadius }.Where(x => x is not null).Select(x => double.Abs(x!.Value - radius)).DefaultIfEmpty(Unbounded).Min();
            var centres = used.Select(s => s.P - s.N * (side * radius)).ToArray();
            var extent = centres.Length < 2 ? 0 : centres.Max(p => (p - centres[0]).Length);
            // The section's sweep: the angle between the ball's two feet, 90° at a box edge, 180° for a full round
            // between facing walls. Support normals are unsigned, so each foot is on the side that reaches its face.
            var middle = centres.Length > 0 ? centres[centres.Length / 2] : Point3D.Origin;
            Vector3D Foot(BlendSupport s) { var n = s.Normal(middle); return s.Distance(middle + n * radius) <= s.Distance(middle - n * radius) ? n : n * -1; }
            var (footA, footB) = (Foot(a), Foot(b));
            var sweep = double.Acos(double.Clamp(footA.Dot(footB), -1, 1)) * 180 / double.Pi;
            return new(h, true, radius, 0, residual, coverage, collapse, extent, sweep, railsOnEdges, used.Length);
        }

        double Residual((Point3D P, Vector3D N)[] used, double radius) => used.Length == 0 ? Unbounded : used.Max(s =>
        {
            var centre = s.P - s.N * (side * radius);
            return double.Max(double.Abs(a.Distance(centre) - radius), double.Abs(b.Distance(centre) - radius));
        });

        (double? Radius, double Coverage) Radius((Point3D P, Vector3D N)[] used)
        {
            // Per sample, from the better-conditioned anchor: the one whose normal is furthest from the face's.
            var radii = new List<double>();
            foreach (var (p, n) in used)
            {
                var rateA = 1 - double.Abs(a.Normal(p).Dot(n));
                var rateB = 1 - double.Abs(b.Normal(p).Dot(n));
                var r = Root(rateA >= rateB ? a : b, p, n) ?? Root(rateA >= rateB ? b : a, p, n);
                if (r is { } value) radii.Add(value);
            }
            if (radii.Count == 0) return (null, 0);
            radii.Sort();
            return (radii[radii.Count / 2], radii.Count / (double)used.Length);
        }

        double? Root(BlendSupport support, Point3D p, Vector3D n)
        {
            double G(double r) => support.Distance(p - n * (side * r)) - r;
            var start = G(0);
            if (start < 1e-9) return null; // on the anchor: its own rail, no information
            const int steps = 96;
            var previous = 0d;
            for (var k = 1; k <= steps; k++)
            {
                var r = reach * k / steps;
                if (G(r) < 0)
                {
                    var (lo, hi) = (previous, r);
                    for (var i = 0; i < 60; i++) { var mid = 0.5 * (lo + hi); if (G(mid) < 0) hi = mid; else lo = mid; }
                    return 0.5 * (lo + hi);
                }
                previous = r;
            }
            return null;
        }
    }

    private static BlendMeasurement MeasureChamfer(Context c, BlendHypothesis h, BlendSupport a, BlendSupport b)
    {
        // A constant chamfer's rail on one anchor keeps a constant distance to the other.
        var railA = c.Rail(h.Face, h.AnchorA).Select(b.Distance).ToArray();
        var railB = c.Rail(h.Face, h.AnchorB).Select(a.Distance).ToArray();
        if (railA.Length == 0 || railB.Length == 0) return new(h, true, 0, 0, Unbounded, 0, Unbounded, 0, 0, false, 0);
        var (sa, sb) = (railA.Average(), railB.Average());
        var spread = double.Max(railA.Max(x => double.Abs(x - sa)), railB.Max(x => double.Abs(x - sb)));
        // The face itself must run from rail to rail: every sample lies between the anchors' sides, nearer than the sizes.
        var samples = c.Samples(h.Face);
        var inside = samples.Count(s => a.Distance(s.P) <= sb + c.Options.Limit(sb) && b.Distance(s.P) <= sa + c.Options.Limit(sa)) / (double)double.Max(1, samples.Length);
        // Outward normals where the rails are: the face against each anchor, and the anchors against each other.
        var pa = c.Rail(h.Face, h.AnchorA).ElementAt(c.Rail(h.Face, h.AnchorA).Count() / 2);
        var pb = c.Rail(h.Face, h.AnchorB).ElementAt(c.Rail(h.Face, h.AnchorB).Count() / 2);
        if (c.Outward(h.Face, pa) is not { } fa || c.Outward(h.Face, pb) is not { } fb || c.Outward(h.AnchorA, pa) is not { } na || c.Outward(h.AnchorB, pb) is not { } nb)
            return new(h, true, sa, sb, spread, inside, Unbounded, Unbounded, 0, true, railA.Length + railB.Length, 0);
        var turn = double.Acos(double.Clamp(na.Dot(nb), -1, 1)) * 180 / double.Pi;
        var lean = double.Min(fa.Dot(na), fb.Dot(nb));
        // Across a convex edge the other anchor's rail lies behind this anchor's outward normal.
        var convex = (pb - pa).Dot(na) < 0;
        return new(h with { Convex = convex }, true, sa, sb, spread, inside, Unbounded, Unbounded, turn, true, railA.Length + railB.Length, lean);
    }

    private sealed class Context
    {
        private readonly Dictionary<FaceId, DisplayFaceMeshPatch[]> _patches;
        private readonly Dictionary<EdgeId, IReadOnlyList<Point3D>> _polylines;
        private readonly Dictionary<EdgeId, FaceId[]> _edgeFaces = new();
        private readonly Dictionary<FaceId, EdgeId[]> _faceEdges = new();
        private readonly Dictionary<VertexId, HashSet<FaceId>> _vertexFaces = new();
        private readonly Dictionary<(EdgeId, FaceId), double> _angle = new();
        private readonly Dictionary<FaceId, (Point3D P, Vector3D N)[]> _samples = new();
        private readonly Dictionary<FaceId, BlendSupport?> _supports = new();
        private readonly Dictionary<(EdgeId, FaceId), (Point3D P, Vector3D N)[]?> _traces = new();

        public Context(BrepBody body, DisplayTessellationResult mesh, BlendRecognitionOptions options)
        {
            Body = body; Options = options;
            _patches = mesh.FacePatches.GroupBy(p => p.FaceId).ToDictionary(g => g.Key, g => g.ToArray());
            _polylines = mesh.EdgePolylines.ToDictionary(p => p.EdgeId, p => p.Points);
            var t = body.Topology;
            foreach (var face in t.Faces)
            {
                var edges = face.LoopIds.SelectMany(l => t.GetLoop(l).CoedgeIds).Select(c => t.GetCoedge(c).EdgeId).Distinct().ToArray();
                _faceEdges[face.Id] = edges;
                foreach (var e in edges)
                {
                    _edgeFaces[e] = _edgeFaces.TryGetValue(e, out var list) ? [.. list, face.Id] : [face.Id];
                    var edge = t.GetEdge(e);
                    foreach (var v in new[] { edge.StartVertexId, edge.EndVertexId })
                    {
                        if (!_vertexFaces.TryGetValue(v, out var set)) _vertexFaces[v] = set = [];
                        set.Add(face.Id);
                    }
                }
            }
        }

        public BrepBody Body { get; }
        public BlendRecognitionOptions Options { get; }

        public SurfaceGeometryKind? Kind(FaceId f) => Body.TryGetFaceSurfaceGeometry(f, out var s) && s is not null ? s.Kind : null;

        public EdgeId[] Edges(FaceId f) => _faceEdges[f].Where(e => _edgeFaces[e].Length == 2).ToArray();

        public FaceId Other(EdgeId e, FaceId f) => _edgeFaces[e][0] == f ? _edgeFaces[e][1] : _edgeFaces[e][0];

        public bool SharesEdge(FaceId f, FaceId g) => Edges(f).Any(e => Other(e, f) == g);

        public FaceId[] VertexNeighbours(FaceId f) => _faceEdges[f].Select(Body.Topology.GetEdge).SelectMany(e => new[] { e.StartVertexId, e.EndVertexId })
            .SelectMany(v => _vertexFaces[v]).Where(g => g != f).Distinct().ToArray();

        /// <summary>G1 across the edge: the faces' mesh normals agree at interior points of the edge, up to sign (a
        /// freeform face's display normals need not be oriented, and a valid solid never folds back on itself).</summary>
        public bool Tangent(EdgeId e, FaceId f) => Angle(e, f) <= Options.TangentDegrees;

        /// <summary>Worst angle between the two faces' normals along the edge, degrees.</summary>
        public double Angle(EdgeId e, FaceId f)
        {
            if (_angle.TryGetValue((e, f), out var known)) return known;
            var g = Other(e, f);
            // A freeform side is measured on the edge itself, through its trim curve, when it has one: its nearest
            // display vertex can be a grid step away, which on a small fillet is several degrees.
            var trace = Traced(e, f) ?? Traced(e, g);
            var points = trace?.Select(x => x.P).ToArray() ?? Interior(e);
            var worst = points.Length == 0 ? 180d : points.Max(p =>
            {
                var (na, nb) = (NormalOn(f, e, p), NormalOn(g, e, p));
                return na is { } x && nb is { } y && x.Length > 0 && y.Length > 0
                    ? double.Acos(double.Clamp(double.Abs(x.Dot(y)) / (x.Length * y.Length), 0, 1)) * 180 / double.Pi : 180;
            });
            return _angle[(e, f)] = _angle[(e, g)] = worst;
        }

        /// <summary>The face's normal at a point of the edge: exact where the face is analytic, through the edge's trim
        /// curve where it is freeform and has one (nearest traced sample), else its display mesh.</summary>
        private Vector3D? NormalOn(FaceId f, EdgeId e, Point3D p)
        {
            if (Traced(e, f) is { } trace) return trace.MinBy(x => (x.P - p).LengthSquared).N;
            return SurfaceNormal(f, p);
        }

        /// <summary>Points and normals of a freeform face along an edge, from the coedge's trim curve; null for an
        /// analytic face or one without a trim curve on that edge.</summary>
        private (Point3D P, Vector3D N)[]? Traced(EdgeId e, FaceId f)
        {
            if (_traces.TryGetValue((e, f), out var known)) return known;
            (Point3D P, Vector3D N)[]? result = null;
            if (Body.TryGetFaceSurfaceGeometry(f, out var surface) && surface?.BSplineSurfaceWithKnots is { } spline)
            {
                var t = Body.Topology;
                var coedge = t.GetFace(f).LoopIds.SelectMany(l => t.GetLoop(l).CoedgeIds).Select(t.GetCoedge).FirstOrDefault(c => c.EdgeId == e);
                if (coedge is not null && Body.Bindings.TryGetPcurveBinding(coedge.Id, out var binding))
                {
                    var d = binding.Pcurve.Domain;
                    result = Enumerable.Range(1, 7).Select(i =>
                    {
                        var uv = binding.Pcurve.Evaluate(d.Start + (d.End - d.Start) * i / 8d);
                        return (spline.Evaluate(uv.U, uv.V), SplineNormal(spline, uv.U, uv.V));
                    }).ToArray();
                }
            }
            return _traces[(e, f)] = result;
        }

        private static Vector3D SplineNormal(Geometry.Surfaces.BSplineSurfaceWithKnots s, double u, double v)
        {
            var du = double.Max((s.DomainEndU - s.DomainStartU) * 1e-5, 1e-9);
            var dv = double.Max((s.DomainEndV - s.DomainStartV) * 1e-5, 1e-9);
            double U(double x) => double.Clamp(x, s.DomainStartU, s.DomainEndU);
            double V(double x) => double.Clamp(x, s.DomainStartV, s.DomainEndV);
            var tu = s.Evaluate(U(u + du), v) - s.Evaluate(U(u - du), v);
            var tv = s.Evaluate(u, V(v + dv)) - s.Evaluate(u, V(v - dv));
            // Normalize by hand: the tangents of a small step are tiny, and TryNormalize's guard reads tiny as zero.
            static Vector3D Unit(Vector3D x) { var l = double.Sqrt(x.X * x.X + x.Y * x.Y + x.Z * x.Z); return l > 0 && double.IsFinite(l) ? new(x.X / l, x.Y / l, x.Z / l) : Vector3D.Zero; }
            return Unit(Unit(tu).Cross(Unit(tv)));
        }

        /// <summary>Points of the rail between the face and an anchor: interior points of their shared edges.</summary>
        public IEnumerable<Point3D> Rail(FaceId f, FaceId anchor) => Edges(f).Where(e => Other(e, f) == anchor).SelectMany(Interior);

        /// <summary>Mesh points with their surface normals, evenly thinned to the sample budget.</summary>
        public (Point3D P, Vector3D N)[] Samples(FaceId f)
        {
            if (_samples.TryGetValue(f, out var known)) return known;
            var all = (_patches.GetValueOrDefault(f) ?? []).SelectMany(p => p.Positions.Zip(p.Normals)).Where(x => x.Second.Length > 1e-12)
                .Select(x => (x.First, x.Second * (1 / x.Second.Length))).ToArray();
            var step = double.Max(1, all.Length / (double)Options.MaxSamples);
            var thinned = Enumerable.Range(0, (int)double.Min(all.Length, Options.MaxSamples)).Select(i => all[(int)(i * step)]).ToArray();
            return _samples[f] = thinned;
        }

        private Point3D[] Interior(EdgeId e)
        {
            if (!_polylines.TryGetValue(e, out var line) || line.Count < 2) return [];
            return line.Count > 2 ? line.Skip(1).Take(line.Count - 2).ToArray() : [line[0] + (line[1] - line[0]) * 0.5];
        }

        public Vector3D? Outward(FaceId f, Point3D p) => SurfaceNormal(f, p) is { } n && n.Length > 1e-12 ? n * (1 / n.Length) : null;

        /// <summary>The exact support normal for an analytic face, oriented by its display mesh; the mesh normal
        /// itself for a freeform face (whose nearest mesh vertex may be a grid step away from the point).</summary>
        private Vector3D? SurfaceNormal(FaceId f, Point3D p)
        {
            var mesh = NearestNormal(f, p);
            if (!_supports.TryGetValue(f, out var support)) _supports[f] = support = BlendSupport.Of(Body, f);
            if (support is null) return mesh;
            var exact = support.Normal(p);
            if (exact.Length < 1e-12) return mesh;
            return mesh is { } m && exact.Dot(m) < 0 ? exact * -1 : exact;
        }

        private Vector3D? NearestNormal(FaceId f, Point3D p)
        {
            Vector3D? best = null; var distance = Unbounded;
            foreach (var patch in _patches.GetValueOrDefault(f) ?? [])
                for (var i = 0; i < patch.Positions.Count; i++)
                {
                    var d = (patch.Positions[i] - p).LengthSquared;
                    if (d < distance) { distance = d; best = patch.Normals[i]; }
                }
            return best;
        }
    }
}
