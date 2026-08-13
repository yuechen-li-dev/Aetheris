using Aetheris.Kernel.Core.Math;

namespace Aetheris.Geometry;

/// <summary>A contact classification. Crossing is retained separately from local transversality because a surface may cross while its first jet is stationary.</summary>
public enum ContactClassification
{
    Disjoint,
    Crossing,
    Transverse,
    Tangent,
    SecondOrderCompatible,
    HigherOrderCandidate,
    Coincident,
    Overlapping,
    Unknown
}

public enum ContactEvidenceScope { Local, WholeDomain, Structural }
public enum ContactOrderStatus { Exact, AtLeast, Candidate, Unknown }
public enum ContactDerivativeRelation { NonZeroWithinTolerance, ZeroWithinTolerance, Unavailable }
public enum ContactTangentRelation { Transverse, Compatible, Singular, Unknown }
public enum ContactNormalRelation { Parallel, NonParallel, Singular, NotApplicable, Unknown }
public enum ContactCurvatureRelation { Separating, Compatible, DirectionDependent, Unavailable, NotApplicable, Unknown }
public enum ContactDistanceRelation { Separated, WithinTolerance, StructuralCoincidence, Unknown }

public sealed record ContactPolicy
{
    public static ContactPolicy Default { get; } = new();
    public double LinearTolerance { get; init; } = 1e-9;
    public double AngularTolerance { get; init; } = 1e-9;
    public double CurvatureTolerance { get; init; } = 1e-9;
    public double ParameterTolerance { get; init; } = 1e-10;
    public int IterationBudget { get; init; } = 96;
    public int SubdivisionBudget { get; init; } = 10_000;
    public int MaximumDerivativeOrderObserved { get; init; } = 2;

    public ContactPolicy Validate()
    {
        Positive(LinearTolerance, nameof(LinearTolerance));
        Positive(AngularTolerance, nameof(AngularTolerance));
        Positive(CurvatureTolerance, nameof(CurvatureTolerance));
        Positive(ParameterTolerance, nameof(ParameterTolerance));
        if (IterationBudget < 1) throw new ArgumentOutOfRangeException(nameof(IterationBudget));
        if (SubdivisionBudget < 16) throw new ArgumentOutOfRangeException(nameof(SubdivisionBudget));
        if (MaximumDerivativeOrderObserved is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(MaximumDerivativeOrderObserved), "M5 observes only first and second jets.");
        return this;
    }

    internal IntersectionPolicy IntersectionPolicy => new()
    {
        LinearTolerance = LinearTolerance,
        AngularTolerance = AngularTolerance,
        ParameterTolerance = ParameterTolerance,
        IterationBudget = IterationBudget,
        SubdivisionBudget = SubdivisionBudget
    };

    private static void Positive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(name);
    }
}

/// <summary>
/// Integer order is populated only for an admitted scalar reduction. In M5 this is curve/plane:
/// g(t)=n dot (C(t)-p). Finite agreement through the maximum checked derivative is only a lower
/// bound/candidate, never proof of the next derivative or of coincidence.
/// </summary>
public sealed record ContactOrderEvidence(
    ContactOrderStatus Status,
    int? Order,
    int? ProvenLowerBound,
    int MaximumDerivativeOrderChecked,
    PredicateEvidenceKind Evidence,
    string Diagnostic);

public sealed record ContactDerivativeObservation(
    int Order,
    double? Value,
    ContactDerivativeRelation Relation,
    PredicateEvidenceKind Evidence,
    string Meaning);

/// <summary>A geometric normal-curvature comparison in a physical tangent direction.</summary>
public sealed record ContactDirectionalObservation(
    Direction3D TangentDirection,
    double? NormalCurvatureA,
    double? NormalCurvatureB,
    double? Difference,
    ContactCurvatureRelation Relation,
    PredicateEvidenceKind Evidence,
    string Diagnostic);

public sealed record ContactWitness(
    Point3D Point,
    Point3D PointOnA,
    Point3D PointOnB,
    IntersectionParameters? ParameterOnA,
    IntersectionParameters? ParameterOnB,
    double Residual,
    ContactTangentRelation TangentRelation,
    ContactNormalRelation NormalRelation,
    ContactCurvatureRelation CurvatureRelation,
    IReadOnlyList<ContactDerivativeObservation> Derivatives,
    IReadOnlyList<ContactDirectionalObservation> DirectionalObservations);

