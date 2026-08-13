using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Geometry;

/// <summary>The strongest whole-domain relation justified by a bounded observational query.</summary>
public enum IntersectionRelation { Disjoint, Crossing, Touching, Coincident, Overlapping, Unknown }
public enum IntersectionEvidencePreference { PreferCertified, AllowToleranceBounded, AllowSampled }
public enum IntersectionLocalRelation { Crossing, TouchingCandidate, CoincidentCandidate, Unclassified }

public sealed record IntersectionPolicy
{
    public static IntersectionPolicy Default { get; } = new();
    public double LinearTolerance { get; init; } = ToleranceContext.Default.Linear;
    public double AngularTolerance { get; init; } = ToleranceContext.Default.Angular;
    public double ParameterTolerance { get; init; } = 1e-10;
    public int IterationBudget { get; init; } = 96;
    public int SubdivisionBudget { get; init; } = 10_000;
    public IntersectionEvidencePreference EvidencePreference { get; init; } = IntersectionEvidencePreference.PreferCertified;

    public IntersectionPolicy Validate()
    {
        Positive(LinearTolerance, nameof(LinearTolerance)); Positive(AngularTolerance, nameof(AngularTolerance));
        Positive(ParameterTolerance, nameof(ParameterTolerance));
        if (IterationBudget < 1) throw new ArgumentOutOfRangeException(nameof(IterationBudget));
        if (SubdivisionBudget < 16) throw new ArgumentOutOfRangeException(nameof(SubdivisionBudget));
        return this;
    }

    internal DistanceQueryPolicy DistancePolicy => new()
    {
        LinearTolerance = LinearTolerance, ParameterTolerance = ParameterTolerance,
        IterationBudget = IterationBudget, SubdivisionBudget = SubdivisionBudget
    };

    private static void Positive(double value, string name)
    { if (!double.IsFinite(value) || value <= 0d) throw new ArgumentOutOfRangeException(name); }
}

public sealed record IntersectionParameters(double? T = null, double? U = null, double? V = null);
public sealed record IntersectionDomain(string Kind, double? MinimumA, double? MaximumA, double? MinimumB = null, double? MaximumB = null);
public sealed record IntersectionOperand(string Kind, GeometryIdentity? Identity, GeometryProvenance? Provenance);
public sealed record ContactObservation(
    double? TangentNormalDot,
    double? NormalNormalDot,
    double? SignedSecondDerivative,
    bool HasSecondJet,
    string Summary);

/// <summary>A point observation only. It carries no trim, edge, face, or model-authoring authority.</summary>
public sealed record IntersectionWitness(
    Point3D Point,
    Point3D PointOnA,
    Point3D PointOnB,
    IntersectionParameters? ParameterOnA,
    IntersectionParameters? ParameterOnB,
    double Residual,
    IntersectionLocalRelation LocalRelation,
    ContactObservation? Contact);

public sealed record IntersectionStatistics(
    int Samples,
    int Subdivisions,
    int Iterations,
    int ClosestPointCalls,
    int CandidateRegions,
    bool BudgetExhausted);

/// <summary>
/// Immutable evidence about two bounded operands. Witnesses are non-authoritative and non-exportable
/// as trims; this result contains no topology or construction operation.
/// </summary>
public sealed record IntersectionResult(
    IntersectionRelation Relation,
    PredicateEvidenceKind Evidence,
    IntersectionPolicy ToleranceUsed,
    IReadOnlyList<IntersectionWitness> WitnessPoints,
    IReadOnlyList<double> Residuals,
    IntersectionDomain DomainA,
    IntersectionDomain DomainB,
    IntersectionStatistics Statistics,
    IntersectionOperand OperandA,
    IntersectionOperand OperandB,
    string Provenance,
    IReadOnlyList<GeometryQueryDiagnostic> Diagnostics)
{
    public bool IsDefinitelyDisjoint => Relation == IntersectionRelation.Disjoint && Evidence is PredicateEvidenceKind.Structural or PredicateEvidenceKind.Certified or PredicateEvidenceKind.ToleranceBounded;
    public bool WitnessesAreAuthoritativeTrims => false;
}

