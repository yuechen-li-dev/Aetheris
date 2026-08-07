using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// The finite analytic-policy card for source-bound Profile EdgeFinish.  This is
/// intentionally evaluated before topology emission: it selects a known exact
/// family or gives the source-preserving reason why no such patch is admitted.
/// It does not infer anonymous BRep edges and it has no spline fallback.
/// </summary>
public enum ProfileEdgeFinishKind { Chamfer, Fillet }
public enum ProfileEdgeFinishSourceFamily { SharpLineLine, ArcDerived }
public enum ProfileEdgeFinishMaterialSide { Convex, Reflex }
public enum ProfileEdgeFinishRadiusRelation { Zero, LessThan, Equal, GreaterThan }
public enum ProfileEdgeFinishSurfaceFamily { Plane, Cone, Cylinder, Sphere, Torus }
public enum ProfileEdgeFinishRegularity { Regular, BoundedDegenerate, InteropSensitive, Invalid }
public enum ProfileEdgeFinishAdmission { Supported, SupportedWithExplicitPolicy, UnsupportedWithTypedDiagnostic }
public enum ProfileEdgeFinishTorusRegime { None, Ring, Horn, Spindle }

public sealed record ProfileEdgeFinishStationContext(
    string Station,
    ProfileEdgeFinishKind FinishKind,
    ProfileEdgeFinishSourceFamily SourceFamily,
    ProfileEdgeFinishMaterialSide MaterialSide,
    double SourceRadius,
    double FinishSize,
    ProfileReflexJunctionStyle ReflexJunctionStyle = ProfileReflexJunctionStyle.ToroidalRolling);

public sealed record ProfileEdgeFinishPlannerPolicy(
    string Station,
    ProfileEdgeFinishKind FinishKind,
    ProfileEdgeFinishSourceFamily SourceFamily,
    ProfileEdgeFinishMaterialSide MaterialSide,
    double SourceRadius,
    double FinishSize,
    ProfileEdgeFinishRadiusRelation RadiusRelation,
    string PlannerKind,
    ProfileEdgeFinishSurfaceFamily SurfaceFamily,
    ProfileEdgeFinishRegularity Regularity,
    ProfileEdgeFinishAdmission Admission,
    ProfileEdgeFinishTorusRegime TorusRegime,
    double? TorusMajorRadius,
    double? TorusMinorRadius,
    string? CompatibilityOverride,
    string? ExpectedDiagnostic,
    double UtilityScore,
    IReadOnlyList<string> RejectedCandidates);

public static class ProfileEdgeFinishAnalyticPolicy
{
    private const double Tolerance = 1e-8;

    public static ProfileEdgeFinishPlannerPolicy Classify(ProfileEdgeFinishStationContext context)
    {
        Validate(context);
        var relation = Relation(context.SourceRadius, context.FinishSize, context.SourceFamily);
        var candidates = Candidates();
        var judgment = new JudgmentEngine<ProfileEdgeFinishStationContext>().Evaluate(context, candidates);
        if (!judgment.IsSuccess || !judgment.Selection.HasValue)
            throw new InvalidOperationException("Profile EdgeFinish policy has no bounded candidate for a valid classification context.");

        var candidate = judgment.Selection.Value;
        // JudgmentResult retains rejection details on all-rejected outcomes.  A
        // policy card needs them for successful selection as well, so preserve
        // the candidate predicates that did not admit this context explicitly.
        var rejected = candidates
            .Where(item => !item.IsAdmissible(context))
            .Select(item => $"{item.Name}:{item.RejectionReason?.Invoke(context) ?? "Candidate predicates were not satisfied."}")
            .ToArray();
        return Create(context, relation, candidate.Candidate.Name, candidate.Score, rejected);
    }