/// <summary>Immutable evidence only. A contact result has no trim, topology, construction, placement, or response authority.</summary>
public sealed record ContactQueryResult(
    ContactClassification Classification,
    PredicateEvidenceKind Evidence,
    ContactEvidenceScope Scope,
    IReadOnlyList<ContactWitness> Witnesses,
    ContactDistanceRelation DistanceRelation,
    IntersectionRelation IntersectionRelation,
    SignedSideClassification? SideRelation,
    ContactTangentRelation TangentRelation,
    ContactNormalRelation NormalRelation,
    ContactCurvatureRelation CurvatureRelation,
    ContactOrderEvidence OrderEvidence,
    ContactPolicy ToleranceUsed,
    IntersectionStatistics Statistics,
    IReadOnlyList<GeometryQueryDiagnostic> Diagnostics,
    IntersectionOperand OperandA,
    IntersectionOperand OperandB,
    string Provenance)
{
    public bool? ContactExists => Classification switch
    {
        ContactClassification.Disjoint => false,
        ContactClassification.Unknown or ContactClassification.Overlapping => null,
        _ => true
    };
    public bool HasTopologyAuthority => false;
}

/// <summary>
/// Evidence-aware local contact classification composed from M0-M4 predicates. It observes authored
/// geometry and never materializes or modifies B-rep topology.
/// </summary>
public static class ContactQuery
{
    public static ContactQueryResult Between(BoundedParametricCurve3 curve, Plane3 plane) => Between(curve, plane, ContactPolicy.Default);
    public static ContactQueryResult Between(BoundedParametricCurve3 curve, Plane3 plane, ContactPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(curve); policy.Validate();
        var intersection = IntersectionQuery.Between(curve, plane, policy.IntersectionPolicy);
        if (intersection.Relation == IntersectionRelation.Disjoint) return Terminal(intersection, policy, ContactClassification.Disjoint, ContactEvidenceScope.WholeDomain);
        if (intersection.Relation == IntersectionRelation.Coincident) return Terminal(intersection, policy, ContactClassification.Coincident, ContactEvidenceScope.Structural);

        var source = intersection.WitnessPoints.OrderBy(w => w.Residual).ThenBy(w => w.ParameterOnA?.T).FirstOrDefault();
        if (source?.ParameterOnA?.T is not double t || source.Residual > policy.LinearTolerance)
            return Unknown(intersection, policy, "No bounded curve/plane contact witness was established.");
        try
        {
            var jet1 = curve.EvaluateJet1(t);
            if (!jet1.IsRegular || jet1.UnitTangent is null)
                return Unknown(intersection, policy, "The curve parameterization is singular at the contact witness.");
            var normal = plane.Normal.ToVector();
            var g1 = normal.Dot(jet1.Derivative);
            var normalizedFirst = normal.Dot(jet1.UnitTangent.Value.ToVector());
            var firstRelation = double.Abs(normalizedFirst) > double.Sin(policy.AngularTolerance)
                ? ContactDerivativeRelation.NonZeroWithinTolerance : ContactDerivativeRelation.ZeroWithinTolerance;
            var derivatives = new List<ContactDerivativeObservation>
            {
                new(0, plane.SignedDistance(jet1.Point), ContactDerivativeRelation.ZeroWithinTolerance, PredicateEvidenceKind.ToleranceBounded, "g(t)=n dot (C(t)-p)"),
                new(1, g1, firstRelation, PredicateEvidenceKind.ToleranceBounded, "g'(t)=n dot C'(t); zero/nonzero is angular-tolerance qualified")
            };
            if (firstRelation == ContactDerivativeRelation.NonZeroWithinTolerance)
            {
                var witness = MakeWitness(source, ContactTangentRelation.Transverse, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.NotApplicable, derivatives, []);
                return Result(intersection, policy, ContactClassification.Transverse, ContactEvidenceScope.Local, [witness],
                    ContactTangentRelation.Transverse, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.NotApplicable,
                    new(ContactOrderStatus.Exact, 1, 1, 1, PredicateEvidenceKind.ToleranceBounded, "Regular scalar reduction has g=0 within tolerance and definitively nonzero g'."));
            }

            if (policy.MaximumDerivativeOrderObserved < 2 || !curve.SupportsSecondJet)
            {
                derivatives.Add(new(2, null, ContactDerivativeRelation.Unavailable, PredicateEvidenceKind.Unknown, "Second jet is unavailable or excluded by policy."));
                var witness = MakeWitness(source, ContactTangentRelation.Compatible, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.Unavailable, derivatives, []);
                return Result(intersection, policy, ContactClassification.Tangent, ContactEvidenceScope.Local, [witness],
                    ContactTangentRelation.Compatible, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.Unavailable,
                    new(ContactOrderStatus.AtLeast, null, 2, 1, PredicateEvidenceKind.ToleranceBounded, "Only first-order compatibility is supported; exact order is unknown."));
            }

            var jet2 = curve.EvaluateJet2(t);
            if (!jet2.IsRegular || jet2.FirstDerivative.Length <= 1e-12)
                return Unknown(intersection, policy, "The second jet is singular at the scalar contact witness.");
            var g2 = normal.Dot(jet2.SecondDerivative);
            var invariantSecond = g2 / jet2.FirstDerivative.LengthSquared;
            var secondRelation = double.Abs(invariantSecond) > policy.CurvatureTolerance
                ? ContactDerivativeRelation.NonZeroWithinTolerance : ContactDerivativeRelation.ZeroWithinTolerance;
            derivatives.Add(new(2, g2, secondRelation, PredicateEvidenceKind.ToleranceBounded,
                "g''(t)=n dot C''(t); at stationary contact g''/|C'|^2 is parameterization invariant"));
            if (secondRelation == ContactDerivativeRelation.NonZeroWithinTolerance)
            {
                var witness = MakeWitness(source, ContactTangentRelation.Compatible, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.Separating, derivatives, []);
                return Result(intersection, policy, ContactClassification.Tangent, ContactEvidenceScope.Local, [witness],
                    ContactTangentRelation.Compatible, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.Separating,
                    new(ContactOrderStatus.Exact, 2, 2, 2, PredicateEvidenceKind.ToleranceBounded, "Regular admitted scalar reduction has g and g' zero within tolerance and definitively nonzero invariant g''."));
            }

            var higher = MakeWitness(source, ContactTangentRelation.Compatible, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.Compatible, derivatives, []);
            return Result(intersection, policy, ContactClassification.HigherOrderCandidate, ContactEvidenceScope.Local, [higher],
                ContactTangentRelation.Compatible, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.Compatible,
                new(ContactOrderStatus.AtLeast, null, 2, 2, PredicateEvidenceKind.ToleranceBounded,
                    "All derivatives available through order 2 vanish within tolerance. This neither proves order 3/4 nor coincidence."));
        }
        catch (Exception ex) when (ex is ArithmeticException or ArgumentOutOfRangeException or NotSupportedException)
        {
            return Unknown(intersection, policy, ex.Message);
        }
    }

