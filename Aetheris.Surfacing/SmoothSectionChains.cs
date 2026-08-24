using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

internal interface ISectionChainTransitionPatch
{
    SurfaceGeometry ExactSurface { get; }
    SurfaceMaterializationKind MaterializationKind { get; }
    Point3D Evaluate(double u, double v);
    BSpline3Curve? LongitudinalBoundary(bool atEndU);
}

internal sealed record RuledSectionChainTransitionPatch(RuledSurfacePatch Patch) : ISectionChainTransitionPatch
{
    public SurfaceGeometry ExactSurface => Patch.ExactSurface;
    public SurfaceMaterializationKind MaterializationKind => Patch.MaterializationKind;
    public Point3D Evaluate(double u, double v) => Patch.Evaluate(u, v);
    public BSpline3Curve? LongitudinalBoundary(bool atEndU) => null;
}

internal sealed record SmoothSectionChainTransitionPatch(BSplineSurfaceWithKnots Spline) : ISectionChainTransitionPatch
{
    public SurfaceGeometry ExactSurface => SurfaceGeometry.FromBSplineSurfaceWithKnots(Spline);
    public SurfaceMaterializationKind MaterializationKind => SurfaceMaterializationKind.ExactPolynomialBSpline;
    public Point3D Evaluate(double u, double v) => Spline.Evaluate(u, v);
    public BSpline3Curve? LongitudinalBoundary(bool atEndU)
    {
        var controls = Spline.ControlPoints[atEndU ? ^1 : 0];
        return new(3, controls, [4, 4], [0d, 1d], "UNSPECIFIED", false, false, "UNSPECIFIED");
    }
}

internal sealed record SmoothSectionChainBuildResult(IReadOnlyList<ISectionChainTransitionPatch[]> Patches,
    SectionChainSmoothSelectionEvidence? Selection, IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0;
}

internal static class SmoothSectionChainBuilder
{
    private static readonly (string Name, double Scale)[] Policies =
    [
        ("ConservativeCompact", 0.5d),
        ("CentripetalLike", 0.75d),
        ("ChordLengthFair", 1d)
    ];

    public static SmoothSectionChainBuildResult Build(SectionChain chain, IReadOnlyList<RuledBoundary[]> boundaries,
        IReadOnlyList<int[]> correspondence)
    {
        var diagnostics = ValidateCompatibility(chain, boundaries, correspondence).ToList();
        if (diagnostics.Count > 0) return new([], null, diagnostics);
        var stations = StationParameters(chain);
        var candidates = Policies.Select(policy => MaterializeCandidate(chain, boundaries, correspondence, stations, policy.Name, policy.Scale)).ToArray();
        var context = new CandidateContext(candidates);
        var judged = new JudgmentEngine<CandidateContext>().Evaluate(context, candidates.Select(candidate =>
            new JudgmentCandidate<CandidateContext>(candidate.Name, _ => candidate.RejectionReason is null,
                _ => candidate.Utility, _ => candidate.RejectionReason ?? "ineligible",
                TieBreakerPriority: Array.FindIndex(Policies, item => item.Name == candidate.Name))).ToArray());
        if (!judged.IsSuccess)
        {
            diagnostics.AddRange(candidates.Where(c => c.RejectionReason is not null).Select(c =>
                new SurfacingDiagnostic(c.RejectionReason!.StartsWith("section-chain-g1-overshoot", StringComparison.Ordinal)
                    ? "section-chain-g1-overshoot" : "section-chain-g1-foldover", $"{c.Name}: {c.RejectionReason}")));
            diagnostics.Add(new("section-chain-g1-no-eligible-solution", "No bounded non-rational cubic tangent policy passed foldover and overshoot eligibility."));
            return new([], Evidence(candidates, "<none>"), diagnostics);
        }
        var selected = candidates.Single(item => item.Name == judged.Selection!.Value.Candidate.Name);
        return new(selected.Patches, Evidence(candidates, selected.Name), []);
    }

