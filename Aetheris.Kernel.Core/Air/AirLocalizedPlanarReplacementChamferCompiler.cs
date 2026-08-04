using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Air.BRepPlan;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Air;

/// <summary>Exact, finite witness for the admitted +X/+Z box edge only.  It is deliberately
/// geometry-first: the emitter receives this plan, rather than discovering topology from a body.</summary>
internal sealed record AirLocalizedPlanarReplacementWitness(
    string SupportPlaneA, string SupportPlaneB, Point3D EdgeStart, Point3D EdgeEnd,
    IReadOnlyList<Point3D> RetainedSupportFaceA, IReadOnlyList<Point3D> RetainedSupportFaceB,
    IReadOnlyList<Point3D> ReplacementChamfer, string MaterialSide, string EndpointPolicy);

internal sealed record AirLocalizedPlanarReplacementTopologyPlan(
    IReadOnlyList<Point3D> CrossSection, double MinY, double MaxY,
    IReadOnlyList<string> FaceRoles, string DeterministicSignature)
{
    public int ExpectedVertexCount => CrossSection.Count * 2;
    public int ExpectedEdgeCount => CrossSection.Count * 3;
    public int ExpectedFaceCount => CrossSection.Count + 2;
    public int ExpectedLoopCount => ExpectedFaceCount;
    public int ExpectedCoedgeCount => 4 * CrossSection.Count + 2 * CrossSection.Count;
}

