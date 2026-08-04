using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Air;

/// <summary>Exact, finite witness for the admitted +X/+Z box edge only.  It is deliberately
/// geometry-first: the emitter receives this plan, rather than discovering topology from a body.</summary>
internal sealed record AirLocalizedPlanarReplacementWitness(
    string SupportPlaneA, string SupportPlaneB, Point3D EdgeStart, Point3D EdgeEnd,
    IReadOnlyList<Point3D> RetainedSupportFaceA, IReadOnlyList<Point3D> RetainedSupportFaceB,
    IReadOnlyList<Point3D> ReplacementChamfer, string MaterialSide, string EndpointPolicy);

internal sealed record AirLocalizedPlanarReplacementTopologyPlan(
    IReadOnlyList<Point3D> CrossSection, double MinY, double MaxY,
    IReadOnlyList<string> FaceRoles, string DeterministicSignature,
    LocalizedEdgeReplacementTopologyPlan SharedTopologyPlan)
{
    public int ExpectedVertexCount => CrossSection.Count * 2;
    public int ExpectedEdgeCount => CrossSection.Count * 3;
    public int ExpectedFaceCount => CrossSection.Count + 2;
    public int ExpectedLoopCount => ExpectedFaceCount;
    public int ExpectedCoedgeCount => 4 * CrossSection.Count + 2 * CrossSection.Count;
}

internal sealed record AirLocalizedPlanarReplacementConstruction(
    string ConstructionId, string SourceFeatureId, AirLocalizedPlanarReplacementWitness Witness,
    AirLocalizedPlanarReplacementTopologyPlan TopologyPlan,
    LocalizedEdgeReplacementConstruction SharedConstruction);

internal sealed record AirLocalizedPlanarReplacementChamferCompileRequest(
    string BodyId, string FeatureId, string FeatureName, double Width, double Depth, double Height,
    string FaceA, string FaceB, string Kind, double Distance, AirSourceSpan SourceSpan, bool HistoryKnown = true);

internal sealed record AirLocalizedPlanarReplacementChamferCompileResult(
    bool Succeeded, AirChamferFeature Feature, AirLocalizedPlanarReplacementConstruction? Construction,
    AirBRepPlan? BRepPlan, BrepBody? Body, ChamferLoweringError? Error, IReadOnlyList<string> Diagnostics)
{
    public const string ProductionRoute = "AirLocalizedPlanarSingleEdgeChamfer";
}

