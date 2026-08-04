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

/// <summary>Bounded M4A request: two equal-radius convex box-edge fillets only.</summary>
internal sealed record AirLocalizedEdgeJunctionFilletCompileRequest(
    string BodyId, string FeatureId, string FeatureName, double Width, double Depth, double Height,
    string FaceA, string TargetA, string FaceB, string TargetB, double RadiusA, double RadiusB,
    AirSourceSpan SourceSpan, bool HistoryKnown = true);

internal enum FilletJunctionErrorKind
{
    EdgesDoNotShareEndpoint, RadiusMismatch, RadiusTooLarge, UnsupportedSurfaceCombination,
    DirectIntersectionMissing, DirectIntersectionAmbiguous, IntersectionBranchSelectionFailed,
    IntersectionCurveUnsupported, BoundaryOwnershipConflict, PatchRequired, VerificationFailure,
}

internal sealed record FilletJunctionError(FilletJunctionErrorKind Kind, string Code, string Message, string Stage, IReadOnlyList<string> Evidence);

/// <summary>
/// The exact selected branch of the two cylinder intersection.  In local coordinates
/// u=x-cx=y-cy and w=z-cz, u²+w²=R².  Embedded in 3-D that is a planar ellipse,
/// not a spherical patch and not an approximation.
/// </summary>
internal sealed record LocalizedEdgeJunctionDirectIntersectionClosure(
    CylinderSurface SurfaceA, CylinderSurface SurfaceB, Ellipse3Curve SharedCurve,
    ParameterInterval Trim, Point3D Start, Point3D End, string Branch, string MaterialSide,
    double SurfaceADeviation, double SurfaceBDeviation, string Provenance);

internal sealed record LocalizedFilletJunctionConstruction(
    string ConstructionId, LocalizedEdgeJunctionReplacement ReplacementA,
    LocalizedEdgeJunctionReplacement ReplacementB, Point3D SharedEndpoint,
    Point3D RemoteEndpointA, Point3D RemoteEndpointB,
    LocalizedEdgeJunctionDirectIntersectionClosure Closure,
    IReadOnlyList<IReadOnlyList<Point3D>> RetainedRegions, string MaterialSide,
    string BoundaryOwnership, LocalizedEdgeJunctionTopologyPlan TopologyPlan, string Provenance);

internal sealed record AirLocalizedEdgeJunctionFilletCompileResult(
    bool Succeeded, LocalizedFilletJunctionConstruction? Construction, AirBRepPlan? BRepPlan,
    BrepBody? Body, FilletJunctionError? Error, IReadOnlyList<string> Diagnostics)
{
    public const string ProductionRoute = "AirLocalizedEdgeJunctionFilletM4A";
}

/// <summary>
/// Exact direct-intersection realization.  It is intentionally not a rolling-ball
/// framework: only the positive orthogonal box corner and the positive branch are
/// admitted.  The spherical construction is reserved for the separate three-edge case.
/// </summary>
internal static class AirLocalizedEdgeJunctionFilletCompiler
{
    private const double Tol = 1e-9;

    public static AirLocalizedEdgeJunctionFilletCompileResult Compile(AirLocalizedEdgeJunctionFilletCompileRequest input)
    {
        var lowered = Lower(input);
        if (lowered.Error is not null) return new(false, null, null, null, lowered.Error, [lowered.Error.Code, .. lowered.Error.Evidence]);
        var construction = lowered.Construction!;
        var plan = BuildPlan(input, construction);
        var emitted = Emit(construction);
        if (!emitted.Succeeded || emitted.Body is null)
            return Failure(construction, plan, new(FilletJunctionErrorKind.VerificationFailure, "localized-fillet-junction-materialization-failed", "The authoritative direct-intersection plan did not materialize.", "BRep", emitted.Diagnostics));

        var body = emitted.Body;
        var cylinderCount = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
        var ellipseCount = body.Geometry.Curves.Count(c => c.Value.Kind == CurveGeometryKind.Ellipse3);
        var preflight = BrepExportPreflight.Validate(body);
        if (cylinderCount != 2 || ellipseCount != 1 || body.Topology.Faces.Count() != 8 || !preflight.IsValid)
            return Failure(construction, plan, new(FilletJunctionErrorKind.VerificationFailure, "localized-fillet-junction-analytic-verification-failed", "The direct junction requires two cylinders, one shared ellipse, eight faces, and valid analytic preflight.", "Verification", preflight.Diagnostics.Select(d => d.Code).ToArray()));

        return new(true, construction, plan, body, null,
            ["localized-fillet-junction-feature-admitted", "localized-fillet-junction-direct-intersection", "localized-fillet-junction-candidate-plans=1", "localized-fillet-junction-hard-valid-plans=1", "localized-fillet-junction-authoritative-brep-plan-consumed", "localized-fillet-junction-no-corner-patch", "localized-fillet-junction-no-legacy-fallback"]);
    }