internal sealed record AirLocalizedPlanarReplacementConstruction(
    string ConstructionId, string SourceFeatureId, AirLocalizedPlanarReplacementWitness Witness,
    AirLocalizedPlanarReplacementTopologyPlan TopologyPlan);

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
    private const double Tol = 1e-9;

    public static AirLocalizedPlanarReplacementChamferCompileResult Compile(AirLocalizedPlanarReplacementChamferCompileRequest input)
    {
        var lowered = Lower(input);
        var feature = Feature(input, lowered.Error);
        if (!lowered.IsSuccess) return new(false, feature, null, null, null, lowered.Error, [lowered.Error!.Code]);

        var construction = lowered.Value!;
        var plan = BuildPlan(input, feature, construction);
        var emitted = AirLocalizedPlanarReplacementEmitter.Emit(construction.TopologyPlan);
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
        if (!input.HistoryKnown) return Fail(ChamferLoweringErrorKind.UnsupportedHistory, "localized-chamfer-unsupported-history", "Localized replacement requires known prismatic construction history.");
        if (!string.Equals(input.Kind, "Chamfer", StringComparison.Ordinal)) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-chamfer-invalid-kind", "Only equal-distance chamfer intent is admitted.");
        if (!string.Equals(input.FaceA, "+X", StringComparison.Ordinal) || !string.Equals(input.FaceB, "+Z", StringComparison.Ordinal))
            return Fail(ChamferLoweringErrorKind.UnsupportedSelection, "localized-chamfer-unsupported-selection:expected-shared-edge-plus-x-plus-z", "The first localized route admits SharedEdge(+X,+Z) only.");
        if (!double.IsFinite(input.Width) || !double.IsFinite(input.Depth) || !double.IsFinite(input.Height) || input.Width <= Tol || input.Depth <= Tol || input.Height <= Tol)
            return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-chamfer-invalid-box-dimensions", "Host dimensions must be finite and positive.");
        if (!double.IsFinite(input.Distance) || input.Distance <= Tol) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-chamfer-distance-must-be-positive", "Distance must be finite and positive.");
        if (input.Distance >= input.Width - Tol || input.Distance >= input.Height - Tol)
            return Fail(ChamferLoweringErrorKind.DistanceTooLarge, "localized-chamfer-distance-too-large", "Distance must remain within both planar support-face extents.");

        var hx = input.Width / 2d; var hy = input.Depth / 2d; var hz = input.Height / 2d; var d = input.Distance;
        var cross = new[] { new Point3D(-hx, -hy, -hz), new Point3D(hx, -hy, -hz), new Point3D(hx, -hy, hz - d), new Point3D(hx - d, -hy, hz), new Point3D(-hx, -hy, hz) };
        var start = new Point3D(hx, -hy, hz);
        var end = new Point3D(hx, hy, hz);
        var replacement = new[] { new Point3D(hx, -hy, hz - d), new Point3D(hx - d, -hy, hz), new Point3D(hx - d, hy, hz), new Point3D(hx, hy, hz - d) };
        var retainedX = new[] { new Point3D(hx, -hy, -hz), new Point3D(hx, hy, -hz), new Point3D(hx, hy, hz - d), new Point3D(hx, -hy, hz - d) };
        var retainedZ = new[] { new Point3D(-hx, -hy, hz), new Point3D(hx - d, -hy, hz), new Point3D(hx - d, hy, hz), new Point3D(-hx, hy, hz) };
        if (replacement.Distinct().Count() != 4) return Fail(ChamferLoweringErrorKind.DegenerateTransition, "localized-chamfer-degenerate-transition", "Endpoint transition quad is degenerate.");
        var signature = Signature(input.Width, input.Depth, input.Height, d);
        var topology = new AirLocalizedPlanarReplacementTopologyPlan(cross, -hy, hy,
            ["UnaffectedFace(-Z)", "UnaffectedFace(-Y)", "RetainedSupportFaceA(+X)", "ChamferFace", "RetainedSupportFaceB(+Z)", "EndpointTransitionStart(-Y)", "EndpointTransitionEnd(+Y)"], signature);
        var witness = new AirLocalizedPlanarReplacementWitness("plane(+X)", "plane(+Z)", start, end, retainedX, retainedZ, replacement,
            "inside:x<=max,z<=max,x+z<=maxX+maxZ-distance", "ExplicitOwnedEndpoints");
        return ChamferLoweringResult<AirLocalizedPlanarReplacementConstruction>.Ok(new($"construction:{input.FeatureId}", input.FeatureId, witness, topology));
    }

    private static AirChamferFeature Feature(AirLocalizedPlanarReplacementChamferCompileRequest input, ChamferLoweringError? error) => new(
        input.FeatureId, input.FeatureName, input.BodyId, new AirFaceBoundarySelection("+X", "SharedEdge(+X,+Z)", false), new AirEqualDistanceChamferRule(input.Distance), input.SourceSpan,
        input.HistoryKnown ? "generated/history-known-axis-aligned-rectangular-prism" : "imported/no-history",
        error is null ? AirFeatureAdmissionStatus.Admitted : error.Kind == ChamferLoweringErrorKind.UnsupportedHistory ? AirFeatureAdmissionStatus.Deferred : AirFeatureAdmissionStatus.Rejected,
        error?.Code ?? "localized-chamfer-planar-single-edge-admitted");

    private static AirBRepPlan BuildPlan(AirLocalizedPlanarReplacementChamferCompileRequest input, AirChamferFeature feature, AirLocalizedPlanarReplacementConstruction construction)
    {
        var p = construction.TopologyPlan;
        var provenance = new AirProvenance("AIR-CHAMFER-LOCALIZED-PLAN-A1", "Localized planar replacement", "shared-edge(+X,+Z)", input.FeatureId,
            nameof(AirLocalizedPlanarReplacementChamferCompiler), AirSelectionClass.None, AirRuleKind.UniformChamfer, feature.ConstructionHistoryKind, true,
            ["No legacy direct-BRep surgery.", "Plan is constructed before emission."]);
        var elements = new List<AirBRepPlanElement>();
        for (var i = 0; i < p.ExpectedVertexCount; i++) elements.Add(new(new($"vertex:{i}"), AirBRepPlanElementKind.Vertex, i is 2 or 7 ? AirBRepPlanRole.EndpointTransitionStart : i is 3 or 8 ? AirBRepPlanRole.EndpointTransitionEnd : AirBRepPlanRole.SectionVertex, input.FeatureId, provenance));
        for (var i = 0; i < p.ExpectedEdgeCount; i++) elements.Add(new(new($"edge:{i}"), AirBRepPlanElementKind.Edge, AirBRepPlanRole.SectionEdge, input.FeatureId, provenance));
        for (var i = 0; i < p.FaceRoles.Count; i++)
        {
            var role = p.FaceRoles[i].StartsWith("RetainedSupportFaceA", StringComparison.Ordinal) ? AirBRepPlanRole.RetainedSupportFaceA : p.FaceRoles[i].StartsWith("RetainedSupportFaceB", StringComparison.Ordinal) ? AirBRepPlanRole.RetainedSupportFaceB : p.FaceRoles[i].StartsWith("ChamferFace", StringComparison.Ordinal) ? AirBRepPlanRole.ChamferFace : p.FaceRoles[i].StartsWith("Endpoint", StringComparison.Ordinal) ? (i == 5 ? AirBRepPlanRole.EndpointTransitionStart : AirBRepPlanRole.EndpointTransitionEnd) : AirBRepPlanRole.UnaffectedFace;
            elements.Add(new(new($"loop:{i}"), AirBRepPlanElementKind.Loop, role, input.FeatureId, provenance));
            for (var c = 0; c < (i < 2 ? 5 : 4); c++) elements.Add(new(new($"coedge:{i}:{c}"), AirBRepPlanElementKind.Coedge, role, input.FeatureId, provenance));
            elements.Add(new(new($"face:{i}"), AirBRepPlanElementKind.Face, role, input.FeatureId, provenance, FaceRole: p.FaceRoles[i]));
        }
        elements.Add(new(new("shell:0"), AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell, input.FeatureId, provenance));
        elements.Add(new(new("body:0"), AirBRepPlanElementKind.Body, AirBRepPlanRole.Body, input.FeatureId, provenance));
        var summary = new AirBRepPlanSummary(AirBRepPlanKind.LocalizedPlanarReplacement, input.FeatureId, p.ExpectedVertexCount, p.ExpectedEdgeCount, p.ExpectedEdgeCount, p.ExpectedCoedgeCount, p.ExpectedLoopCount, p.ExpectedFaceCount, p.ExpectedFaceCount, 1, 1, 0, 4, 1, 1,
            $"box={input.Width:R}x{input.Depth:R}x{input.Height:R}", "explicit-owned-endpoints", [], ["authoritative", "no legacy fallback", "direct single candidate"],
            new(AirNodeKind.TopFaceLoopChamfer, AirRouteKind.Unsupported, AirSelectionClass.None, AirRuleKind.UniformChamfer, feature.ConstructionHistoryKind, "Direct", ["SharedEdge(+X,+Z)"]));
        return new($"brep-plan:localized-planar-replacement:{input.FeatureId}", AirBRepPlanKind.LocalizedPlanarReplacement, input.FeatureId, provenance, elements, summary, [], summary.Guarantees, summary.FeatureContext, LocalizedPlanarReplacementRealizationPlan: p);
    }

    private static string Signature(double w, double d, double h, double c) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"localized-planar-replacement:+X,+Z:{w:R}:{d:R}:{h:R}:{c:R}"))));
}

