using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.EdgeFinishing;

/// <summary>
/// M5a bounded functional chamfer for convex planar-planar external edges on axis-aligned box-like bodies.
/// This intentionally supports only a single explicit vertical box edge token with one uniform distance.
/// </summary>
public static class BrepBoundedChamfer
{
    private const string PlanarCornerCutCandidate = "planar_corner_cut";
    private const string RejectCandidate = "reject";

    public static KernelResult<BrepBody> ChamferAxisAlignedBoxSingleCorner(
        AxisAlignedBoxExtents box,
        BrepBoundedChamferCorner corner,
        double distance)
    {
        var contextResult = BrepBoundedChamferCornerContext.TryCreate(box, corner, distance);
        if (!contextResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(contextResult.Diagnostics);
        }

        var context = contextResult.Value;
        var engine = new JudgmentEngine<BrepBoundedChamferCornerContext>();
        var candidates = BuildCornerCandidates();
        var judgment = engine.Evaluate(context, candidates);
        if (!judgment.IsSuccess || !judgment.Selection.HasValue)
        {
            var reason = judgment.Rejections.Count == 0
                ? "No bounded corner-resolution candidate was admissible."
                : string.Join(" ", judgment.Rejections.Select(rejection => $"{rejection.CandidateName}: {rejection.Reason}."));
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer corner resolution rejected: {reason}", "firmament.chamfer-bounded")]);
        }

        var selected = judgment.Selection.Value.Candidate.Name;
        if (selected == RejectCandidate)
        {
            var reason = judgment.Rejections.Count == 0
                ? "No bounded corner-resolution candidate was admissible."
                : string.Join(" ", judgment.Rejections.Select(rejection => $"{rejection.CandidateName}: {rejection.Reason}."));
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer corner resolution rejected: {reason}", "firmament.chamfer-bounded")]);
        }

        return selected switch
        {
            PlanarCornerCutCandidate => CreateSingleCornerPlanarChamferBody(context),
            _ => KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer corner resolution selected unsupported candidate '{selected}'.", "firmament.chamfer-bounded")])
        };
    }

    public static KernelResult<BrepBody> ChamferTrustedPolyhedralSingleCorner(
        BrepBody sourceBody,
        BrepBoundedChamferCorner corner,
        double distance)
    {
        var contextResult = BrepBoundedChamferCornerContext.TryCreateFromTrustedBody(sourceBody, corner, distance);
        if (!contextResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(contextResult.Diagnostics);
        }

        var context = contextResult.Value;
        var engine = new JudgmentEngine<BrepBoundedChamferCornerContext>();
        var candidates = BuildCornerCandidates();
        var judgment = engine.Evaluate(context, candidates);
        if (!judgment.IsSuccess || !judgment.Selection.HasValue || judgment.Selection.Value.Candidate.Name == RejectCandidate)
        {
            var reason = judgment.Rejections.Count == 0
                ? "No bounded corner-resolution candidate was admissible."
                : string.Join(" ", judgment.Rejections.Select(rejection => $"{rejection.CandidateName}: {rejection.Reason}."));
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer corner resolution rejected: {reason}", "firmament.chamfer-bounded")]);
        }

        if (!context.IsTrustedOrthogonalBody || !context.HasOrthogonalGeometryConstructor)
        {
            return KernelResult<BrepBody>.Failure([Failure(
                "Bounded chamfer corner resolution reached non-orthogonal tri-planar corner policy, but geometry construction is currently bounded to orthogonal box-like corners only.",
                "firmament.chamfer-bounded")]);
        }

        return CreateSingleCornerPlanarChamferBody(context);
    }

    public static KernelResult<BrepBody> ChamferAxisAlignedBoxVerticalEdge(
        AxisAlignedBoxExtents box,
        BrepBoundedChamferEdge edge,
        double distance)
    {
        if (!double.IsFinite(distance) || distance <= 0d)
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer distance must be finite and greater than 0.", "firmament.chamfer-bounded")]);
        }

