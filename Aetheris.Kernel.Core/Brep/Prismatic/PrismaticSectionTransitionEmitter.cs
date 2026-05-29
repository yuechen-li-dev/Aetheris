using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Prismatic;

internal enum PrismaticSectionTransitionStatus
{
    Succeeded,
    Rejected,
    Deferred,
    Failed,
}

internal sealed record PrismaticSection(double Z, IReadOnlyList<(double X, double Y)> OuterLoop, bool HasHoles = false, bool HasArcs = false, int OuterLoopCount = 1);

internal sealed record PrismaticCorrespondenceMap(IReadOnlyList<int> VertexMap)
{
    public static PrismaticCorrespondenceMap Identity(int vertexCount) => new(Enumerable.Range(0, vertexCount).ToArray());
}

internal sealed record PrismaticSectionTransitionOptions(bool RunStepSmoke = false, string? TraceLabel = null);

internal sealed record PrismaticSectionTransitionRequest(
    IReadOnlyList<PrismaticSection> Sections,
    PrismaticCorrespondenceMap? Correspondence,
    PrismaticSectionTransitionOptions? Options = null);

internal sealed record PrismaticTransitionTopologySummary(
    bool BodyProduced,
    int SectionCount,
    int VertexCount,
    int EdgeCount,
    int BottomProfileEdgeCount,
    int TopProfileEdgeCount,
    int TransitionEdgeCount,
    int CapFaceCount,
    int TransitionFaceCount,
    int StableIntervalFaceCount,
    int ChangedIntervalFaceCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int LoopCount,
    int CoedgeCount,
    string Bounds);

internal sealed record PrismaticSectionTransitionStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

internal sealed record PrismaticSectionTransitionResult(
    PrismaticSectionTransitionStatus Status,
    BrepBody? Body,
    PrismaticTransitionTopologySummary Topology,
    PrismaticSectionTransitionStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation)
{
    public bool Succeeded => Status == PrismaticSectionTransitionStatus.Succeeded && Body is not null;
}

internal static class PrismaticSectionTransitionEmitter
{
    private const double Tol = 1e-9;

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static PrismaticSectionTransitionResult Emit(PrismaticSectionTransitionRequest request)
    {
        var diagnostics = new List<string> { "edge-prismatic-v1-emitter-started" };
        if (!string.IsNullOrWhiteSpace(request.Options?.TraceLabel))
        {
            diagnostics.Add($"edge-prismatic-v1-trace:{request.Options.TraceLabel}");
        }

        var validation = Validate(request, diagnostics);
        if (validation.Status != PrismaticSectionTransitionStatus.Succeeded)
        {
            return Stop(validation.Status, diagnostics, validation.Recommendation);
        }

        diagnostics.Add("edge-prismatic-v1-request-validated");
        for (var i = 0; i < request.Sections.Count - 1; i++)
        {
            diagnostics.Add("edge-prismatic-v1-transition-interval-created");
        }

        var built = TryBuildBody(request.Sections, request.Correspondence!);
        if (built.Body is null)
        {
            diagnostics.Add($"edge-prismatic-v1-request-rejected:{built.Diagnostic}");
            return Stop(PrismaticSectionTransitionStatus.Failed, diagnostics, "prismatic-section-transition-needs-profile-validation-hardening");
        }

        diagnostics.Add("edge-prismatic-v1-cap-faces-created");
        diagnostics.Add("edge-prismatic-v1-transition-faces-created");
        diagnostics.Add("edge-prismatic-v1-body-created");
        diagnostics.Add("edge-prismatic-v1-no-air-edge-sweep-used");
        diagnostics.Add("edge-prismatic-v1-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-prismatic-v1-no-topology-graft-used");
        diagnostics.Add("edge-prismatic-v1-no-3d-boolean-used");
        diagnostics.Add("edge-prismatic-v1-no-production-route-replacement");

        var topology = SummarizeTopology(built.Body, request.Sections);
        var topologySucceeded = ValidateTopologyFormula(topology, request.Sections);
        if (topologySucceeded)
        {
            diagnostics.Add("edge-prismatic-v1-topology-validated");
        }

        var step = request.Options?.RunStepSmoke == true ? SummarizeStep(built.Body) : EmptyStep();
        var stepSucceeded = request.Options?.RunStepSmoke != true || (step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0);
        if (request.Options?.RunStepSmoke == true)
        {
            diagnostics.Add(stepSucceeded ? "edge-prismatic-v1-step-smoke-succeeded" : "edge-prismatic-v1-step-smoke-failed:markers");
        }

        if (!topologySucceeded)
        {
            return new(
                PrismaticSectionTransitionStatus.Failed,
                built.Body,
                topology,
                step,
                StableDiagnostics(diagnostics),
                "prismatic-section-transition-needs-profile-validation-hardening");
        }

        if (!stepSucceeded)
        {
            return new(
                PrismaticSectionTransitionStatus.Failed,
                built.Body,
                topology,
                step,
                StableDiagnostics(diagnostics),
                "prismatic-section-transition-needs-profile-validation-hardening");
        }

        return new(
            PrismaticSectionTransitionStatus.Succeeded,
            built.Body,
            topology,
            step,
            StableDiagnostics(diagnostics),
            "prismatic-section-transition-ready-for-controlled-route-evaluation");
    }