    private static IEnumerable<SurfacingDiagnostic> ValidateCompatibility(SectionChain chain, IReadOnlyList<RuledBoundary[]> boundaries,
        IReadOnlyList<int[]> correspondence)
    {
        for (var transition = 0; transition < correspondence.Count; transition++)
        for (var span = 0; span < correspondence[transition].Length; span++)
        {
            var a = ExtractCurveData(boundaries[transition][span]);
            var b = ExtractCurveData(boundaries[transition + 1][correspondence[transition][span]]);
            var identity = $"{chain.Sections[transition].SectionId}->{chain.Sections[transition + 1].SectionId}/{chain.Sections[transition].Profile.Spans[span].SpanId}";
            if (a is null || b is null)
                yield return new("section-chain-g1-degree-limit", $"{identity}: G1 currently admits line and polynomial B-spline profile spans; circular arcs require a future bounded polynomial normalization.");
            else if (a.Degree != b.Degree || !a.Multiplicities.SequenceEqual(b.Multiplicities) || !a.Knots.SequenceEqual(b.Knots))
                yield return new("section-chain-g1-degree-limit", $"{identity}: corresponding spans require equal non-rational degree and knot structure in the bounded G1 lane.");
        }
    }

    private static Candidate MaterializeCandidate(SectionChain chain, IReadOnlyList<RuledBoundary[]> boundaries,
        IReadOnlyList<int[]> correspondence, IReadOnlyList<double> stations, string name, double scale)
    {
        var controls = boundaries.Select(section => section.Select(boundary => ExtractCurveData(boundary)!).ToArray()).ToArray();
        var patches = new List<ISectionChainTransitionPatch[]>();
        for (var transition = 0; transition < chain.Sections.Count - 1; transition++)
        {
            var spanPatches = new ISectionChainTransitionPatch[controls[transition].Length];
            for (var span = 0; span < spanPatches.Length; span++)
            {
                var targetSpan = correspondence[transition][span];
                var source = controls[transition][span]; var target = controls[transition + 1][targetSpan];
                var net = new Point3D[source.Points.Count][];
                var distance = stations[transition + 1] - stations[transition];
                for (var point = 0; point < net.Length; point++)
                {
                    var d0 = Tangent(controls, correspondence, stations, transition, span, point);
                    var d1 = Tangent(controls, correspondence, stations, transition + 1, targetSpan, point);
                    net[point] = [source.Points[point], source.Points[point] + d0 * (distance * scale / 3d),
                        target.Points[point] - d1 * (distance * scale / 3d), target.Points[point]];
                }
                var spline = new BSplineSurfaceWithKnots(source.Degree, 3, net, "UNSPECIFIED", false, false, false,
                    source.Multiplicities, [4, 4], source.Knots, [0d, 1d], "UNSPECIFIED");
                spanPatches[span] = new SmoothSectionChainTransitionPatch(spline);
            }
            patches.Add(spanPatches);
        }
        var flat = patches.SelectMany(item => item).ToArray();
        var foldover = flat.Any(SectionChainMaterializer.HasFoldover);
        var maxOvershoot = MaximumOvershoot(chain, flat);
        var bending = BendingEnergy(flat); var variation = NormalVariation(flat);
        var rejection = foldover ? "section-chain-g1-foldover: sampled Jacobian is singular or reverses orientation"
            : maxOvershoot > 0.55d ? $"section-chain-g1-overshoot: normalized envelope deviation {maxOvershoot:R} exceeds 0.55" : null;
        var utility = rejection is null ? 1d / (1d + bending + 0.25d * variation + 0.1d * maxOvershoot) : 0d;
        return new(name, scale, patches, bending, variation, maxOvershoot, rejection, utility);
    }

    private static Vector3D Tangent(CurveData[][] sections, IReadOnlyList<int[]> maps, IReadOnlyList<double> s,
        int section, int span, int point)
    {
        var current = sections[section][span].Points[point];
        if (section == 0)
        {
            var nextSpan = maps[0][span];
            return (sections[1][nextSpan].Points[point] - current) / (s[1] - s[0]);
        }
        if (section == sections.Length - 1)
        {
            var previousSpan = Array.IndexOf(maps[section - 1], span);
            return (current - sections[section - 1][previousSpan].Points[point]) / (s[section] - s[section - 1]);
        }
        var previous = Array.IndexOf(maps[section - 1], span); var next = maps[section][span];
        var h0 = s[section] - s[section - 1]; var h1 = s[section + 1] - s[section];
        var p0 = sections[section - 1][previous].Points[point]; var p2 = sections[section + 1][next].Points[point];
        return (p0 - Point3D.Origin) * (-h1 / (h0 * (h0 + h1)))
            + (current - Point3D.Origin) * ((h1 - h0) / (h0 * h1))
            + (p2 - Point3D.Origin) * (h0 / (h1 * (h0 + h1)));
    }