        var sizeX = box.MaxX - box.MinX;
        var sizeY = box.MaxY - box.MinY;
        var sizeZ = box.MaxZ - box.MinZ;
        if (sizeX <= 0d || sizeY <= 0d || sizeZ <= 0d)
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer requires a source box with strictly positive extents.", "firmament.chamfer-bounded")]);
        }

        var maxDistance = System.Math.Min(sizeX, sizeY);
        if (distance >= maxDistance)
        {
            return KernelResult<BrepBody>.Failure(
            [
                Failure(
                    "Bounded chamfer distance is too large for the selected edge; it must be strictly less than the local adjacent face extents to preserve manifoldness.",
                    "firmament.chamfer-bounded")
            ]);
        }

        var profile = BuildProfile(box, edge, distance);
        var frame = new ExtrudeFrame3D(
            origin: new Point3D(0d, 0d, box.MinZ),
            normal: Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            uAxis: Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        return BrepExtrude.Create(profile, frame, sizeZ);
    }

    private static PolylineProfile2D BuildProfile(AxisAlignedBoxExtents box, BrepBoundedChamferEdge edge, double d)
        => CreateProfile(edge switch
        {
            BrepBoundedChamferEdge.XMaxYMax =>
            [
                new ProfilePoint2D(box.MinX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MaxY - d),
                new ProfilePoint2D(box.MaxX - d, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY)
            ],
            BrepBoundedChamferEdge.XMaxYMin =>
            [
                new ProfilePoint2D(box.MinX, box.MinY),
                new ProfilePoint2D(box.MaxX - d, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY + d),
                new ProfilePoint2D(box.MaxX, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY)
            ],
            BrepBoundedChamferEdge.XMinYMax =>
            [
                new ProfilePoint2D(box.MinX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MaxY),
                new ProfilePoint2D(box.MinX + d, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY - d)
            ],
            BrepBoundedChamferEdge.XMinYMin =>
            [
                new ProfilePoint2D(box.MinX, box.MinY + d),
                new ProfilePoint2D(box.MinX + d, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MinY),
                new ProfilePoint2D(box.MaxX, box.MaxY),
                new ProfilePoint2D(box.MinX, box.MaxY)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null)
        });

    private static PolylineProfile2D CreateProfile(IReadOnlyList<ProfilePoint2D> vertices)
    {
        var profileResult = PolylineProfile2D.Create(vertices);
        if (!profileResult.IsSuccess)
        {
            throw new InvalidOperationException("Bounded chamfer profile generation produced an invalid profile.");
        }

        return profileResult.Value;
    }

    private static KernelDiagnostic Failure(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, Source: source);

    private static IReadOnlyList<JudgmentCandidate<BrepBoundedChamferCornerContext>> BuildCornerCandidates()
    {
        var planarCornerGuard = When.All<BrepBoundedChamferCornerContext>(
            context => context.ParticipatingEdgeCount == 3,
            context => context.IsConvexCorner,
            context => context.AllFacesPlanar,
            context => context.AreChamferDistancesEqual,
            context => context.IsDistanceWithinLocalBounds,
            context => context.HasCoherentBoundedPlanarCut,
            context => context.PreservesManifoldTopology,
            context => context.Corner == BrepBoundedChamferCorner.XMaxYMaxZMax);
        return
        [
            new JudgmentCandidate<BrepBoundedChamferCornerContext>(
                Name: PlanarCornerCutCandidate,
                IsAdmissible: planarCornerGuard,
                Score: context => context.AllAdjacentFacesOrthogonal ? 200d : 150d,
                RejectionReason: context => $"requires a convex planar tri-corner with coherent bounded planar cut and manifold safety (orthogonal={context.AllAdjacentFacesOrthogonal}, acuteAngles={context.AcuteFaceAngleCount}, obtuseAngles={context.ObtuseFaceAngleCount})",
                TieBreakerPriority: 0),
            new JudgmentCandidate<BrepBoundedChamferCornerContext>(
                Name: RejectCandidate,
                IsAdmissible: _ => true,
                Score: _ => 0d,
                RejectionReason: _ => "fallback reject candidate",
                TieBreakerPriority: 99)
        ];
    }

    private static KernelResult<BrepBody> CreateSingleCornerPlanarChamferBody(BrepBoundedChamferCornerContext context)
    {
        var box = context.Box;
        var d = context.Distance;
        var builder = new TopologyBuilder();

        var v000 = builder.AddVertex();
        var v100 = builder.AddVertex();
        var v110 = builder.AddVertex();
        var v010 = builder.AddVertex();
        var v001 = builder.AddVertex();
        var v101 = builder.AddVertex();
        var v011 = builder.AddVertex();
        var vX = builder.AddVertex();
        var vY = builder.AddVertex();
        var vZ = builder.AddVertex();

        var p000 = new Point3D(box.MinX, box.MinY, box.MinZ);
        var p100 = new Point3D(box.MaxX, box.MinY, box.MinZ);
        var p110 = new Point3D(box.MaxX, box.MaxY, box.MinZ);
        var p010 = new Point3D(box.MinX, box.MaxY, box.MinZ);
        var p001 = new Point3D(box.MinX, box.MinY, box.MaxZ);
        var p101 = new Point3D(box.MaxX, box.MinY, box.MaxZ);
        var p011 = new Point3D(box.MinX, box.MaxY, box.MaxZ);
        var pX = new Point3D(box.MaxX - d, box.MaxY, box.MaxZ);
        var pY = new Point3D(box.MaxX, box.MaxY - d, box.MaxZ);
        var pZ = new Point3D(box.MaxX, box.MaxY, box.MaxZ - d);

        var e000100 = builder.AddEdge(v000, v100);
        var e100110 = builder.AddEdge(v100, v110);
        var e110010 = builder.AddEdge(v110, v010);
        var e010000 = builder.AddEdge(v010, v000);
        var e000001 = builder.AddEdge(v000, v001);
        var e100101 = builder.AddEdge(v100, v101);
        var e010011 = builder.AddEdge(v010, v011);
        var e001101 = builder.AddEdge(v001, v101);
        var e101vY = builder.AddEdge(v101, vY);
        var eYvX = builder.AddEdge(vY, vX);
        var evX011 = builder.AddEdge(vX, v011);
        var e011001 = builder.AddEdge(v011, v001);
        var e110vZ = builder.AddEdge(v110, vZ);
        var evZvY = builder.AddEdge(vZ, vY);
        var evXvZ = builder.AddEdge(vX, vZ);

        var faces = new List<FaceId>(7)
        {
            AddFaceWithLoop(builder, [EdgeUse.Forward(e000100), EdgeUse.Forward(e100110), EdgeUse.Forward(e110010), EdgeUse.Forward(e010000)]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(e001101), EdgeUse.Forward(e101vY), EdgeUse.Forward(eYvX), EdgeUse.Forward(evX011), EdgeUse.Forward(e011001)]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(e000001), EdgeUse.Reversed(e011001), EdgeUse.Reversed(e010011), EdgeUse.Forward(e010000)]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(e000100), EdgeUse.Forward(e100101), EdgeUse.Reversed(e001101), EdgeUse.Reversed(e000001)]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(e100110), EdgeUse.Forward(e110vZ), EdgeUse.Forward(evZvY), EdgeUse.Reversed(e101vY), EdgeUse.Reversed(e100101)]),
            AddFaceWithLoop(builder, [EdgeUse.Forward(e110010), EdgeUse.Forward(e010011), EdgeUse.Reversed(evX011), EdgeUse.Forward(evXvZ), EdgeUse.Reversed(e110vZ)]),
            AddFaceWithLoop(builder, [EdgeUse.Reversed(eYvX), EdgeUse.Forward(evZvY), EdgeUse.Reversed(evXvZ)])
        };

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var curveId = 1;
        void AddEdgeGeometry(EdgeId edgeId, Point3D start, Point3D end)
        {
            var direction = end - start;
            var cid = new CurveGeometryId(curveId++);
            geometry.AddCurve(cid, CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(direction))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, cid, new ParameterInterval(0d, direction.Length)));
        }

        AddEdgeGeometry(e000100, p000, p100);
        AddEdgeGeometry(e100110, p100, p110);
        AddEdgeGeometry(e110010, p110, p010);
        AddEdgeGeometry(e010000, p010, p000);
        AddEdgeGeometry(e000001, p000, p001);
        AddEdgeGeometry(e100101, p100, p101);
        AddEdgeGeometry(e010011, p010, p011);
        AddEdgeGeometry(e001101, p001, p101);
        AddEdgeGeometry(e101vY, p101, pY);
        AddEdgeGeometry(eYvX, pY, pX);
        AddEdgeGeometry(evX011, pX, p011);
        AddEdgeGeometry(e011001, p011, p001);
        AddEdgeGeometry(e110vZ, p110, pZ);
        AddEdgeGeometry(evZvY, pZ, pY);
        AddEdgeGeometry(evXvZ, pX, pZ);

        var surfaceId = 1;
        void AddFacePlane(FaceId face, Point3D origin, Vector3D normal, Vector3D uAxis)
        {
            var sid = new SurfaceGeometryId(surfaceId++);
            geometry.AddSurface(sid, SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(uAxis))));
            bindings.AddFaceBinding(new FaceGeometryBinding(face, sid));
        }

        AddFacePlane(faces[0], p000, new Vector3D(0d, 0d, -1d), new Vector3D(1d, 0d, 0d)); // z_min
        AddFacePlane(faces[1], p001, new Vector3D(0d, 0d, 1d), new Vector3D(1d, 0d, 0d)); // z_max clipped
        AddFacePlane(faces[2], p000, new Vector3D(-1d, 0d, 0d), new Vector3D(0d, 1d, 0d)); // x_min
        AddFacePlane(faces[3], p000, new Vector3D(0d, -1d, 0d), new Vector3D(1d, 0d, 0d)); // y_min
        AddFacePlane(faces[4], p100, new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d)); // x_max clipped
        AddFacePlane(faces[5], p010, new Vector3D(0d, 1d, 0d), new Vector3D(-1d, 0d, 0d)); // y_max clipped
        AddFacePlane(faces[6], pX, new Vector3D(1d, 1d, 1d), new Vector3D(1d, -1d, 0d)); // chamfer plane

        var vertexPoints = new Dictionary<VertexId, Point3D>
        {
            [v000] = p000,
            [v100] = p100,
            [v110] = p110,
            [v010] = p010,
            [v001] = p001,
            [v101] = p101,
            [v011] = p011,
            [vX] = pX,
            [vY] = pY,
            [vZ] = pZ
        };

        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[edgeUses.Count];
        for (var i = 0; i < edgeUses.Count; i++)
        {
            coedgeIds[i] = builder.AllocateCoedgeId();
        }

        for (var i = 0; i < edgeUses.Count; i++)
        {
            var next = coedgeIds[(i + 1) % edgeUses.Count];
            var prev = coedgeIds[(i + edgeUses.Count - 1) % edgeUses.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edgeUses[i].EdgeId, loopId, next, prev, edgeUses[i].IsReversed));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return builder.AddFace([loopId]);
    }

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, false);
        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, true);
    }
}