    private static (PrismaticSectionTransitionStatus Status, string Recommendation) Validate(PrismaticSectionTransitionRequest request, List<string> diagnostics)
    {
        if (request.Sections.Count < 2)
        {
            diagnostics.Add("edge-prismatic-v1-invalid-section-rejected");
            diagnostics.Add("edge-prismatic-v1-request-rejected:fewer-than-two-sections");
            return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-invalid-rejected");
        }

        if (request.Sections.Count > 3)
        {
            diagnostics.Add("edge-prismatic-v1-invalid-section-rejected");
            diagnostics.Add("edge-prismatic-v1-request-deferred:more-than-three-sections");
            return (PrismaticSectionTransitionStatus.Deferred, "prismatic-section-transition-deferred");
        }

        foreach (var section in request.Sections)
        {
            if (section.HasHoles)
            {
                diagnostics.Add("edge-prismatic-v1-holes-deferred");
                diagnostics.Add("edge-prismatic-v1-request-deferred:holes");
                return (PrismaticSectionTransitionStatus.Deferred, "prismatic-section-transition-deferred");
            }

            if (section.HasArcs)
            {
                diagnostics.Add("edge-prismatic-v1-line-arc-deferred");
                diagnostics.Add("edge-prismatic-v1-request-deferred:line-arc-profile");
                return (PrismaticSectionTransitionStatus.Deferred, "prismatic-section-transition-deferred");
            }

            if (section.OuterLoopCount != 1)
            {
                diagnostics.Add("edge-prismatic-v1-multiple-loops-deferred");
                diagnostics.Add("edge-prismatic-v1-request-deferred:multiple-loops");
                return (PrismaticSectionTransitionStatus.Deferred, "prismatic-section-transition-deferred");
            }

            if (!ValidateProfile(section))
            {
                diagnostics.Add("edge-prismatic-v1-invalid-profile-rejected");
                diagnostics.Add("edge-prismatic-v1-request-rejected:invalid-profile");
                return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-invalid-rejected");
            }

            diagnostics.Add("edge-prismatic-v1-section-validated");
        }

        for (var i = 0; i < request.Sections.Count; i++)
        {
            if (!double.IsFinite(request.Sections[i].Z))
            {
                diagnostics.Add("edge-prismatic-v1-invalid-section-rejected");
                diagnostics.Add("edge-prismatic-v1-request-rejected:non-finite-z");
                return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-invalid-rejected");
            }

            if (i > 0 && request.Sections[i].Z <= request.Sections[i - 1].Z + Tol)
            {
                diagnostics.Add("edge-prismatic-v1-non-increasing-sections-rejected");
                diagnostics.Add("edge-prismatic-v1-request-rejected:non-increasing-z");
                return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-invalid-rejected");
            }
        }

        var vertexCount = request.Sections[0].OuterLoop.Count;
        if (request.Sections.Any(s => s.OuterLoop.Count != vertexCount))
        {
            diagnostics.Add("edge-prismatic-v1-mismatched-vertex-count-rejected");
            diagnostics.Add("edge-prismatic-v1-request-rejected:mismatched-vertex-count");
            return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-invalid-rejected");
        }

        var orientation = SignedArea(request.Sections[0].OuterLoop) > 0d;
        if (request.Sections.Skip(1).Any(s => (SignedArea(s.OuterLoop) > 0d) != orientation))
        {
            diagnostics.Add("edge-prismatic-v1-invalid-profile-rejected");
            diagnostics.Add("edge-prismatic-v1-request-rejected:unstable-orientation");
            return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-invalid-rejected");
        }

        if (request.Correspondence is null)
        {
            diagnostics.Add("edge-prismatic-v1-missing-correspondence-rejected");
            diagnostics.Add("edge-prismatic-v1-request-rejected:missing-correspondence");
            return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-invalid-rejected");
        }

        if (request.Correspondence.VertexMap.Count != vertexCount || !request.Correspondence.VertexMap.SequenceEqual(Enumerable.Range(0, vertexCount)))
        {
            diagnostics.Add("edge-prismatic-v1-missing-correspondence-rejected");
            diagnostics.Add("edge-prismatic-v1-request-rejected:non-identity-correspondence");
            return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-needs-correspondence-hardening");
        }

        diagnostics.Add("edge-prismatic-v1-correspondence-validated");

        if (!AllTransitionFacesPlanar(request.Sections))
        {
            diagnostics.Add("edge-prismatic-v1-invalid-profile-rejected");
            diagnostics.Add("edge-prismatic-v1-request-rejected:non-planar-transition-face");
            return (PrismaticSectionTransitionStatus.Rejected, "prismatic-section-transition-needs-profile-validation-hardening");
        }

        return (PrismaticSectionTransitionStatus.Succeeded, string.Empty);
    }

