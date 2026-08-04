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

/// <summary>Closed semantic family for the admitted localized edge replacement routes.
/// It is deliberately not a public runtime extension point.</summary>
internal enum AirLocalizedEdgeFinishKind { Chamfer, Fillet }

internal abstract record AirEdgeFinishRule;
internal sealed record AirEqualDistanceEdgeFinishRule(double Distance) : AirEdgeFinishRule;
internal sealed record AirConstantRadiusEdgeFinishRule(double Radius) : AirEdgeFinishRule;

/// <summary>Shared Feature AIR.  Compatibility feature records remain route-facing only.</summary>
internal sealed record AirEdgeFinishFeature(
    string FeatureId, string FeatureName, string BodyId, AirFaceBoundarySelection Selection,
    AirLocalizedEdgeFinishKind Kind, AirEdgeFinishRule Rule, AirSourceSpan SourceSpan,
    string ConstructionHistoryKind, AirFeatureAdmissionStatus Admission, string AdmissionReason);

internal sealed record LocalizedEdgeReplacementContext(
    string BodyId, string FeatureId, string FeatureName, double Width, double Depth, double Height,
    string FaceA, string FaceB, AirLocalizedEdgeFinishKind Kind, double Value,
    AirSourceSpan SourceSpan, bool HistoryKnown);

internal sealed record LocalizedEdgeReplacementAdmission(
    Point3D EdgeStart, Point3D EdgeEnd, double HalfWidth, double HalfDepth, double HalfHeight,
    string MaterialSide, string EndpointPolicy, AirEdgeFinishFeature Feature);

internal abstract record LocalizedReplacementGeometry(
    IReadOnlyList<Point3D> BoundaryAtStart, IReadOnlyList<Point3D> BoundaryAtEnd,
    string GeometryKind);

internal sealed record PlanarChamferReplacement(
    IReadOnlyList<Point3D> BoundaryAtStart, IReadOnlyList<Point3D> BoundaryAtEnd,
    IReadOnlyList<Point3D> ReplacementFace) : LocalizedReplacementGeometry(BoundaryAtStart, BoundaryAtEnd, "PlanarChamfer");

internal sealed record CylindricalFilletReplacement(
    IReadOnlyList<Point3D> BoundaryAtStart, IReadOnlyList<Point3D> BoundaryAtEnd,
    Circle3Curve StartArc, Circle3Curve EndArc, CylinderSurface Cylinder, double Radius)
    : LocalizedReplacementGeometry(BoundaryAtStart, BoundaryAtEnd, "CylindricalFillet");

/// <summary>Immutable Construction AIR: common ownership and exact typed replacement geometry.</summary>
internal sealed record LocalizedEdgeReplacementConstruction(
    string ConstructionId, string SourceFeatureId, LocalizedEdgeReplacementAdmission Admission,
    IReadOnlyList<Point3D> RetainedSupportFaceA, IReadOnlyList<Point3D> RetainedSupportFaceB,
    IReadOnlyList<Point3D> CrossSection, LocalizedReplacementGeometry Replacement,
    LocalizedEdgeReplacementTopologyPlan TopologyPlan, string Provenance);

/// <summary>Topology authority shared by every localized edge replacement family.</summary>
internal sealed record LocalizedEdgeReplacementTopologyPlan(
    IReadOnlyList<Point3D> CrossSection, double MinY, double MaxY,
    AirLocalizedEdgeFinishKind FinishKind, IReadOnlyList<string> FaceRoles,
    string DeterministicSignature)
{
    public int ExpectedVertexCount => CrossSection.Count * 2;
    public int ExpectedEdgeCount => CrossSection.Count * 3;
    public int ExpectedFaceCount => CrossSection.Count + 2;
    public int ExpectedLoopCount => ExpectedFaceCount;
    public int ExpectedCoedgeCount => 6 * CrossSection.Count;
}

internal static class LocalizedEdgeReplacementCompilerModel
{
    internal const double Tolerance = 1e-9;