public readonly record struct BrepBoundedChamferCornerContext(
    AxisAlignedBoxExtents Box,
    BrepBoundedChamferCorner Corner,
    double Distance,
    int ParticipatingEdgeCount,
    bool IsConvexCorner,
    bool AllFacesPlanar,
    bool AllAdjacentFacesOrthogonal,
    int AcuteFaceAngleCount,
    int ObtuseFaceAngleCount,
    bool AreChamferDistancesEqual,
    bool IsDistanceWithinLocalBounds,
    bool HasCoherentBoundedPlanarCut,
    bool PreservesManifoldTopology,
    bool IsTrustedOrthogonalBody,
    bool HasOrthogonalGeometryConstructor)
{
    public static KernelResult<BrepBoundedChamferCornerContext> TryCreate(
        AxisAlignedBoxExtents box,
        BrepBoundedChamferCorner corner,
        double distance)
    {
        if (!double.IsFinite(distance) || distance <= 0d)
        {
            return KernelResult<BrepBoundedChamferCornerContext>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Bounded chamfer corner distance must be finite and greater than 0.", Source: "firmament.chamfer-bounded")]);
        }

        var sizeX = box.MaxX - box.MinX;
        var sizeY = box.MaxY - box.MinY;
        var sizeZ = box.MaxZ - box.MinZ;
        if (sizeX <= 0d || sizeY <= 0d || sizeZ <= 0d)
        {
            return KernelResult<BrepBoundedChamferCornerContext>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Bounded chamfer corner requires a source box with strictly positive extents.", Source: "firmament.chamfer-bounded")]);
        }

        var withinBounds = distance < System.Math.Min(sizeX, System.Math.Min(sizeY, sizeZ));
        return KernelResult<BrepBoundedChamferCornerContext>.Success(new BrepBoundedChamferCornerContext(
            box,
            corner,
            distance,
            ParticipatingEdgeCount: 3,
            IsConvexCorner: true,
            AllFacesPlanar: true,
            AllAdjacentFacesOrthogonal: true,
            AcuteFaceAngleCount: 0,
            ObtuseFaceAngleCount: 0,
            AreChamferDistancesEqual: true,
            IsDistanceWithinLocalBounds: withinBounds,
            HasCoherentBoundedPlanarCut: true,
            PreservesManifoldTopology: true,
            IsTrustedOrthogonalBody: true,
            HasOrthogonalGeometryConstructor: true));
    }

    public static KernelResult<BrepBoundedChamferCornerContext> TryCreateFromTrustedBody(
        BrepBody sourceBody,
        BrepBoundedChamferCorner corner,
        double distance)
    {
        if (!double.IsFinite(distance) || distance <= 0d)
        {
            return KernelResult<BrepBoundedChamferCornerContext>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Bounded chamfer corner distance must be finite and greater than 0.", Source: "firmament.chamfer-bounded")]);
        }

        if (!TryResolveMaxCornerVertex(sourceBody, out var cornerVertex, out var cornerPoint, out var resolveReason))
        {
            return KernelResult<BrepBoundedChamferCornerContext>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"Bounded chamfer corner resolution rejected: {resolveReason}", Source: "firmament.chamfer-bounded")]);
        }

        var incidentEdges = sourceBody.Topology.Edges
            .Where(edge => edge.StartVertexId == cornerVertex || edge.EndVertexId == cornerVertex)
            .ToArray();
        var participatingEdgeCount = incidentEdges.Length;
        var edgeVectors = new List<Vector3D>(participatingEdgeCount);
        var localMinEdgeLength = double.PositiveInfinity;
        foreach (var edge in incidentEdges)
        {
            var other = edge.StartVertexId == cornerVertex ? edge.EndVertexId : edge.StartVertexId;
            if (!TryResolveVertexPoint(sourceBody, other, out var otherPoint))
            {
                continue;
            }

            var vector = otherPoint - cornerPoint;
            edgeVectors.Add(vector);
            localMinEdgeLength = double.Min(localMinEdgeLength, vector.Length);
        }

        var incidentFaceNormals = new List<Vector3D>();
        foreach (var face in sourceBody.Topology.Faces)
        {
            if (!FaceContainsVertex(sourceBody, face, cornerVertex))
            {
                continue;
            }

            if (sourceBody.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                && surface?.Kind == SurfaceGeometryKind.Plane
                && surface.Plane.HasValue)
            {
                incidentFaceNormals.Add(surface.Plane.Value.Normal.ToVector());
            }
        }

        var allPlanar = incidentFaceNormals.Count >= 3;
        var angleFacts = AnalyzeAngles(incidentFaceNormals);
        var withinBounds = double.IsFinite(localMinEdgeLength) && distance < localMinEdgeLength;
        var isConvex = edgeVectors.Count == 3 && TryIsConvexTrihedral(edgeVectors, incidentFaceNormals);
        var allOrthogonal = angleFacts.nonOrthogonalCount == 0;
        var coherentCut = participatingEdgeCount == 3 && withinBounds && allPlanar && isConvex;

        return KernelResult<BrepBoundedChamferCornerContext>.Success(new BrepBoundedChamferCornerContext(
            Box: default,
            Corner: corner,
            Distance: distance,
            ParticipatingEdgeCount: participatingEdgeCount,
            IsConvexCorner: isConvex,
            AllFacesPlanar: allPlanar,
            AllAdjacentFacesOrthogonal: allOrthogonal,
            AcuteFaceAngleCount: angleFacts.acuteCount,
            ObtuseFaceAngleCount: angleFacts.obtuseCount,
            AreChamferDistancesEqual: true,
            IsDistanceWithinLocalBounds: withinBounds,
            HasCoherentBoundedPlanarCut: coherentCut,
            PreservesManifoldTopology: coherentCut,
            IsTrustedOrthogonalBody: allOrthogonal,
            HasOrthogonalGeometryConstructor: allOrthogonal));
    }

    private static bool TryResolveMaxCornerVertex(BrepBody body, out VertexId vertexId, out Point3D point, out string reason)
    {
        vertexId = default;
        point = default;
        reason = "could not resolve x_max_y_max_z_max corner from source body vertex points";

        var allPoints = body.Topology.Vertices
            .Where(vertex => TryResolveVertexPoint(body, vertex.Id, out _))
            .Select(vertex =>
            {
                TryResolveVertexPoint(body, vertex.Id, out var p);
                return (vertex.Id, Point: p);
            })
            .ToArray();
        if (allPoints.Length == 0)
        {
            reason = "source body is missing explicit vertex points for corner-token resolution";
            return false;
        }

        var maxScore = allPoints.Max(item => item.Point.X + item.Point.Y + item.Point.Z);
        var tolerance = 1e-9d;
        var candidates = allPoints
            .Where(item => double.Abs((item.Point.X + item.Point.Y + item.Point.Z) - maxScore) <= tolerance)
            .OrderByDescending(item => item.Point.Z)
            .ThenByDescending(item => item.Point.Y)
            .ThenByDescending(item => item.Point.X)
            .ToArray();
        if (candidates.Length == 0)
        {
            reason = "corner token x_max_y_max_z_max could not resolve a dominant positive-octant corner vertex on source body";
            return false;
        }

        vertexId = candidates[0].Id;
        point = candidates[0].Point;
        return true;
    }

    private static bool FaceContainsVertex(BrepBody body, Face face, VertexId vertexId)
    {
        foreach (var loopId in face.LoopIds)
        {
            if (!body.Topology.TryGetLoop(loopId, out var loop) || loop is null)
            {
                continue;
            }

            foreach (var coedgeId in loop.CoedgeIds)
            {
                if (!body.Topology.TryGetCoedge(coedgeId, out var coedge) || coedge is null
                    || !body.Topology.TryGetEdge(coedge.EdgeId, out var edge) || edge is null)
                {
                    continue;
                }

                if (edge.StartVertexId == vertexId || edge.EndVertexId == vertexId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryResolveVertexPoint(BrepBody body, VertexId vertexId, out Point3D point)
    {
        if (body.TryGetVertexPoint(vertexId, out point))
        {
            return true;
        }

        foreach (var edge in body.Topology.Edges)
        {
            var isStart = edge.StartVertexId == vertexId;
            var isEnd = edge.EndVertexId == vertexId;
            if (!isStart && !isEnd)
            {
                continue;
            }

            if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var binding)
                || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve)
                || curve?.Kind != CurveGeometryKind.Line3
                || curve.Line3 is null)
            {
                continue;
            }

            var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d);
            point = curve.Line3.Value.Evaluate(isStart ? interval.Start : interval.End);
            return true;
        }

        point = default;
        return false;
    }

    private static (int acuteCount, int obtuseCount, int nonOrthogonalCount) AnalyzeAngles(IReadOnlyList<Vector3D> normals)
    {
        var acute = 0;
        var obtuse = 0;
        var nonOrth = 0;
        const double orthTolerance = 1e-6d;
        for (var i = 0; i < normals.Count; i++)
        {
            if (!normals[i].TryNormalize(out var ni))
            {
                continue;
            }

            for (var j = i + 1; j < normals.Count; j++)
            {
                if (!normals[j].TryNormalize(out var nj))
                {
                    continue;
                }

                var dot = double.Clamp(ni.Dot(nj), -1d, 1d);
                if (double.Abs(dot) > orthTolerance)
                {
                    nonOrth++;
                }

                if (dot > orthTolerance)
                {
                    obtuse++;
                }
                else if (dot < -orthTolerance)
                {
                    acute++;
                }
            }
        }

        return (acute, obtuse, nonOrth);
    }

    private static bool TryIsConvexTrihedral(IReadOnlyList<Vector3D> edgeVectors, IReadOnlyList<Vector3D> faceNormals)
    {
        if (edgeVectors.Count != 3 || faceNormals.Count < 3)
        {
            return false;
        }

        var cross = edgeVectors[0].Cross(edgeVectors[1]);
        if (!cross.TryNormalize(out var triNormal))
        {
            return false;
        }

        return faceNormals.Any(normal =>
            normal.TryNormalize(out var n)
            && n.Dot(triNormal) > 1e-6d);
    }
}

public enum BrepBoundedChamferCorner
{
    XMaxYMaxZMax
}

public enum BrepBoundedChamferEdge
{
    XMinYMin,
    XMinYMax,
    XMaxYMin,
    XMaxYMax
}
