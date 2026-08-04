using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Air;

/// <summary>
/// The first admitted exact fillet construction: a finite quarter-circle swept linearly
/// along the history-known +X/+Z edge of an axis-aligned box.  This is intentionally
/// not a general rolling-ball or direct-BRep edge surgery route.
/// </summary>
internal sealed record AirLocalizedTangentBlendWitness(
    string SupportPlaneA, string SupportPlaneB, Point3D SelectedEdgeStart, Point3D SelectedEdgeEnd,
    Point3D ArcCenterStart, Point3D ArcCenterEnd, Circle3Curve QuarterCircleStart,
    Circle3Curve QuarterCircleEnd, CylinderSurface BlendCylinder, double Radius,
    IReadOnlyList<Point3D> RetainedSupportFaceA, IReadOnlyList<Point3D> RetainedSupportFaceB,
    string MaterialSide, string EndpointPolicy, string Provenance);

internal sealed record AirLocalizedTangentBlendTopologyPlan(
    IReadOnlyList<Point3D> CrossSection, double MinY, double MaxY, double Radius,
    IReadOnlyList<string> FaceRoles, string DeterministicSignature,
    LocalizedEdgeReplacementTopologyPlan SharedTopologyPlan)
{
    public int ExpectedVertexCount => CrossSection.Count * 2;
    public int ExpectedEdgeCount => CrossSection.Count * 3;
    public int ExpectedFaceCount => CrossSection.Count + 2;
    public int ExpectedLoopCount => ExpectedFaceCount;
    public int ExpectedCoedgeCount => 6 * CrossSection.Count;
}

internal sealed record AirLocalizedTangentBlendConstruction(
    string ConstructionId, string SourceFeatureId, AirLocalizedTangentBlendWitness Witness,
    AirLocalizedTangentBlendTopologyPlan TopologyPlan,
    LocalizedEdgeReplacementConstruction SharedConstruction);

internal sealed record AirLocalizedTangentBlendFilletCompileRequest(
    string BodyId, string FeatureId, string FeatureName, double Width, double Depth, double Height,
    string FaceA, string FaceB, string Kind, double Radius, AirSourceSpan SourceSpan, bool HistoryKnown = true);

internal sealed record AirLocalizedTangentBlendFilletCompileResult(
    bool Succeeded, AirFilletFeature Feature, AirLocalizedTangentBlendConstruction? Construction,
    AirBRepPlan? BRepPlan, BrepBody? Body, ChamferLoweringError? Error, IReadOnlyList<string> Diagnostics)
{
    public const string ProductionRoute = "AirLocalizedTangentBlendSingleEdgeFillet";
}

internal sealed record AirConstantRadiusFilletRule(double Radius, string Unit = "mm");
internal sealed record AirFilletFeature(
    string FeatureId, string FeatureName, string BodyId, AirFaceBoundarySelection Selection,
    AirConstantRadiusFilletRule Rule, AirSourceSpan SourceSpan, string ConstructionHistoryKind,
    AirFeatureAdmissionStatus Admission, string AdmissionReason);

internal static class AirLocalizedTangentBlendFilletCompiler
{
    private const double Tol = 1e-9;

