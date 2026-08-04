using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Air;

internal sealed record AirRevolutionProfileWitness(
    string WitnessId,
    string Axis,
    IReadOnlyList<ProfilePoint2D> SharpProfile,
    IReadOnlyList<ProfilePoint2D> ReplacementProfile,
    string MaterialSide,
    bool CompilerGenerated);

internal sealed record AirRevolutionProfileConstruction(
    string ConstructionId,
    string SourceFeatureId,
    AirRevolutionProfileWitness Witness,
    RevolvedProfileTopologyPlan TopologyPlan);

internal sealed record AirCylinderTopRimChamferCompileRequest(
    string BodyId,
    string FeatureId,
    string FeatureName,
    double Radius,
    double Height,
    string FaceAxis,
    string Target,
    string Kind,
    double Distance,
    AirSourceSpan SourceSpan,
    bool HistoryKnown = true);

internal sealed record AirCylinderTopRimChamferCompileResult(
    bool Succeeded,
    AirChamferFeature Feature,
    AirRevolutionProfileConstruction? Construction,
    AirBRepPlan? BRepPlan,
    BrepBody? Body,
    ChamferLoweringError? Error,
    IReadOnlyList<string> Diagnostics)
{
    public const string ProductionRoute = "AirRevolutionProfileTopRimChamfer";
}

/// <summary>Exact lowering for a complete convex top rim of a history-known right circular cylinder.</summary>
internal static class AirCylinderTopRimChamferCompiler
{
    private const double Tol = 1e-9;

    public static AirCylinderTopRimChamferCompileResult Compile(AirCylinderTopRimChamferCompileRequest input)
    {
        var lowered = Lower(input);
        var feature = Feature(input, lowered.Error);
        if (!lowered.IsSuccess)
            return new(false, feature, null, null, null, lowered.Error, [lowered.Error!.Code]);

        var construction = lowered.Value!;
        var plan = BuildBRepPlan(input, feature, construction);
        var emitted = RevolvedProfileStackEmitter.Emit(construction.TopologyPlan);
        if (!emitted.Succeeded || emitted.Body is null)
        {
            var error = new ChamferLoweringError(ChamferLoweringErrorKind.BackendMaterializationDefect,
                "chamfer-backend-revolved-profile-materialization-failed",
                "The admitted revolution-profile construction did not materialize as a bound BRep.",
                "BRep", emitted.Diagnostics);
            return new(false, feature, construction, plan, null, error, [error.Code, .. emitted.Diagnostics]);
        }

        var coneCount = emitted.Body.Geometry.Surfaces.Count(s => s.Value.Kind == Geometry.SurfaceGeometryKind.Cone);
        var cylinderCount = emitted.Body.Geometry.Surfaces.Count(s => s.Value.Kind == Geometry.SurfaceGeometryKind.Cylinder);
        if (coneCount != 1 || cylinderCount != 1)
        {
            var error = new ChamferLoweringError(ChamferLoweringErrorKind.VerificationFailure,
                "chamfer-verification-expected-cylinder-and-cone",
                "Circular rim output must contain one cylindrical side and one conical chamfer band.",
                "Verification", [$"cylinders={cylinderCount}", $"cones={coneCount}"]);
            return new(false, feature, construction, plan, emitted.Body, error, [error.Code]);
        }

        return new(true, feature, construction, plan, emitted.Body, null,
            emitted.Diagnostics.Concat([
                "chamfer-feature-admitted:circular-convex-top-rim",
                "chamfer-generated-witness:revolution-profile-rewrite",
                "chamfer-analytic-evidence:cylinder=1,cone=1,planes=2",
            ]).ToArray());
    }