    private static IReadOnlyList<JudgmentCandidate<ProfileEdgeFinishStationContext>> Candidates() =>
    [
        new("ChamferSharpPlane", c => c.FinishKind == ProfileEdgeFinishKind.Chamfer && c.SourceFamily == ProfileEdgeFinishSourceFamily.SharpLineLine,
            _ => 100d, _ => "Requires a sharp line-line chamfer station."),
        new("ChamferConvexCollapsedOffset", c => Is(c, ProfileEdgeFinishKind.Chamfer, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex) && c.SourceRadius < c.FinishSize - Tolerance,
            _ => 10d, _ => "Requires Convex ArcDerived Rs < F."),
        new("ChamferConvexApex", c => Is(c, ProfileEdgeFinishKind.Chamfer, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex) && NearlyEqual(c.SourceRadius, c.FinishSize),
            _ => 90d, _ => "Requires Convex ArcDerived Rs = F."),
        new("ChamferConvexCone", c => Is(c, ProfileEdgeFinishKind.Chamfer, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex) && c.SourceRadius > c.FinishSize + Tolerance,
            _ => 100d, _ => "Requires Convex ArcDerived Rs > F."),
        new("ChamferReflexCone", c => Is(c, ProfileEdgeFinishKind.Chamfer, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex),
            _ => 100d, _ => "Requires Reflex ArcDerived station."),
        new("FilletConvexSharpMiter", c => Is(c, ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Convex),
            _ => 100d, _ => "Requires sharp convex station."),
        new("FilletReflexSharpSphereCompatibility", c => Is(c, ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Reflex) && c.ReflexJunctionStyle == ProfileReflexJunctionStyle.SphereSeamCompatibility,
            _ => 95d, _ => "Requires sharp reflex SphereSeamCompatibility override."),
        new("FilletReflexSharpExactRolling", c => Is(c, ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Reflex) && c.ReflexJunctionStyle == ProfileReflexJunctionStyle.ToroidalRolling,
            _ => 100d, _ => "Requires sharp reflex ExactRolling policy."),
        new("FilletConvexSpindle", c => Is(c, ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex) && c.SourceRadius < c.FinishSize - Tolerance,
            _ => 10d, _ => "Requires Convex ArcDerived Rs < F."),
        new("FilletConvexSphereLimit", c => Is(c, ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex) && NearlyEqual(c.SourceRadius, c.FinishSize),
            _ => 90d, _ => "Requires Convex ArcDerived Rs = F."),
        new("FilletConvexHorn", c => Is(c, ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex) && c.SourceRadius > c.FinishSize + Tolerance,
            _ => 80d, _ => "Requires Convex ArcDerived Rs > F."),
        new("FilletReflexRing", c => Is(c, ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex),
            _ => 100d, _ => "Requires Reflex ArcDerived station.")
    ];