internal sealed record AirLocalizedPlanarReplacementEmissionResult(bool Succeeded, BrepBody? Body, IReadOnlyList<string> Diagnostics);

internal static class AirLocalizedPlanarReplacementEmitter
{
    public static AirLocalizedPlanarReplacementEmissionResult Emit(AirLocalizedPlanarReplacementTopologyPlan plan)
    {
        var n = plan.CrossSection.Count;
        if (n != 5 || plan.FaceRoles.Count != 7) return new(false, null, ["localized-chamfer-invalid-authoritative-plan"]);
        var b = new TopologyBuilder(); var low = new VertexId[n]; var high = new VertexId[n];
        for (var i = 0; i < n; i++) { low[i] = b.AddVertex(); high[i] = b.AddVertex(); }
        var lowEdges = new EdgeId[n]; var highEdges = new EdgeId[n]; var spans = new EdgeId[n];
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; lowEdges[i] = b.AddEdge(low[i], low[next]); highEdges[i] = b.AddEdge(high[i], high[next]); spans[i] = b.AddEdge(low[i], high[i]); }
        var faces = new List<FaceId> { AddFace(b, lowEdges.Select(Use.F).ToArray()), AddFace(b, highEdges.Reverse().Select(Use.R).ToArray()) };
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; faces.Add(AddFace(b, [Use.R(lowEdges[i]), Use.F(spans[i]), Use.F(highEdges[i]), Use.R(spans[next])])); }
        var shell = b.AddShell(faces); b.AddBody([shell]);
        var lower = plan.CrossSection.ToArray(); var upper = lower.Select(p => new Point3D(p.X, plan.MaxY, p.Z)).ToArray();
        var geo = new BrepGeometryStore(); var bindings = new BrepBindingModel(); int curve = 1;
        void BindEdge(EdgeId id, Point3D a, Point3D z) { var cid = new CurveGeometryId(curve++); geo.AddCurve(cid, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(z - a)))); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, cid, new ParameterInterval(0, (z-a).Length))); }
        for (var i = 0; i < n; i++) { var next=(i+1)%n; BindEdge(lowEdges[i],lower[i],lower[next]); BindEdge(highEdges[i],upper[i],upper[next]); BindEdge(spans[i],lower[i],upper[i]); }
        int surface = 1;
        void BindFace(FaceId id, Point3D origin, Vector3D normal, Vector3D u) { var sid=new SurfaceGeometryId(surface++); geo.AddSurface(sid,SurfaceGeometry.FromPlane(new PlaneSurface(origin,Direction3D.Create(normal),Direction3D.Create(u)))); bindings.AddFaceBinding(new FaceGeometryBinding(id,sid)); }
        BindFace(faces[0], lower[0], new Vector3D(0,-1,0), new Vector3D(1,0,0));
        BindFace(faces[1], upper[0], new Vector3D(0,1,0), new Vector3D(1,0,0));
        for (var i=0;i<n;i++) { var next=(i+1)%n; var edge=lower[next]-lower[i]; BindFace(faces[i+2],lower[i],new Vector3D(0,1,0).Cross(edge),new Vector3D(0,1,0)); }
        var points = new Dictionary<VertexId,Point3D>(); for(var i=0;i<n;i++){points[low[i]]=lower[i];points[high[i]]=upper[i];}
        var body = new BrepBody(b.Model,geo,bindings,points); var check=BrepBindingValidator.Validate(body,true);
        return check.IsSuccess ? new(true,body,["localized-chamfer-plan-emitted"]) : new(false,null,check.Diagnostics.Select(d=>d.Message).ToArray());
    }
    private static FaceId AddFace(TopologyBuilder b, IReadOnlyList<Use> uses) { var loop=b.AllocateLoopId(); var ids=new CoedgeId[uses.Count]; for(var i=0;i<ids.Length;i++)ids[i]=b.AllocateCoedgeId(); for(var i=0;i<ids.Length;i++)b.AddCoedge(new Coedge(ids[i],uses[i].Id,loop,ids[(i+1)%ids.Length],ids[(i+ids.Length-1)%ids.Length],uses[i].Reverse)); b.AddLoop(new Loop(loop,ids)); return b.AddFace([loop]); }
    private readonly record struct Use(EdgeId Id,bool Reverse) { public static Use F(EdgeId id)=>new(id,false); public static Use R(EdgeId id)=>new(id,true); }
}
