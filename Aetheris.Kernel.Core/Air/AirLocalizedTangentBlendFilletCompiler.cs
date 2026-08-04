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
    IReadOnlyList<string> FaceRoles, string DeterministicSignature)
{
    public int ExpectedVertexCount => CrossSection.Count * 2;
    public int ExpectedEdgeCount => CrossSection.Count * 3;
    public int ExpectedFaceCount => CrossSection.Count + 2;
    public int ExpectedLoopCount => ExpectedFaceCount;
    public int ExpectedCoedgeCount => 6 * CrossSection.Count;
}

internal sealed record AirLocalizedTangentBlendConstruction(
    string ConstructionId, string SourceFeatureId, AirLocalizedTangentBlendWitness Witness,
    AirLocalizedTangentBlendTopologyPlan TopologyPlan);

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
        var emitted = AirLocalizedTangentBlendEmitter.Emit(construction);
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
        if (!input.HistoryKnown) return Fail(ChamferLoweringErrorKind.UnsupportedHistory, "localized-fillet-unsupported-history", "Localized tangent blending requires known prismatic construction history.");
        if (!string.Equals(input.Kind, "Fillet", StringComparison.Ordinal)) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-fillet-invalid-kind", "This lowerer accepts constant-radius Fillet intent only.");
        if (!string.Equals(input.FaceA, "+X", StringComparison.Ordinal) || !string.Equals(input.FaceB, "+Z", StringComparison.Ordinal))
            return Fail(ChamferLoweringErrorKind.UnsupportedSelection, "localized-fillet-unsupported-selection:expected-shared-edge-plus-x-plus-z", "The first localized fillet route admits SharedEdge(+X,+Z) only.");
        if (!double.IsFinite(input.Width) || !double.IsFinite(input.Depth) || !double.IsFinite(input.Height) || input.Width <= Tol || input.Depth <= Tol || input.Height <= Tol)
            return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-fillet-invalid-box-dimensions", "Host dimensions must be finite and positive.");
        if (!double.IsFinite(input.Radius) || input.Radius <= Tol) return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, "localized-fillet-radius-must-be-positive", "Radius must be finite and positive.");
        if (input.Radius >= input.Width - Tol || input.Radius >= input.Height - Tol)
            return Fail(ChamferLoweringErrorKind.DistanceTooLarge, "localized-fillet-radius-too-large", "Radius must remain within both orthogonal planar support-face extents.");

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
        var topology = new AirLocalizedTangentBlendTopologyPlan(cross, -hy, hy, r,
            ["UnaffectedFace(-Z)", "UnaffectedFace(-Y)", "RetainedSupportFaceA(+X)", "CylindricalFilletFace", "RetainedSupportFaceB(+Z)", "EndpointTransitionStart(-Y)", "EndpointTransitionEnd(+Y)"], Signature(input.Width, input.Depth, input.Height, r));
        var witness = new AirLocalizedTangentBlendWitness("plane(+X)", "plane(+Z)", new Point3D(hx, -hy, hz), new Point3D(hx, hy, hz), centerStart, centerEnd, arcStart, arcEnd, cylinder, r,
            retainedX, retainedZ, "inside:x<=max,z<=max; remove exterior corner outside quarter-circle", "ExplicitOwnedEndpoints", "history-known-axis-aligned-rectangular-prism");
        return ChamferLoweringResult<AirLocalizedTangentBlendConstruction>.Ok(new($"construction:{input.FeatureId}", input.FeatureId, witness, topology));
    }

    private static AirFilletFeature Feature(AirLocalizedTangentBlendFilletCompileRequest input, ChamferLoweringError? error) => new(
        input.FeatureId, input.FeatureName, input.BodyId, new AirFaceBoundarySelection("+X", "SharedEdge(+X,+Z)", false), new AirConstantRadiusFilletRule(input.Radius), input.SourceSpan,
        input.HistoryKnown ? "generated/history-known-axis-aligned-rectangular-prism" : "imported/no-history",
        error is null ? AirFeatureAdmissionStatus.Admitted : error.Kind == ChamferLoweringErrorKind.UnsupportedHistory ? AirFeatureAdmissionStatus.Deferred : AirFeatureAdmissionStatus.Rejected,
        error?.Code ?? "localized-fillet-tangent-blend-single-edge-admitted");

    private static AirBRepPlan BuildPlan(AirLocalizedTangentBlendFilletCompileRequest input, AirFilletFeature feature, AirLocalizedTangentBlendConstruction construction)
    {
        var p = construction.TopologyPlan;
        var provenance = new AirProvenance("AIR-FILLET-LOCALIZED-M1", "Localized tangent-blend Construction AIR", "shared-edge(+X,+Z)", input.FeatureId,
            nameof(AirLocalizedTangentBlendFilletCompiler), AirSelectionClass.SingleEdge, AirRuleKind.ConstantRadiusFillet, feature.ConstructionHistoryKind, true,
            ["Exact quarter-circle profile.", "Exact cylinder generated by linear sweep.", "No legacy direct-BRep surgery.", "Plan is constructed before emission."]);
        var elements = new List<AirBRepPlanElement>();
        for (var i = 0; i < p.ExpectedVertexCount; i++) elements.Add(new(new($"vertex:{i}"), AirBRepPlanElementKind.Vertex, i is 2 or 7 ? AirBRepPlanRole.EndpointTransitionStart : i is 3 or 8 ? AirBRepPlanRole.EndpointTransitionEnd : AirBRepPlanRole.SectionVertex, input.FeatureId, provenance));
        for (var i = 0; i < p.ExpectedEdgeCount; i++) elements.Add(new(new($"edge:{i}"), AirBRepPlanElementKind.Edge, i is 2 or 7 ? AirBRepPlanRole.FilletFace : AirBRepPlanRole.SectionEdge, input.FeatureId, provenance));
        for (var i = 0; i < p.FaceRoles.Count; i++)
        {
            var role = p.FaceRoles[i].StartsWith("RetainedSupportFaceA", StringComparison.Ordinal) ? AirBRepPlanRole.RetainedSupportFaceA : p.FaceRoles[i].StartsWith("RetainedSupportFaceB", StringComparison.Ordinal) ? AirBRepPlanRole.RetainedSupportFaceB : p.FaceRoles[i].StartsWith("CylindricalFilletFace", StringComparison.Ordinal) ? AirBRepPlanRole.FilletFace : p.FaceRoles[i].StartsWith("Endpoint", StringComparison.Ordinal) ? (i == 5 ? AirBRepPlanRole.EndpointTransitionStart : AirBRepPlanRole.EndpointTransitionEnd) : AirBRepPlanRole.UnaffectedFace;
            elements.Add(new(new($"loop:{i}"), AirBRepPlanElementKind.Loop, role, input.FeatureId, provenance));
            for (var c = 0; c < (i < 2 ? 5 : 4); c++) elements.Add(new(new($"coedge:{i}:{c}"), AirBRepPlanElementKind.Coedge, role, input.FeatureId, provenance));
            elements.Add(new(new($"face:{i}"), AirBRepPlanElementKind.Face, role, input.FeatureId, provenance, FaceRole: p.FaceRoles[i]));
        }
        elements.Add(new(new("surface:cylinder:0"), AirBRepPlanElementKind.Surface, AirBRepPlanRole.FilletFace, input.FeatureId, provenance));
        elements.Add(new(new("shell:0"), AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell, input.FeatureId, provenance));
        elements.Add(new(new("body:0"), AirBRepPlanElementKind.Body, AirBRepPlanRole.Body, input.FeatureId, provenance));
        var summary = new AirBRepPlanSummary(AirBRepPlanKind.LocalizedTangentBlend, input.FeatureId, p.ExpectedVertexCount, p.ExpectedEdgeCount, p.ExpectedEdgeCount, p.ExpectedCoedgeCount, p.ExpectedLoopCount, p.ExpectedFaceCount, p.ExpectedFaceCount, 1, 1, 0, 4, 1, 0,
            $"box={input.Width:R}x{input.Depth:R}x{input.Height:R};radius={input.Radius:R}", "explicit-owned-endpoints", [], ["authoritative", "exact-cylinder", "no legacy fallback", "direct single candidate"],
            new(AirNodeKind.Unsupported, AirRouteKind.Unsupported, AirSelectionClass.SingleEdge, AirRuleKind.ConstantRadiusFillet, feature.ConstructionHistoryKind, "Direct", ["SharedEdge(+X,+Z)"]));
        return new($"brep-plan:localized-tangent-blend:{input.FeatureId}", AirBRepPlanKind.LocalizedTangentBlend, input.FeatureId, provenance, elements, summary, [], summary.Guarantees, summary.FeatureContext, LocalizedTangentBlendRealizationPlan: p);
    }

    private static string Signature(double w, double d, double h, double r) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"localized-tangent-blend:+X,+Z:{w:R}:{d:R}:{h:R}:{r:R}"))));
}