    private static ProfileEdgeFinishPlannerPolicy Create(ProfileEdgeFinishStationContext c, ProfileEdgeFinishRadiusRelation relation, string candidate, double utility, IReadOnlyList<string> rejected) => candidate switch
    {
        "ChamferSharpPlane" => Policy(c, relation, "LineChamferPlan", ProfileEdgeFinishSurfaceFamily.Plane, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported, utility, rejected),
        "ChamferConvexCollapsedOffset" => Policy(c, relation, "ArcChamferCollapsedOffsetRejected", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.Invalid, ProfileEdgeFinishAdmission.UnsupportedWithTypedDiagnostic, utility, rejected, diagnostic: "ProfileBoundaryChamferCollapsedOffsetInvalid"),
        "ChamferConvexApex" => Policy(c, relation, "ArcChamferApexPlan", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.BoundedDegenerate, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy, utility, rejected),
        "ChamferConvexCone" or "ChamferReflexCone" => Policy(c, relation, "ArcChamferConePlan", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported, utility, rejected),
        "FilletConvexSharpMiter" => Policy(c, relation, "ConvexSharpCylinderMiterPlan", ProfileEdgeFinishSurfaceFamily.Cylinder, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported, utility, rejected),
        "FilletReflexSharpSphereCompatibility" => Policy(c, relation, "ReflexSharpSphereSeamCompatibilityPlan", ProfileEdgeFinishSurfaceFamily.Sphere, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy, utility, rejected, compatibility: "SphereSeamCompatibility"),
        "FilletReflexSharpExactRolling" => Policy(c, relation, "ReflexSharpExactRollingPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.InteropSensitive, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy, utility, rejected, torus: ProfileEdgeFinishTorusRegime.Horn, major: c.FinishSize, minor: c.FinishSize, compatibility: "SphereSeamCompatibility"),
        "FilletConvexSpindle" => Policy(c, relation, "ArcFilletSpindleRejected", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.Invalid, ProfileEdgeFinishAdmission.UnsupportedWithTypedDiagnostic, utility, rejected, torus: ProfileEdgeFinishTorusRegime.Spindle, major: c.FinishSize - c.SourceRadius, minor: c.FinishSize, diagnostic: "ProfileBoundaryFilletSpindlePatchInvalid"),
        "FilletConvexSphereLimit" => Policy(c, relation, "ArcFilletSphereLimitPlan", ProfileEdgeFinishSurfaceFamily.Sphere, ProfileEdgeFinishRegularity.BoundedDegenerate, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy, utility, rejected),
        "FilletConvexHorn" => Policy(c, relation, "ArcFilletTorusPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.InteropSensitive, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy, utility, rejected, torus: ProfileEdgeFinishTorusRegime.Horn, major: c.SourceRadius - c.FinishSize, minor: c.FinishSize),
        "FilletReflexRing" => Policy(c, relation, "ArcFilletTorusPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported, utility, rejected, torus: ProfileEdgeFinishTorusRegime.Ring, major: c.SourceRadius + c.FinishSize, minor: c.FinishSize),
        _ => throw new InvalidOperationException($"Unmapped Profile EdgeFinish policy candidate '{candidate}'.")
    };

    private static ProfileEdgeFinishPlannerPolicy Policy(ProfileEdgeFinishStationContext c, ProfileEdgeFinishRadiusRelation relation, string planner, ProfileEdgeFinishSurfaceFamily surface, ProfileEdgeFinishRegularity regularity, ProfileEdgeFinishAdmission admission, double utility, IReadOnlyList<string> rejected, ProfileEdgeFinishTorusRegime torus = ProfileEdgeFinishTorusRegime.None, double? major = null, double? minor = null, string? compatibility = null, string? diagnostic = null)
        => new(c.Station, c.FinishKind, c.SourceFamily, c.MaterialSide, c.SourceRadius, c.FinishSize, relation, planner, surface, regularity, admission, torus, major, minor, compatibility, diagnostic, utility, rejected);

    private static bool Is(ProfileEdgeFinishStationContext c, ProfileEdgeFinishKind finish, ProfileEdgeFinishSourceFamily source, ProfileEdgeFinishMaterialSide material)
        => c.FinishKind == finish && c.SourceFamily == source && c.MaterialSide == material;

    private static ProfileEdgeFinishRadiusRelation Relation(double sourceRadius, double finishSize, ProfileEdgeFinishSourceFamily source)
        => source == ProfileEdgeFinishSourceFamily.SharpLineLine ? ProfileEdgeFinishRadiusRelation.Zero
            : sourceRadius < finishSize - Tolerance ? ProfileEdgeFinishRadiusRelation.LessThan
            : sourceRadius > finishSize + Tolerance ? ProfileEdgeFinishRadiusRelation.GreaterThan
            : ProfileEdgeFinishRadiusRelation.Equal;

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) <= Tolerance;

    private static void Validate(ProfileEdgeFinishStationContext c)
    {
        if (string.IsNullOrWhiteSpace(c.Station) || !double.IsFinite(c.FinishSize) || c.FinishSize <= 0d || !double.IsFinite(c.SourceRadius) || c.SourceRadius < 0d)
            throw new ArgumentOutOfRangeException(nameof(c), "Profile EdgeFinish policy requires a station and finite non-negative source radius plus positive finish size.");
        if (c.SourceFamily == ProfileEdgeFinishSourceFamily.SharpLineLine && c.SourceRadius != 0d)
            throw new ArgumentException("SharpLineLine policy contexts must use source radius zero.", nameof(c));
    }
}