    private static bool ValidateProfile(PrismaticSection section)
    {
        var loop = section.OuterLoop;
        if (loop.Count < 4 || !double.IsFinite(section.Z))
        {
            return false;
        }

        foreach (var point in loop)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            {
                return false;
            }
        }

        for (var i = 0; i < loop.Count; i++)
        {
            var next = (i + 1) % loop.Count;
            if (Distance(loop[i], loop[next]) <= Tol)
            {
                return false;
            }
        }

        if (System.Math.Abs(SignedArea(loop)) <= Tol)
        {
            return false;
        }

        for (var i = 0; i < loop.Count; i++)
        {
            var i2 = (i + 1) % loop.Count;
            for (var j = i + 1; j < loop.Count; j++)
            {
                var j2 = (j + 1) % loop.Count;
                if (i == j || i2 == j || i == j2)
                {
                    continue;
                }

                if (i == 0 && j2 == 0)
                {
                    continue;
                }

                if (SegmentsIntersect(loop[i], loop[i2], loop[j], loop[j2]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ValidateTopologyFormula(PrismaticTransitionTopologySummary topology, IReadOnlyList<PrismaticSection> sections)
    {
        var n = sections[0].OuterLoop.Count;
        var sectionCount = sections.Count;
        var expectedFaces = 2 + ((sectionCount - 1) * n);
        return topology.BodyProduced
            && topology.SectionCount == sectionCount
            && topology.VertexCount == sectionCount * n
            && topology.EdgeCount == (sectionCount * n) + ((sectionCount - 1) * n)
            && topology.TransitionEdgeCount == (sectionCount - 1) * n
            && topology.CapFaceCount == 2
            && topology.TransitionFaceCount == (sectionCount - 1) * n
            && topology.FaceCount == expectedFaces
            && topology.PlanarFaceCount == expectedFaces
            && topology.CylindricalFaceCount == 0
            && topology.LoopCount == expectedFaces
            && topology.CoedgeCount == (2 * n) + (4 * (sectionCount - 1) * n);
    }

    private static (BrepBody? Body, string Diagnostic) TryBuildBody(IReadOnlyList<PrismaticSection> sections, PrismaticCorrespondenceMap correspondence)
    {
        _ = correspondence;
        var n = sections[0].OuterLoop.Count;
        var points = sections
            .SelectMany(s => s.OuterLoop.Select(p => new Point3D(p.X, p.Y, s.Z)))
            .ToArray();

        var b = new TopologyBuilder();
        var vertices = points.Select(_ => b.AddVertex()).ToArray();
        var profileEdges = new EdgeId[sections.Count][];
        for (var s = 0; s < sections.Count; s++)
        {
            profileEdges[s] = new EdgeId[n];
            for (var i = 0; i < n; i++)
            {
                profileEdges[s][i] = b.AddEdge(vertices[(s * n) + i], vertices[(s * n) + ((i + 1) % n)]);
            }
        }

        var transitionEdges = new EdgeId[sections.Count - 1][];
        for (var s = 0; s < sections.Count - 1; s++)
        {
            transitionEdges[s] = new EdgeId[n];
            for (var i = 0; i < n; i++)
            {
                transitionEdges[s][i] = b.AddEdge(vertices[(s * n) + i], vertices[((s + 1) * n) + i]);
            }
        }

        var faces = new List<FaceId>
        {
            AddFaceWithLoop(b, profileEdges[0].Select(Use.F).ToArray()),
            AddFaceWithLoop(b, profileEdges[^1].Select(Use.R).ToArray()),
        };

        for (var s = 0; s < sections.Count - 1; s++)
        {
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                faces.Add(AddFaceWithLoop(b, [Use.F(profileEdges[s][i]), Use.F(transitionEdges[s][next]), Use.R(profileEdges[s + 1][i]), Use.R(transitionEdges[s][i])]));
            }
        }

        var shell = b.AddShell(faces);
        b.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexMap = new Dictionary<VertexId, Point3D>();
        for (var i = 0; i < vertices.Length; i++)
        {
            vertexMap[vertices[i]] = points[i];
        }

        var curveId = 1;
        foreach (var edge in b.Model.Edges.OrderBy(e => e.Id.Value))
        {
            var p0 = vertexMap[edge.StartVertexId];
            var p1 = vertexMap[edge.EndVertexId];
            var vector = p1 - p0;
            if (vector.Length <= Tol)
            {
                return (null, "zero-length-edge");
            }

            geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(new Line3Curve(p0, Direction3D.Create(vector))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge.Id, new CurveGeometryId(curveId), new ParameterInterval(0d, vector.Length)));
            curveId++;
        }

        var surfaceId = 1;
        AddPlane(geometry, bindings, faces[0], new Point3D(0, 0, sections[0].Z), new Vector3D(0, 0, -1), new Vector3D(1, 0, 0), surfaceId++);
        AddPlane(geometry, bindings, faces[1], new Point3D(0, 0, sections[^1].Z), new Vector3D(0, 0, 1), new Vector3D(1, 0, 0), surfaceId++);

        var faceIndex = 2;
        for (var s = 0; s < sections.Count - 1; s++)
        {
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                AddPlaneFromQuad(geometry, bindings, faces[faceIndex++], points[(s * n) + i], points[(s * n) + next], points[((s + 1) * n) + next], surfaceId++);
            }
        }

        return (new BrepBody(b.Model, geometry, bindings, vertexMap), string.Empty);
    }

    private static PrismaticTransitionTopologySummary SummarizeTopology(BrepBody body, IReadOnlyList<PrismaticSection> sections)
    {
        var faceCount = body.Topology.Faces.Count();
        var planarFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane);
        var cylindricalFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        var n = sections[0].OuterLoop.Count;
        var stableFaces = 0;
        var changedFaces = 0;
        for (var s = 0; s < sections.Count - 1; s++)
        {
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                if (SamePoint(sections[s].OuterLoop[i], sections[s + 1].OuterLoop[i]) && SamePoint(sections[s].OuterLoop[next], sections[s + 1].OuterLoop[next]))
                {
                    stableFaces++;
                }
                else
                {
                    changedFaces++;
                }
            }
        }