/// <summary>
/// Bounded intersection predicates. Generic numerical intersection may establish geometric evidence,
/// but it does not author semantic topology.
/// </summary>
public static class IntersectionQuery
{
    public static IntersectionResult Between(BoundedParametricCurve3 curve, Plane3 plane) => Between(curve, plane, IntersectionPolicy.Default);
    public static IntersectionResult Between(BoundedParametricCurve3 curve, Plane3 plane, IntersectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(curve); policy.Validate();
        if (!Valid(plane)) return Unknown(curve, plane, policy, [new(GeometryQueryDiagnosticCode.InvalidPlane, "Plane normal must be finite and non-zero.")]);

        var samples = SampleCurvePlane(curve, plane, policy, out var diagnostics);
        if (samples.Count == 0) return Unknown(curve, plane, policy, diagnostics);
        var positive = samples.Where(x => x.Distance > policy.LinearTolerance).ToArray();
        var negative = samples.Where(x => x.Distance < -policy.LinearTolerance).ToArray();
        var contacts = samples.Where(x => double.Abs(x.Distance) <= policy.LinearTolerance).ToArray();
        var analyticLine = curve.NativeFamily == "Line3";

        if (analyticLine)
        {
            var first = samples[0]; var last = samples[^1];
            if (first.Distance == 0d && last.Distance == 0d)
                return Result(IntersectionRelation.Coincident, PredicateEvidenceKind.Structural, policy,
                    [Witness(curve, plane, first, IntersectionLocalRelation.CoincidentCandidate, null)], curve, plane,
                    new(samples.Count, 0, 0, 0, 1, false), diagnostics, "analytic-line/plane structural zero over bounded segment");
            if (SameStrictSign(first.Distance, last.Distance, policy.LinearTolerance))
                return Result(IntersectionRelation.Disjoint, PredicateEvidenceKind.Certified, policy, [], curve, plane,
                    new(samples.Count, 0, 0, 0, 0, false), diagnostics, "affine signed scalar has strict bounded endpoint separation");
        }

        if (positive.Length > 0 && negative.Length > 0)
        {
            var bracket = FirstBracket(samples, policy.LinearTolerance);
            var roots = AllRoots(curve, plane, samples, policy);
            var root = roots.FirstOrDefault();
            var evidence = analyticLine ? PredicateEvidenceKind.Certified : PredicateEvidenceKind.ToleranceBounded;
            return Result(IntersectionRelation.Crossing, evidence, policy,
                roots.Select(x => Witness(curve, plane, x, IntersectionLocalRelation.Crossing, CurvePlaneObservation(curve, plane, x.T, policy))).ToArray(), curve, plane,
                new(samples.Count, 0, bracket is null ? 0 : policy.IterationBudget, 0, roots.Count, false), diagnostics,
                "continuous signed scalar has opposite strict-side witnesses");
        }

        if (TryCurveScalarRange(curve, plane, out var range) && (range.Lower > policy.LinearTolerance || range.Upper < -policy.LinearTolerance))
            return Result(IntersectionRelation.Disjoint, PredicateEvidenceKind.Certified, policy, [], curve, plane,
                new(samples.Count, 1, 0, 0, 0, false), diagnostics, "interval signed scalar proves strict separation");

        if (contacts.Length > 0)
        {
            var contact = contacts.OrderBy(x => double.Abs(x.Distance)).ThenBy(x => x.T).First();
            var observation = CurvePlaneObservation(curve, plane, contact.T, policy);
            var globallyOneSided = TryCurveScalarRange(curve, plane, out range)
                && (range.Lower >= -policy.LinearTolerance || range.Upper <= policy.LinearTolerance);
            if (observation.HasSecondJet && double.Abs(observation.TangentNormalDot ?? 1d) <= policy.AngularTolerance
                && double.Abs(observation.SignedSecondDerivative ?? 0d) > policy.LinearTolerance && globallyOneSided)
                return Result(IntersectionRelation.Touching, PredicateEvidenceKind.ToleranceBounded, policy,
                    [Witness(curve, plane, contact, IntersectionLocalRelation.TouchingCandidate, observation)], curve, plane,
                    new(samples.Count, 1, 0, 0, 1, false), diagnostics, "whole-domain one-sided interval plus isolated second-order tangent contact");
            diagnostics.Add(new(GeometryQueryDiagnosticCode.InsufficientSecondJetEvidence,
                "A local contact candidate does not establish a whole-domain touching relation."));
            return Result(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, policy,
                [Witness(curve, plane, contact, IntersectionLocalRelation.TouchingCandidate, observation)], curve, plane,
                new(samples.Count, 1, 0, 0, 1, false), diagnostics, "local curve/plane contact remains globally inconclusive");
        }

        return Result(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, policy, [], curve, plane,
            new(samples.Count, 1, 0, 0, 0, false), diagnostics, "bounded samples and intervals were inconclusive");
    }

    public static IntersectionResult Between(Plane3 plane, BoundedParametricCurve3 curve) => Swap(Between(curve, plane));
    public static IntersectionResult Between(Plane3 plane, BoundedParametricCurve3 curve, IntersectionPolicy policy) => Swap(Between(curve, plane, policy));