internal sealed record AirLocalizedTangentBlendEmissionResult(bool Succeeded, BrepBody? Body, IReadOnlyList<string> Diagnostics);

internal static class AirLocalizedTangentBlendEmitter
{
    public static AirLocalizedTangentBlendEmissionResult Emit(AirLocalizedTangentBlendConstruction construction)
    {
        var plan = construction.TopologyPlan; var witness = construction.Witness; var n = plan.CrossSection.Count;
        if (n != 5 || plan.FaceRoles.Count != 7 || plan.Radius <= 0d) return new(false, null, ["localized-fillet-invalid-authoritative-plan"]);
        var b = new TopologyBuilder(); var low = new VertexId[n]; var high = new VertexId[n];
        for (var i = 0; i < n; i++) { low[i] = b.AddVertex(); high[i] = b.AddVertex(); }
        var lowEdges = new EdgeId[n]; var highEdges = new EdgeId[n]; var spans = new EdgeId[n];
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; lowEdges[i] = b.AddEdge(low[i], low[next]); highEdges[i] = b.AddEdge(high[i], high[next]); spans[i] = b.AddEdge(low[i], high[i]); }
        var faces = new List<FaceId> { AddFace(b, lowEdges.Select(Use.F).ToArray()), AddFace(b, highEdges.Reverse().Select(Use.R).ToArray()) };
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; faces.Add(AddFace(b, [Use.R(lowEdges[i]), Use.F(spans[i]), Use.F(highEdges[i]), Use.R(spans[next])])); }
        var shell = b.AddShell(faces); b.AddBody([shell]);
        var lower = plan.CrossSection.ToArray(); var upper = lower.Select(p => new Point3D(p.X, plan.MaxY, p.Z)).ToArray();
        var geo = new BrepGeometryStore(); var bindings = new BrepBindingModel(); int curve = 1;
        void BindLine(EdgeId id, Point3D a, Point3D z) { var cid = new CurveGeometryId(curve++); geo.AddCurve(cid, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(z - a)))); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, cid, new ParameterInterval(0, (z-a).Length))); }
        void BindArc(EdgeId id, Circle3Curve arc) { var cid = new CurveGeometryId(curve++); geo.AddCurve(cid, CurveGeometry.FromCircle(arc)); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, cid, new ParameterInterval(0d, double.Pi / 2d))); }
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; if (i == 2) { BindArc(lowEdges[i], witness.QuarterCircleStart); BindArc(highEdges[i], witness.QuarterCircleEnd); } else { BindLine(lowEdges[i], lower[i], lower[next]); BindLine(highEdges[i], upper[i], upper[next]); } BindLine(spans[i], lower[i], upper[i]); }
        int surface = 1;
        void BindPlane(FaceId id, Point3D origin, Vector3D normal, Vector3D u) { var sid = new SurfaceGeometryId(surface++); geo.AddSurface(sid, SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(u)))); bindings.AddFaceBinding(new FaceGeometryBinding(id, sid)); }
        BindPlane(faces[0], lower[0], new Vector3D(0, -1, 0), new Vector3D(1, 0, 0));
        BindPlane(faces[1], upper[0], new Vector3D(0, 1, 0), new Vector3D(1, 0, 0));
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            if (i == 2) { var sid = new SurfaceGeometryId(surface++); geo.AddSurface(sid, SurfaceGeometry.FromCylinder(witness.BlendCylinder)); bindings.AddFaceBinding(new FaceGeometryBinding(faces[i + 2], sid)); }
            else { var edge = lower[next] - lower[i]; BindPlane(faces[i + 2], lower[i], new Vector3D(0, 1, 0).Cross(edge), new Vector3D(0, 1, 0)); }
        }
        var points = new Dictionary<VertexId, Point3D>(); for (var i = 0; i < n; i++) { points[low[i]] = lower[i]; points[high[i]] = upper[i]; }
        var body = new BrepBody(b.Model, geo, bindings, points); var check = BrepBindingValidator.Validate(body, true);
        return check.IsSuccess ? new(true, body, ["localized-fillet-plan-emitted"]) : new(false, null, check.Diagnostics.Select(d => d.Message).ToArray());
    }

    private static FaceId AddFace(TopologyBuilder b, IReadOnlyList<Use> uses) { var loop = b.AllocateLoopId(); var ids = new CoedgeId[uses.Count]; for (var i = 0; i < ids.Length; i++) ids[i] = b.AllocateCoedgeId(); for (var i = 0; i < ids.Length; i++) b.AddCoedge(new Coedge(ids[i], uses[i].Id, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse)); b.AddLoop(new Loop(loop, ids)); return b.AddFace([loop]); }
    private readonly record struct Use(EdgeId Id, bool Reverse) { public static Use F(EdgeId id) => new(id, false); public static Use R(EdgeId id) => new(id, true); }
}