        var allX = sections.SelectMany(s => s.OuterLoop.Select(p => p.X)).ToArray();
        var allY = sections.SelectMany(s => s.OuterLoop.Select(p => p.Y)).ToArray();
        return new(
            true,
            sections.Count,
            body.Topology.Vertices.Count(),
            body.Topology.Edges.Count(),
            n,
            n,
            (sections.Count - 1) * n,
            2,
            (sections.Count - 1) * n,
            stableFaces,
            changedFaces,
            faceCount,
            planarFaceCount,
            cylindricalFaceCount,
            body.Topology.Loops.Count(),
            body.Topology.Coedges.Count(),
            FormattableString.Invariant($"[{allX.Min():0.###},{allY.Min():0.###},{sections.Min(s => s.Z):0.###}]..[{allX.Max():0.###},{allY.Max():0.###},{sections.Max(s => s.Z):0.###}]"));
    }

    private static bool AllTransitionFacesPlanar(IReadOnlyList<PrismaticSection> sections)
    {
        var n = sections[0].OuterLoop.Count;
        for (var s = 0; s < sections.Count - 1; s++)
        {
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                var p0 = new Point3D(sections[s].OuterLoop[i].X, sections[s].OuterLoop[i].Y, sections[s].Z);
                var p1 = new Point3D(sections[s].OuterLoop[next].X, sections[s].OuterLoop[next].Y, sections[s].Z);
                var p2 = new Point3D(sections[s + 1].OuterLoop[next].X, sections[s + 1].OuterLoop[next].Y, sections[s + 1].Z);
                var p3 = new Point3D(sections[s + 1].OuterLoop[i].X, sections[s + 1].OuterLoop[i].Y, sections[s + 1].Z);
                var normal = (p1 - p0).Cross(p2 - p0);
                if (normal.Length <= Tol)
                {
                    return false;
                }

                if (System.Math.Abs((p3 - p0).Dot(normal / normal.Length)) > Tol)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static PrismaticSectionTransitionStepSummary SummarizeStep(BrepBody body)
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

    private static PrismaticSectionTransitionResult Stop(PrismaticSectionTransitionStatus status, List<string> diagnostics, string recommendation) =>
        new(status, null, EmptyTopology(), EmptyStep(), StableDiagnostics(diagnostics), recommendation);

    private static PrismaticTransitionTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static PrismaticSectionTransitionStepSummary EmptyStep() => new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static double SignedArea(IReadOnlyList<(double X, double Y)> loop)
    {
        var area = 0d;
        for (var i = 0; i < loop.Count; i++)
        {
            var next = (i + 1) % loop.Count;
            area += (loop[i].X * loop[next].Y) - (loop[next].X * loop[i].Y);
        }

        return area * 0.5d;
    }

    private static bool SegmentsIntersect((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        static double Cross((double X, double Y) p, (double X, double Y) q, (double X, double Y) r) => ((q.X - p.X) * (r.Y - p.Y)) - ((q.Y - p.Y) * (r.X - p.X));
        var c1 = Cross(a, b, c);
        var c2 = Cross(a, b, d);
        var c3 = Cross(c, d, a);
        var c4 = Cross(c, d, b);
        return (c1 * c2 < -Tol) && (c3 * c4 < -Tol);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) => System.Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static bool SamePoint((double X, double Y) a, (double X, double Y) b) => Distance(a, b) <= Tol;

    private static void AddPlaneFromQuad(BrepGeometryStore geometry, BrepBindingModel bindings, FaceId face, Point3D p0, Point3D p1, Point3D p2, int surfaceId)
    {
        var u = p1 - p0;
        var v = p2 - p1;
        var normal = u.Cross(v);
        if (normal.Length <= Tol)
        {
            normal = new Vector3D(0, 0, 1);
        }

        var reference = u.Length > Tol ? u : new Vector3D(1, 0, 0);
        AddPlane(geometry, bindings, face, p0, normal, reference, surfaceId);
    }

    private static void AddPlane(BrepGeometryStore geometry, BrepBindingModel bindings, FaceId face, Point3D origin, Vector3D normal, Vector3D reference, int surfaceId)
    {
        geometry.AddSurface(new SurfaceGeometryId(surfaceId), SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(reference))));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(surfaceId)));
    }

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

    private readonly record struct Use(EdgeId Edge, bool Reversed)
    {
        public static Use F(EdgeId edge) => new(edge, false);
        public static Use R(EdgeId edge) => new(edge, true);
    }
}
