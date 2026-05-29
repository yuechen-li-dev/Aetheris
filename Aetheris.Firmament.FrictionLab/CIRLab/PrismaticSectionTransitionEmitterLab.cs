using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record PrismaticSection(double Z, IReadOnlyList<(double X, double Y)> OuterLoop, bool HasHoles = false, bool HasArcs = false, int OuterLoopCount = 1);

public sealed record PrismaticCorrespondenceMap(IReadOnlyList<int> VertexMap)
{
    public static PrismaticCorrespondenceMap Identity(int vertexCount) => new(Enumerable.Range(0, vertexCount).ToArray());
}

public sealed record PrismaticSectionTransitionCase(
    string Name,
    IReadOnlyList<PrismaticSection> Sections,
    PrismaticCorrespondenceMap? Correspondence);

public sealed record PrismaticTransitionTopologySummary(
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

public sealed record PrismaticSectionTransitionStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record PrismaticSectionTransitionRow(
    string CaseName,
    LabProfileStatus Status,
    bool Succeeded,
    PrismaticTransitionTopologySummary Topology,
    PrismaticSectionTransitionStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

public static class PrismaticSectionTransitionEmitterLab
{
    private const double Tol = 1e-9;

    public static readonly string[] AllowedRecommendations =
    [
        "prismatic-section-transition-ready-for-production-evaluation",
        "prismatic-section-transition-needs-profile-validation-hardening",
        "prismatic-section-transition-needs-correspondence-hardening",
        "prismatic-section-transition-invalid-rejected",
        "prismatic-section-transition-deferred",
    ];

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static IReadOnlyList<PrismaticSectionTransitionRow> RunAll() =>
    [
        Run(RectangleToInsetRectangle()),
        Run(ThreeSectionStableThenInsetRectangle()),
        Run(ScaledPentagon()),
        Run(new("invalid-non-increasing-z", [RectangleSection(0, 10, 8), RectangleSection(0, 8, 6)], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("invalid-mismatched-vertex-count", [RectangleSection(0, 10, 8), RegularPolygonSection(1, 5, 5)], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("invalid-missing-correspondence", [RectangleSection(0, 10, 8), RectangleSection(1, 8, 6)], null)),
        Run(new("invalid-self-intersecting-profile", [new PrismaticSection(0, [(0, 0), (2, 2), (0, 2), (2, 0)]), RectangleSection(1, 8, 6)], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("deferred-holes", [RectangleSection(0, 10, 8) with { HasHoles = true }, RectangleSection(1, 8, 6) with { HasHoles = true }], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("deferred-line-arc", [RectangleSection(0, 10, 8) with { HasArcs = true }, RectangleSection(1, 8, 6) with { HasArcs = true }], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("deferred-multiple-loops", [RectangleSection(0, 10, 8) with { OuterLoopCount = 2 }, RectangleSection(1, 8, 6) with { OuterLoopCount = 2 }], PrismaticCorrespondenceMap.Identity(4))),
    ];

    public static PrismaticSectionTransitionCase RectangleToInsetRectangle() =>
        new("rectangle-to-inset-rectangle", [RectangleSection(0, 10, 8), RectangleSection(1, 8, 6)], PrismaticCorrespondenceMap.Identity(4));

    public static PrismaticSectionTransitionCase ThreeSectionStableThenInsetRectangle() =>
        new("three-section-stable-plus-transition", [RectangleSection(0, 10, 8), RectangleSection(5, 10, 8), RectangleSection(6, 8, 6)], PrismaticCorrespondenceMap.Identity(4));

    public static PrismaticSectionTransitionCase ScaledPentagon() =>
        new("scaled-pentagon", [RegularPolygonSection(0, 5, 5), RegularPolygonSection(2, 4, 5)], PrismaticCorrespondenceMap.Identity(5));

    public static PrismaticSectionTransitionRow Run(PrismaticSectionTransitionCase c)
    {
        var diagnostics = new List<string> { "edge-prismatic-x1-lab-started" };
        var validation = Validate(c, diagnostics);
        if (validation.Status != LabProfileStatus.Succeeded)
        {
            return Stop(c.Name, validation.Status, diagnostics, validation.Recommendation);
        }

        diagnostics.Add("edge-prismatic-x1-correspondence-created");
        for (var i = 0; i < c.Sections.Count - 1; i++)
        {
            diagnostics.Add("edge-prismatic-x1-transition-interval-created");
        }

        var built = PrismaticSectionTransitionEmitter.TryEmit(c.Sections, c.Correspondence!);
        if (built.Body is null)
        {
            diagnostics.Add($"edge-prismatic-x1-transition-face-emission-failed:{built.Diagnostic}");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "prismatic-section-transition-needs-profile-validation-hardening");
        }

        diagnostics.Add("edge-prismatic-x1-cap-faces-created");
        diagnostics.Add("edge-prismatic-x1-transition-faces-created");
        diagnostics.Add("edge-prismatic-x1-body-created");
        diagnostics.Add("edge-prismatic-x1-no-air-edge-sweep-used");
        diagnostics.Add("edge-prismatic-x1-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-prismatic-x1-no-topology-graft-used");
        diagnostics.Add("edge-prismatic-x1-no-3d-boolean-used");

        var topology = SummarizeTopology(built.Body, c.Sections);
        var step = SummarizeStep(built.Body);
        var stepSucceeded = step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0;
        diagnostics.Add(stepSucceeded ? "edge-prismatic-x1-step-smoke-succeeded" : "edge-prismatic-x1-step-smoke-failed:markers");

        var n = c.Sections[0].OuterLoop.Count;
        var sectionCount = c.Sections.Count;
        var expectedFaces = 2 + ((sectionCount - 1) * n);
        var topologySucceeded = topology.BodyProduced
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

        return new(
            c.Name,
            LabProfileStatus.Succeeded,
            topologySucceeded && stepSucceeded,
            topology,
            step,
            StableDiagnostics(diagnostics),
            topologySucceeded && stepSucceeded
                ? "prismatic-section-transition-ready-for-production-evaluation"
                : "prismatic-section-transition-needs-profile-validation-hardening");
    }

    private static (LabProfileStatus Status, string Recommendation) Validate(PrismaticSectionTransitionCase c, List<string> diagnostics)
    {
        if (c.Sections.Count is < 2 or > 3)
        {
            diagnostics.Add("edge-prismatic-x1-invalid-section-rejected");
            return (LabProfileStatus.Failed, "prismatic-section-transition-invalid-rejected");
        }

        foreach (var section in c.Sections)
        {
            if (section.HasHoles)
            {
                diagnostics.Add("edge-prismatic-x1-holes-deferred");
                return (LabProfileStatus.Deferred, "prismatic-section-transition-deferred");
            }

            if (section.HasArcs)
            {
                diagnostics.Add("edge-prismatic-x1-line-arc-deferred");
                return (LabProfileStatus.Deferred, "prismatic-section-transition-deferred");
            }

            if (section.OuterLoopCount != 1)
            {
                diagnostics.Add("edge-prismatic-x1-multiple-loops-deferred");
                return (LabProfileStatus.Deferred, "prismatic-section-transition-deferred");
            }

            if (!ValidateProfile(section, out var profileDiagnostic))
            {
                diagnostics.Add(profileDiagnostic);
                return (LabProfileStatus.Failed, "prismatic-section-transition-invalid-rejected");
            }

            diagnostics.Add("edge-prismatic-x1-section-validated");
        }

        for (var i = 0; i < c.Sections.Count; i++)
        {
            if (!double.IsFinite(c.Sections[i].Z))
            {
                diagnostics.Add("edge-prismatic-x1-invalid-section-rejected");
                return (LabProfileStatus.Failed, "prismatic-section-transition-invalid-rejected");
            }

            if (i > 0 && c.Sections[i].Z <= c.Sections[i - 1].Z + Tol)
            {
                diagnostics.Add("edge-prismatic-x1-non-increasing-sections-rejected");
                return (LabProfileStatus.Failed, "prismatic-section-transition-invalid-rejected");
            }
        }

        var vertexCount = c.Sections[0].OuterLoop.Count;
        if (c.Sections.Any(s => s.OuterLoop.Count != vertexCount))
        {
            diagnostics.Add("edge-prismatic-x1-mismatched-vertex-count-rejected");
            return (LabProfileStatus.Failed, "prismatic-section-transition-invalid-rejected");
        }

        var orientation = SignedArea(c.Sections[0].OuterLoop) > 0d;
        if (c.Sections.Skip(1).Any(s => (SignedArea(s.OuterLoop) > 0d) != orientation))
        {
            diagnostics.Add("edge-prismatic-x1-invalid-profile-rejected");
            return (LabProfileStatus.Failed, "prismatic-section-transition-invalid-rejected");
        }

        if (c.Correspondence is null || c.Correspondence.VertexMap.Count != vertexCount || !c.Correspondence.VertexMap.SequenceEqual(Enumerable.Range(0, vertexCount)))
        {
            diagnostics.Add("edge-prismatic-x1-missing-correspondence-rejected");
            return (LabProfileStatus.Failed, c.Correspondence is null ? "prismatic-section-transition-invalid-rejected" : "prismatic-section-transition-needs-correspondence-hardening");
        }

        if (!AllTransitionFacesPlanar(c.Sections))
        {
            diagnostics.Add("edge-prismatic-x1-invalid-profile-rejected");
            return (LabProfileStatus.Failed, "prismatic-section-transition-needs-profile-validation-hardening");
        }

        return (LabProfileStatus.Succeeded, "");
    }

    private static bool ValidateProfile(PrismaticSection section, out string diagnostic)
    {
        diagnostic = "edge-prismatic-x1-invalid-profile-rejected";
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

        if (Math.Abs(SignedArea(loop)) <= Tol)
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

                if (Math.Abs((p3 - p0).Dot(normal / normal.Length)) > Tol)
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

    private static PrismaticSectionTransitionRow Stop(string caseName, LabProfileStatus status, List<string> diagnostics, string recommendation) =>
        new(caseName, status, false, EmptyTopology(), EmptyStep(), StableDiagnostics(diagnostics), recommendation);

    private static PrismaticTransitionTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "none");

    private static PrismaticSectionTransitionStepSummary EmptyStep() => new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);

    private static PrismaticSection RectangleSection(double z, double width, double depth)
    {
        var x = width * 0.5d;
        var y = depth * 0.5d;
        return new(z, [(-x, -y), (x, -y), (x, y), (-x, y)]);
    }

    private static PrismaticSection RegularPolygonSection(double z, double radius, int vertices)
    {
        var points = Enumerable.Range(0, vertices)
            .Select(i =>
            {
                var a = ((Math.PI * 2d) * i / vertices) - (Math.PI * 0.5d);
                return (X: Math.Cos(a) * radius, Y: Math.Sin(a) * radius);
            })
            .ToArray();
        return new(z, points);
    }

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

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static bool SamePoint((double X, double Y) a, (double X, double Y) b) => Distance(a, b) <= Tol;
}

public static class PrismaticSectionTransitionEmitter
{
    private const double Tol = 1e-9;

    public static (BrepBody? Body, string Diagnostic) TryEmit(IReadOnlyList<PrismaticSection> sections, PrismaticCorrespondenceMap correspondence)
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