    public static ContactQueryResult Between(Plane3 plane, BoundedParametricCurve3 curve) => Swap(Between(curve, plane));
    public static ContactQueryResult Between(Plane3 plane, BoundedParametricCurve3 curve, ContactPolicy policy) => Swap(Between(curve, plane, policy));

    public static ContactQueryResult Between(BoundedParametricPatch3 patch, Plane3 plane) => Between(patch, plane, ContactPolicy.Default);
    public static ContactQueryResult Between(BoundedParametricPatch3 patch, Plane3 plane, ContactPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(patch); policy.Validate();
        var intersection = IntersectionQuery.Between(patch, plane, policy.IntersectionPolicy);
        var side = SignedSideQuery.Query(patch, plane, SignedSidePolicy.Certified(policy.LinearTolerance,
            int.Clamp((int)double.Log2(policy.SubdivisionBudget) / 2, 0, 12), policy.SubdivisionBudget));
        if (intersection.Relation == IntersectionRelation.Disjoint) return Terminal(intersection, policy, ContactClassification.Disjoint, ContactEvidenceScope.WholeDomain, side.Classification);
        if (intersection.Relation == IntersectionRelation.Coincident) return Terminal(intersection, policy, ContactClassification.Coincident, ContactEvidenceScope.Structural, side.Classification);

        var source = BestPatchPlaneWitness(patch, plane, intersection, policy);
        if (side.Classification == SignedSideClassification.Crossing)
        {
            var witnesses = source is null ? Array.Empty<ContactWitness>() : new[] { PatchPlaneWitness(patch, plane, source, policy).Witness };
            return Result(intersection, policy, ContactClassification.Crossing, ContactEvidenceScope.WholeDomain, witnesses,
                witnesses.FirstOrDefault()?.TangentRelation ?? ContactTangentRelation.Unknown,
                witnesses.FirstOrDefault()?.NormalRelation ?? ContactNormalRelation.Unknown,
                witnesses.FirstOrDefault()?.CurvatureRelation ?? ContactCurvatureRelation.Unknown,
                UnknownOrder("A 2D signed-side crossing has no generic scalar contact order.", witnesses.Length == 0 ? 0 : policy.MaximumDerivativeOrderObserved), side.Classification);
        }
        if (source is null) return Unknown(intersection, policy, "SignedSide did not yield a regular zero witness.", side.Classification);
        var observation = PatchPlaneWitness(patch, plane, source, policy);
        if (observation.Witness.TangentRelation == ContactTangentRelation.Transverse)
            return Result(intersection, policy, ContactClassification.Transverse, ContactEvidenceScope.Local, [observation.Witness],
                ContactTangentRelation.Transverse, ContactNormalRelation.NonParallel, ContactCurvatureRelation.NotApplicable,
                UnknownOrder("Patch/plane contact does not use scalar integer multiplicity.", 1), side.Classification);
        var classification = observation.Witness.CurvatureRelation == ContactCurvatureRelation.Compatible
            ? ContactClassification.SecondOrderCompatible : ContactClassification.Tangent;
        return Result(intersection, policy, classification, ContactEvidenceScope.Local, [observation.Witness],
            observation.Witness.TangentRelation, observation.Witness.NormalRelation, observation.Witness.CurvatureRelation,
            UnknownOrder("Patch/plane reports directional differential compatibility, not an integer contact order.", policy.MaximumDerivativeOrderObserved), side.Classification);
    }