    public static IntersectionResult Between(BoundedParametricPatch3 patch, Plane3 plane) => Between(patch, plane, IntersectionPolicy.Default);
    public static IntersectionResult Between(BoundedParametricPatch3 patch, Plane3 plane, IntersectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(patch); policy.Validate();
        if (!Valid(plane)) return Unknown(patch, plane, policy, [new(GeometryQueryDiagnosticCode.InvalidPlane, "Plane normal must be finite and non-zero.")]);
        var signedPolicy = policy.EvidencePreference == IntersectionEvidencePreference.AllowSampled
            ? SignedSidePolicy.Sampled(policy.LinearTolerance, Samples(policy.SubdivisionBudget), Samples(policy.SubdivisionBudget))
            : SignedSidePolicy.Certified(policy.LinearTolerance, MaxDepth(policy.SubdivisionBudget), policy.SubdivisionBudget);
        var side = SignedSideQuery.Query(patch, plane, signedPolicy);
        var statistics = new IntersectionStatistics(side.Statistics.SampleCount, side.Statistics.LeafCount, 0, 0, side.Witnesses.Count, side.Diagnostics.Any(d => d.Code == GeometryQueryDiagnosticCode.SubdivisionBudgetExhausted));
        if (side.Classification is SignedSideClassification.Positive or SignedSideClassification.Negative)
            return Result(IntersectionRelation.Disjoint, side.EvidenceKind, policy, [], patch, plane, statistics, side.Diagnostics, "SignedSide whole-domain strict-side result");
        if (side.Classification == SignedSideClassification.Crossing)
            return Result(IntersectionRelation.Crossing, side.EvidenceKind, policy,
                side.Witnesses.Select(w => Witness(patch, plane, w, IntersectionLocalRelation.Crossing, null)).ToArray(), patch, plane, statistics, side.Diagnostics,
                "SignedSide opposite-side bounded witnesses establish crossing");

        var diagnostics = side.Diagnostics.ToList();
        if (TryPatchScalarRange(patch, plane, out var range))
        {
            var candidate = PatchPlaneCandidates(patch, plane, policy).FirstOrDefault();
            if (candidate is not null)
            {
                var contact = PatchPlaneObservation(patch, plane, candidate.U, candidate.V, policy);
                var oneSided = range.Lower >= -policy.LinearTolerance || range.Upper <= policy.LinearTolerance;
                if (oneSided && contact.NormalNormalDot is double alignment && 1d - double.Abs(alignment) <= policy.AngularTolerance)
                    return Result(IntersectionRelation.Touching, PredicateEvidenceKind.ToleranceBounded, policy,
                        [Witness(patch, plane, candidate, IntersectionLocalRelation.TouchingCandidate, contact)], patch, plane, statistics with { CandidateRegions = 1 }, diagnostics,
                        "SignedSide-backed whole-domain one-sided interval plus tangent-plane contact");
            }
        }
        diagnostics.Add(new(GeometryQueryDiagnosticCode.InsufficientSecondJetEvidence, "SignedSide did not establish a strict side or crossing; local contact evidence is not a global touching proof."));
        return Result(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, policy, [], patch, plane, statistics, diagnostics, "patch/plane relation remains inconclusive");
    }

    public static IntersectionResult Between(Plane3 plane, BoundedParametricPatch3 patch) => Swap(Between(patch, plane));
    public static IntersectionResult Between(Plane3 plane, BoundedParametricPatch3 patch, IntersectionPolicy policy) => Swap(Between(patch, plane, policy));

    public static IntersectionResult Between(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch) => Between(curve, patch, IntersectionPolicy.Default);
    public static IntersectionResult Between(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch, IntersectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(curve); ArgumentNullException.ThrowIfNull(patch); policy.Validate();
        var closest = ClosestPointQuery.Between(curve, patch, policy.DistancePolicy);
        if (closest.Status == DistanceQueryStatus.Unknown) return FromUnknown(closest, curve, patch, policy);
        if (closest.Relation == DistanceRelation.Separated)
            return FromClosest(IntersectionRelation.Disjoint, closest.Evidence, closest, curve, patch, policy, [], "M3 distance gate proves bounded separation");
        var witness = CurvePatchWitness(curve, patch, closest, policy);
        var discovered = FindInteriorCurvePatchWitnesses(curve, patch, policy);
        if (witness is null || witness.LocalRelation != IntersectionLocalRelation.Crossing
            || !Interior(curve, witness.ParameterOnA!.T!.Value, policy.ParameterTolerance)
            || !Interior(patch, witness.ParameterOnB!.U!.Value, witness.ParameterOnB.V!.Value, policy.ParameterTolerance))
            witness = discovered.FirstOrDefault() ?? witness;
        if (witness is null) return FromClosest(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, closest, curve, patch, policy, closest.Diagnostics, "near-zero distance lacks a regular differential candidate");
        var witnesses = Merge(witness, discovered, policy.LinearTolerance);
        if (witnesses.Select(x => x.LocalRelation).Distinct().Count() > 1)
            return FromClosest(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, closest, curve, patch, policy,
                closest.Diagnostics.Concat([new GeometryQueryDiagnostic(GeometryQueryDiagnosticCode.ConflictingLocalRelations, "Different bounded candidate regions have conflicting local relations.")]).ToArray(),
                "conflicting local candidate evidence", witnesses);
        if (witness.LocalRelation == IntersectionLocalRelation.Crossing && Interior(curve, witness.ParameterOnA!.T!.Value, policy.ParameterTolerance)
            && Interior(patch, witness.ParameterOnB!.U!.Value, witness.ParameterOnB.V!.Value, policy.ParameterTolerance))
            return FromClosest(IntersectionRelation.Crossing, PredicateEvidenceKind.ToleranceBounded, closest, curve, patch, policy, closest.Diagnostics,
                "M3 near-zero candidate plus interior transverse tangent/normal evidence", witnesses);
        var diagnostics = closest.Diagnostics.Concat([new GeometryQueryDiagnostic(GeometryQueryDiagnosticCode.InsufficientSecondJetEvidence,
            "Near-zero distance and local tangency do not establish whole-domain touching.")]).ToArray();
        return FromClosest(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, closest, curve, patch, policy, diagnostics,
            "local contact candidate remains globally inconclusive", witnesses);
    }

