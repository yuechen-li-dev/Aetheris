using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Air;

/// <summary>
/// Bounded M3 construction for the only admitted junction: the +X/+Z and +Y/+Z
/// convex box edges, with equal-distance planar chamfers.  The two cut planes meet
/// in one owned miter edge; therefore an additional corner face would overlap the
/// replacement region and is not admissible.
/// </summary>
internal sealed record AirLocalizedEdgeJunctionChamferCompileRequest(
    string BodyId, string FeatureId, string FeatureName, double Width, double Depth, double Height,
    string FaceA, string TargetA, string FaceB, string TargetB, double DistanceA, double DistanceB,
    AirSourceSpan SourceSpan, bool HistoryKnown = true);

internal sealed record LocalizedEdgeJunctionReplacement(
    string SemanticEdge, AirEdgeFinishFeature Feature, IReadOnlyList<Point3D> Boundary,
    IReadOnlyList<Point3D> RemoteEndpointBoundary, string SurfaceFamily);

/// <summary>The exact zero-area closure is materialized as an owned miter boundary, not metadata.</summary>
internal sealed record LocalizedEdgeJunctionCornerPatch(
    string Kind, IReadOnlyList<Point3D> Boundary, string Closure, string SurfaceFamily);

internal sealed record LocalizedEdgeJunctionTopologyPlan(
    IReadOnlyList<Point3D> Vertices, IReadOnlyList<IReadOnlyList<int>> FaceLoops,
    IReadOnlyList<string> FaceRoles, string DeterministicSignature)
{
    public int ExpectedVertexCount => Vertices.Count;
    public int ExpectedFaceCount => FaceLoops.Count;
    public int ExpectedEdgeCount => FaceLoops.Sum(loop => loop.Count) / 2;
    public int ExpectedLoopCount => ExpectedFaceCount;
    public int ExpectedCoedgeCount => FaceLoops.Sum(loop => loop.Count);
}

/// <summary>Construction AIR owns both replacement regions and their shared miter closure.</summary>
internal sealed record LocalizedEdgeJunctionConstruction(
    string ConstructionId, LocalizedEdgeJunctionReplacement ReplacementA,
    LocalizedEdgeJunctionReplacement ReplacementB, Point3D SharedEndpoint,
    Point3D RemoteEndpointA, Point3D RemoteEndpointB, LocalizedEdgeJunctionCornerPatch CornerPatch,
    IReadOnlyList<IReadOnlyList<Point3D>> RetainedRegions, string MaterialSide,
    string BoundaryOwnership, LocalizedEdgeJunctionTopologyPlan TopologyPlan, string Provenance);

internal sealed record AirLocalizedEdgeJunctionChamferCompileResult(
    bool Succeeded, LocalizedEdgeJunctionConstruction? Construction, AirBRepPlan? BRepPlan,
    BrepBody? Body, ChamferLoweringError? Error, IReadOnlyList<string> Diagnostics)
{
    public const string ProductionRoute = "AirLocalizedEdgeJunctionChamferM3";
}

internal static class AirLocalizedEdgeJunctionChamferCompiler
{
    private const double Tol = 1e-9;

    public static AirLocalizedEdgeJunctionChamferCompileResult Compile(AirLocalizedEdgeJunctionChamferCompileRequest input)
    {
        var lowered = Lower(input);
        if (!lowered.IsSuccess)
            return new(false, null, null, null, lowered.Error, [lowered.Error!.Code]);

        var construction = lowered.Value!;
        var plan = BuildPlan(input, construction);
        var emitted = LocalizedEdgeReplacementCompilerModel.EmitPlanarPolyhedron(construction.TopologyPlan.Vertices, construction.TopologyPlan.FaceLoops);
        if (!emitted.Succeeded || emitted.Body is null)
            return Failure(construction, plan, ChamferLoweringErrorKind.BackendMaterializationDefect, "localized-junction-chamfer-materialization-failed", "The authoritative junction plan did not materialize.", emitted.Diagnostics);

        var body = emitted.Body;
        if (body.Topology.Vertices.Count() != construction.TopologyPlan.ExpectedVertexCount ||
            body.Topology.Edges.Count() != construction.TopologyPlan.ExpectedEdgeCount ||
            body.Topology.Faces.Count() != construction.TopologyPlan.ExpectedFaceCount ||
            body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Plane) != 8)
            return Failure(construction, plan, ChamferLoweringErrorKind.VerificationFailure, "localized-junction-chamfer-analytic-topology-verification-failed", "The mitered junction must contain 11 vertices, 17 edges, and eight planar faces.");