    public static AirLocalizedTangentBlendFilletCompileResult Compile(AirLocalizedTangentBlendFilletCompileRequest input)
    {
        var lowered = Lower(input);
        var feature = Feature(input, lowered.Error);
        if (!lowered.IsSuccess) return new(false, feature, null, null, null, lowered.Error, [lowered.Error!.Code]);

        var construction = lowered.Value!;
        var plan = BuildPlan(input, feature, construction);
        var emitted = AirLocalizedTangentBlendEmitter.Emit(construction.SharedConstruction);
        if (!emitted.Succeeded || emitted.Body is null)
        {
            var error = new ChamferLoweringError(ChamferLoweringErrorKind.BackendMaterializationDefect,
                "localized-fillet-materialization-failed", "The authoritative localized tangent-blend BRepPlan did not materialize.", "BRep", emitted.Diagnostics);
            return new(false, feature, construction, plan, null, error, [error.Code, .. emitted.Diagnostics]);
        }

        var body = emitted.Body;
        var cylinders = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
        var planes = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane);
        if (cylinders != 1 || planes != 6 || body.Topology.Faces.Count() != construction.TopologyPlan.ExpectedFaceCount)
        {
            var error = new ChamferLoweringError(ChamferLoweringErrorKind.VerificationFailure,
                "localized-fillet-analytic-topology-verification-failed", "The localized blend must contain six planar faces and one exact cylindrical face.", "Verification");
            return new(false, feature, construction, plan, body, error, [error.Code]);
        }
        return new(true, feature, construction, plan, body, null,
            ["localized-fillet-feature-admitted", "localized-fillet-direct-single-candidate", "localized-fillet-authoritative-brep-plan-consumed", "localized-fillet-exact-quarter-circle-linear-sweep", "localized-fillet-explicit-owned-endpoints"]);
    }

    public static ChamferLoweringResult<AirLocalizedTangentBlendConstruction> Lower(AirLocalizedTangentBlendFilletCompileRequest input)
    {
        ChamferLoweringResult<AirLocalizedTangentBlendConstruction> Fail(ChamferLoweringErrorKind kind, string code, string message) =>
            ChamferLoweringResult<AirLocalizedTangentBlendConstruction>.Err(new(kind, code, message, "FeatureAIR->ConstructionAIR"));
        if (!string.Equals(input.Kind, "Fillet", StringComparison.Ordinal)) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-fillet-invalid-kind", "This lowerer accepts constant-radius Fillet intent only.");
        var context = new LocalizedEdgeReplacementContext(input.BodyId, input.FeatureId, input.FeatureName, input.Width, input.Depth, input.Height, input.FaceA, input.FaceB, AirLocalizedEdgeFinishKind.Fillet, input.Radius, input.SourceSpan, input.HistoryKnown);
        var admitted = LocalizedEdgeReplacementCompilerModel.Admit(context);
        if (!admitted.IsSuccess) return ChamferLoweringResult<AirLocalizedTangentBlendConstruction>.Err(admitted.Error!);

        var hx = input.Width / 2d; var hy = input.Depth / 2d; var hz = input.Height / 2d; var r = input.Radius;
        var cross = new[] { new Point3D(-hx, -hy, -hz), new Point3D(hx, -hy, -hz), new Point3D(hx, -hy, hz - r), new Point3D(hx - r, -hy, hz), new Point3D(-hx, -hy, hz) };
        var centerStart = new Point3D(hx - r, -hy, hz - r);
        var centerEnd = new Point3D(hx - r, hy, hz - r);
        var yAxis = Direction3D.Create(new Vector3D(0, 1, 0));
        // The profile uses the opposite normal so its increasing trim [0,π/2]
        // follows the topology edge from the +X tangent to the +Z tangent.
        var profileNormal = Direction3D.Create(new Vector3D(0, -1, 0));
        var xAxis = Direction3D.Create(new Vector3D(1, 0, 0));
        var arcStart = new Circle3Curve(centerStart, profileNormal, r, xAxis);
        var arcEnd = new Circle3Curve(centerEnd, profileNormal, r, xAxis);
        var cylinder = new CylinderSurface(centerStart, yAxis, r, xAxis);
        var retainedX = new[] { new Point3D(hx, -hy, -hz), new Point3D(hx, hy, -hz), new Point3D(hx, hy, hz-r), new Point3D(hx, -hy, hz-r) };
        var retainedZ = new[] { new Point3D(-hx, -hy, hz), new Point3D(hx-r, -hy, hz), new Point3D(hx-r, hy, hz), new Point3D(-hx, hy, hz) };
        if ((cross[2] - cross[3]).Length <= Tol) return Fail(ChamferLoweringErrorKind.DegenerateTransition, "localized-fillet-degenerate-blend", "The tangent points produce a degenerate blend boundary.");
        var sharedTopology = LocalizedEdgeReplacementCompilerModel.Topology(context, cross);
        var topology = new AirLocalizedTangentBlendTopologyPlan(cross, -hy, hy, r,
            sharedTopology.FaceRoles, sharedTopology.DeterministicSignature, sharedTopology);
        var witness = new AirLocalizedTangentBlendWitness("plane(+X)", "plane(+Z)", new Point3D(hx, -hy, hz), new Point3D(hx, hy, hz), centerStart, centerEnd, arcStart, arcEnd, cylinder, r,
            retainedX, retainedZ, "inside:x<=max,z<=max; remove exterior corner outside quarter-circle", "ExplicitOwnedEndpoints", "history-known-axis-aligned-rectangular-prism");
        var shared = new LocalizedEdgeReplacementConstruction($"construction:{input.FeatureId}", input.FeatureId, admitted.Value!, retainedX, retainedZ, cross,
            new CylindricalFilletReplacement([cross[2], cross[3]], [new Point3D(hx, hy, hz - r), new Point3D(hx - r, hy, hz)], arcStart, arcEnd, cylinder, r), sharedTopology, "history-known-axis-aligned-rectangular-prism");
        return ChamferLoweringResult<AirLocalizedTangentBlendConstruction>.Ok(new($"construction:{input.FeatureId}", input.FeatureId, witness, topology, shared));
    }

    private static AirFilletFeature Feature(AirLocalizedTangentBlendFilletCompileRequest input, ChamferLoweringError? error) => new(
        input.FeatureId, input.FeatureName, input.BodyId, new AirFaceBoundarySelection("+X", "SharedEdge(+X,+Z)", false), new AirConstantRadiusFilletRule(input.Radius), input.SourceSpan,
        input.HistoryKnown ? "generated/history-known-axis-aligned-rectangular-prism" : "imported/no-history",
        error is null ? AirFeatureAdmissionStatus.Admitted : error.Kind == ChamferLoweringErrorKind.UnsupportedHistory ? AirFeatureAdmissionStatus.Deferred : AirFeatureAdmissionStatus.Rejected,
        error?.Code ?? "localized-fillet-tangent-blend-single-edge-admitted");

    private static AirBRepPlan BuildPlan(AirLocalizedTangentBlendFilletCompileRequest input, AirFilletFeature feature, AirLocalizedTangentBlendConstruction construction)
    {
        var provenance = new AirProvenance("AIR-FILLET-LOCALIZED-M1", "Localized tangent-blend Construction AIR", "shared-edge(+X,+Z)", input.FeatureId,
            nameof(AirLocalizedTangentBlendFilletCompiler), AirSelectionClass.SingleEdge, AirRuleKind.ConstantRadiusFillet, feature.ConstructionHistoryKind, true,
            ["Exact quarter-circle profile.", "Exact cylinder generated by linear sweep.", "No legacy direct-BRep surgery.", "Plan is constructed before emission."]);
        return LocalizedEdgeReplacementCompilerModel.BuildBRepPlan(construction.SharedConstruction, provenance, AirBRepPlanKind.LocalizedTangentBlend);
    }

}

internal sealed record AirLocalizedTangentBlendEmissionResult(bool Succeeded, BrepBody? Body, IReadOnlyList<string> Diagnostics);

internal static class AirLocalizedTangentBlendEmitter
{
    public static AirLocalizedTangentBlendEmissionResult Emit(LocalizedEdgeReplacementConstruction construction)
    {
        var emitted = LocalizedEdgeReplacementCompilerModel.Emit(construction);
        return new(emitted.Succeeded, emitted.Body, emitted.Diagnostics);
    }
}