    public static ContactQueryResult Between(Plane3 plane, BoundedParametricPatch3 patch) => Swap(Between(patch, plane));
    public static ContactQueryResult Between(Plane3 plane, BoundedParametricPatch3 patch, ContactPolicy policy) => Swap(Between(patch, plane, policy));

    public static ContactQueryResult Between(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch) => Between(curve, patch, ContactPolicy.Default);
    public static ContactQueryResult Between(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch, ContactPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(curve); ArgumentNullException.ThrowIfNull(patch); policy.Validate();
        var intersection = IntersectionQuery.Between(curve, patch, policy.IntersectionPolicy);
        if (intersection.Relation == IntersectionRelation.Disjoint) return Terminal(intersection, policy, ContactClassification.Disjoint, ContactEvidenceScope.WholeDomain);
        var source = intersection.WitnessPoints.OrderBy(w => w.Residual).FirstOrDefault();
        if (source?.ParameterOnA?.T is not double t || source.ParameterOnB?.U is not double u || source.ParameterOnB.V is not double v || source.Residual > policy.LinearTolerance)
            return Unknown(intersection, policy, "M4 did not establish a bounded curve/patch contact witness.");
        try
        {
            var cj = curve.EvaluateJet1(t); var pj = patch.EvaluateJet1(u, v);
            if (!cj.IsRegular || cj.UnitTangent is null || pj.Normal is null) return Unknown(intersection, policy, "A first jet is singular at the contact witness.");
            var dot = pj.Normal.Value.ToVector().Dot(cj.UnitTangent.Value.ToVector());
            if (double.Abs(dot) > double.Sin(policy.AngularTolerance))
            {
                var derivatives = new[] { new ContactDerivativeObservation(1, dot, ContactDerivativeRelation.NonZeroWithinTolerance, PredicateEvidenceKind.ToleranceBounded, "patch normal dot curve unit tangent") };
                var witness = MakeWitness(source, ContactTangentRelation.Transverse, ContactNormalRelation.NotApplicable, ContactCurvatureRelation.NotApplicable, derivatives, []);
                return Result(intersection, policy, ContactClassification.Transverse, ContactEvidenceScope.Local, [witness], ContactTangentRelation.Transverse,
                    ContactNormalRelation.NotApplicable, ContactCurvatureRelation.NotApplicable, UnknownOrder("Curve/patch M5 does not assign generic integer order.", 1));
            }
            var directional = CurvePatchCurvature(curve, patch, t, u, v, pj.Normal.Value, policy);
            var curvatureRelation = directional?.Relation ?? ContactCurvatureRelation.Unavailable;
            var tangentDerivatives = new[] { new ContactDerivativeObservation(1, dot, ContactDerivativeRelation.ZeroWithinTolerance, PredicateEvidenceKind.ToleranceBounded, "patch normal dot curve unit tangent") };
            var tangentWitness = MakeWitness(source, ContactTangentRelation.Compatible, ContactNormalRelation.NotApplicable, curvatureRelation,
                tangentDerivatives, directional is null ? [] : [directional]);
            var classification = curvatureRelation == ContactCurvatureRelation.Compatible ? ContactClassification.SecondOrderCompatible : ContactClassification.Tangent;
            return Result(intersection, policy, classification, ContactEvidenceScope.Local, [tangentWitness], ContactTangentRelation.Compatible,
                ContactNormalRelation.NotApplicable, curvatureRelation, UnknownOrder("Curve/patch reports first/second-order geometry without generic scalar multiplicity.", policy.MaximumDerivativeOrderObserved));
        }
        catch (Exception ex) when (ex is ArithmeticException or ArgumentOutOfRangeException or NotSupportedException) { return Unknown(intersection, policy, ex.Message); }
    }