        return new(true, construction, plan, body, null,
            ["localized-junction-chamfer-feature-admitted", "localized-junction-chamfer-candidate-plans=1", "localized-junction-chamfer-hard-valid-plans=1", "localized-junction-chamfer-direct-miter", "localized-junction-chamfer-authoritative-brep-plan-consumed", "localized-junction-chamfer-no-legacy-fallback"]);
    }

    public static ChamferLoweringResult<LocalizedEdgeJunctionConstruction> Lower(AirLocalizedEdgeJunctionChamferCompileRequest input)
    {
        ChamferLoweringResult<LocalizedEdgeJunctionConstruction> Fail(ChamferLoweringErrorKind kind, string code, string message) =>
            ChamferLoweringResult<LocalizedEdgeJunctionConstruction>.Err(new(kind, code, message, "FeatureAIR->ConstructionAIR"));
        if (!input.HistoryKnown) return Fail(ChamferLoweringErrorKind.UnsupportedHistory, "localized-junction-unsupported-history", "Localized junctions require known axis-aligned box history.");
        if (!FinitePositive(input.Width) || !FinitePositive(input.Depth) || !FinitePositive(input.Height)) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-junction-invalid-box-dimensions", "Box dimensions must be finite and positive.");
        if (!Matches(input.FaceA, input.TargetA, "+X", "SharedEdgePlusZ") || !Matches(input.FaceB, input.TargetB, "+Y", "SharedEdgePlusZ"))
            return Fail(ChamferLoweringErrorKind.UnsupportedSelection, "localized-junction-edges-do-not-share-canonical-endpoint", "M3 admits SharedEdge(+X,+Z) and SharedEdge(+Y,+Z) only, in that order.");
        if (!FinitePositive(input.DistanceA) || !FinitePositive(input.DistanceB)) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-junction-distance-must-be-positive", "Both chamfer distances must be finite and positive.");
        if (double.Abs(input.DistanceA - input.DistanceB) > Tol) return Fail(ChamferLoweringErrorKind.ConstructionWitnessRequired, "localized-junction-parameter-mismatch", "The admitted miter construction requires equal chamfer distances.");
        var d = input.DistanceA;
        if (d >= input.Width - Tol || d >= input.Depth - Tol || d >= input.Height - Tol)
            return Fail(ChamferLoweringErrorKind.DistanceTooLarge, "localized-junction-distance-too-large", "The equal distance must remain within all three incident box extents.");

        var hx = input.Width / 2d; var hy = input.Depth / 2d; var hz = input.Height / 2d;
        var featureA = Feature(input, "A", "+X", "SharedEdge(+X,+Z)", d);
        var featureB = Feature(input, "B", "+Y", "SharedEdge(+Y,+Z)", d);
        var shared = new Point3D(hx, hy, hz);
        var remoteA = new Point3D(hx, -hy, hz);
        var remoteB = new Point3D(-hx, hy, hz);
        var p = new Point3D(hx, hy, hz - d);
        var q = new Point3D(hx - d, hy - d, hz);
        var vertices = new[]
        {
            new Point3D(-hx, -hy, -hz), new Point3D(hx, -hy, -hz), new Point3D(hx, hy, -hz), new Point3D(-hx, hy, -hz),
            new Point3D(-hx, -hy, hz), new Point3D(hx, -hy, hz - d), new Point3D(hx - d, -hy, hz),
            new Point3D(-hx, hy, hz - d), new Point3D(-hx, hy - d, hz), p, q
        };
        IReadOnlyList<int>[] loops =
        [
            [0, 3, 2, 1],       // -Z
            [0, 4, 8, 7, 3],    // -X / remote B transition
            [0, 1, 5, 6, 4],    // -Y / remote A transition
            [1, 2, 9, 5],       // +X
            [3, 7, 9, 2],       // +Y
            [4, 6, 10, 8],      // +Z
            [5, 9, 10, 6],      // replacement A
            [7, 8, 10, 9],      // replacement B; owns the opposite miter coedge
        ];
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"localized-edge-junction:Chamfer:+X,+Z:+Y,+Z:{input.Width:R}:{input.Depth:R}:{input.Height:R}:{d:R}:direct-miter"))));
        var topology = new LocalizedEdgeJunctionTopologyPlan(vertices, loops,
            ["UnaffectedFace(-Z)", "RemoteEndpointB(-X)", "RemoteEndpointA(-Y)", "RetainedSupportFace(+X)", "RetainedSupportFace(+Y)", "RetainedSupportFace(+Z)", "ReplacementA(PlanarChamfer)", "ReplacementB(PlanarChamfer)"], signature);
        var replacementA = new LocalizedEdgeJunctionReplacement("SharedEdge(+X,+Z)", featureA, [vertices[5], vertices[6], q, p], [vertices[5], vertices[6]], "PlanarChamfer");
        var replacementB = new LocalizedEdgeJunctionReplacement("SharedEdge(+Y,+Z)", featureB, [vertices[7], vertices[8], q, p], [vertices[7], vertices[8]], "PlanarChamfer");
        var patch = new LocalizedEdgeJunctionCornerPatch("MiteredReplacementBoundary", [p, q], "replacementA/replacementB share one opposite-oriented exact coedge; no additional face is admissible", "Line3");
        return ChamferLoweringResult<LocalizedEdgeJunctionConstruction>.Ok(new(
            $"construction:{input.FeatureId}:junction", replacementA, replacementB, shared, remoteA, remoteB, patch,
            [[vertices[1], vertices[2], p, vertices[5]], [vertices[3], vertices[7], p, vertices[2]], [vertices[4], vertices[6], q, vertices[8]]],
            "inside:x<=max,y<=max,z<=max,x+z<=maxX+maxZ-distance,y+z<=maxY+maxZ-distance",
            "SharedMiterEdge(opposite coedges); RemoteEndpointA(-Y); RemoteEndpointB(-X)", topology,
            "history-known-axis-aligned-rectangular-box; equal-distance-planar-miter"));
    }

    private static AirLocalizedEdgeJunctionChamferCompileResult Failure(LocalizedEdgeJunctionConstruction construction, AirBRepPlan plan, ChamferLoweringErrorKind kind, string code, string message, IReadOnlyList<string>? evidence = null) =>
        new(false, construction, plan, null, new(kind, code, message, "BRep", evidence), [code, .. (evidence ?? [])]);

    private static AirEdgeFinishFeature Feature(AirLocalizedEdgeJunctionChamferCompileRequest input, string suffix, string face, string edge, double distance) => new(
        $"{input.FeatureId}.{suffix}", $"{input.FeatureName}.{suffix}", input.BodyId, new AirFaceBoundarySelection(face, edge, false), AirLocalizedEdgeFinishKind.Chamfer,
        new AirEqualDistanceEdgeFinishRule(distance), input.SourceSpan, "generated/history-known-axis-aligned-rectangular-prism", AirFeatureAdmissionStatus.Admitted, "localized-junction-direct-single-miter-candidate");

    private static AirBRepPlan BuildPlan(AirLocalizedEdgeJunctionChamferCompileRequest input, LocalizedEdgeJunctionConstruction construction)
    {
        var provenance = new AirProvenance("AIR-EDGE-FINISH-JUNCTION-M3", "Localized two-edge planar miter", "shared-edge(+X,+Z)/shared-edge(+Y,+Z)", input.FeatureId,
            nameof(AirLocalizedEdgeJunctionChamferCompiler), AirSelectionClass.None, AirRuleKind.UniformChamfer, construction.Provenance, true,
            ["One hard-valid construction.", "Direct miter owns the shared boundary.", "No legacy direct-BRep stitching."]);
        var elements = new List<AirBRepPlanElement>();
        for (var i = 0; i < construction.TopologyPlan.ExpectedVertexCount; i++)
            elements.Add(new(new($"vertex:{i}"), AirBRepPlanElementKind.Vertex, i is 9 or 10 ? AirBRepPlanRole.SharedJunction : i is 5 or 6 ? AirBRepPlanRole.RemoteEndpointA : i is 7 or 8 ? AirBRepPlanRole.RemoteEndpointB : AirBRepPlanRole.SectionVertex, input.FeatureId, provenance));
        for (var i = 0; i < construction.TopologyPlan.ExpectedFaceCount; i++)
        {
            var role = i == 6 ? AirBRepPlanRole.ReplacementFaceA : i == 7 ? AirBRepPlanRole.ReplacementFaceB : i == 1 ? AirBRepPlanRole.RemoteEndpointB : i == 2 ? AirBRepPlanRole.RemoteEndpointA : i is 3 or 4 or 5 ? AirBRepPlanRole.RetainedSupportFaceA : AirBRepPlanRole.UnaffectedFace;
            elements.Add(new(new($"loop:{i}"), AirBRepPlanElementKind.Loop, role, input.FeatureId, provenance));
            for (var c = 0; c < construction.TopologyPlan.FaceLoops[i].Count; c++) elements.Add(new(new($"coedge:{i}:{c}"), AirBRepPlanElementKind.Coedge, role, input.FeatureId, provenance));
            elements.Add(new(new($"face:{i}"), AirBRepPlanElementKind.Face, role, input.FeatureId, provenance, FaceRole: construction.TopologyPlan.FaceRoles[i], SemanticRoles: i is 6 or 7 ? [role, AirBRepPlanRole.ReplacementFace, AirBRepPlanRole.ChamferFace] : [role]));
        }
        for (var i = 0; i < construction.TopologyPlan.ExpectedEdgeCount; i++) elements.Add(new(new($"edge:{i}"), AirBRepPlanElementKind.Edge, i == construction.TopologyPlan.ExpectedEdgeCount - 1 ? AirBRepPlanRole.SharedJunction : AirBRepPlanRole.SectionEdge, input.FeatureId, provenance));
        for (var i = 0; i < construction.TopologyPlan.ExpectedFaceCount; i++) elements.Add(new(new($"surface:{i}"), AirBRepPlanElementKind.Surface, i == 6 ? AirBRepPlanRole.ReplacementFaceA : i == 7 ? AirBRepPlanRole.ReplacementFaceB : AirBRepPlanRole.RetainedSupportFaceA, input.FeatureId, provenance));
        elements.Add(new(new("corner-patch:miter:0"), AirBRepPlanElementKind.Curve, AirBRepPlanRole.CornerPatch, input.FeatureId, provenance, SemanticRoles: [AirBRepPlanRole.CornerPatch, AirBRepPlanRole.SharedJunction]));
        elements.Add(new(new("shell:0"), AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell, input.FeatureId, provenance));
        elements.Add(new(new("body:0"), AirBRepPlanElementKind.Body, AirBRepPlanRole.Body, input.FeatureId, provenance));
        var p = construction.TopologyPlan;
        var summary = new AirBRepPlanSummary(AirBRepPlanKind.LocalizedEdgeJunction, input.FeatureId, p.ExpectedVertexCount, p.ExpectedEdgeCount, p.ExpectedEdgeCount, p.ExpectedCoedgeCount, p.ExpectedLoopCount, p.ExpectedFaceCount, p.ExpectedFaceCount, 1, 1, 0, 3, 2, 2,
            $"localized-edge-junction=Chamfer;signature={p.DeterministicSignature}", construction.BoundaryOwnership, [], ["authoritative combined junction topology", "direct single hard-valid miter candidate", "no legacy fallback"],
            new(AirNodeKind.Unsupported, AirRouteKind.Unsupported, AirSelectionClass.None, AirRuleKind.UniformChamfer, construction.Provenance, "Direct", ["SharedEdge(+X,+Z)", "SharedEdge(+Y,+Z)"]));
        return new($"brep-plan:localized-edge-junction:{input.FeatureId}", AirBRepPlanKind.LocalizedEdgeJunction, input.FeatureId, provenance, elements, summary, [], summary.Guarantees, summary.FeatureContext, LocalizedEdgeJunctionRealizationPlan: p);
    }

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;
    private static bool Matches(string face, string target, string expectedFace, string expectedTarget) => string.Equals(face, expectedFace, StringComparison.Ordinal) && string.Equals(target, expectedTarget, StringComparison.Ordinal);
}