    public static IntersectionResult Between(BoundedParametricPatch3 patch, BoundedParametricCurve3 curve) => Swap(Between(curve, patch));
    public static IntersectionResult Between(BoundedParametricPatch3 patch, BoundedParametricCurve3 curve, IntersectionPolicy policy) => Swap(Between(curve, patch, policy));

    public static IntersectionResult Between(BoundedParametricPatch3 a, BoundedParametricPatch3 b) => Between(a, b, IntersectionPolicy.Default);
    public static IntersectionResult Between(BoundedParametricPatch3 a, BoundedParametricPatch3 b, IntersectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b); policy.Validate();
        var closest = ClosestPointQuery.Between(a, b, policy.DistancePolicy);
        if (closest.Status == DistanceQueryStatus.Unknown) return FromUnknown(closest, a, b, policy);
        if (closest.Relation == DistanceRelation.Coincident && closest.Evidence == PredicateEvidenceKind.Structural && a.Domain == b.Domain)
            return FromClosest(IntersectionRelation.Coincident, PredicateEvidenceKind.Structural, closest, a, b, policy, [], "shared authored geometry identity", [PatchPatchWitness(a, b, closest, policy) ?? BasicWitness(closest, IntersectionLocalRelation.CoincidentCandidate)]);
        if (closest.Relation == DistanceRelation.Coincident && closest.Evidence == PredicateEvidenceKind.Structural)
            return FromClosest(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, closest, a, b, policy,
                [new(GeometryQueryDiagnosticCode.AmbiguousOverlap, "Shared support identity with different bounded domains does not establish whole-domain coincidence; partial overlap classification is deferred.")],
                "shared support identity has non-identical bounded domains");
        if (closest.Relation == DistanceRelation.Separated)
            return FromClosest(IntersectionRelation.Disjoint, closest.Evidence, closest, a, b, policy, [], "M3 distance gate proves bounded separation");
        var witness = PatchPatchWitness(a, b, closest, policy);
        var discovered = FindInteriorPatchPatchWitnesses(a, b, policy);
        if (witness is null || witness.LocalRelation != IntersectionLocalRelation.Crossing
            || !Interior(a, witness.ParameterOnA!.U!.Value, witness.ParameterOnA.V!.Value, policy.ParameterTolerance)
            || !Interior(b, witness.ParameterOnB!.U!.Value, witness.ParameterOnB.V!.Value, policy.ParameterTolerance))
            witness = discovered.FirstOrDefault() ?? witness;
        if (witness is null) return FromClosest(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, closest, a, b, policy, closest.Diagnostics, "near-zero distance lacks regular tangent planes");
        var witnesses = Merge(witness, discovered, policy.LinearTolerance);
        if (witnesses.Select(x => x.LocalRelation).Distinct().Count() > 1)
            return FromClosest(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, closest, a, b, policy,
                closest.Diagnostics.Concat([new GeometryQueryDiagnostic(GeometryQueryDiagnosticCode.ConflictingLocalRelations, "Different bounded candidate regions have conflicting local relations.")]).ToArray(),
                "conflicting local candidate evidence", witnesses);
        var pa = witness.ParameterOnA!; var pb = witness.ParameterOnB!;
        if (witness.LocalRelation == IntersectionLocalRelation.Crossing
            && Interior(a, pa.U!.Value, pa.V!.Value, policy.ParameterTolerance)
            && Interior(b, pb.U!.Value, pb.V!.Value, policy.ParameterTolerance))
            return FromClosest(IntersectionRelation.Crossing, PredicateEvidenceKind.ToleranceBounded, closest, a, b, policy, closest.Diagnostics,
                "M3 near-zero candidate plus interior transverse tangent planes", witnesses);
        var diagnostics = closest.Diagnostics.Concat([new GeometryQueryDiagnostic(GeometryQueryDiagnosticCode.AmbiguousOverlap,
            "Compatible tangent planes or boundary-only contact do not certify overlap or whole-domain touching.")]).ToArray();
        return FromClosest(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, closest, a, b, policy, diagnostics,
            "local patch contact remains globally inconclusive", witnesses);
    }

    private static IntersectionWitness? CurvePatchWitness(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch, ClosestPointResult c, IntersectionPolicy p)
    {
        if (c.ParameterOnA?.T is not double t || c.ParameterOnB?.U is not double u || c.ParameterOnB.V is not double v || c.PointOnA is not Point3D a || c.PointOnB is not Point3D b) return null;
        try
        {
            var cj = curve.EvaluateJet1(t); var pj = patch.Evaluate(u, v); if (!cj.IsRegular || pj.Normal is null) return null;
            var dot = pj.Normal.Value.ToVector().Dot(cj.UnitTangent!.Value.ToVector());
            var local = double.Abs(dot) > double.Sin(p.AngularTolerance) ? IntersectionLocalRelation.Crossing : IntersectionLocalRelation.TouchingCandidate;
            var observation = new ContactObservation(dot, null, null, false, local == IntersectionLocalRelation.Crossing ? "transverse curve tangent and patch normal" : "curve tangent lies in patch tangent plane");
            return new(Mid(a, b), a, b, new(T: t), new(U: u, V: v), c.ComputedDistance ?? (a - b).Length, local, observation);
        }
        catch (Exception ex) when (ex is ArithmeticException or ArgumentOutOfRangeException) { return null; }
    }

    private static IntersectionWitness? PatchPatchWitness(BoundedParametricPatch3 a, BoundedParametricPatch3 b, ClosestPointResult c, IntersectionPolicy p)
    {
        if (c.ParameterOnA?.U is not double au || c.ParameterOnA.V is not double av || c.ParameterOnB?.U is not double bu || c.ParameterOnB.V is not double bv || c.PointOnA is not Point3D pa || c.PointOnB is not Point3D pb) return null;
        try
        {
            var ja = a.Evaluate(au, av); var jb = b.Evaluate(bu, bv); if (ja.Normal is null || jb.Normal is null) return null;
            var dot = ja.Normal.Value.ToVector().Dot(jb.Normal.Value.ToVector());
            var transverse = 1d - double.Abs(dot) > p.AngularTolerance;
            return new(Mid(pa, pb), pa, pb, new(U: au, V: av), new(U: bu, V: bv), c.ComputedDistance ?? (pa - pb).Length,
                transverse ? IntersectionLocalRelation.Crossing : IntersectionLocalRelation.TouchingCandidate,
                new(null, dot, null, false, transverse ? "transverse tangent planes" : "compatible tangent planes"));
        }
        catch (Exception ex) when (ex is ArithmeticException or ArgumentOutOfRangeException) { return null; }
    }

    private static IReadOnlyList<IntersectionWitness> FindInteriorCurvePatchWitnesses(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch, IntersectionPolicy policy)
    {
        const int n = 5; var found = new List<IntersectionWitness>();
        for (var i = 1; i < n - 1; i++) for (var j = 1; j < n - 1; j++) for (var k = 1; k < n - 1; k++)
        {
            var t = curve.Domain.Map(i / (double)(n - 1)); var u = patch.Domain.U.Map(j / (double)(n - 1)); var v = patch.Domain.V.Map(k / (double)(n - 1));
            try
            {
                var a = curve.Evaluate(t); var b = patch.EvaluatePoint(u, v); var residual = (a - b).Length;
                if (residual > policy.LinearTolerance) continue;
                var cj = curve.EvaluateJet1(t); var pj = patch.Evaluate(u, v); if (!cj.IsRegular || pj.Normal is null) continue;
                var dot = pj.Normal.Value.ToVector().Dot(cj.UnitTangent!.Value.ToVector());
                var local = double.Abs(dot) > double.Sin(policy.AngularTolerance) ? IntersectionLocalRelation.Crossing : IntersectionLocalRelation.TouchingCandidate;
                found.Add(new(Mid(a, b), a, b, new(T: t), new(U: u, V: v), residual, local,
                    new(dot, null, null, false, local == IntersectionLocalRelation.Crossing ? "interior transverse lattice witness" : "interior tangent lattice witness")));
            }
            catch (ArithmeticException) { }
        }
        return Deduplicate(found, policy.LinearTolerance);
    }

    private static IReadOnlyList<IntersectionWitness> FindInteriorPatchPatchWitnesses(BoundedParametricPatch3 a, BoundedParametricPatch3 b, IntersectionPolicy policy)
    {
        const int n = 5; var found = new List<IntersectionWitness>();
        for (var i = 1; i < n - 1; i++) for (var j = 1; j < n - 1; j++) for (var k = 1; k < n - 1; k++) for (var l = 1; l < n - 1; l++)
        {
            var au = a.Domain.U.Map(i / (double)(n - 1)); var av = a.Domain.V.Map(j / (double)(n - 1));
            var bu = b.Domain.U.Map(k / (double)(n - 1)); var bv = b.Domain.V.Map(l / (double)(n - 1));
            try
            {
                var pa = a.Evaluate(au, av); var pb = b.Evaluate(bu, bv); var residual = (pa.Point - pb.Point).Length;
                if (residual > policy.LinearTolerance || pa.Normal is null || pb.Normal is null) continue;
                var dot = pa.Normal.Value.ToVector().Dot(pb.Normal.Value.ToVector()); var transverse = 1d - double.Abs(dot) > policy.AngularTolerance;
                found.Add(new(Mid(pa.Point, pb.Point), pa.Point, pb.Point, new(U: au, V: av), new(U: bu, V: bv), residual,
                    transverse ? IntersectionLocalRelation.Crossing : IntersectionLocalRelation.TouchingCandidate,
                    new(null, dot, null, false, transverse ? "interior transverse lattice witness" : "interior compatible tangent-plane witness")));
            }
            catch (ArithmeticException) { }
        }
        return Deduplicate(found, policy.LinearTolerance);
    }

    private static List<CurvePlaneSample> SampleCurvePlane(BoundedParametricCurve3 curve, Plane3 plane, IntersectionPolicy policy, out List<GeometryQueryDiagnostic> diagnostics)
    {
        diagnostics = []; var n = int.Clamp(policy.SubdivisionBudget / 32, 9, 257); var result = new List<CurvePlaneSample>(n);
        for (var i = 0; i < n; i++)
        {
            var t = curve.Domain.Map(i / (double)(n - 1));
            try { var point = curve.Evaluate(t); var distance = plane.SignedDistance(point); if (double.IsFinite(distance)) result.Add(new(t, point, distance)); else diagnostics.Add(new(GeometryQueryDiagnosticCode.NonFiniteEvaluation, $"Non-finite signed distance at t={t:R}.")); }
            catch (ArithmeticException ex) { diagnostics.Add(new(GeometryQueryDiagnosticCode.NonFiniteEvaluation, ex.Message)); }
        }
        return result;
    }

    private static (CurvePlaneSample A, CurvePlaneSample B)? FirstBracket(IReadOnlyList<CurvePlaneSample> samples, double tolerance)
    {
        for (var i = 1; i < samples.Count; i++) if (samples[i - 1].Distance * samples[i].Distance < 0 && double.Abs(samples[i - 1].Distance) > tolerance && double.Abs(samples[i].Distance) > tolerance) return (samples[i - 1], samples[i]);
        return null;
    }

    private static CurvePlaneSample Bisect(BoundedParametricCurve3 curve, Plane3 plane, CurvePlaneSample a, CurvePlaneSample b, IntersectionPolicy policy)
    {
        for (var i = 0; i < policy.IterationBudget && b.T - a.T > policy.ParameterTolerance; i++)
        {
            var t = (a.T + b.T) / 2d; var point = curve.Evaluate(t); var candidate = new CurvePlaneSample(t, point, plane.SignedDistance(point));
            if (double.Abs(candidate.Distance) <= policy.LinearTolerance) return candidate;
            if (a.Distance * candidate.Distance <= 0) b = candidate; else a = candidate;
        }
        return double.Abs(a.Distance) <= double.Abs(b.Distance) ? a : b;
    }

    private static IReadOnlyList<CurvePlaneSample> AllRoots(BoundedParametricCurve3 curve, Plane3 plane, IReadOnlyList<CurvePlaneSample> samples, IntersectionPolicy policy)
    {
        var roots = new List<CurvePlaneSample>();
        for (var i = 1; i < samples.Count; i++)
        {
            var a = samples[i - 1]; var b = samples[i];
            if (a.Distance * b.Distance < 0) roots.Add(Bisect(curve, plane, a, b, policy));
            else if (double.Abs(a.Distance) <= policy.LinearTolerance) roots.Add(a);
        }
        if (double.Abs(samples[^1].Distance) <= policy.LinearTolerance) roots.Add(samples[^1]);
        return roots.OrderBy(x => x.T).Aggregate(new List<CurvePlaneSample>(), (list, item) =>
        { if (list.Count == 0 || double.Abs(item.T - list[^1].T) > policy.ParameterTolerance * 8) list.Add(item); return list; });
    }

    private static IReadOnlyList<IntersectionWitness> Merge(IntersectionWitness primary, IReadOnlyList<IntersectionWitness> discovered, double tolerance)
        => Deduplicate(new[] { primary }.Concat(discovered), tolerance);
    private static IReadOnlyList<IntersectionWitness> Deduplicate(IEnumerable<IntersectionWitness> source, double tolerance)
    {
        var result = new List<IntersectionWitness>();
        foreach (var witness in source.OrderBy(x => x.ParameterOnA?.T ?? x.ParameterOnA?.U ?? double.PositiveInfinity)
                     .ThenBy(x => x.ParameterOnA?.V ?? double.PositiveInfinity).ThenBy(x => x.ParameterOnB?.U ?? double.PositiveInfinity).ThenBy(x => x.ParameterOnB?.V ?? double.PositiveInfinity))
            if (!result.Any(x => (x.Point - witness.Point).Length <= tolerance)) result.Add(witness);
        return result;
    }

    private static ContactObservation CurvePlaneObservation(BoundedParametricCurve3 curve, Plane3 plane, double t, IntersectionPolicy policy)
    {
        var jet = curve.EvaluateJet1(t); var first = jet.UnitTangent is null ? double.NaN : plane.Normal.ToVector().Dot(jet.UnitTangent.Value.ToVector()); double? second = null; var hasSecond = false;
        if (curve.SupportsSecondJet) { try { second = plane.Normal.ToVector().Dot(curve.EvaluateJet2(t).SecondDerivative); hasSecond = true; } catch (NotSupportedException) { } }
        return new(first, null, second, hasSecond, double.Abs(first) > policy.AngularTolerance ? "signed scalar crosses transversely" : hasSecond ? "stationary signed scalar with second-order observation" : "stationary signed scalar without second jet");
    }

    private static bool TryCurveScalarRange(BoundedParametricCurve3 curve, Plane3 plane, out Interval range)
    {
        if (curve.PointExpression is null) { range = default; return false; }
        var scalar = Scalar(curve.PointExpression.X, curve.PointExpression.Y, curve.PointExpression.Z, plane);
        return IntervalEvaluator.TryEvaluate(scalar, new(new(curve.Domain.Minimum, curve.Domain.Maximum), new(-1, 1)), out range);
    }

    private static bool TryPatchScalarRange(BoundedParametricPatch3 patch, Plane3 plane, out Interval range)
    {
        if (patch.PointExpression is null) { range = default; return false; }
        var p = patch.PointExpression; return IntervalEvaluator.TryEvaluate(Scalar(p.X, p.Y, p.Z, plane), patch.Domain, out range);
    }

    private static SurfaceScalarExpression Scalar(SurfaceScalarExpression x, SurfaceScalarExpression y, SurfaceScalarExpression z, Plane3 plane)
    {
        var n = plane.Normal.ToVector();
        return SurfaceExpression.Subtract(SurfaceExpression.Add(SurfaceExpression.Add(Scale(x, n.X), Scale(y, n.Y)), Scale(z, n.Z)), SurfaceExpression.Length(n.Dot(plane.Origin - Point3D.Origin)));
    }
    private static SurfaceScalarExpression Scale(SurfaceScalarExpression x, double factor) => SurfaceExpression.Multiply(x, SurfaceExpression.Number(factor));

    private static IEnumerable<PatchPlaneSample> PatchPlaneCandidates(BoundedParametricPatch3 patch, Plane3 plane, IntersectionPolicy policy)
    {
        var n = Samples(policy.SubdivisionBudget); var list = new List<PatchPlaneSample>();
        for (var i = 0; i < n; i++) for (var j = 0; j < n; j++)
        {
            var u = patch.Domain.U.Map(i / (double)(n - 1)); var v = patch.Domain.V.Map(j / (double)(n - 1));
            try { var point = patch.EvaluatePoint(u, v); var d = plane.SignedDistance(point); if (double.Abs(d) <= policy.LinearTolerance) list.Add(new(u, v, point, d)); } catch (ArithmeticException) { }
        }
        return list.OrderBy(x => double.Abs(x.Distance)).ThenBy(x => x.U).ThenBy(x => x.V);
    }

    private static ContactObservation PatchPlaneObservation(BoundedParametricPatch3 patch, Plane3 plane, double u, double v, IntersectionPolicy policy)
    {
        var jet = patch.Evaluate(u, v); var dot = jet.Normal?.ToVector().Dot(plane.Normal.ToVector());
        return new(null, dot, null, patch.SupportsSecondJet, dot is double d && 1d - double.Abs(d) <= policy.AngularTolerance ? "compatible tangent planes" : "transverse or singular tangent plane");
    }

    private static IntersectionWitness Witness(BoundedParametricCurve3 curve, Plane3 plane, CurvePlaneSample s, IntersectionLocalRelation local, ContactObservation? contact)
        => new(s.Point, s.Point, s.Point, new(T: s.T), null, double.Abs(s.Distance), local, contact);
    private static IntersectionWitness Witness(BoundedParametricPatch3 patch, Plane3 plane, SignedSideWitness s, IntersectionLocalRelation local, ContactObservation? contact)
        => new(s.Point, s.Point, s.Point, new(U: s.U, V: s.V), null, double.Abs(s.SignedDistance), local, contact);
    private static IntersectionWitness Witness(BoundedParametricPatch3 patch, Plane3 plane, PatchPlaneSample s, IntersectionLocalRelation local, ContactObservation? contact)
        => new(s.Point, s.Point, s.Point, new(U: s.U, V: s.V), null, double.Abs(s.Distance), local, contact);

    private static IntersectionResult FromUnknown(ClosestPointResult c, object a, object b, IntersectionPolicy p)
        => Result(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, p, [], a, b, Stats(c), c.Diagnostics, "M3 distance query exhausted its bounded evidence budget");
    private static IntersectionResult FromClosest(IntersectionRelation relation, PredicateEvidenceKind evidence, ClosestPointResult c, object a, object b, IntersectionPolicy p, IReadOnlyList<GeometryQueryDiagnostic> diagnostics, string provenance, IReadOnlyList<IntersectionWitness>? witnesses = null)
        => Result(relation, evidence, p, witnesses ?? [], a, b, Stats(c), diagnostics, provenance);
    private static IntersectionStatistics Stats(ClosestPointResult c) => new(0, c.Statistics.Subdivisions, c.Statistics.Iterations, 1, c.Statistics.CandidateCount, c.Statistics.BudgetExhausted);
    private static IntersectionWitness BasicWitness(ClosestPointResult c, IntersectionLocalRelation relation)
    {
        var a = c.PointOnA ?? Point3D.Origin; var b = c.PointOnB ?? a;
        return new(Mid(a, b), a, b, Parameters(c.ParameterOnA), Parameters(c.ParameterOnB), c.ComputedDistance ?? (a - b).Length, relation, null);
    }
    private static IntersectionParameters? Parameters(DistanceParameters? p) => p is null ? null : new(p.T, p.U, p.V);

    private static IntersectionResult Unknown(object a, object b, IntersectionPolicy p, IReadOnlyList<GeometryQueryDiagnostic> diagnostics)
        => Result(IntersectionRelation.Unknown, PredicateEvidenceKind.Unknown, p, [], a, b, new(0, 0, 0, 0, 0, false), diagnostics, "invalid or unavailable bounded evidence");
    private static IntersectionResult Result(IntersectionRelation relation, PredicateEvidenceKind evidence, IntersectionPolicy policy, IReadOnlyList<IntersectionWitness> witnesses,
        object a, object b, IntersectionStatistics statistics, IReadOnlyList<GeometryQueryDiagnostic> diagnostics, string provenance)
        => new(relation, evidence, policy, witnesses, witnesses.Select(x => x.Residual).ToArray(), Domain(a), Domain(b), statistics, Operand(a), Operand(b), provenance, diagnostics);
    private static IntersectionResult Swap(IntersectionResult r) => r with
    {
        DomainA = r.DomainB, DomainB = r.DomainA, OperandA = r.OperandB, OperandB = r.OperandA,
        WitnessPoints = r.WitnessPoints.Select(w => w with { PointOnA = w.PointOnB, PointOnB = w.PointOnA, ParameterOnA = w.ParameterOnB, ParameterOnB = w.ParameterOnA }).ToArray()
    };

    private static IntersectionDomain Domain(object x) => x switch
    {
        BoundedParametricCurve3 c => new("Curve", c.Domain.Minimum, c.Domain.Maximum),
        BoundedParametricPatch3 p => new("Patch", p.Domain.U.Minimum, p.Domain.U.Maximum, p.Domain.V.Minimum, p.Domain.V.Maximum),
        Plane3 => new("UnboundedPlane", null, null),
        _ => new("Unknown", null, null)
    };
    private static IntersectionOperand Operand(object x) => x switch
    {
        BoundedParametricCurve3 c => new("Curve", c.Identity, c.Provenance),
        BoundedParametricPatch3 p => new("Patch", p.Identity, p.GeometryProvenance),
        Plane3 => new("Plane", null, new("query-plane")),
        _ => new("Unknown", null, null)
    };

    private static bool Interior(BoundedParametricCurve3 c, double t, double tolerance) => t > c.Domain.Minimum + tolerance && t < c.Domain.Maximum - tolerance;
    private static bool Interior(BoundedParametricPatch3 p, double u, double v, double tolerance) => u > p.Domain.U.Minimum + tolerance && u < p.Domain.U.Maximum - tolerance && v > p.Domain.V.Minimum + tolerance && v < p.Domain.V.Maximum - tolerance;
    private static bool Valid(Plane3 p) { var n = p.Normal.ToVector(); return double.IsFinite(n.X) && double.IsFinite(n.Y) && double.IsFinite(n.Z) && n.LengthSquared > .5d; }
    private static bool SameStrictSign(double a, double b, double tolerance) => (a > tolerance && b > tolerance) || (a < -tolerance && b < -tolerance);
    private static int Samples(int budget) => int.Clamp((int)double.Sqrt(budget), 3, 33);
    private static int MaxDepth(int budget) => int.Clamp((int)double.Log2(double.Max(1, budget)) / 2, 0, 12);
    private static Point3D Mid(Point3D a, Point3D b) => new((a.X + b.X) / 2d, (a.Y + b.Y) / 2d, (a.Z + b.Z) / 2d);
    private readonly record struct CurvePlaneSample(double T, Point3D Point, double Distance);
    private sealed record PatchPlaneSample(double U, double V, Point3D Point, double Distance);
}