    public static ChamferLoweringResult<AirRevolutionProfileConstruction> Lower(AirCylinderTopRimChamferCompileRequest input)
    {
        ChamferLoweringResult<AirRevolutionProfileConstruction> Fail(ChamferLoweringErrorKind kind, string code, string message) =>
            ChamferLoweringResult<AirRevolutionProfileConstruction>.Err(new(kind, code, message, "FeatureAIR->ConstructionAIR"));

        if (!input.HistoryKnown)
            return Fail(ChamferLoweringErrorKind.UnsupportedHistory, "chamfer-unsupported-history:cylinder-rim-requires-construction-history", "Circular rim lowering requires a history-known right circular cylinder.");
        if (!string.Equals(input.FaceAxis, "+Z", StringComparison.Ordinal) || !string.Equals(input.Target, "Boundary", StringComparison.Ordinal))
            return Fail(ChamferLoweringErrorKind.UnsupportedSelection, "chamfer-unsupported-selection:expected-complete-+Z-boundary", "The admitted circular selection is the complete outer boundary of the +Z cap.");
        if (!string.Equals(input.Kind, "Chamfer", StringComparison.Ordinal))
            return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "chamfer-invalid-kind:expected-chamfer", "This lowerer accepts chamfer intent only.");
        if (!double.IsFinite(input.Radius) || !double.IsFinite(input.Height) || input.Radius <= Tol || input.Height <= Tol)
            return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "chamfer-invalid-cylinder-dimensions", "Cylinder radius and height must be finite and positive.");
        if (!double.IsFinite(input.Distance) || input.Distance <= Tol)
            return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "chamfer-invalid-distance:must-be-positive", "Chamfer distance must be finite and positive.");
        if (input.Distance >= input.Radius - Tol || input.Distance >= input.Height - Tol)
            return Fail(ChamferLoweringErrorKind.DistanceTooLarge, "chamfer-distance-too-large:circular-top-rim", "Chamfer distance must be smaller than both the cylinder radius and height.");

        var z0 = -input.Height / 2d;
        var z1 = input.Height / 2d;
        var sharp = new[] { new ProfilePoint2D(input.Radius, z0), new ProfilePoint2D(input.Radius, z1) };
        var replacement = new[]
        {
            new ProfilePoint2D(input.Radius, z0),
            new ProfilePoint2D(input.Radius, z1 - input.Distance),
            new ProfilePoint2D(input.Radius - input.Distance, z1),
        };
        var frame = new ExtrudeFrame3D(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)));
        var axis = new RevolveAxis3D(Point3D.Origin, new Vector3D(0, 0, 1));
        var planned = RevolvedProfileStackEmitter.Plan(replacement, frame, axis);
        if (!planned.IsSuccess || planned.Value is null)
            return Fail(ChamferLoweringErrorKind.MissingConstructionWitness, "chamfer-revolution-profile-plan-invalid", string.Join(" | ", planned.Diagnostics.Select(d => d.Message)));
        var witness = new AirRevolutionProfileWitness($"witness:{input.FeatureId}", "Z@origin", sharp, replacement, "radially-inward/axially-below-cap", true);
        return ChamferLoweringResult<AirRevolutionProfileConstruction>.Ok(new($"construction:{input.FeatureId}", input.FeatureId, witness, planned.Value));
    }

    private static AirChamferFeature Feature(AirCylinderTopRimChamferCompileRequest input, ChamferLoweringError? error) => new(
        input.FeatureId, input.FeatureName, input.BodyId, new AirFaceBoundarySelection(input.FaceAxis), new AirEqualDistanceChamferRule(input.Distance), input.SourceSpan,
        input.HistoryKnown ? "generated/history-known-right-circular-cylinder" : "imported/no-history",
        error is null ? AirFeatureAdmissionStatus.Admitted : error.Kind == ChamferLoweringErrorKind.UnsupportedHistory ? AirFeatureAdmissionStatus.Deferred : AirFeatureAdmissionStatus.Rejected,
        error?.Code ?? "chamfer-bounded-circular-top-rim-admitted");

    private static AirBRepPlan BuildBRepPlan(AirCylinderTopRimChamferCompileRequest input, AirChamferFeature feature, AirRevolutionProfileConstruction construction)
    {
        var topology = construction.TopologyPlan;
        var provenance = new AirProvenance("CHAMFER-FIXTURE-PRESSURE-M6", "Circular rim profile rewrite", input.FeatureName, input.FeatureId,
            nameof(AirChamferFeature), AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer, feature.ConstructionHistoryKind, true,
            ["Compiler-generated witness is equivalent to the explicit sharp/replacement radial profile pair."]);
        var elements = new List<AirBRepPlanElement>();
        for (var i = 0; i < topology.Profile.Count; i++)
        {
            elements.Add(new(new($"profile:v:{i}"), AirBRepPlanElementKind.Vertex, AirBRepPlanRole.ProfileVertex, input.FeatureId, provenance, ProfileVertexIndex: i));
            elements.Add(new(new($"rim:e:{i}"), AirBRepPlanElementKind.Edge, AirBRepPlanRole.CircularRim, input.FeatureId, provenance, ProfileVertexIndex: i));
        }
        for (var i = 0; i < topology.Profile.Count - 1; i++)
        {
            var role = System.Math.Abs(topology.Profile[i].X - topology.Profile[i + 1].X) <= Tol ? AirBRepPlanRole.CylindricalFace : AirBRepPlanRole.ConicalTransitionFace;
            elements.Add(new(new($"profile:segment:{i}"), AirBRepPlanElementKind.Face, role, input.FeatureId, provenance, IntervalIndex: i,
                SemanticRoles: role == AirBRepPlanRole.ConicalTransitionFace ? [role, AirBRepPlanRole.ChamferFace] : [role]));
        }
        elements.Add(new(new("cap:bottom"), AirBRepPlanElementKind.Face, AirBRepPlanRole.CapFace, input.FeatureId, provenance));
        elements.Add(new(new("cap:top"), AirBRepPlanElementKind.Face, AirBRepPlanRole.CapFace, input.FeatureId, provenance));
        elements.Add(new(new("shell:body:0"), AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell, input.FeatureId, provenance));
        elements.Add(new(new("body:0"), AirBRepPlanElementKind.Body, AirBRepPlanRole.Body, input.FeatureId, provenance));
        var diagnostics = new[] { new AirDiagnostic("chamfer-brep-plan-revolved-profile-authoritative", AirDiagnosticSeverity.Info, "Authoritative revolved-profile plan created before BRep emission.") };
        var context = new AirBRepPlanFeatureContext(AirNodeKind.TopFaceLoopChamfer, AirRouteKind.Unsupported, AirSelectionClass.FaceBoundaryLoop, AirRuleKind.UniformChamfer,
            feature.ConstructionHistoryKind, "BoundedProductionAdmission", ["complete +Z circular outer rim", "uniform equal-distance rule"]);
        var summary = new AirBRepPlanSummary(AirBRepPlanKind.RevolvedProfile, input.FeatureId, topology.ExpectedVertexCount, topology.ExpectedEdgeCount, topology.ExpectedEdgeCount,
            topology.ExpectedCoedgeCount, topology.ExpectedLoopCount, topology.ExpectedFaceCount, topology.ExpectedFaceCount, 1, 1, 2, 1, 1, 1,
            $"radius={input.Radius:R};height={input.Height:R}", "preserve-profile-corners", diagnostics,
            ["no Boolean", "no edge surgery", "no legacy fallback", "analytic cylinder/cone/plane surfaces"], context);
        return new($"brep-plan:revolved-profile:{input.FeatureId}", AirBRepPlanKind.RevolvedProfile, input.FeatureId, provenance, elements, summary, diagnostics, summary.Guarantees, context, RevolvedRealizationPlan: topology);
    }
}