    public static ChamferLoweringResult<LocalizedEdgeReplacementAdmission> Admit(LocalizedEdgeReplacementContext input)
    {
        ChamferLoweringResult<LocalizedEdgeReplacementAdmission> Fail(ChamferLoweringErrorKind kind, string code, string message) =>
            ChamferLoweringResult<LocalizedEdgeReplacementAdmission>.Err(new(kind, code, message, "FeatureAIR->LocalizedEdgeReplacement"));

        var family = input.Kind == AirLocalizedEdgeFinishKind.Chamfer ? "chamfer" : "fillet";
        if (!input.HistoryKnown) return Fail(ChamferLoweringErrorKind.UnsupportedHistory, $"localized-{family}-unsupported-history", "Localized edge replacement requires known prismatic construction history.");
        if (!string.Equals(input.FaceA, "+X", StringComparison.Ordinal) || !string.Equals(input.FaceB, "+Z", StringComparison.Ordinal))
            return Fail(ChamferLoweringErrorKind.UnsupportedSelection, $"localized-{family}-unsupported-selection:expected-shared-edge-plus-x-plus-z", "The admitted localized route selects SharedEdge(+X,+Z) only.");
        if (!double.IsFinite(input.Width) || !double.IsFinite(input.Depth) || !double.IsFinite(input.Height) || input.Width <= Tolerance || input.Depth <= Tolerance || input.Height <= Tolerance)
            return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, $"localized-{family}-invalid-box-dimensions", "Host dimensions must be finite and positive.");
        if (!double.IsFinite(input.Value) || input.Value <= Tolerance)
            return Fail(ChamferLoweringErrorKind.InvalidAuthoredInput, $"localized-{family}-{(input.Kind == AirLocalizedEdgeFinishKind.Chamfer ? "distance" : "radius")}-must-be-positive", "Finish value must be finite and positive.");
        if (input.Value >= input.Width - Tolerance || input.Value >= input.Height - Tolerance)
            return Fail(ChamferLoweringErrorKind.DistanceTooLarge, $"localized-{family}-{(input.Kind == AirLocalizedEdgeFinishKind.Chamfer ? "distance" : "radius")}-too-large", "Finish value must remain within both orthogonal planar support-face extents.");

