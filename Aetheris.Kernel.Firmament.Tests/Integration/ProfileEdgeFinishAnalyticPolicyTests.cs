using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class ProfileEdgeFinishAnalyticPolicyTests
{
    public static IEnumerable<object[]> ChamferRows()
    {
        yield return Row("ConvexSharp", ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Convex, 0d, "LineChamferPlan", ProfileEdgeFinishSurfaceFamily.Plane, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ConvexSmall", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 2d, "ArcChamferCollapsedOffsetRejected", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.Invalid, ProfileEdgeFinishAdmission.UnsupportedWithTypedDiagnostic);
        yield return Row("ConvexMedium", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 4d, "ArcChamferApexPlan", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.BoundedDegenerate, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy);
        yield return Row("ConvexLarge", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 8d, "ArcChamferConePlan", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ReflexSharp", ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Reflex, 0d, "LineChamferPlan", ProfileEdgeFinishSurfaceFamily.Plane, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ReflexSmall", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex, 2d, "ArcChamferConePlan", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ReflexMedium", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex, 4d, "ArcChamferConePlan", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ReflexLarge", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex, 8d, "ArcChamferConePlan", ProfileEdgeFinishSurfaceFamily.Cone, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
    }

    public static IEnumerable<object[]> FilletRows()
    {
        yield return Row("ConvexSharp", ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Convex, 0d, "ConvexSharpSphereJunctionPlan", ProfileEdgeFinishSurfaceFamily.Sphere, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ConvexSmall", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 2d, "ArcFilletSpindleRejected", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.Invalid, ProfileEdgeFinishAdmission.UnsupportedWithTypedDiagnostic);
        yield return Row("ConvexMedium", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 4d, "ArcFilletSphereLimitPlan", ProfileEdgeFinishSurfaceFamily.Sphere, ProfileEdgeFinishRegularity.BoundedDegenerate, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy);
        yield return Row("ConvexLarge", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 8d, "ArcFilletTorusPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.InteropSensitive, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy);
        yield return Row("ReflexSharp", ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Reflex, 0d, "ReflexSharpExactRollingPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.InteropSensitive, ProfileEdgeFinishAdmission.SupportedWithExplicitPolicy);
        yield return Row("ReflexSmall", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex, 2d, "ArcFilletTorusPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ReflexMedium", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex, 4d, "ArcFilletTorusPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
        yield return Row("ReflexLarge", ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Reflex, 8d, "ArcFilletTorusPlan", ProfileEdgeFinishSurfaceFamily.Torus, ProfileEdgeFinishRegularity.Regular, ProfileEdgeFinishAdmission.Supported);
    }

    [Theory]
    [MemberData(nameof(ChamferRows))]
    public void ChamferMatrixIsExplicit(string station, ProfileEdgeFinishSourceFamily source, ProfileEdgeFinishMaterialSide material, double radius, string planner, ProfileEdgeFinishSurfaceFamily surface, ProfileEdgeFinishRegularity regularity, ProfileEdgeFinishAdmission admission)
        => AssertPolicy(new(station, ProfileEdgeFinishKind.Chamfer, source, material, radius, 4d), planner, surface, regularity, admission);

    [Theory]
    [MemberData(nameof(FilletRows))]
    public void FilletMatrixIsExplicit(string station, ProfileEdgeFinishSourceFamily source, ProfileEdgeFinishMaterialSide material, double radius, string planner, ProfileEdgeFinishSurfaceFamily surface, ProfileEdgeFinishRegularity regularity, ProfileEdgeFinishAdmission admission)
        => AssertPolicy(new(station, ProfileEdgeFinishKind.Fillet, source, material, radius, 4d), planner, surface, regularity, admission);

    [Fact]
    public void TorusAndSphereLimitsAreTypedRatherThanAccidental()
    {
        var sphere = ProfileEdgeFinishAnalyticPolicy.Classify(new("ConvexMedium", ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 4d, 4d));
        var horn = ProfileEdgeFinishAnalyticPolicy.Classify(new("ConvexLarge", ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 8d, 4d));
        var spindle = ProfileEdgeFinishAnalyticPolicy.Classify(new("ConvexSmall", ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.ArcDerived, ProfileEdgeFinishMaterialSide.Convex, 2d, 4d));

        Assert.Equal(ProfileEdgeFinishSurfaceFamily.Sphere, sphere.SurfaceFamily);
        Assert.Equal(ProfileEdgeFinishTorusRegime.Horn, horn.TorusRegime);
        Assert.Equal(4d, horn.TorusMajorRadius);
        Assert.Equal(ProfileEdgeFinishTorusRegime.Spindle, spindle.TorusRegime);
        Assert.Equal("ProfileBoundaryFilletSpindlePatchInvalid", spindle.ExpectedDiagnostic);
    }

    [Fact]
    public void SharpReflexCompatibilityIsAnExplicitAlternativeToExactRolling()
    {
        var compatibility = ProfileEdgeFinishAnalyticPolicy.Classify(new("ReflexSharp", ProfileEdgeFinishKind.Fillet, ProfileEdgeFinishSourceFamily.SharpLineLine, ProfileEdgeFinishMaterialSide.Reflex, 0d, 4d, ProfileReflexJunctionStyle.SphereSeamCompatibility));

        Assert.Equal("ReflexSharpSphereSeamCompatibilityPlan", compatibility.PlannerKind);
        Assert.Equal(ProfileEdgeFinishSurfaceFamily.Sphere, compatibility.SurfaceFamily);
        Assert.Equal("SphereSeamCompatibility", compatibility.CompatibilityOverride);
    }

    private static object[] Row(string station, ProfileEdgeFinishSourceFamily source, ProfileEdgeFinishMaterialSide material, double radius, string planner, ProfileEdgeFinishSurfaceFamily surface, ProfileEdgeFinishRegularity regularity, ProfileEdgeFinishAdmission admission)
        => [station, source, material, radius, planner, surface, regularity, admission];

    private static void AssertPolicy(ProfileEdgeFinishStationContext context, string planner, ProfileEdgeFinishSurfaceFamily surface, ProfileEdgeFinishRegularity regularity, ProfileEdgeFinishAdmission admission)
    {
        var policy = ProfileEdgeFinishAnalyticPolicy.Classify(context);
        Assert.Equal(planner, policy.PlannerKind);
        Assert.Equal(surface, policy.SurfaceFamily);
        Assert.Equal(regularity, policy.Regularity);
        Assert.Equal(admission, policy.Admission);
        Assert.NotEmpty(policy.RejectedCandidates);
    }
}