    public static ContactQueryResult Between(BoundedParametricPatch3 patch, BoundedParametricCurve3 curve) => Swap(Between(curve, patch));
    public static ContactQueryResult Between(BoundedParametricPatch3 patch, BoundedParametricCurve3 curve, ContactPolicy policy) => Swap(Between(curve, patch, policy));

    public static ContactQueryResult Between(BoundedParametricPatch3 a, BoundedParametricPatch3 b) => Between(a, b, ContactPolicy.Default);
    public static ContactQueryResult Between(BoundedParametricPatch3 a, BoundedParametricPatch3 b, ContactPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(a); ArgumentNullException.ThrowIfNull(b); policy.Validate();
        var intersection = IntersectionQuery.Between(a, b, policy.IntersectionPolicy);
        if (intersection.Relation == IntersectionRelation.Disjoint) return Terminal(intersection, policy, ContactClassification.Disjoint, ContactEvidenceScope.WholeDomain);
        if (intersection.Relation == IntersectionRelation.Coincident) return Terminal(intersection, policy, ContactClassification.Coincident, ContactEvidenceScope.Structural);
        var source = intersection.WitnessPoints.OrderBy(w => w.Residual).FirstOrDefault();
        if (source?.ParameterOnA?.U is not double au || source.ParameterOnA.V is not double av || source.ParameterOnB?.U is not double bu || source.ParameterOnB.V is not double bv || source.Residual > policy.LinearTolerance)
            return Unknown(intersection, policy, "M4 did not establish a bounded patch/patch contact witness.");
        try
        {
            var ja = a.EvaluateJet1(au, av); var jb = b.EvaluateJet1(bu, bv);
            if (ja.Normal is null || jb.Normal is null) return Unknown(intersection, policy, "A patch tangent plane is singular at the contact witness.");
            var dot = ja.Normal.Value.ToVector().Dot(jb.Normal.Value.ToVector());
            var parallel = 1 - double.Abs(dot) <= policy.AngularTolerance;
            if (!parallel)
            {
                var witness = MakeWitness(source, ContactTangentRelation.Transverse, ContactNormalRelation.NonParallel, ContactCurvatureRelation.NotApplicable, [], []);
                return Result(intersection, policy, ContactClassification.Transverse, ContactEvidenceScope.Local, [witness], ContactTangentRelation.Transverse,
                    ContactNormalRelation.NonParallel, ContactCurvatureRelation.NotApplicable, UnknownOrder("Patch/patch has no generic scalar integer order.", 1));
            }
            var observations = PatchPatchCurvatures(a, b, au, av, bu, bv, ja, dot, policy);
            var curvature = Combine(observations);
            var witness2 = MakeWitness(source, ContactTangentRelation.Compatible, ContactNormalRelation.Parallel, curvature, [], observations);
            var classification = curvature == ContactCurvatureRelation.Compatible ? ContactClassification.SecondOrderCompatible : ContactClassification.Tangent;
            return Result(intersection, policy, classification, ContactEvidenceScope.Local, [witness2], ContactTangentRelation.Compatible,
                ContactNormalRelation.Parallel, curvature, UnknownOrder("Patch/patch reports geometric directional compatibility and never assigns integer multiplicity.", policy.MaximumDerivativeOrderObserved));
        }
        catch (Exception ex) when (ex is ArithmeticException or ArgumentOutOfRangeException or NotSupportedException) { return Unknown(intersection, policy, ex.Message); }
    }