        var hx = input.Width / 2d; var hy = input.Depth / 2d; var hz = input.Height / 2d;
        var rule = input.Kind == AirLocalizedEdgeFinishKind.Chamfer
            ? (AirEdgeFinishRule)new AirEqualDistanceEdgeFinishRule(input.Value)
            : new AirConstantRadiusEdgeFinishRule(input.Value);
        var feature = new AirEdgeFinishFeature(input.FeatureId, input.FeatureName, input.BodyId,
            new AirFaceBoundarySelection("+X", "SharedEdge(+X,+Z)", false), input.Kind, rule, input.SourceSpan,
            "generated/history-known-axis-aligned-rectangular-prism", AirFeatureAdmissionStatus.Admitted,
            $"localized-{family}-direct-single-candidate");
        return ChamferLoweringResult<LocalizedEdgeReplacementAdmission>.Ok(new(
            new Point3D(hx, -hy, hz), new Point3D(hx, hy, hz), hx, hy, hz,
            "inside:x<=max,z<=max", "ExplicitOwnedEndpoints", feature));
    }

    public static LocalizedEdgeReplacementTopologyPlan Topology(LocalizedEdgeReplacementContext input, IReadOnlyList<Point3D> crossSection)
    {
        var roles = new[] { "UnaffectedFace(-Z)", "UnaffectedFace(-Y)", "RetainedSupportFaceA(+X)",
            $"ReplacementFace({(input.Kind == AirLocalizedEdgeFinishKind.Chamfer ? "PlanarChamfer" : "CylindricalFillet")})",
            "RetainedSupportFaceB(+Z)", "EndpointTransitionStart(-Y)", "EndpointTransitionEnd(+Y)" };
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant(
            $"localized-edge-replacement:{input.Kind}:+X,+Z:{input.Width:R}:{input.Depth:R}:{input.Height:R}:{input.Value:R}"))));
        return new(crossSection, -input.Depth / 2d, input.Depth / 2d, input.Kind, roles, signature);
    }

    public static AirBRepPlan BuildBRepPlan(LocalizedEdgeReplacementConstruction construction, AirProvenance provenance, AirBRepPlanKind compatibilityKind)
    {
        var p = construction.TopologyPlan;
        var elements = new List<AirBRepPlanElement>();
        for (var i = 0; i < p.ExpectedVertexCount; i++)
            elements.Add(new(new($"vertex:{i}"), AirBRepPlanElementKind.Vertex, i is 2 or 7 ? AirBRepPlanRole.EndpointTransitionStart : i is 3 or 8 ? AirBRepPlanRole.EndpointTransitionEnd : AirBRepPlanRole.SectionVertex, construction.SourceFeatureId, provenance));
        for (var i = 0; i < p.ExpectedEdgeCount; i++)
            elements.Add(new(new($"edge:{i}"), AirBRepPlanElementKind.Edge, i is 2 or 7 ? AirBRepPlanRole.ReplacementBoundaryA : i is 3 or 8 ? AirBRepPlanRole.ReplacementBoundaryB : AirBRepPlanRole.SectionEdge, construction.SourceFeatureId, provenance));
        for (var i = 0; i < p.FaceRoles.Count; i++)
        {
            var role = Role(p.FaceRoles[i], i);
            elements.Add(new(new($"loop:{i}"), AirBRepPlanElementKind.Loop, role, construction.SourceFeatureId, provenance));
            for (var c = 0; c < (i < 2 ? 5 : 4); c++) elements.Add(new(new($"coedge:{i}:{c}"), AirBRepPlanElementKind.Coedge, role, construction.SourceFeatureId, provenance));
            elements.Add(new(new($"face:{i}"), AirBRepPlanElementKind.Face, role, construction.SourceFeatureId, provenance, FaceRole: p.FaceRoles[i], SemanticRoles: role == AirBRepPlanRole.ReplacementFace ? [AirBRepPlanRole.ReplacementFace, p.FinishKind == AirLocalizedEdgeFinishKind.Chamfer ? AirBRepPlanRole.ChamferFace : AirBRepPlanRole.FilletFace] : [role]));
        }
        elements.Add(new(new("surface:replacement:0"), AirBRepPlanElementKind.Surface, AirBRepPlanRole.ReplacementFace, construction.SourceFeatureId, provenance));
        elements.Add(new(new("shell:0"), AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell, construction.SourceFeatureId, provenance));
        elements.Add(new(new("body:0"), AirBRepPlanElementKind.Body, AirBRepPlanRole.Body, construction.SourceFeatureId, provenance));
        var feature = construction.Admission.Feature;
        var summary = new AirBRepPlanSummary(compatibilityKind, construction.SourceFeatureId, p.ExpectedVertexCount, p.ExpectedEdgeCount, p.ExpectedEdgeCount, p.ExpectedCoedgeCount, p.ExpectedLoopCount, p.ExpectedFaceCount, p.ExpectedFaceCount, 1, 1, 0, 4, 1, p.FinishKind == AirLocalizedEdgeFinishKind.Chamfer ? 1 : 0,
            $"localized-edge-replacement={p.FinishKind};signature={p.DeterministicSignature}", "explicit-owned-endpoints", [],
            ["authoritative localized edge-replacement topology", "no legacy fallback", "direct single candidate", "shared endpoint ownership"],
            new(AirNodeKind.Unsupported, AirRouteKind.Unsupported, AirSelectionClass.SingleEdge, p.FinishKind == AirLocalizedEdgeFinishKind.Chamfer ? AirRuleKind.UniformChamfer : AirRuleKind.ConstantRadiusFillet, feature.ConstructionHistoryKind, "Direct", ["SharedEdge(+X,+Z)"]));
        return new($"brep-plan:localized-edge-replacement:{construction.SourceFeatureId}", compatibilityKind, construction.SourceFeatureId, provenance, elements, summary, [], summary.Guarantees, summary.FeatureContext, LocalizedEdgeReplacementRealizationPlan: p);
    }

    public static AirLocalizedEdgeReplacementEmissionResult Emit(LocalizedEdgeReplacementConstruction construction)
    {
        var plan = construction.TopologyPlan; var n = plan.CrossSection.Count;
        if (n != 5 || plan.FaceRoles.Count != 7) return new(false, null, ["localized-edge-replacement-invalid-authoritative-plan"]);
        var builder = new TopologyBuilder(); var low = new VertexId[n]; var high = new VertexId[n];
        for (var i = 0; i < n; i++) { low[i] = builder.AddVertex(); high[i] = builder.AddVertex(); }
        var lowEdges = new EdgeId[n]; var highEdges = new EdgeId[n]; var spans = new EdgeId[n];
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; lowEdges[i] = builder.AddEdge(low[i], low[next]); highEdges[i] = builder.AddEdge(high[i], high[next]); spans[i] = builder.AddEdge(low[i], high[i]); }
        var faces = new List<FaceId> { AddFace(builder, lowEdges.Select(Use.F).ToArray()), AddFace(builder, highEdges.Reverse().Select(Use.R).ToArray()) };
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; faces.Add(AddFace(builder, [Use.R(lowEdges[i]), Use.F(spans[i]), Use.F(highEdges[i]), Use.R(spans[next])])); }
        var shell = builder.AddShell(faces); builder.AddBody([shell]);
        var lower = plan.CrossSection.ToArray(); var upper = lower.Select(p => new Point3D(p.X, plan.MaxY, p.Z)).ToArray();
        var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var curveNumber = 1;
        void BindLine(EdgeId id, Point3D a, Point3D z) { var cid = new CurveGeometryId(curveNumber++); geometry.AddCurve(cid, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(z - a)))); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, cid, new ParameterInterval(0, (z - a).Length))); }
        void BindArc(EdgeId id, Circle3Curve arc) { var cid = new CurveGeometryId(curveNumber++); geometry.AddCurve(cid, CurveGeometry.FromCircle(arc)); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, cid, new ParameterInterval(0, double.Pi / 2d))); }
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; if (i == 2 && construction.Replacement is CylindricalFilletReplacement fillet) { BindArc(lowEdges[i], fillet.StartArc); BindArc(highEdges[i], fillet.EndArc); } else { BindLine(lowEdges[i], lower[i], lower[next]); BindLine(highEdges[i], upper[i], upper[next]); } BindLine(spans[i], lower[i], upper[i]); }
        var surfaceNumber = 1;
        void BindPlane(FaceId id, Point3D origin, Vector3D normal, Vector3D u) { var sid = new SurfaceGeometryId(surfaceNumber++); geometry.AddSurface(sid, SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(u)))); bindings.AddFaceBinding(new FaceGeometryBinding(id, sid)); }
        BindPlane(faces[0], lower[0], new Vector3D(0, -1, 0), new Vector3D(1, 0, 0));
        BindPlane(faces[1], upper[0], new Vector3D(0, 1, 0), new Vector3D(1, 0, 0));
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; if (i == 2 && construction.Replacement is CylindricalFilletReplacement fillet) { var sid = new SurfaceGeometryId(surfaceNumber++); geometry.AddSurface(sid, SurfaceGeometry.FromCylinder(fillet.Cylinder)); bindings.AddFaceBinding(new FaceGeometryBinding(faces[i + 2], sid)); } else { var edge = lower[next] - lower[i]; BindPlane(faces[i + 2], lower[i], new Vector3D(0, 1, 0).Cross(edge), new Vector3D(0, 1, 0)); } }
        var points = new Dictionary<VertexId, Point3D>(); for (var i = 0; i < n; i++) { points[low[i]] = lower[i]; points[high[i]] = upper[i]; }
        var body = new BrepBody(builder.Model, geometry, bindings, points); var check = BrepBindingValidator.Validate(body, true);
        return check.IsSuccess ? new(true, body, ["localized-edge-replacement-plan-emitted"]) : new(false, null, check.Diagnostics.Select(d => d.Message).ToArray());
    }

    private static AirBRepPlanRole Role(string faceRole, int faceIndex) =>
        faceRole.StartsWith("RetainedSupportFaceA", StringComparison.Ordinal) ? AirBRepPlanRole.RetainedSupportFaceA :
        faceRole.StartsWith("RetainedSupportFaceB", StringComparison.Ordinal) ? AirBRepPlanRole.RetainedSupportFaceB :
        faceRole.StartsWith("ReplacementFace", StringComparison.Ordinal) ? AirBRepPlanRole.ReplacementFace :
        faceRole.StartsWith("Endpoint", StringComparison.Ordinal) ? (faceIndex == 5 ? AirBRepPlanRole.EndpointTransitionStart : AirBRepPlanRole.EndpointTransitionEnd) : AirBRepPlanRole.UnaffectedFace;
    private static FaceId AddFace(TopologyBuilder b, IReadOnlyList<Use> uses) { var loop = b.AllocateLoopId(); var ids = new CoedgeId[uses.Count]; for (var i = 0; i < ids.Length; i++) ids[i] = b.AllocateCoedgeId(); for (var i = 0; i < ids.Length; i++) b.AddCoedge(new Coedge(ids[i], uses[i].Id, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse)); b.AddLoop(new Loop(loop, ids)); return b.AddFace([loop]); }
    private readonly record struct Use(EdgeId Id, bool Reverse) { public static Use F(EdgeId id) => new(id, false); public static Use R(EdgeId id) => new(id, true); }
}

internal sealed record AirLocalizedEdgeReplacementEmissionResult(bool Succeeded, BrepBody? Body, IReadOnlyList<string> Diagnostics);