internal static class AirLocalizedPlanarReplacementChamferCompiler
{
    public static AirLocalizedPlanarReplacementChamferCompileResult Compile(AirLocalizedPlanarReplacementChamferCompileRequest input)
    {
        var lowered = Lower(input);
        var feature = Feature(input, lowered.Error);
        if (!lowered.IsSuccess) return new(false, feature, null, null, null, lowered.Error, [lowered.Error!.Code]);

        var construction = lowered.Value!;
        var plan = BuildPlan(input, feature, construction);
        var emitted = AirLocalizedPlanarReplacementEmitter.Emit(construction.SharedConstruction);
        if (!emitted.Succeeded || emitted.Body is null)
        {
            var error = new ChamferLoweringError(ChamferLoweringErrorKind.BackendMaterializationDefect,
                "localized-chamfer-materialization-failed", "The authoritative localized BRepPlan did not materialize.", "BRep", emitted.Diagnostics);
            return new(false, feature, construction, plan, null, error, [error.Code, .. emitted.Diagnostics]);
        }
        var body = emitted.Body;
        var planes = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane);
        if (planes != 7 || body.Topology.Faces.Count() != construction.TopologyPlan.ExpectedFaceCount)
        {
            var error = new ChamferLoweringError(ChamferLoweringErrorKind.VerificationFailure, "localized-chamfer-analytic-topology-verification-failed",
                "Localized planar replacement must produce seven planar faces with the planned topology.", "Verification");
            return new(false, feature, construction, plan, body, error, [error.Code]);
        }
        return new(true, feature, construction, plan, body, null,
            ["localized-chamfer-feature-admitted", "localized-chamfer-direct-single-candidate", "localized-chamfer-authoritative-brep-plan-consumed", "localized-chamfer-explicit-owned-endpoints"]);
    }

    public static ChamferLoweringResult<AirLocalizedPlanarReplacementConstruction> Lower(AirLocalizedPlanarReplacementChamferCompileRequest input)
    {
        ChamferLoweringResult<AirLocalizedPlanarReplacementConstruction> Fail(ChamferLoweringErrorKind kind, string code, string message) =>
            ChamferLoweringResult<AirLocalizedPlanarReplacementConstruction>.Err(new(kind, code, message, "FeatureAIR->ConstructionAIR"));
        if (!string.Equals(input.Kind, "Chamfer", StringComparison.Ordinal)) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-chamfer-invalid-kind", "Only equal-distance chamfer intent is admitted.");
        var context = new LocalizedEdgeReplacementContext(input.BodyId, input.FeatureId, input.FeatureName, input.Width, input.Depth, input.Height, input.FaceA, input.FaceB, AirLocalizedEdgeFinishKind.Chamfer, input.Distance, input.SourceSpan, input.HistoryKnown);
        var admitted = LocalizedEdgeReplacementCompilerModel.Admit(context);
        if (!admitted.IsSuccess) return ChamferLoweringResult<AirLocalizedPlanarReplacementConstruction>.Err(admitted.Error!);

        var hx = input.Width / 2d; var hy = input.Depth / 2d; var hz = input.Height / 2d; var d = input.Distance;
        var cross = new[] { new Point3D(-hx, -hy, -hz), new Point3D(hx, -hy, -hz), new Point3D(hx, -hy, hz - d), new Point3D(hx - d, -hy, hz), new Point3D(-hx, -hy, hz) };
        var start = new Point3D(hx, -hy, hz);
        var end = new Point3D(hx, hy, hz);
        var replacement = new[] { new Point3D(hx, -hy, hz - d), new Point3D(hx - d, -hy, hz), new Point3D(hx - d, hy, hz), new Point3D(hx, hy, hz - d) };
        var retainedX = new[] { new Point3D(hx, -hy, -hz), new Point3D(hx, hy, -hz), new Point3D(hx, hy, hz - d), new Point3D(hx, -hy, hz - d) };
        var retainedZ = new[] { new Point3D(-hx, -hy, hz), new Point3D(hx - d, -hy, hz), new Point3D(hx - d, hy, hz), new Point3D(-hx, hy, hz) };
        if (replacement.Distinct().Count() != 4) return Fail(ChamferLoweringErrorKind.DegenerateTransition, "localized-chamfer-degenerate-transition", "Endpoint transition quad is degenerate.");
        var sharedTopology = LocalizedEdgeReplacementCompilerModel.Topology(context, cross);
        var topology = new AirLocalizedPlanarReplacementTopologyPlan(cross, -hy, hy,
            sharedTopology.FaceRoles, sharedTopology.DeterministicSignature, sharedTopology);
        var witness = new AirLocalizedPlanarReplacementWitness("plane(+X)", "plane(+Z)", start, end, retainedX, retainedZ, replacement,
            "inside:x<=max,z<=max,x+z<=maxX+maxZ-distance", "ExplicitOwnedEndpoints");
        var shared = new LocalizedEdgeReplacementConstruction($"construction:{input.FeatureId}", input.FeatureId, admitted.Value!, retainedX, retainedZ, cross,
            new PlanarChamferReplacement([cross[2], cross[3]], [replacement[3], replacement[2]], replacement), sharedTopology, "history-known-axis-aligned-rectangular-prism");
        return ChamferLoweringResult<AirLocalizedPlanarReplacementConstruction>.Ok(new($"construction:{input.FeatureId}", input.FeatureId, witness, topology, shared));
    }

    private static AirChamferFeature Feature(AirLocalizedPlanarReplacementChamferCompileRequest input, ChamferLoweringError? error) => new(
        input.FeatureId, input.FeatureName, input.BodyId, new AirFaceBoundarySelection("+X", "SharedEdge(+X,+Z)", false), new AirEqualDistanceChamferRule(input.Distance), input.SourceSpan,
        input.HistoryKnown ? "generated/history-known-axis-aligned-rectangular-prism" : "imported/no-history",
        error is null ? AirFeatureAdmissionStatus.Admitted : error.Kind == ChamferLoweringErrorKind.UnsupportedHistory ? AirFeatureAdmissionStatus.Deferred : AirFeatureAdmissionStatus.Rejected,
        error?.Code ?? "localized-chamfer-planar-single-edge-admitted");

    private static AirBRepPlan BuildPlan(AirLocalizedPlanarReplacementChamferCompileRequest input, AirChamferFeature feature, AirLocalizedPlanarReplacementConstruction construction)
    {
        var provenance = new AirProvenance("AIR-CHAMFER-LOCALIZED-PLAN-A1", "Localized planar replacement", "shared-edge(+X,+Z)", input.FeatureId,
            nameof(AirLocalizedPlanarReplacementChamferCompiler), AirSelectionClass.None, AirRuleKind.UniformChamfer, feature.ConstructionHistoryKind, true,
            ["No legacy direct-BRep surgery.", "Plan is constructed before emission."]);
        return LocalizedEdgeReplacementCompilerModel.BuildBRepPlan(construction.SharedConstruction, provenance, AirBRepPlanKind.LocalizedPlanarReplacement);
    }

}

internal sealed record AirLocalizedPlanarReplacementEmissionResult(bool Succeeded, BrepBody? Body, IReadOnlyList<string> Diagnostics);

internal static class AirLocalizedPlanarReplacementEmitter
{
    public static AirLocalizedPlanarReplacementEmissionResult Emit(LocalizedEdgeReplacementConstruction construction)
    {
        var emitted = LocalizedEdgeReplacementCompilerModel.Emit(construction);
        return new(emitted.Succeeded, emitted.Body, emitted.Diagnostics);
    }
}