    private static (ContactWitness Witness, ContactCurvatureRelation Curvature) PatchPlaneWitness(BoundedParametricPatch3 patch, Plane3 plane, IntersectionWitness source, ContactPolicy policy)
    {
        var u = source.ParameterOnA!.U!.Value; var v = source.ParameterOnA.V!.Value; var jet = patch.EvaluateJet1(u, v);
        if (jet.Normal is null) return (MakeWitness(source, ContactTangentRelation.Singular, ContactNormalRelation.Singular, ContactCurvatureRelation.Unknown, [], []), ContactCurvatureRelation.Unknown);
        var dot = jet.Normal.Value.ToVector().Dot(plane.Normal.ToVector());
        if (1 - double.Abs(dot) > policy.AngularTolerance)
            return (MakeWitness(source, ContactTangentRelation.Transverse, ContactNormalRelation.NonParallel, ContactCurvatureRelation.NotApplicable, [], []), ContactCurvatureRelation.NotApplicable);
        var directions = TangentBasis(jet.Du, jet.Normal.Value);
        var observations = policy.MaximumDerivativeOrderObserved < 2 ? Array.Empty<ContactDirectionalObservation>() : directions.Select(direction =>
        {
            var k = CurvatureQuery.NormalCurvature(patch, u, v, direction.ToVector());
            if (k.Status != DifferentialQueryStatus.Available) return new ContactDirectionalObservation(direction, k.Curvature, 0d, null, ContactCurvatureRelation.Unavailable, PredicateEvidenceKind.Unknown, k.Diagnostic ?? "Normal curvature unavailable.");
            var value = k.Curvature!.Value * (dot >= 0 ? 1 : -1);
            var relation = double.Abs(value) <= policy.CurvatureTolerance ? ContactCurvatureRelation.Compatible : ContactCurvatureRelation.Separating;
            return new ContactDirectionalObservation(direction, value, 0d, value, relation, PredicateEvidenceKind.ToleranceBounded, "Compared with zero plane normal curvature.");
        }).ToArray();
        var combined = Combine(observations);
        return (MakeWitness(source, ContactTangentRelation.Compatible, ContactNormalRelation.Parallel, combined, [], observations), combined);
    }

    private static ContactDirectionalObservation? CurvePatchCurvature(BoundedParametricCurve3 curve, BoundedParametricPatch3 patch, double t, double u, double v, Direction3D patchNormal, ContactPolicy policy)
    {
        if (policy.MaximumDerivativeOrderObserved < 2 || !curve.SupportsSecondJet || !patch.SupportsSecondJet) return null;
        var curveJet = curve.EvaluateJet2(t); if (!curveJet.IsRegular || !curveJet.FirstDerivative.TryNormalize(out var tangent)) return null;
        var patchK = CurvatureQuery.NormalCurvature(patch, u, v, tangent);
        if (patchK.Status != DifferentialQueryStatus.Available) return new(Direction3D.Create(tangent), null, patchK.Curvature, null, ContactCurvatureRelation.Unavailable, PredicateEvidenceKind.Unknown, patchK.Diagnostic ?? "Patch normal curvature unavailable.");
        var curveNormalCurvature = patchNormal.ToVector().Dot(curveJet.SecondDerivative) / curveJet.FirstDerivative.LengthSquared;
        var difference = curveNormalCurvature - patchK.Curvature!.Value;
        var relation = double.Abs(difference) <= policy.CurvatureTolerance ? ContactCurvatureRelation.Compatible : ContactCurvatureRelation.Separating;
        return new(Direction3D.Create(tangent), curveNormalCurvature, patchK.Curvature, difference, relation, PredicateEvidenceKind.ToleranceBounded,
            "Curve normal acceleration compared with patch normal curvature in the curve tangent direction.");
    }

