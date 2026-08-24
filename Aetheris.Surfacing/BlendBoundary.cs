using Aetheris.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public enum BlendContinuity { G0, G1, G2 }
public enum BlendCandidateDisposition { Rejected, Eligible, Selected }

public sealed record BlendJudgmentPolicy(
    string PolicyId,
    double FairnessWeight,
    double CurvatureVariationWeight,
    double CompactnessWeight,
    double ComplexityWeight)
{
    public static BlendJudgmentPolicy StandardBlendJudgment { get; } = new(
        "StandardBlendJudgment/v1", .40d, .30d, .20d, .10d);
}

public sealed record BlendBoundaryOperation(
    string StableId,
    string SupportA,
    string SupportB,
    string Region,
    BlendContinuity PreferredContinuity,
    BlendContinuity MinimumContinuity,
    double RegionWidth,
    double RegionDepth,
    double CrownHeight,
    IReadOnlyList<string> MayModify,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<PreservationContract> Preserves,
    IReadOnlyList<SculptRequirement> Requirements,
    BlendJudgmentPolicy? Policy = null,
    string? UseCandidate = null,
    int MaximumDegree = 10,
    double GeometricTolerance = 1e-6,
    double G1AngularToleranceDegrees = .1d,
    double G2CurvatureTolerance = 1e-6) : IConstructionOperation
{
    public string OperationKind => "BlendBoundary";
    public int SchemaVersion => 1;
    public IReadOnlyList<string> Reads => [SupportA, SupportB, .. Preserves.Select(item => item.EntityId)];
    public IReadOnlyList<string> MayModifySet => MayModify;
    public SpatialInfluenceEnvelope AuthorizedEnvelope => InfluenceEnvelope;
    public IReadOnlyList<PreservationContract> PreservationContracts => Preserves;
    public BlendJudgmentPolicy EffectivePolicy => Policy ?? BlendJudgmentPolicy.StandardBlendJudgment;
    public string Canonical => string.Join('|', StableId, SupportA, SupportB, Region, PreferredContinuity, MinimumContinuity,
        RegionWidth.ToString("R"), RegionDepth.ToString("R"), CrownHeight.ToString("R"), MaximumDegree,
        string.Join(',', MayModify.Order(StringComparer.Ordinal)),
        $"{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}",
        string.Join(',', Preserves.OrderBy(item => item.EntityId, StringComparer.Ordinal).Select(item => $"{item.EntityId}:{item.Mode}")),
        string.Join(',', Requirements.Order()), EffectivePolicy.PolicyId, UseCandidate ?? "<policy>");
}

public sealed record BlendBoundaryEvidence(
    double MaximumPositionError,
    double MaximumTangentPlaneErrorDegrees,
    double MaximumNormalCurvatureError,
    int SampleCount,
    string Formulation);

public sealed record BlendQualityMetrics(
    double BendingEnergy,
    double CurvatureVariation,
    double ChangedAreaFraction,
    int ControlPointCount,
    double Fairness,
    double CurvatureSmoothness,
    double Compactness,
    double Complexity,
    double Utility);

public sealed record BlendCandidateTrace(
    string CandidateId,
    string ConstructionFamily,
    IReadOnlyDictionary<string, string> Parameters,
    BlendContinuity ContinuityCapability,
    string SurfaceClass,
    int DegreeU,
    int DegreeV,
    int ControlPointCount,
    BlendCandidateDisposition Disposition,
    string? RejectionReason,
    BlendBoundaryEvidence BoundaryEvidence,
    BlendQualityMetrics? Metrics);

public sealed record BlendJudgmentTrace(
    string Request,
    string JudgmentPolicyId,
    string CandidateSetId,
    string? SelectedCandidateId,
    bool ManualOverride,
    string TieBreak,
    IReadOnlyList<BlendCandidateTrace> Candidates);

public sealed record BlendJudgmentProvenance(
    string JudgmentPolicyId,
    string CandidateSetId,
    string SelectedCandidateId,
    IReadOnlyDictionary<string, double> QualityMetrics);

public sealed record BlendBoundaryResult(
    bool IsSuccess,
    BodyState? OutputState,
    GeometricDelta? Delta,
    BlendJudgmentTrace Trace,
    IReadOnlyList<SculptDiagnostic> Diagnostics);