    private static IReadOnlyList<double> StationParameters(SectionChain chain)
    {
        var result = new double[chain.Sections.Count];
        for (var i = 1; i < result.Length; i++) result[i] = result[i - 1] + (chain.Sections[i].Frame.Origin - chain.Sections[i - 1].Frame.Origin).Length;
        return result;
    }

    private static CurveData? ExtractCurveData(RuledBoundary boundary) => boundary switch
    {
        RuledBoundary.Line line => new(3,
            [line.Start, line.Start + (line.End - line.Start) / 3d, line.Start + (line.End - line.Start) * (2d / 3d), line.End],
            [4, 4], [0d, 1d]),
        RuledBoundary.BSpline spline => new(spline.Curve.Degree, spline.Curve.ControlPoints,
            spline.Curve.KnotMultiplicities, spline.Curve.KnotValues),
        _ => null
    };

    private static double MaximumOvershoot(SectionChain chain, IReadOnlyList<ISectionChainTransitionPatch> patches)
    {
        var points = chain.Sections.SelectMany(section => section.Profile.Spans.SelectMany(span => span.Curve switch
        {
            SectionProfileCurve.Line line => new[] { section.Frame.Transform(line.Start), section.Frame.Transform(line.End) },
            SectionProfileCurve.PolynomialBSpline spline => spline.ControlPoints.Select(section.Frame.Transform),
            _ => Array.Empty<Point3D>()
        })).ToArray();
        var diagonal = new Vector3D(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y), points.Max(p => p.Z) - points.Min(p => p.Z)).Length;
        var max = 0d;
        foreach (var patch in patches) for (var u = 0; u <= 8; u++) for (var v = 0; v <= 8; v++)
        {
            var p = patch.Evaluate(u / 8d, v / 8d);
            var outside = Math.Max(0, points.Min(x => x.X) - p.X) + Math.Max(0, p.X - points.Max(x => x.X))
                + Math.Max(0, points.Min(x => x.Y) - p.Y) + Math.Max(0, p.Y - points.Max(x => x.Y))
                + Math.Max(0, points.Min(x => x.Z) - p.Z) + Math.Max(0, p.Z - points.Max(x => x.Z));
            max = Math.Max(max, outside / Math.Max(diagonal, 1e-9));
        }
        return max;
    }

    private static double BendingEnergy(IReadOnlyList<ISectionChainTransitionPatch> patches) => patches.Sum(patch =>
    {
        var energy = 0d;
        for (var u = 1; u < 8; u++) for (var v = 1; v < 8; v++)
        {
            var c = patch.Evaluate(u / 8d, v / 8d);
            var longitudinal = (patch.Evaluate(u / 8d, (v + 1) / 8d) - c)
                - (c - patch.Evaluate(u / 8d, (v - 1) / 8d));
            energy += longitudinal.LengthSquared;
        }
        return energy / 49d;
    });

    private static double NormalVariation(IReadOnlyList<ISectionChainTransitionPatch> patches) => patches.Sum(patch =>
    {
        var variation = 0d; Vector3D? previous = null;
        for (var v = 0; v <= 16; v++)
        {
            var t = v / 16d; const double h = 1e-5;
            var normal = (patch.Evaluate(.5 + h, t) - patch.Evaluate(.5 - h, t)).Cross(patch.Evaluate(.5, Math.Min(1, t + h)) - patch.Evaluate(.5, Math.Max(0, t - h)));
            if (normal.TryNormalize(out var n) && previous is { } p) variation += Math.Acos(Math.Clamp(p.Dot(n), -1d, 1d));
            if (normal.TryNormalize(out n)) previous = n;
        }
        return variation;
    });

    private static SectionChainSmoothSelectionEvidence Evidence(IReadOnlyList<Candidate> candidates, string selected) =>
        new("world-space nonuniform three-section quadratic derivative stencil", "cumulative section-frame-origin chord length",
            "one-sided first chord derivative", candidates.Count, selected,
            candidates.Select(c => new SectionChainSmoothCandidateEvidence(c.Name, c.Scale, c.RejectionReason is null,
                c.Bending, c.Variation, c.Overshoot, c.RejectionReason, c.Utility)).ToArray());

    private sealed record CurveData(int Degree, IReadOnlyList<Point3D> Points, IReadOnlyList<int> Multiplicities, IReadOnlyList<double> Knots);
    private sealed record Candidate(string Name, double Scale, IReadOnlyList<ISectionChainTransitionPatch[]> Patches,
        double Bending, double Variation, double Overshoot, string? RejectionReason, double Utility);
    private sealed record CandidateContext(IReadOnlyList<Candidate> Candidates);
}