    private static IReadOnlyList<ContactDirectionalObservation> PatchPatchCurvatures(BoundedParametricPatch3 a, BoundedParametricPatch3 b,
        double au, double av, double bu, double bv, SurfaceDifferential ja, double normalDot, ContactPolicy policy)
    {
        if (policy.MaximumDerivativeOrderObserved < 2 || !a.SupportsSecondJet || !b.SupportsSecondJet) return [];
        var sign = normalDot >= 0 ? 1d : -1d;
        return TangentBasis(ja.Du, ja.Normal!.Value).Select(direction =>
        {
            var ka = CurvatureQuery.NormalCurvature(a, au, av, direction.ToVector());
            var kb = CurvatureQuery.NormalCurvature(b, bu, bv, direction.ToVector());
            if (ka.Status != DifferentialQueryStatus.Available || kb.Status != DifferentialQueryStatus.Available)
                return new ContactDirectionalObservation(direction, ka.Curvature, kb.Curvature, null, ContactCurvatureRelation.Unavailable, PredicateEvidenceKind.Unknown, ka.Diagnostic ?? kb.Diagnostic ?? "Normal curvature unavailable.");
            var orientedB = kb.Curvature!.Value * sign; var difference = ka.Curvature!.Value - orientedB;
            var relation = double.Abs(difference) <= policy.CurvatureTolerance ? ContactCurvatureRelation.Compatible : ContactCurvatureRelation.Separating;
            return new ContactDirectionalObservation(direction, ka.Curvature, orientedB, difference, relation, PredicateEvidenceKind.ToleranceBounded,
                "Geometric normal curvatures compared in a shared physical tangent direction.");
        }).ToArray();
    }

    // Three directions determine the symmetric quadratic normal-curvature form, including its mixed term.
    private static IReadOnlyList<Direction3D> TangentBasis(Vector3D seed, Direction3D normal)
    {
        var projected = seed - normal.ToVector() * seed.Dot(normal.ToVector());
        if (!projected.TryNormalize(out var e1)) return [];
        var e2 = normal.ToVector().Cross(e1); e2.TryNormalize(out e2);
        var diagonal = e1 + e2; diagonal.TryNormalize(out diagonal);
        return [Direction3D.Create(e1), Direction3D.Create(e2), Direction3D.Create(diagonal)];
    }

    private static ContactCurvatureRelation Combine(IReadOnlyList<ContactDirectionalObservation> observations)
    {
        if (observations.Count == 0 || observations.Any(x => x.Relation == ContactCurvatureRelation.Unavailable)) return ContactCurvatureRelation.Unavailable;
        var compatible = observations.Count(x => x.Relation == ContactCurvatureRelation.Compatible);
        if (compatible == observations.Count) return ContactCurvatureRelation.Compatible;
        if (compatible > 0) return ContactCurvatureRelation.DirectionDependent;
        return ContactCurvatureRelation.Separating;
    }

    private static IntersectionWitness? BestPatchPlaneWitness(BoundedParametricPatch3 patch, Plane3 plane, IntersectionResult intersection, ContactPolicy policy)
    {
        var fromIntersection = intersection.WitnessPoints.Where(w => w.ParameterOnA?.U is not null && w.Residual <= policy.LinearTolerance)
            .OrderBy(w => w.Residual).ThenBy(w => CenterDistance(patch, w.ParameterOnA!)).FirstOrDefault();
        if (fromIntersection is not null) return fromIntersection;
        var n = int.Clamp((int)double.Sqrt(policy.SubdivisionBudget), 5, 33); if (n % 2 == 0) n--;
        IntersectionWitness? best = null;
        for (var i = 0; i < n; i++) for (var j = 0; j < n; j++)
        {
            var u = patch.Domain.U.Map(i / (double)(n - 1)); var v = patch.Domain.V.Map(j / (double)(n - 1));
            try
            {
                var point = patch.EvaluatePoint(u, v); var residual = double.Abs(plane.SignedDistance(point));
                if (residual > policy.LinearTolerance) continue;
                var candidate = new IntersectionWitness(point, point, point, new(U: u, V: v), null, residual, IntersectionLocalRelation.Unclassified, null);
                if (best is null || residual < best.Residual || residual == best.Residual && CenterDistance(patch, candidate.ParameterOnA!) < CenterDistance(patch, best.ParameterOnA!)) best = candidate;
            }
            catch (ArithmeticException) { }
        }
        return best;
    }