internal sealed record MaterializedBlendCandidate(
    string CandidateId,
    int EdgeOrder,
    BSplineSurfacePatch Patch,
    BlendContinuity Capability,
    BlendBoundaryEvidence BoundaryEvidence,
    BlendQualityMetrics RawMetrics,
    bool IsAdmissible,
    string? RejectionReason);

/// <summary>
/// Deterministic two-support transition for the qualified housing crown/planar-shoulder case.
/// The product patch is the exact tensor product z=h*g_m(u)g_m(v), where
/// g_m(t)=4^m[t(1-t)]^m. m&gt;=3 has zero first and second transverse derivatives at
/// every planar shoulder boundary, which is Aetheris' bounded G2 contract here:
/// coincident position, coincident tangent plane, and equal transverse normal curvature.
/// </summary>
public static class BlendBoundarySculptor
{
    private const int MetricSamples = 25;

    public static BlendBoundaryResult Apply(BodyState input, string outputName, BlendBoundaryOperation operation)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(operation);
        var diagnostics = ValidateRequest(input, operation).ToList();
        if (diagnostics.Count > 0) return Failure(operation, diagnostics);

        var candidates = Enumerable.Range(2, 4)
            .Select(order => ConstructAndQualify(input, operation, order))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var eligibleForPreferred = candidates.Where(candidate => candidate.IsAdmissible && candidate.Capability >= operation.PreferredContinuity).ToArray();
        var activeRequirement = eligibleForPreferred.Length > 0 ? operation.PreferredContinuity : operation.MinimumContinuity;
        var eligible = candidates.Where(candidate => candidate.IsAdmissible && candidate.Capability >= activeRequirement).ToArray();
        if (eligible.Length > 0)
        {
            var normalized = Normalize(eligible, operation.EffectivePolicy);
            candidates = candidates.Select(candidate => normalized.FirstOrDefault(item => item.CandidateId == candidate.CandidateId) ?? candidate).ToArray();
            eligible = candidates.Where(candidate => candidate.IsAdmissible && candidate.Capability >= activeRequirement).ToArray();
        }