    private static (LocalizedFilletJunctionConstruction? Construction, FilletJunctionError? Error) Lower(AirLocalizedEdgeJunctionFilletCompileRequest input)
    {
        FilletJunctionError Fail(FilletJunctionErrorKind kind, string code, string message, params string[] evidence) => new(kind, code, message, "FeatureAIR->ConstructionAIR", evidence);
        if (!input.HistoryKnown) return (null, Fail(FilletJunctionErrorKind.UnsupportedSurfaceCombination, "localized-fillet-junction-unsupported-history", "Direct fillet junctions require history-known axis-aligned box support planes."));
        if (!FinitePositive(input.Width) || !FinitePositive(input.Depth) || !FinitePositive(input.Height)) return (null, Fail(FilletJunctionErrorKind.UnsupportedSurfaceCombination, "localized-fillet-junction-invalid-box-dimensions", "Box dimensions must be finite and positive."));
        if (!Matches(input.FaceA, input.TargetA, "+X") || !Matches(input.FaceB, input.TargetB, "+Y")) return (null, Fail(FilletJunctionErrorKind.EdgesDoNotShareEndpoint, "localized-fillet-junction-edges-do-not-share-canonical-endpoint", "M4A admits SharedEdge(+X,+Z) and SharedEdge(+Y,+Z) only."));
        if (!FinitePositive(input.RadiusA) || !FinitePositive(input.RadiusB)) return (null, Fail(FilletJunctionErrorKind.RadiusTooLarge, "localized-fillet-junction-radius-must-be-positive", "Both radii must be finite and positive."));
        if (double.Abs(input.RadiusA - input.RadiusB) > Tol) return (null, Fail(FilletJunctionErrorKind.RadiusMismatch, "localized-fillet-junction-radius-mismatch", "M4A requires equal radii."));
        if (input.RadiusA >= input.Width - Tol || input.RadiusA >= input.Depth - Tol || input.RadiusA >= input.Height - Tol) return (null, Fail(FilletJunctionErrorKind.RadiusTooLarge, "localized-fillet-junction-radius-too-large", "The radius must remain within every incident support extent."));

        var r = input.RadiusA; var hx = input.Width / 2d; var hy = input.Depth / 2d; var hz = input.Height / 2d;
        var cx = hx - r; var cy = hy - r; var cz = hz - r;
        var center = new Point3D(cx, cy, cz);
        var x = Direction3D.Create(new Vector3D(1, 0, 0)); var y = Direction3D.Create(new Vector3D(0, 1, 0)); var z = Direction3D.Create(new Vector3D(0, 0, 1));
        var cylinderA = new CylinderSurface(center, y, r, x);
        var cylinderB = new CylinderSurface(center, x, r, y);
        var diagonal = Direction3D.Create(new Vector3D(1, 1, 0));
        var planeNormal = Direction3D.Create(new Vector3D(1, -1, 0));
        var seam = new Ellipse3Curve(center, planeNormal, r * double.Sqrt(2d), r, diagonal);
        var p = new Point3D(hx, hy, cz); var q = new Point3D(cx, cy, hz);
        var trim = new ParameterInterval(0d, double.Pi / 2d);
        var closure = new LocalizedEdgeJunctionDirectIntersectionClosure(cylinderA, cylinderB, seam, trim, p, q, "positive: x-cx=y-cy>=0; z-cz>=0", "convex exterior removal", 0d, 0d, "exact ellipse: (x-cx)^2+(z-cz)^2=R^2=(y-cy)^2+(z-cz)^2");

        var vertices = new[]
        {
            new Point3D(-hx, -hy, -hz), new Point3D(hx, -hy, -hz), new Point3D(hx, hy, -hz), new Point3D(-hx, hy, -hz),
            new Point3D(-hx, -hy, hz), new Point3D(hx, -hy, cz), new Point3D(cx, -hy, hz),
            new Point3D(-hx, hy, cz), new Point3D(-hx, cy, hz), p, q,
        };
        IReadOnlyList<int>[] loops =
        [
            [0, 3, 2, 1], [0, 4, 8, 7, 3], [0, 1, 5, 6, 4], [1, 2, 9, 5],
            [3, 7, 9, 2], [4, 6, 10, 8], [5, 9, 10, 6], [7, 8, 10, 9],
        ];
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"localized-edge-junction:Fillet:+X,+Z:+Y,+Z:{input.Width:R}:{input.Depth:R}:{input.Height:R}:{r:R}:direct-ellipse"))));
        var topology = new LocalizedEdgeJunctionTopologyPlan(vertices, loops,
            ["UnaffectedFace(-Z)", "RemoteEndpointB(-X)", "RemoteEndpointA(-Y)", "RetainedSupportFace(+X)", "RetainedSupportFace(+Y)", "RetainedSupportFace(+Z)", "FilletReplacementA(Cylinder)", "FilletReplacementB(Cylinder)"], signature);
        var featureA = Feature(input, "A", "+X", "SharedEdge(+X,+Z)"); var featureB = Feature(input, "B", "+Y", "SharedEdge(+Y,+Z)");
        var replacementA = new LocalizedEdgeJunctionReplacement("SharedEdge(+X,+Z)", featureA, [vertices[5], vertices[9], vertices[10], vertices[6]], [vertices[5], vertices[6]], "CylindricalFillet");
        var replacementB = new LocalizedEdgeJunctionReplacement("SharedEdge(+Y,+Z)", featureB, [vertices[7], vertices[8], vertices[10], vertices[9]], [vertices[7], vertices[8]], "CylindricalFillet");
        return (new($"construction:{input.FeatureId}:fillet-junction", replacementA, replacementB, new Point3D(hx, hy, hz), new Point3D(hx, -hy, hz), new Point3D(-hx, hy, hz), closure,
            [[vertices[1], vertices[2], vertices[9], vertices[5]], [vertices[3], vertices[7], vertices[9], vertices[2]], [vertices[4], vertices[6], vertices[10], vertices[8]]],
            "inside:x<=max,y<=max,z<=max; convex exterior removal", "DirectIntersectionEllipse(opposite coedges); RemoteEndpointA(-Y); RemoteEndpointB(-X)", topology,
            "history-known-axis-aligned-rectangular-box; equal-radius-direct-cylinder-cylinder-intersection"), null);
    }

    private static AirLocalizedEdgeJunctionFilletCompileResult Failure(LocalizedFilletJunctionConstruction construction, AirBRepPlan plan, FilletJunctionError error) => new(false, construction, plan, null, error, [error.Code, .. error.Evidence]);

    private static AirEdgeFinishFeature Feature(AirLocalizedEdgeJunctionFilletCompileRequest input, string suffix, string face, string edge) => new($"{input.FeatureId}.{suffix}", $"{input.FeatureName}.{suffix}", input.BodyId, new AirFaceBoundarySelection(face, edge, false), AirLocalizedEdgeFinishKind.Fillet, new AirConstantRadiusEdgeFinishRule(input.RadiusA), input.SourceSpan, "generated/history-known-axis-aligned-rectangular-prism", AirFeatureAdmissionStatus.Admitted, "localized-fillet-junction-direct-intersection-candidate");

    private static AirBRepPlan BuildPlan(AirLocalizedEdgeJunctionFilletCompileRequest input, LocalizedFilletJunctionConstruction construction)
    {
        var provenance = new AirProvenance("AIR-FILLET-JUNCTION-M4A", "Localized two-edge direct cylinder intersection", "shared-edge(+X,+Z)/shared-edge(+Y,+Z)", input.FeatureId, nameof(AirLocalizedEdgeJunctionFilletCompiler), AirSelectionClass.None, AirRuleKind.ConstantRadiusFillet, construction.Provenance, true, ["One hard-valid direct-intersection construction.", "Shared ellipse is owned by opposite replacement coedges.", "No corner patch and no legacy surgery."]);
        var p = construction.TopologyPlan; var elements = new List<AirBRepPlanElement>();
        for (var i = 0; i < p.ExpectedVertexCount; i++) elements.Add(new(new($"vertex:{i}"), AirBRepPlanElementKind.Vertex, i is 9 or 10 ? AirBRepPlanRole.SharedJunction : i is 5 or 6 ? AirBRepPlanRole.RemoteEndpointA : i is 7 or 8 ? AirBRepPlanRole.RemoteEndpointB : AirBRepPlanRole.SectionVertex, input.FeatureId, provenance));
        for (var i = 0; i < p.ExpectedFaceCount; i++)
        {
            var role = i == 6 ? AirBRepPlanRole.ReplacementFaceA : i == 7 ? AirBRepPlanRole.ReplacementFaceB : i == 1 ? AirBRepPlanRole.RemoteEndpointB : i == 2 ? AirBRepPlanRole.RemoteEndpointA : i is 3 or 4 or 5 ? AirBRepPlanRole.RetainedSupportFaceA : AirBRepPlanRole.UnaffectedFace;
            elements.Add(new(new($"loop:{i}"), AirBRepPlanElementKind.Loop, role, input.FeatureId, provenance));
            for (var c = 0; c < p.FaceLoops[i].Count; c++) elements.Add(new(new($"coedge:{i}:{c}"), AirBRepPlanElementKind.Coedge, role, input.FeatureId, provenance));
            elements.Add(new(new($"face:{i}"), AirBRepPlanElementKind.Face, role, input.FeatureId, provenance, FaceRole: p.FaceRoles[i], SemanticRoles: i is 6 or 7 ? [role, AirBRepPlanRole.ReplacementFace, AirBRepPlanRole.FilletFace] : [role]));
        }
        for (var i = 0; i < p.ExpectedEdgeCount; i++) elements.Add(new(new($"edge:{i}"), AirBRepPlanElementKind.Edge, i == p.ExpectedEdgeCount - 1 ? AirBRepPlanRole.DirectJunctionBoundary : AirBRepPlanRole.SectionEdge, input.FeatureId, provenance));
        elements.Add(new(new("curve:direct-intersection:0"), AirBRepPlanElementKind.Curve, AirBRepPlanRole.DirectJunctionBoundary, input.FeatureId, provenance, SemanticRoles: [AirBRepPlanRole.DirectJunctionBoundary, AirBRepPlanRole.SharedJunction]));
        elements.Add(new(new("shell:0"), AirBRepPlanElementKind.Shell, AirBRepPlanRole.BodyShell, input.FeatureId, provenance)); elements.Add(new(new("body:0"), AirBRepPlanElementKind.Body, AirBRepPlanRole.Body, input.FeatureId, provenance));
        var summary = new AirBRepPlanSummary(AirBRepPlanKind.LocalizedEdgeJunction, input.FeatureId, p.ExpectedVertexCount, p.ExpectedEdgeCount, p.ExpectedEdgeCount, p.ExpectedCoedgeCount, p.ExpectedLoopCount, p.ExpectedFaceCount, p.ExpectedFaceCount, 1, 1, 0, 3, 2, 0, $"localized-edge-junction=Fillet;closure=DirectIntersection;signature={p.DeterministicSignature}", construction.BoundaryOwnership, [], ["authoritative combined direct-intersection topology", "one hard-valid plan", "no corner patch", "no legacy fallback"], new(AirNodeKind.Unsupported, AirRouteKind.Unsupported, AirSelectionClass.None, AirRuleKind.ConstantRadiusFillet, construction.Provenance, "Direct", ["SharedEdge(+X,+Z)", "SharedEdge(+Y,+Z)"]));
        return new($"brep-plan:localized-edge-junction:{input.FeatureId}", AirBRepPlanKind.LocalizedEdgeJunction, input.FeatureId, provenance, elements, summary, [], summary.Guarantees, summary.FeatureContext, LocalizedEdgeJunctionRealizationPlan: p);
    }

    private static AirLocalizedEdgeReplacementEmissionResult Emit(LocalizedFilletJunctionConstruction construction)
    {
        var points = construction.TopologyPlan.Vertices; var loops = construction.TopologyPlan.FaceLoops;
        var builder = new TopologyBuilder(); var vertices = points.Select(_ => builder.AddVertex()).ToArray(); var edges = new Dictionary<(int, int), EdgeId>(); var directions = new Dictionary<(int, int), (int Start, int End)>(); var faces = new List<FaceId>();
        foreach (var loop in loops)
        {
            var uses = new Use[loop.Count];
            for (var i = 0; i < loop.Count; i++) { var a = loop[i]; var b = loop[(i + 1) % loop.Count]; var key = a < b ? (a, b) : (b, a); if (!edges.TryGetValue(key, out var edge)) { edge = builder.AddEdge(vertices[a], vertices[b]); edges[key] = edge; directions[key] = (a, b); } var d = directions[key]; uses[i] = d.Start == a && d.End == b ? Use.F(edge) : Use.R(edge); }
            faces.Add(AddFace(builder, uses));
        }
        var shell = builder.AddShell(faces); builder.AddBody([shell]);
        var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var curveNumber = 1;
        foreach (var pair in edges.OrderBy(p => p.Value.Value))
        {
            var (a, b) = directions[pair.Key]; var cid = new CurveGeometryId(curveNumber++);
            if (pair.Key == (5, 6)) geometry.AddCurve(cid, CurveGeometry.FromCircle(new Circle3Curve(new Point3D(points[6].X, points[5].Y, points[5].Z), Direction3D.Create(new Vector3D(0, -1, 0)), construction.Closure.SurfaceA.Radius, Direction3D.Create(new Vector3D(1, 0, 0)))));
            else if (pair.Key == (7, 8))
            {
                // The -X retained face encounters this shared remote edge as 8→7.
                // Bind its circle in that same edge direction; otherwise the curve
                // endpoints disagree with topology and CAD consumers may fold the cap.
                var center = new Point3D(points[7].X, points[8].Y, points[7].Z);
                var arc = a == 8 && b == 7
                    ? new Circle3Curve(center, Direction3D.Create(new Vector3D(-1, 0, 0)), construction.Closure.SurfaceB.Radius, Direction3D.Create(new Vector3D(0, 0, 1)))
                    : new Circle3Curve(center, Direction3D.Create(new Vector3D(1, 0, 0)), construction.Closure.SurfaceB.Radius, Direction3D.Create(new Vector3D(0, 1, 0)));
                geometry.AddCurve(cid, CurveGeometry.FromCircle(arc));
            }
            else if (pair.Key == (9, 10)) geometry.AddCurve(cid, CurveGeometry.FromEllipse(construction.Closure.SharedCurve));
            else geometry.AddCurve(cid, CurveGeometry.FromLine(new Line3Curve(points[a], Direction3D.Create(points[b] - points[a]))));
            var trim = pair.Key is (5, 6) or (7, 8) or (9, 10) ? new ParameterInterval(0d, double.Pi / 2d) : new ParameterInterval(0d, (points[b] - points[a]).Length);
            bindings.AddEdgeBinding(new EdgeGeometryBinding(pair.Value, cid, trim));
        }
        var surfaceNumber = 1;
        for (var i = 0; i < faces.Count; i++)
        {
            var sid = new SurfaceGeometryId(surfaceNumber++);
            if (i == 6) geometry.AddSurface(sid, SurfaceGeometry.FromCylinder(construction.Closure.SurfaceA));
            else if (i == 7) geometry.AddSurface(sid, SurfaceGeometry.FromCylinder(construction.Closure.SurfaceB));
            else { var loop = loops[i]; var a = points[loop[0]]; var u = points[loop[1]] - a; Vector3D normal = default; for (var j = 2; j < loop.Count; j++) { normal = u.Cross(points[loop[j]] - a); if (normal.Length > Tol) break; } geometry.AddSurface(sid, SurfaceGeometry.FromPlane(new PlaneSurface(a, Direction3D.Create(normal), Direction3D.Create(u)))); }
            bindings.AddFaceBinding(new FaceGeometryBinding(faces[i], sid));
        }
        var vertexPoints = vertices.Select((v, i) => new KeyValuePair<VertexId, Point3D>(v, points[i])).ToDictionary(); var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints); var check = BrepBindingValidator.Validate(body, true);
        return check.IsSuccess ? new(true, body, ["localized-fillet-junction-plan-emitted"]) : new(false, null, check.Diagnostics.Select(d => d.Message).ToArray());
    }

    private static FaceId AddFace(TopologyBuilder b, IReadOnlyList<Use> uses) { var loop = b.AllocateLoopId(); var ids = new CoedgeId[uses.Count]; for (var i = 0; i < ids.Length; i++) ids[i] = b.AllocateCoedgeId(); for (var i = 0; i < ids.Length; i++) b.AddCoedge(new Coedge(ids[i], uses[i].Id, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse)); b.AddLoop(new Loop(loop, ids)); return b.AddFace([loop]); }
    private readonly record struct Use(EdgeId Id, bool Reverse) { public static Use F(EdgeId id) => new(id, false); public static Use R(EdgeId id) => new(id, true); }
    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;
    private static bool Matches(string face, string target, string expectedFace) => string.Equals(face, expectedFace, StringComparison.Ordinal) && string.Equals(target, "SharedEdgePlusZ", StringComparison.Ordinal);
}
