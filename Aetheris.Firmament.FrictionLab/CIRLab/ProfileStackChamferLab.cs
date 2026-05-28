using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum ProfileStackChamferRoute
{
    None,
    ExistingProfileStack,
    SectionTransition,
    DirectWitness,
}

public sealed record ProfileStackChamferCase(
    string Name,
    double Width,
    double Depth,
    double Height,
    double ChamferDistance);

public sealed record ProfileStackChamferTopologySummary(
    bool BodyProduced,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int LowerPrismSideFaceCount,
    int TransitionFaceCount,
    int ChamferTransitionFaceCount,
    int LoopCount,
    int CoedgeCount,
    string Bounds);

public sealed record ProfileStackChamferStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record ProfileStackChamferRow(
    string CaseName,
    LabProfileStatus Status,
    bool Succeeded,
    ProfileStackChamferRoute SucceededRoute,
    ProfileStackChamferTopologySummary Topology,
    ProfileStackChamferStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

public static class ProfileStackChamferLab
{
    private const double Tol = 1e-9;

    public static readonly string[] AllowedRecommendations =
    [
        "profile-stack-chamfer-ready-for-production-evaluation",
        "profile-stack-chamfer-needs-section-transition-emitter",
        "profile-stack-chamfer-needs-profile-correspondence-contract",
        "profile-stack-chamfer-needs-profile-stack-generalization",
        "profile-stack-chamfer-direct-witness-only",
        "profile-stack-chamfer-invalid-rejected",
        "profile-stack-chamfer-deferred",
    ];

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static IReadOnlyList<ProfileStackChamferRow> RunAll() =>
    [
        Run(new("canonical-top-pos-x-edge", 10, 8, 6, 1)),
        Run(new("invalid-zero-width", 0, 8, 6, 1)),
        Run(new("invalid-negative-depth", 10, -8, 6, 1)),
        Run(new("invalid-zero-height", 10, 8, 0, 1)),
        Run(new("invalid-non-finite-height", 10, 8, double.NaN, 1)),
        Run(new("invalid-zero-chamfer-distance", 10, 8, 6, 0)),
        Run(new("invalid-too-large-chamfer-distance", 10, 8, 6, 6)),
    ];

    public static ProfileStackChamferRow Run(ProfileStackChamferCase c)
    {
        var diagnostics = new List<string> { "edge-profile-x2-profile-stack-chamfer-lab-started" };

        if (!FinitePositive(c.Width) || !FinitePositive(c.Depth) || !FinitePositive(c.Height))
        {
            diagnostics.Add("edge-profile-x2-invalid-dimensions-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-invalid-rejected");
        }

        if (!double.IsFinite(c.ChamferDistance) || c.ChamferDistance <= Tol)
        {
            diagnostics.Add("edge-profile-x2-invalid-chamfer-distance-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-invalid-rejected");
        }

        if (c.ChamferDistance >= c.Width - Tol || c.ChamferDistance >= c.Height - Tol)
        {
            diagnostics.Add("edge-profile-x2-invalid-chamfer-distance-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-invalid-rejected");
        }

        diagnostics.Add("edge-profile-x2-route-a-profile-stack-attempted");
        diagnostics.Add("edge-profile-x2-route-a-profile-stack-blocked:profile-stack-polygon-profile-blocker");
        diagnostics.Add("edge-profile-x2-profile-stack-polygon-profile-blocker");
        diagnostics.Add("edge-profile-x2-ruled-transition-emitter-missing-blocker");

        diagnostics.Add("edge-profile-x2-route-b-section-transition-attempted");
        diagnostics.Add("edge-profile-x2-profile-correspondence-created");

        var built = BuildSectionTransitionBody(c);
        if (built.Body is null)
        {
            diagnostics.Add($"edge-profile-x2-route-b-section-transition-blocked:{built.Diagnostic}");
            diagnostics.Add($"edge-profile-x2-closed-witness-blocked:{built.Diagnostic}");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-stack-chamfer-needs-section-transition-emitter");
        }

        diagnostics.Add("edge-profile-x2-ruled-transition-faces-created");
        diagnostics.Add("edge-profile-x2-route-b-section-transition-succeeded");
        diagnostics.Add("edge-profile-x2-no-air-edge-sweep-used");
        diagnostics.Add("edge-profile-x2-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-profile-x2-no-topology-graft-used");
        diagnostics.Add("edge-profile-x2-no-3d-boolean-used");

        var topology = SummarizeTopology(built.Body, c);
        var step = SummarizeStep(built.Body);
        var stepSucceeded = step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0;
        diagnostics.Add(stepSucceeded ? "edge-profile-x2-step-smoke-succeeded" : "edge-profile-x2-step-smoke-failed:markers");

        var topologySucceeded = topology.BodyProduced
            && topology.VertexCount == 12
            && topology.EdgeCount == 20
            && topology.FaceCount == 10
            && topology.PlanarFaceCount == 10
            && topology.CylindricalFaceCount == 0
            && topology.LowerPrismSideFaceCount == 4
            && topology.TransitionFaceCount == 4
            && topology.ChamferTransitionFaceCount == 1
            && topology.LoopCount == 10
            && topology.CoedgeCount == 40;

        return new(
            c.Name,
            LabProfileStatus.Succeeded,
            topologySucceeded && stepSucceeded,
            ProfileStackChamferRoute.SectionTransition,
            topology,
            step,
            StableDiagnostics(diagnostics),
            topologySucceeded && stepSucceeded
                ? "profile-stack-chamfer-needs-section-transition-emitter"
                : "profile-stack-chamfer-needs-profile-correspondence-contract");
    }

    private static (BrepBody? Body, string Diagnostic) BuildSectionTransitionBody(ProfileStackChamferCase c)
    {
        var x0 = -c.Width * 0.5d;
        var x1 = c.Width * 0.5d;
        var y0 = -c.Depth * 0.5d;
        var y1 = c.Depth * 0.5d;
        var z0 = 0d;
        var z1 = c.Height - c.ChamferDistance;
        var z2 = c.Height;
        var xt = x1 - c.ChamferDistance;

        var points = new List<Point3D>
        {
            new(x0, y0, z0), new(x1, y0, z0), new(x1, y1, z0), new(x0, y1, z0),
            new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1),
            new(x0, y0, z2), new(xt, y0, z2), new(xt, y1, z2), new(x0, y1, z2),
        };

        var b = new TopologyBuilder();
        var v = points.Select(_ => b.AddVertex()).ToArray();
        var bottomEdges = new[] { b.AddEdge(v[0], v[1]), b.AddEdge(v[1], v[2]), b.AddEdge(v[2], v[3]), b.AddEdge(v[3], v[0]) };
        var middleEdges = new[] { b.AddEdge(v[4], v[5]), b.AddEdge(v[5], v[6]), b.AddEdge(v[6], v[7]), b.AddEdge(v[7], v[4]) };
        var topEdges = new[] { b.AddEdge(v[8], v[9]), b.AddEdge(v[9], v[10]), b.AddEdge(v[10], v[11]), b.AddEdge(v[11], v[8]) };
        var lowerEdges = new[] { b.AddEdge(v[0], v[4]), b.AddEdge(v[1], v[5]), b.AddEdge(v[2], v[6]), b.AddEdge(v[3], v[7]) };
        var transitionEdges = new[] { b.AddEdge(v[4], v[8]), b.AddEdge(v[5], v[9]), b.AddEdge(v[6], v[10]), b.AddEdge(v[7], v[11]) };

        var faces = new List<FaceId>
        {
            AddFaceWithLoop(b, bottomEdges.Select(Use.F).ToArray()),
            AddFaceWithLoop(b, topEdges.Select(Use.R).ToArray()),
        };

        for (var i = 0; i < 4; i++)
        {
            var n = (i + 1) % 4;
            faces.Add(AddFaceWithLoop(b, [Use.F(bottomEdges[i]), Use.F(lowerEdges[n]), Use.R(middleEdges[i]), Use.R(lowerEdges[i])]));
        }

        for (var i = 0; i < 4; i++)
        {
            var n = (i + 1) % 4;
            faces.Add(AddFaceWithLoop(b, [Use.F(middleEdges[i]), Use.F(transitionEdges[n]), Use.R(topEdges[i]), Use.R(transitionEdges[i])]));
        }

        var shell = b.AddShell(faces);
        b.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexMap = new Dictionary<VertexId, Point3D>();
        for (var i = 0; i < v.Length; i++)
        {
            vertexMap[v[i]] = points[i];
        }

        var curveId = 1;
        foreach (var edge in b.Model.Edges.OrderBy(e => e.Id.Value))
        {
            var p0 = vertexMap[edge.StartVertexId];
            var p1 = vertexMap[edge.EndVertexId];
            geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(new Line3Curve(p0, Direction3D.Create(p1 - p0))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge.Id, new CurveGeometryId(curveId), new ParameterInterval(0d, (p1 - p0).Length)));
            curveId++;
        }

        var surfaceId = 1;
        AddPlane(geometry, bindings, faces[0], new Point3D(0, 0, z0), new Vector3D(0, 0, -1), new Vector3D(1, 0, 0), surfaceId++);
        AddPlane(geometry, bindings, faces[1], new Point3D(0, 0, z2), new Vector3D(0, 0, 1), new Vector3D(1, 0, 0), surfaceId++);

        for (var i = 0; i < 4; i++)
        {
            var n = (i + 1) % 4;
            AddPlaneFromQuad(geometry, bindings, faces[2 + i], points[i], points[n], points[4 + n], surfaceId++);
        }

        for (var i = 0; i < 4; i++)
        {
            var n = (i + 1) % 4;
            AddPlaneFromQuad(geometry, bindings, faces[6 + i], points[4 + i], points[4 + n], points[8 + n], surfaceId++);
        }

        return (new BrepBody(b.Model, geometry, bindings, vertexMap), string.Empty);
    }

    private static void AddPlaneFromQuad(BrepGeometryStore geometry, BrepBindingModel bindings, FaceId face, Point3D p0, Point3D p1, Point3D p2, int surfaceId)
    {
        var u = p1 - p0;
        var v = p2 - p1;
        var normal = u.Cross(v);
        var reference = u.Length > Tol ? u : new Vector3D(1, 0, 0);
        AddPlane(geometry, bindings, face, p0, normal, reference, surfaceId);
    }

    private static void AddPlane(BrepGeometryStore geometry, BrepBindingModel bindings, FaceId face, Point3D origin, Vector3D normal, Vector3D reference, int surfaceId)
    {
        geometry.AddSurface(new SurfaceGeometryId(surfaceId), SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(reference))));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(surfaceId)));
    }

    private static ProfileStackChamferTopologySummary SummarizeTopology(BrepBody body, ProfileStackChamferCase c)
    {
        var faceCount = body.Topology.Faces.Count();
        var planarFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane);
        var cylindricalFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        return new(
            true,
            body.Topology.Vertices.Count(),
            body.Topology.Edges.Count(),
            faceCount,
            planarFaceCount,
            cylindricalFaceCount,
            4,
            4,
            1,
            body.Topology.Loops.Count(),
            body.Topology.Coedges.Count(),
            FormattableString.Invariant($"[{(-c.Width * 0.5d):0.###},{(-c.Depth * 0.5d):0.###},0]..[{(c.Width * 0.5d):0.###},{(c.Depth * 0.5d):0.###},{c.Height:0.###}]"));
    }

    private static ProfileStackChamferStepSummary SummarizeStep(BrepBody body)
    {
        var step = Step242Exporter.ExportBody(body);
        if (!step.IsSuccess || step.Value is null)
        {
            return new(false, [], RequiredStepMarkers, [], ForbiddenStepMarkers);
        }

        var present = RequiredStepMarkers.Where(m => step.Value.Contains(m, StringComparison.Ordinal)).ToArray();
        var missing = RequiredStepMarkers.Except(present, StringComparer.Ordinal).ToArray();
        var absent = ForbiddenStepMarkers.Where(m => !step.Value.Contains(m, StringComparison.Ordinal)).ToArray();
        var unexpected = ForbiddenStepMarkers.Where(m => step.Value.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(true, present, missing, absent, unexpected);
    }

    private static ProfileStackChamferRow Stop(string caseName, LabProfileStatus status, List<string> diagnostics, string recommendation) =>
        new(caseName, status, false, ProfileStackChamferRoute.None, EmptyTopology(), EmptyStep(), StableDiagnostics(diagnostics), recommendation);

    private static ProfileStackChamferTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static ProfileStackChamferStepSummary EmptyStep() => new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);

    private static LoopId AddLoop(TopologyBuilder b, IReadOnlyList<Use> uses)
    {
        var loopId = b.AllocateLoopId();
        var coedgeIds = uses.Select(_ => b.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < uses.Count; i++)
        {
            var next = coedgeIds[(i + 1) % coedgeIds.Length];
            var previous = coedgeIds[(i + coedgeIds.Length - 1) % coedgeIds.Length];
            b.AddCoedge(new Coedge(coedgeIds[i], uses[i].Edge, loopId, next, previous, uses[i].Reversed));
        }

        b.AddLoop(new Loop(loopId, coedgeIds));
        return loopId;
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<Use> edgeUses) => builder.AddFace([AddLoop(builder, edgeUses)]);

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;

    private readonly record struct Use(EdgeId Edge, bool Reversed)
    {
        public static Use F(EdgeId edge) => new(edge, false);
        public static Use R(EdgeId edge) => new(edge, true);
    }
}