        MaterializedBlendCandidate? selected = null;
        var manual = false;
        if (operation.UseCandidate is not null)
        {
            selected = eligible.SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.CandidateId, operation.UseCandidate));
            if (selected is null)
                diagnostics.Add(new("surf-blend-override-ineligible", $"Candidate override '{operation.UseCandidate}' is absent or ineligible for {activeRequirement}."));
            else manual = true;
        }
        else if (eligible.Length > 0)
        {
            var context = new BlendSelectionContext(eligible);
            var options = eligible.Select(candidate => new JudgmentCandidate<BlendSelectionContext>(candidate.CandidateId, _ => true,
                _ => candidate.RawMetrics.Utility, TieBreakerPriority: candidate.RawMetrics.ControlPointCount)).ToArray();
            var judgment = new JudgmentEngine<BlendSelectionContext>().Evaluate(context, options);
            if (judgment.IsSuccess)
                selected = eligible.Single(candidate => candidate.CandidateId == judgment.Selection!.Value.Candidate.Name);
        }

        if (selected is null)
        {
            if (diagnostics.Count == 0)
            {
                foreach (var candidate in candidates)
                {
                    var reason = candidate.RejectionReason ?? (candidate.Capability < activeRequirement
                        ? $"continuity capability {candidate.Capability} is below active {activeRequirement}."
                        : "candidate was not selected");
                    diagnostics.Add(new("surf-blend-candidate-rejected", $"{candidate.CandidateId}: {reason}", candidate.CandidateId));
                }
                diagnostics.Add(new("surf-blend-no-eligible-candidates", $"No candidate satisfies minimum continuity {operation.MinimumContinuity}; continuity requirements are never lowered by scoring."));
            }
            return Failure(operation, diagnostics, Trace(operation, candidates, null, manual, activeRequirement));
        }

        var patchContinuity = selected.Capability switch { BlendContinuity.G2 => PatchBoundaryContinuity.G2, BlendContinuity.G1 => PatchBoundaryContinuity.G1, _ => PatchBoundaryContinuity.G0 };
        var selectedPatch = selected.Patch with { BoundaryLoop = BoundaryLoop(operation.StableId, patchContinuity) };
        var replace = new ReplaceRegionOperation(operation.StableId + ".ReplaceRegion", operation.SupportA, selectedPatch,
            operation.MayModify, operation.InfluenceEnvelope, operation.Preserves, operation.Requirements,
            operation.GeometricTolerance, operation.G1AngularToleranceDegrees, operation.G2CurvatureTolerance);
        var certifiedBounds = new SpatialInfluenceEnvelope(-operation.RegionWidth / 2d, -operation.RegionDepth / 2d, operation.InfluenceEnvelope.MinZ,
            operation.RegionWidth / 2d, operation.RegionDepth / 2d, operation.InfluenceEnvelope.MinZ + operation.CrownHeight);
        var realized = ReplaceRegionSculptor.ApplyWithCertifiedPolynomialBounds(input, outputName, replace, certifiedBounds);
        if (!realized.IsSuccess || realized.OutputState is null || realized.Delta is null)
            return Failure(operation, realized.Diagnostics, Trace(operation, candidates, selected, manual, activeRequirement));

        var trace = Trace(operation, candidates, selected, manual, activeRequirement);
        var provenance = new BlendJudgmentProvenance(operation.EffectivePolicy.PolicyId, trace.CandidateSetId, selected.CandidateId,
            new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["Fairness"] = selected.RawMetrics.Fairness,
                ["CurvatureSmoothness"] = selected.RawMetrics.CurvatureSmoothness,
                ["Compactness"] = selected.RawMetrics.Compactness,
                ["Complexity"] = selected.RawMetrics.Complexity,
                ["Utility"] = selected.RawMetrics.Utility
            });
        var outputId = BodyStateId.Derive($"{input.StateId.Value}|BlendBoundary|{operation.Canonical}|{selected.CandidateId}");
        var delta = realized.Delta with { OutputState = outputId, BlendJudgment = provenance };
        var authority = ConstructionAuthorityEvolution.Append(input, outputName, operation, outputId, delta, realized.Evidence);
        var state = realized.OutputState with { StateId = outputId, Delta = delta, BlendJudgment = trace, ConstructionAuthority = authority };
        return new(true, state, delta, trace, []);
    }

    private static IEnumerable<SculptDiagnostic> ValidateRequest(BodyState input, BlendBoundaryOperation operation)
    {
        if (!input.SemanticInventory.ContainsKey(operation.SupportA)) yield return new("surf-blend-support-unresolved", $"SupportA '{operation.SupportA}' is not present in the input BodyState.");
        if (string.IsNullOrWhiteSpace(operation.SupportB)) yield return new("surf-blend-support-unresolved", "SupportB must identify the preserved analytic shoulder.");
        if (!operation.MayModify.Contains(operation.Region, StringComparer.Ordinal) && !operation.MayModify.Contains(operation.SupportA, StringComparer.Ordinal))
            yield return new("sculpt-target-not-authorized", "BlendBoundary region/support is absent from MayModify.");
        if (operation.MinimumContinuity > operation.PreferredContinuity) yield return new("surf-blend-continuity-invalid", "Minimum continuity cannot exceed preferred continuity.");
        if (!double.IsFinite(operation.RegionWidth) || !double.IsFinite(operation.RegionDepth) || !double.IsFinite(operation.CrownHeight)
            || operation.RegionWidth <= 0d || operation.RegionDepth <= 0d || operation.CrownHeight <= 0d
            || operation.RegionWidth >= input.Construction.Width || operation.RegionDepth >= input.Construction.Depth)
            yield return new("surf-blend-region-invalid", "The bounded transition region must be positive and strictly inside the housing footprint.");
        if (operation.MaximumDegree < 1) yield return new("surf-blend-degree-invalid", "MaximumDegree must be positive.");
    }

    private static MaterializedBlendCandidate ConstructAndQualify(BodyState input, BlendBoundaryOperation operation, int order)
    {
        var degree = order * 2;
        var id = $"PowerM{order}Degree{degree}";
        var capability = order >= 3 ? BlendContinuity.G2 : order >= 2 ? BlendContinuity.G1 : BlendContinuity.G0;
        var patch = CreatePatch(operation, order, id);
        var boundary = BoundaryEvidence(operation, order);
        var raw = MeasureQuality(operation, order, degree + 1);
        string? rejection = null;
        if (degree > operation.MaximumDegree) rejection = $"surf-blend-complexity-limit — degree {degree} exceeds MaximumDegree {operation.MaximumDegree}.";
        else if (capability < operation.MinimumContinuity) rejection = $"surf-blend-g2-unsatisfied — capability {capability} is below required {operation.MinimumContinuity}; maximum normal-curvature error is {boundary.MaximumNormalCurvatureError:R}.";
        else if (patch.Validate().Count > 0) rejection = "surf-blend-trim-invalid — materialized patch contract is invalid.";
        else
        {
            var construction = input.Construction with { CrownWidth = operation.RegionWidth, CrownDepth = operation.RegionDepth, CrownOffset = operation.CrownHeight, ReplacementPatch = patch };
            var built = SculptedHousingBrepBuilder.Build(construction);
            if (built.Body is null) rejection = "surf-blend-topology-invalid — " + string.Join(" | ", built.Diagnostics.Select(item => item.Message));
            else
            {
                var bodyEvidence = SculptedHousingFactory.ValidateBody(built.Body, operation.GeometricTolerance);
                var failed = bodyEvidence.FirstOrDefault(item => !item.Satisfied);
                if (failed is not null) rejection = $"surf-blend-realized-brep-invalid — {failed.Check}: {failed.Detail}";
                else
                {
                    var points = SamplePatch(patch, 17);
                    var actual = new SpatialInfluenceEnvelope(points.Min(p => p.X), points.Min(p => p.Y), input.Construction.BaseHeight,
                        points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));
                    if (!operation.InfluenceEnvelope.Contains(actual, operation.GeometricTolerance)) rejection = "surf-blend-locality-violation — candidate exceeds the authorized influence envelope.";
                }
            }
        }
        return new(id, order, patch, capability, boundary, raw, rejection is null, rejection);
    }

    private static BSplineSurfacePatch CreatePatch(BlendBoundaryOperation operation, int order, string id)
    {
        var degree = order * 2; var count = degree + 1;
        var central = double.Pow(4d, order) / Binomial(degree, order);
        var rows = new Point3D[count][];
        for (var i = 0; i < count; i++)
        {
            rows[i] = new Point3D[count];
            for (var j = 0; j < count; j++)
            {
                var x = -operation.RegionWidth / 2d + operation.RegionWidth * i / degree;
                var y = -operation.RegionDepth / 2d + operation.RegionDepth * j / degree;
                var z = operation.InfluenceEnvelope.MinZ + (i == order && j == order ? operation.CrownHeight * central * central : 0d);
                rows[i][j] = new(x, y, z);
            }
        }
        var spline = new BSplineSurfaceWithKnots(degree, degree, rows, "UNSPECIFIED", false, false, false,
            [degree + 1, degree + 1], [degree + 1, degree + 1], [0d, 1d], [0d, 1d], "UNSPECIFIED");
        return new(operation.StableId + "." + id, spline, new(0d, 1d, 0d, 1d), BoundaryLoop(operation.StableId, PatchBoundaryContinuity.G0));
    }

    private static SurfaceBoundaryLoop BoundaryLoop(string stableId, PatchBoundaryContinuity continuity) => new(stableId + ".OuterLoop",
    [
        new(stableId + ".South", PatchBoundarySide.South, "CrownBoundarySouth", continuity),
        new(stableId + ".East", PatchBoundarySide.East, "CrownBoundaryEast", continuity),
        new(stableId + ".North", PatchBoundarySide.North, "CrownBoundaryNorth", continuity),
        new(stableId + ".West", PatchBoundarySide.West, "CrownBoundaryWest", continuity)
    ]);

    private static BlendBoundaryEvidence BoundaryEvidence(BlendBoundaryOperation operation, int order)
    {
        var maxCurvature = 0d;
        for (var i = 0; i < 33; i++)
        {
            var t = i / 32d;
            foreach (var (u, v, alongV) in new[] { (t, 0d, true), (1d, t, false), (t, 1d, true), (0d, t, false) })
            {
                var jet = Jet(operation, order, u, v);
                var normal = jet.Du.Cross(jet.Dv); if (!normal.TryNormalize(out var n)) { maxCurvature = double.PositiveInfinity; continue; }
                var tangent = alongV ? jet.Dv : jet.Du;
                var second = alongV ? jet.Dvv : jet.Duu;
                maxCurvature = double.Max(maxCurvature, double.Abs(second.Dot(n) / tangent.Dot(tangent)));
            }
        }
        return new(0d, 0d, maxCurvature, 132,
            "G2 = G0 position + coincident tangent plane + transverse normal-curvature continuity to the planar shoulder; exact polynomial first/second jets sampled at 33 parameters per side.");
    }

    private static BlendQualityMetrics MeasureQuality(BlendBoundaryOperation operation, int order, int controlCount)
    {
        var bending = 0d; var variation = 0d; var footprint = 0; var previous = new double?[MetricSamples, MetricSamples];
        var du = 1d / (MetricSamples - 1d); var dv = du;
        for (var i = 0; i < MetricSamples; i++) for (var j = 0; j < MetricSamples; j++)
        {
            var jet = Jet(operation, order, i * du, j * dv);
            var cross = jet.Du.Cross(jet.Dv); var area = cross.Length;
            if (!cross.TryNormalize(out var normal) || area <= 1e-14d) continue;
            var E = jet.Du.Dot(jet.Du); var F = jet.Du.Dot(jet.Dv); var G = jet.Dv.Dot(jet.Dv); var det = E * G - F * F;
            var e = jet.Duu.Dot(normal); var f = jet.Duv.Dot(normal); var g = jet.Dvv.Dot(normal);
            var mean = (e * G - 2d * f * F + g * E) / (2d * det);
            var gaussian = (e * g - f * f) / det;
            var discriminant = double.Sqrt(double.Max(0d, mean * mean - gaussian));
            var k1 = mean + discriminant; var k2 = mean - discriminant;
            bending += (k1 * k1 + k2 * k2) * area * du * dv;
            previous[i, j] = mean;
            if (operation.CrownHeight * Shape(order, i * du) * Shape(order, j * dv) > operation.CrownHeight * .01d) footprint++;
            if (i > 0 && previous[i - 1, j].HasValue) variation += double.Abs(mean - previous[i - 1, j]!.Value);
            if (j > 0 && previous[i, j - 1].HasValue) variation += double.Abs(mean - previous[i, j - 1]!.Value);
        }
        var characteristic = double.Sqrt(operation.RegionWidth * operation.RegionDepth);
        var changed = footprint / (double)(MetricSamples * MetricSamples);
        return new(bending, variation * characteristic / (MetricSamples * MetricSamples), changed, controlCount * controlCount,
            0d, 0d, 0d, 0d, 0d);
    }

    private static MaterializedBlendCandidate[] Normalize(IReadOnlyList<MaterializedBlendCandidate> eligible, BlendJudgmentPolicy policy)
    {
        static double PreferLow(double value, double min, double max) => max - min <= 1e-15d ? 1d : 1d - Utility.Remap(value, min, max);
        var bendMin = eligible.Min(c => c.RawMetrics.BendingEnergy); var bendMax = eligible.Max(c => c.RawMetrics.BendingEnergy);
        var variationMin = eligible.Min(c => c.RawMetrics.CurvatureVariation); var variationMax = eligible.Max(c => c.RawMetrics.CurvatureVariation);
        var footprintMin = eligible.Min(c => c.RawMetrics.ChangedAreaFraction); var footprintMax = eligible.Max(c => c.RawMetrics.ChangedAreaFraction);
        var complexityMin = eligible.Min(c => c.RawMetrics.ControlPointCount); var complexityMax = eligible.Max(c => c.RawMetrics.ControlPointCount);
        return eligible.Select(candidate =>
        {
            var raw = candidate.RawMetrics;
            var fairness = PreferLow(raw.BendingEnergy, bendMin, bendMax);
            var smoothness = PreferLow(raw.CurvatureVariation, variationMin, variationMax);
            var compactness = PreferLow(raw.ChangedAreaFraction, footprintMin, footprintMax);
            var complexity = PreferLow(raw.ControlPointCount, complexityMin, complexityMax);
            var utility = Utility.Weighted((fairness, policy.FairnessWeight), (smoothness, policy.CurvatureVariationWeight),
                (compactness, policy.CompactnessWeight), (complexity, policy.ComplexityWeight));
            return candidate with { RawMetrics = raw with { Fairness = fairness, CurvatureSmoothness = smoothness, Compactness = compactness, Complexity = complexity, Utility = utility } };
        }).ToArray();
    }

    private static BlendJudgmentTrace Trace(BlendBoundaryOperation operation, IReadOnlyList<MaterializedBlendCandidate> candidates,
        MaterializedBlendCandidate? selected, bool manual, BlendContinuity activeRequirement)
    {
        var ids = string.Join('|', candidates.Select(candidate => candidate.CandidateId));
        var candidateSet = BodyStateId.Derive(operation.Canonical + "|" + ids).Value.Replace("state-", "blend-set-", StringComparison.Ordinal);
        return new($"Preferred {operation.PreferredContinuity}; minimum {operation.MinimumContinuity}; active {activeRequirement} between {operation.SupportA} and {operation.SupportB}",
            operation.EffectivePolicy.PolicyId, candidateSet, selected?.CandidateId, manual,
            "Highest composite utility; ties use lower materialized control-point count, then ordinal CandidateId.",
            candidates.Select(candidate => new BlendCandidateTrace(candidate.CandidateId, "symmetric polynomial Bezier power transition",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["EdgeVanishingOrder"] = candidate.EdgeOrder.ToString(), ["RegionWidthMm"] = operation.RegionWidth.ToString("R"), ["RegionDepthMm"] = operation.RegionDepth.ToString("R"), ["CrownHeightMm"] = operation.CrownHeight.ToString("R") },
                candidate.Capability, candidate.Patch.ExportClass, candidate.Patch.Spline.DegreeU, candidate.Patch.Spline.DegreeV,
                candidate.RawMetrics.ControlPointCount,
                selected?.CandidateId == candidate.CandidateId ? BlendCandidateDisposition.Selected : candidate.IsAdmissible && candidate.Capability >= activeRequirement ? BlendCandidateDisposition.Eligible : BlendCandidateDisposition.Rejected,
                candidate.IsAdmissible && candidate.Capability < activeRequirement ? $"Continuity capability {candidate.Capability} is below active {activeRequirement}." : candidate.RejectionReason,
                candidate.BoundaryEvidence, candidate.IsAdmissible && candidate.Capability >= activeRequirement ? candidate.RawMetrics : null)).ToArray());
    }

    private static BlendBoundaryResult Failure(BlendBoundaryOperation operation, IReadOnlyList<SculptDiagnostic> diagnostics,
        BlendJudgmentTrace? trace = null) => new(false, null, null, trace ?? new($"{operation.PreferredContinuity} between {operation.SupportA} and {operation.SupportB}",
            operation.EffectivePolicy.PolicyId, "<not-generated>", null, false, "No selection.", []), diagnostics);

    private static PatchJet2 Jet(BlendBoundaryOperation operation, int order, double u, double v)
    {
        var gu = Shape(order, u); var gv = Shape(order, v); var gpu = ShapeFirst(order, u); var gpv = ShapeFirst(order, v);
        var gppu = ShapeSecond(order, u); var gppv = ShapeSecond(order, v); var h = operation.CrownHeight;
        return new(new(-operation.RegionWidth / 2d + operation.RegionWidth * u, -operation.RegionDepth / 2d + operation.RegionDepth * v, operation.InfluenceEnvelope.MinZ + h * gu * gv),
            new(operation.RegionWidth, 0d, h * gpu * gv), new(0d, operation.RegionDepth, h * gu * gpv),
            new(0d, 0d, h * gppu * gv), new(0d, 0d, h * gpu * gpv), new(0d, 0d, h * gu * gppv), DifferentialSingularityKind.Regular);
    }

    private static double Shape(int order, double t) => double.Pow(4d * t * (1d - t), order);
    private static double ShapeFirst(int order, double t)
    {
        var q = t * (1d - t); return double.Pow(4d, order) * order * double.Pow(q, order - 1) * (1d - 2d * t);
    }
    private static double ShapeSecond(int order, double t)
    {
        var q = t * (1d - t); var a = order == 1 ? 1d : double.Pow(q, order - 2);
        return double.Pow(4d, order) * order * (a * ((order - 1d) * (1d - 2d * t) * (1d - 2d * t) - 2d * q));
    }
    private static double Binomial(int n, int k) { var result = 1d; for (var i = 1; i <= k; i++) result = result * (n - (k - i)) / i; return result; }
    private static IReadOnlyList<Point3D> SamplePatch(BoundedSurfacePatch patch, int count) => Enumerable.Range(0, count).SelectMany(i => Enumerable.Range(0, count).Select(j => patch.Evaluate(i / (count - 1d), j / (count - 1d)))).ToArray();
    private sealed record BlendSelectionContext(IReadOnlyList<MaterializedBlendCandidate> Candidates);
}