    private static double CenterDistance(BoundedParametricPatch3 patch, IntersectionParameters p)
    {
        var cu = (patch.Domain.U.Minimum + patch.Domain.U.Maximum) / 2; var cv = (patch.Domain.V.Minimum + patch.Domain.V.Maximum) / 2;
        return double.Pow((p.U ?? cu) - cu, 2) + double.Pow((p.V ?? cv) - cv, 2);
    }

    private static ContactWitness MakeWitness(IntersectionWitness source, ContactTangentRelation tangent, ContactNormalRelation normal,
        ContactCurvatureRelation curvature, IReadOnlyList<ContactDerivativeObservation> derivatives, IReadOnlyList<ContactDirectionalObservation> directions)
        => new(source.Point, source.PointOnA, source.PointOnB, source.ParameterOnA, source.ParameterOnB, source.Residual, tangent, normal, curvature, derivatives, directions);

    private static ContactOrderEvidence UnknownOrder(string diagnostic, int maximumChecked = 0)
        => new(ContactOrderStatus.Unknown, null, null, maximumChecked, PredicateEvidenceKind.Unknown, diagnostic);

    private static ContactQueryResult Terminal(IntersectionResult intersection, ContactPolicy policy, ContactClassification classification,
        ContactEvidenceScope scope, SignedSideClassification? side = null)
    {
        var distance = classification == ContactClassification.Disjoint ? ContactDistanceRelation.Separated : ContactDistanceRelation.StructuralCoincidence;
        var order = UnknownOrder(classification == ContactClassification.Coincident ? "Coincidence has no finite contact order." : "No contact exists.");
        return new(classification, intersection.Evidence, scope, [], distance, intersection.Relation, side, ContactTangentRelation.Unknown,
            ContactNormalRelation.Unknown, ContactCurvatureRelation.NotApplicable, order, policy, intersection.Statistics,
            intersection.Diagnostics, intersection.OperandA, intersection.OperandB, "ContactQuery composed from " + intersection.Provenance);
    }

    private static ContactQueryResult Unknown(IntersectionResult intersection, ContactPolicy policy, string diagnostic, SignedSideClassification? side = null)
        => Result(intersection, policy, ContactClassification.Unknown, ContactEvidenceScope.Local, [], ContactTangentRelation.Unknown,
            ContactNormalRelation.Unknown, ContactCurvatureRelation.Unknown, UnknownOrder(diagnostic), side,
            [.. intersection.Diagnostics, new(GeometryQueryDiagnosticCode.EvidenceUnavailable, diagnostic)]);

    private static ContactQueryResult Result(IntersectionResult intersection, ContactPolicy policy, ContactClassification classification,
        ContactEvidenceScope scope, IReadOnlyList<ContactWitness> witnesses, ContactTangentRelation tangent, ContactNormalRelation normal,
        ContactCurvatureRelation curvature, ContactOrderEvidence order, SignedSideClassification? side = null,
        IReadOnlyList<GeometryQueryDiagnostic>? diagnostics = null)
        => new(classification, classification == ContactClassification.Unknown ? PredicateEvidenceKind.Unknown : intersection.Evidence == PredicateEvidenceKind.Unknown ? PredicateEvidenceKind.ToleranceBounded : intersection.Evidence,
            scope, witnesses, witnesses.Count > 0 ? ContactDistanceRelation.WithinTolerance : ContactDistanceRelation.Unknown,
            intersection.Relation, side, tangent, normal, curvature, order, policy, intersection.Statistics,
            diagnostics ?? intersection.Diagnostics, intersection.OperandA, intersection.OperandB, "ContactQuery composed from " + intersection.Provenance);

    private static ContactQueryResult Swap(ContactQueryResult result) => result with
    {
        OperandA = result.OperandB,
        OperandB = result.OperandA,
        Witnesses = result.Witnesses.Select(w => w with
        {
            PointOnA = w.PointOnB,
            PointOnB = w.PointOnA,
            ParameterOnA = w.ParameterOnB,
            ParameterOnB = w.ParameterOnA,
            DirectionalObservations = w.DirectionalObservations.Select(d => d with
            {
                NormalCurvatureA = d.NormalCurvatureB,
                NormalCurvatureB = d.NormalCurvatureA,
                Difference = d.Difference is null ? null : -d.Difference
            }).ToArray()
        }).ToArray()
    };
}
