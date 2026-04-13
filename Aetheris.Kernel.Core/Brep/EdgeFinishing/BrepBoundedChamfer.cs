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
    private const string PlanarEdgePairCutCandidate = "planar_edge_pair_cut";
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
            return CreateTrustedPolyhedralSingleCornerPlanarChamferBody(sourceBody, context);
        }

        return CreateSingleCornerPlanarChamferBody(context);
    }

    public static KernelResult<BrepBody> ChamferAxisAlignedBoxVerticalEdge(
        AxisAlignedBoxExtents box,
        BrepBoundedChamferEdge edge,
        double distance)
    {
        if (edge.IsInternalConcaveToken())
        {
            return KernelResult<BrepBody>.Failure([Failure(
                "Bounded chamfer box-edge mode supports only external convex edge tokens; internal concave edge tokens require trusted occupied-cell concave-edge mode.",
                "firmament.chamfer-bounded")]);
        }

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

    public static KernelResult<BrepBody> ChamferTrustedPolyhedralSingleInternalConcaveEdge(
        BrepBody sourceBody,
        BrepBoundedChamferEdge edge,
        double distance)
    {
        var preflight = BrepBoundedConcaveChamferPreflight.ResolveInternalConcaveVerticalEdge(sourceBody.SafeBooleanComposition, edge, distance);
        if (!preflight.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(preflight.Diagnostics);
        }

        var context = new BrepBoundedConcaveChamferContext(
            IsPlanarPolyhedralSource: sourceBody.Bindings.FaceBindings.All(binding =>
            {
                var surface = sourceBody.Geometry.GetSurface(binding.SurfaceGeometryId);
                return surface.Kind == SurfaceGeometryKind.Plane;
            }),
            IsSingleEdgeSelection: true,
            IsEqualDistance: true,
            IsBoundedDistance: distance < preflight.Value.MaxAllowedDistance);
        var engine = new JudgmentEngine<BrepBoundedConcaveChamferContext>();
        var judgment = engine.Evaluate(context, BuildInternalConcaveEdgeCandidates());
        var selected = judgment.Selection?.Candidate.Name ?? RejectCandidate;
        if (!judgment.IsSuccess || selected == RejectCandidate)
        {
            var reason = judgment.Rejections.Count == 0
                ? "No bounded internal concave edge candidate was admissible."
                : string.Join(" ", judgment.Rejections.Select(rejection => $"{rejection.CandidateName}: {rejection.Reason}."));
            return KernelResult<BrepBody>.Failure([Failure($"Bounded concave edge resolution rejected: {reason}", "firmament.chamfer-bounded")]);
        }

        return KernelResult<BrepBody>.Failure([Failure(
            "Bounded concave edge preflight succeeded, but local loop rewrite construction is not yet implemented for this milestone slice.",
            "firmament.chamfer-bounded")]);
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

    private static IReadOnlyList<JudgmentCandidate<BrepBoundedConcaveChamferContext>> BuildInternalConcaveEdgeCandidates()
    {
        var boundedConcavePlanarGuard = When.All<BrepBoundedConcaveChamferContext>(
            context => context.IsPlanarPolyhedralSource,
            context => context.IsSingleEdgeSelection,
            context => context.IsEqualDistance,
            context => context.IsBoundedDistance);

        return
        [
            new JudgmentCandidate<BrepBoundedConcaveChamferContext>(
                Name: "planar_internal_concave_edge_cut",
                IsAdmissible: boundedConcavePlanarGuard,
                Score: _ => 100d,
                RejectionReason: _ => "requires planar trusted-polyhedral source with one bounded internal concave edge and equal distance",
                TieBreakerPriority: 0),
            new JudgmentCandidate<BrepBoundedConcaveChamferContext>(
                Name: RejectCandidate,
                IsAdmissible: _ => true,
                Score: _ => 0d,
                RejectionReason: _ => "fallback reject candidate",
                TieBreakerPriority: 99)
        ];
    }

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

    public static KernelResult<BrepBody> ChamferTrustedPolyhedralIncidentEdgePair(
        BrepBody sourceBody,
        BrepBoundedChamferIncidentEdgePairSelector selector,
        double distance)
    {
        var contextResult = BrepBoundedChamferCornerContext.TryCreateFromTrustedBody(sourceBody, selector.Corner, distance);
        if (!contextResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(contextResult.Diagnostics);
        }

        var context = contextResult.Value;
        var engine = new JudgmentEngine<BrepBoundedChamferCornerContext>();
        var candidates = BuildIncidentEdgePairCandidates();
        var judgment = engine.Evaluate(context, candidates);
        if (!judgment.IsSuccess || !judgment.Selection.HasValue || judgment.Selection.Value.Candidate.Name == RejectCandidate)
        {
            var reason = judgment.Rejections.Count == 0
                ? "No bounded two-edge corner-resolution candidate was admissible."
                : string.Join(" ", judgment.Rejections.Select(rejection => $"{rejection.CandidateName}: {rejection.Reason}."));
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer two-edge corner resolution rejected: {reason}", "firmament.chamfer-bounded")]);
        }

        return judgment.Selection.Value.Candidate.Name switch
        {
            PlanarEdgePairCutCandidate => CreateTrustedPolyhedralIncidentEdgePairPlanarChamferBody(sourceBody, context, selector),
            _ => KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer two-edge corner resolution selected unsupported candidate '{judgment.Selection.Value.Candidate.Name}'.", "firmament.chamfer-bounded")])
        };
    }

    private static IReadOnlyList<JudgmentCandidate<BrepBoundedChamferCornerContext>> BuildIncidentEdgePairCandidates()
    {
        var planarGuard = When.All<BrepBoundedChamferCornerContext>(
            context => context.ParticipatingEdgeCount == 3,
            context => context.IsConvexCorner,
            context => context.AllFacesPlanar,
            context => context.AreChamferDistancesEqual,
            context => context.IsDistanceWithinLocalBounds,
            context => context.HasCoherentBoundedPlanarCut,
            context => context.PreservesManifoldTopology,
            context => context.IsTrustedOrthogonalBody,
            context => context.Corner == BrepBoundedChamferCorner.XMaxYMaxZMax);

        return
        [
            new JudgmentCandidate<BrepBoundedChamferCornerContext>(
                Name: PlanarEdgePairCutCandidate,
                IsAdmissible: planarGuard,
                Score: context => context.AllAdjacentFacesOrthogonal ? 180d : 140d,
                RejectionReason: _ => "requires convex planar corner with coherent bounded planar cut and manifold safety",
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

    internal static bool TryResolveCornerIncidentEdgesByToken(
        BrepBody sourceBody,
        VertexId cornerVertex,
        Point3D cornerPoint,
        out Dictionary<BrepBoundedChamferCornerIncidentEdge, Edge> byToken,
        out string reason)
    {
        byToken = new Dictionary<BrepBoundedChamferCornerIncidentEdge, Edge>(3);
        reason = "could not resolve incident corner edges";
        var scoreByToken = new Dictionary<BrepBoundedChamferCornerIncidentEdge, double>();
        const double eps = 1e-8d;
        foreach (var edge in sourceBody.Topology.Edges.Where(edge => edge.StartVertexId == cornerVertex || edge.EndVertexId == cornerVertex))
        {
            var otherVertex = edge.StartVertexId == cornerVertex ? edge.EndVertexId : edge.StartVertexId;
            if (!BrepBoundedChamferCornerContext.TryResolveVertexPoint(sourceBody, otherVertex, out var otherPoint))
            {
                reason = "source body is missing explicit resolvable points for incident edge vertices";
                return false;
            }

            var vector = otherPoint - cornerPoint;
            var candidates = new List<(BrepBoundedChamferCornerIncidentEdge Token, double Score)>(3);
            if (vector.X < -eps)
            {
                candidates.Add((BrepBoundedChamferCornerIncidentEdge.XNegative, -vector.X));
            }

            if (vector.Y < -eps)
            {
                candidates.Add((BrepBoundedChamferCornerIncidentEdge.YNegative, -vector.Y));
            }

            if (vector.Z < -eps)
            {
                candidates.Add((BrepBoundedChamferCornerIncidentEdge.ZNegative, -vector.Z));
            }

            foreach (var candidate in candidates)
            {
                if (!scoreByToken.TryGetValue(candidate.Token, out var existing) || candidate.Score > existing)
                {
                    scoreByToken[candidate.Token] = candidate.Score;
                    byToken[candidate.Token] = edge;
                }
            }
        }

        reason = string.Empty;
        return true;
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


    private static KernelResult<BrepBody> CreateTrustedPolyhedralIncidentEdgePairPlanarChamferBody(
        BrepBody sourceBody,
        BrepBoundedChamferCornerContext context,
        BrepBoundedChamferIncidentEdgePairSelector selector)
    {
        if (!BrepBoundedChamferCornerContext.TryResolveMaxCornerVertex(sourceBody, out var cornerVertex, out var cornerPoint, out var reason))
        {
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer two-edge corner construction rejected: {reason}.", "firmament.chamfer-bounded")]);
        }

        if (!TryResolveCanonicalIncidentEdgePair(sourceBody, cornerVertex, cornerPoint, selector, out var firstEdge, out var secondEdge, out reason))
        {
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer two-edge corner construction rejected: {reason}.", "firmament.chamfer-bounded")]);
        }

        var selectedEdgeIds = new HashSet<EdgeId> { firstEdge.Id, secondEdge.Id };

        var vertexPoints = sourceBody.Topology.Vertices
            .Where(vertex => BrepBoundedChamferCornerContext.TryResolveVertexPoint(sourceBody, vertex.Id, out _))
            .ToDictionary(
                vertex => VertexToken.FromVertex(vertex.Id),
                vertex =>
                {
                    BrepBoundedChamferCornerContext.TryResolveVertexPoint(sourceBody, vertex.Id, out var point);
                    return point;
                });
        if (vertexPoints.Count != sourceBody.Topology.Vertices.ToArray().Length)
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer two-edge corner construction requires explicit resolvable points for every source body vertex.", "firmament.chamfer-bounded")]);
        }

        var splitVertexByIncidentEdge = new Dictionary<EdgeId, VertexToken>(2);
        foreach (var edgeId in selectedEdgeIds)
        {
            var edge = sourceBody.Topology.Edges.First(candidate => candidate.Id == edgeId);
            var otherVertex = edge.StartVertexId == cornerVertex ? edge.EndVertexId : edge.StartVertexId;
            var otherToken = VertexToken.FromVertex(otherVertex);
            var otherPoint = vertexPoints[otherToken];
            var edgeVector = otherPoint - cornerPoint;
            var edgeLength = edgeVector.Length;
            if (!double.IsFinite(edgeLength) || edgeLength <= context.Distance)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer two-edge corner construction rejected: corner distance is too large for one or more selected incident edges.", "firmament.chamfer-bounded")]);
            }

            var splitRatio = (edgeLength - context.Distance) / edgeLength;
            var splitPoint = cornerPoint + (edgeVector * splitRatio);
            var splitToken = VertexToken.FromSplitEdge(edge.Id);
            vertexPoints[splitToken] = splitPoint;
            splitVertexByIncidentEdge[edge.Id] = splitToken;
        }

        var rewrittenFaces = new List<IReadOnlyList<VertexToken>>();
        foreach (var face in sourceBody.Topology.Faces)
        {
            if (face.LoopIds.Count != 1 || !sourceBody.Topology.TryGetLoop(face.LoopIds[0], out var loop) || loop is null)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer two-edge corner construction currently supports only single-loop planar face participation.", "firmament.chamfer-bounded")]);
            }

            var cycle = TryGetLoopVertexCycle(sourceBody, loop);
            if (cycle is null || cycle.Count < 3)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer two-edge corner construction failed to resolve a coherent loop cycle from source topology.", "firmament.chamfer-bounded")]);
            }

            var cornerIndex = cycle.FindIndex(vertexId => vertexId == cornerVertex);
            if (cornerIndex < 0)
            {
                rewrittenFaces.Add(cycle.Select(VertexToken.FromVertex).ToArray());
                continue;
            }

            var previousVertex = cycle[(cornerIndex + cycle.Count - 1) % cycle.Count];
            var nextVertex = cycle[(cornerIndex + 1) % cycle.Count];
            var previousEdge = sourceBody.Topology.Edges.FirstOrDefault(edge =>
                (edge.StartVertexId == cornerVertex && edge.EndVertexId == previousVertex)
                || (edge.EndVertexId == cornerVertex && edge.StartVertexId == previousVertex));
            var nextEdge = sourceBody.Topology.Edges.FirstOrDefault(edge =>
                (edge.StartVertexId == cornerVertex && edge.EndVertexId == nextVertex)
                || (edge.EndVertexId == cornerVertex && edge.StartVertexId == nextVertex));
            if (previousEdge == default || nextEdge == default)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer two-edge corner construction could not resolve local face edges for participating rewrite.", "firmament.chamfer-bounded")]);
            }

            var hasPreviousSelection = splitVertexByIncidentEdge.TryGetValue(previousEdge.Id, out var splitPrevious);
            var hasNextSelection = splitVertexByIncidentEdge.TryGetValue(nextEdge.Id, out var splitNext);
            var rewritten = new List<VertexToken>(cycle.Count + 1);
            for (var i = 0; i < cycle.Count; i++)
            {
                if (i == cornerIndex)
                {
                    if (hasPreviousSelection)
                    {
                        rewritten.Add(splitPrevious);
                    }

                    if (!hasPreviousSelection || !hasNextSelection)
                    {
                        rewritten.Add(VertexToken.FromVertex(cycle[i]));
                    }

                    if (hasNextSelection)
                    {
                        rewritten.Add(splitNext);
                    }

                    continue;
                }

                rewritten.Add(VertexToken.FromVertex(cycle[i]));
            }

            rewrittenFaces.Add(rewritten);
        }

        var cutFace = new List<VertexToken>
        {
            splitVertexByIncidentEdge[firstEdge.Id],
            splitVertexByIncidentEdge[secondEdge.Id],
            VertexToken.FromVertex(cornerVertex)
        };
        if (cutFace[0].Equals(cutFace[1]))
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer two-edge corner construction rejected: selected incident edge pair did not resolve to two distinct local split vertices.", "firmament.chamfer-bounded")]);
        }

        var interiorPoint = new Point3D(
            vertexPoints.Values.Average(point => point.X),
            vertexPoints.Values.Average(point => point.Y),
            vertexPoints.Values.Average(point => point.Z));
        OrientCutFaceOutward(cutFace, vertexPoints, interiorPoint);
        rewrittenFaces.Add(cutFace);

        var localRewriteResult = BuildPolyhedralBodyFromFaces(rewrittenFaces, vertexPoints);
        if (localRewriteResult.IsSuccess)
        {
            return localRewriteResult;
        }

        return KernelResult<BrepBody>.Failure(
            [
                Failure(
                    "Bounded chamfer two-edge corner construction rejected: dedicated local rewrite failed to produce a valid manifold body for the supported canonical subset.",
                    "firmament.chamfer-bounded"),
                .. localRewriteResult.Diagnostics
            ]);
    }

    private static bool TryResolveCanonicalIncidentEdgePair(
        BrepBody sourceBody,
        VertexId cornerVertex,
        Point3D cornerPoint,
        BrepBoundedChamferIncidentEdgePairSelector selector,
        out Edge first,
        out Edge second,
        out string reason)
    {
        const double eps = 1e-8d;
        first = default;
        second = default;
        reason = "selected edges were not resolved at the selected corner";
        var incidents = new List<(Edge Edge, Vector3D Vector)>();
        foreach (var incident in sourceBody.Topology.Edges.Where(candidate => candidate.StartVertexId == cornerVertex || candidate.EndVertexId == cornerVertex))
        {
            var otherVertex = incident.StartVertexId == cornerVertex ? incident.EndVertexId : incident.StartVertexId;
            if (!BrepBoundedChamferCornerContext.TryResolveVertexPoint(sourceBody, otherVertex, out var otherPoint))
            {
                reason = "source body is missing explicit resolvable points for incident edge vertices";
                return false;
            }

            incidents.Add((incident, otherPoint - cornerPoint));
        }

        if (incidents.Count < 2)
        {
            reason = "selected corner has fewer than two incident edges";
            return false;
        }

        static double Score(BrepBoundedChamferCornerIncidentEdge token, Vector3D vector, double eps)
            => token switch
            {
                BrepBoundedChamferCornerIncidentEdge.XNegative when vector.X < -eps => -vector.X,
                BrepBoundedChamferCornerIncidentEdge.YNegative when vector.Y < -eps => -vector.Y,
                BrepBoundedChamferCornerIncidentEdge.ZNegative when vector.Z < -eps => -vector.Z,
                _ => double.NegativeInfinity
            };

        var candidates = new List<(Edge First, Edge Second, double Score)>();
        foreach (var firstCandidate in incidents)
        {
            var firstScore = Score(selector.First, firstCandidate.Vector, eps);
            if (!double.IsFinite(firstScore))
            {
                continue;
            }

            foreach (var secondCandidate in incidents)
            {
                if (secondCandidate.Edge.Id == firstCandidate.Edge.Id)
                {
                    continue;
                }

                var secondScore = Score(selector.Second, secondCandidate.Vector, eps);
                if (!double.IsFinite(secondScore))
                {
                    continue;
                }

                candidates.Add((firstCandidate.Edge, secondCandidate.Edge, firstScore + secondScore));
            }
        }

        if (candidates.Count == 0)
        {
            reason = "selected incident edge pair did not resolve to two distinct token-matching edges at selected corner";
            return false;
        }

        var resolved = candidates.OrderByDescending(candidate => candidate.Score).First();
        first = resolved.First;
        second = resolved.Second;
        return true;
    }

    private static KernelResult<BrepBody> CreateTrustedPolyhedralSingleCornerPlanarChamferBody(
        BrepBody sourceBody,
        BrepBoundedChamferCornerContext context)
    {
        if (!BrepBoundedChamferCornerContext.TryResolveMaxCornerVertex(sourceBody, out var cornerVertex, out var cornerPoint, out var reason))
        {
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer corner construction rejected: {reason}.", "firmament.chamfer-bounded")]);
        }

        var vertexPoints = sourceBody.Topology.Vertices
            .Where(vertex => BrepBoundedChamferCornerContext.TryResolveVertexPoint(sourceBody, vertex.Id, out _))
            .ToDictionary(
                vertex => VertexToken.FromVertex(vertex.Id),
                vertex =>
                {
                    BrepBoundedChamferCornerContext.TryResolveVertexPoint(sourceBody, vertex.Id, out var point);
                    return point;
                });
        if (vertexPoints.Count != sourceBody.Topology.Vertices.ToArray().Length)
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction requires explicit resolvable points for every source body vertex.", "firmament.chamfer-bounded")]);
        }

        var incidentEdges = sourceBody.Topology.Edges
            .Where(edge => edge.StartVertexId == cornerVertex || edge.EndVertexId == cornerVertex)
            .ToArray();
        if (incidentEdges.Length != 3)
        {
            return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer corner construction requires exactly 3 incident edges at selected corner; found {incidentEdges.Length}.", "firmament.chamfer-bounded")]);
        }

        var splitVertexByIncidentEdge = new Dictionary<EdgeId, VertexToken>(incidentEdges.Length);
        foreach (var edge in incidentEdges)
        {
            var otherVertex = edge.StartVertexId == cornerVertex ? edge.EndVertexId : edge.StartVertexId;
            var otherToken = VertexToken.FromVertex(otherVertex);
            var otherPoint = vertexPoints[otherToken];
            var edgeVector = otherPoint - cornerPoint;
            var edgeLength = edgeVector.Length;
            if (!double.IsFinite(edgeLength) || edgeLength <= context.Distance)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction rejected: corner distance is too large for one or more local incident edges.", "firmament.chamfer-bounded")]);
            }

            var splitRatio = (edgeLength - context.Distance) / edgeLength;
            var splitPoint = cornerPoint + (edgeVector * splitRatio);
            var splitToken = VertexToken.FromSplitEdge(edge.Id);
            vertexPoints[splitToken] = splitPoint;
            splitVertexByIncidentEdge[edge.Id] = splitToken;
        }

        var rewrittenFaces = new List<IReadOnlyList<VertexToken>>();
        foreach (var face in sourceBody.Topology.Faces)
        {
            if (face.LoopIds.Count != 1 || !sourceBody.Topology.TryGetLoop(face.LoopIds[0], out var loop) || loop is null)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction currently supports only single-loop planar face participation.", "firmament.chamfer-bounded")]);
            }

            var cycle = TryGetLoopVertexCycle(sourceBody, loop);
            if (cycle is null || cycle.Count < 3)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction failed to resolve a coherent loop cycle from source topology.", "firmament.chamfer-bounded")]);
            }

            var cornerIndex = cycle.FindIndex(vertexId => vertexId == cornerVertex);
            if (cornerIndex < 0)
            {
                rewrittenFaces.Add(cycle.Select(VertexToken.FromVertex).ToArray());
                continue;
            }

            var previousVertex = cycle[(cornerIndex + cycle.Count - 1) % cycle.Count];
            var nextVertex = cycle[(cornerIndex + 1) % cycle.Count];
            var previousEdge = sourceBody.Topology.Edges.FirstOrDefault(edge =>
                (edge.StartVertexId == cornerVertex && edge.EndVertexId == previousVertex)
                || (edge.EndVertexId == cornerVertex && edge.StartVertexId == previousVertex));
            var nextEdge = sourceBody.Topology.Edges.FirstOrDefault(edge =>
                (edge.StartVertexId == cornerVertex && edge.EndVertexId == nextVertex)
                || (edge.EndVertexId == cornerVertex && edge.StartVertexId == nextVertex));
            if (previousEdge == default || nextEdge == default
                || !splitVertexByIncidentEdge.TryGetValue(previousEdge.Id, out var splitPrevious)
                || !splitVertexByIncidentEdge.TryGetValue(nextEdge.Id, out var splitNext))
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction could not resolve split vertices for participating face rewrite.", "firmament.chamfer-bounded")]);
            }

            var rewritten = new List<VertexToken>(cycle.Count + 1);
            for (var i = 0; i < cycle.Count; i++)
            {
                if (i == cornerIndex)
                {
                    rewritten.Add(splitPrevious);
                    rewritten.Add(splitNext);
                    continue;
                }

                rewritten.Add(VertexToken.FromVertex(cycle[i]));
            }

            rewrittenFaces.Add(rewritten);
        }

        var cutFace = splitVertexByIncidentEdge.Values.ToList();
        var interiorPoint = new Point3D(
            vertexPoints.Values.Average(point => point.X),
            vertexPoints.Values.Average(point => point.Y),
            vertexPoints.Values.Average(point => point.Z));
        OrientCutFaceOutward(cutFace, vertexPoints, interiorPoint);
        rewrittenFaces.Add(cutFace);

        return BuildPolyhedralBodyFromFaces(rewrittenFaces, vertexPoints);
    }

    private static List<VertexId>? TryGetLoopVertexCycle(BrepBody body, Loop loop)
    {
        var cycle = new List<VertexId>(loop.CoedgeIds.Count);
        foreach (var coedgeId in loop.CoedgeIds)
        {
            if (!body.Topology.TryGetCoedge(coedgeId, out var coedge) || coedge is null
                || !body.Topology.TryGetEdge(coedge.EdgeId, out var edge) || edge is null)
            {
                return null;
            }

            cycle.Add(coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId);
        }

        return cycle;
    }

    private static void OrientCutFaceOutward(
        List<VertexToken> cutFace,
        IReadOnlyDictionary<VertexToken, Point3D> vertexPoints,
        Point3D interiorPoint)
    {
        var p0 = vertexPoints[cutFace[0]];
        var p1 = vertexPoints[cutFace[1]];
        var p2 = vertexPoints[cutFace[2]];
        var normal = (p1 - p0).Cross(p2 - p0);
        if (normal.Dot(interiorPoint - p0) > 0d)
        {
            cutFace.Reverse();
        }
    }

    private static KernelResult<BrepBody> BuildPolyhedralBodyFromFaces(
        IReadOnlyList<IReadOnlyList<VertexToken>> faceCycles,
        IReadOnlyDictionary<VertexToken, Point3D> vertexPoints)
    {
        var builder = new TopologyBuilder();
        var vertexIds = new Dictionary<VertexToken, VertexId>();
        var usedVertexTokens = faceCycles.SelectMany(cycle => cycle).Distinct().ToArray();
        foreach (var token in usedVertexTokens)
        {
            vertexIds[token] = builder.AddVertex();
        }

        var edgeIds = new Dictionary<(VertexToken, VertexToken), EdgeId>();
        var edgeEndpoints = new Dictionary<EdgeId, (VertexToken Start, VertexToken End)>();
        var faces = new List<FaceId>(faceCycles.Count);
        var normalizedFaceCycles = new List<IReadOnlyList<VertexToken>>(faceCycles.Count);
        for (var faceIndex = 0; faceIndex < faceCycles.Count; faceIndex++)
        {
            var normalizedCycle = NormalizeFaceCycle(faceCycles[faceIndex]);
            if (normalizedCycle is null)
            {
                return KernelResult<BrepBody>.Failure([Failure($"Bounded chamfer corner construction produced an invalid face loop at index {faceIndex}.", "firmament.chamfer-bounded")]);
            }

            normalizedFaceCycles.Add(normalizedCycle);
        }

        foreach (var cycle in normalizedFaceCycles)
        {
            var edgeUses = new List<EdgeUse>(cycle.Count);
            for (var i = 0; i < cycle.Count; i++)
            {
                var start = cycle[i];
                var end = cycle[(i + 1) % cycle.Count];
                if (start.Equals(end))
                {
                    return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction produced a degenerate loop edge.", "firmament.chamfer-bounded")]);
                }

                var key = OrderedEdgeKey(start, end);
                if (!edgeIds.TryGetValue(key, out var edgeId))
                {
                    edgeId = builder.AddEdge(vertexIds[start], vertexIds[end]);
                    edgeIds[key] = edgeId;
                    edgeEndpoints[edgeId] = (start, end);
                }

                var endpoints = edgeEndpoints[edgeId];
                var isReversed = !(endpoints.Start.Equals(start) && endpoints.End.Equals(end));
                edgeUses.Add(new EdgeUse(edgeId, isReversed));
            }

            faces.Add(AddFaceWithLoop(builder, edgeUses));
        }

        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var curveId = 1;
        foreach (var pair in edgeEndpoints)
        {
            var start = vertexPoints[pair.Value.Start];
            var end = vertexPoints[pair.Value.End];
            var direction = end - start;
            if (direction.Length <= 1e-9d)
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction produced a zero-length edge.", "firmament.chamfer-bounded")]);
            }

            var cid = new CurveGeometryId(curveId++);
            geometry.AddCurve(cid, CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(direction))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(pair.Key, cid, new ParameterInterval(0d, direction.Length)));
        }

        var surfaceId = 1;
        for (var faceIndex = 0; faceIndex < normalizedFaceCycles.Count; faceIndex++)
        {
            var cycle = normalizedFaceCycles[faceIndex];
            var points = cycle.Select(token => vertexPoints[token]).ToArray();
            if (!TryCreatePlane(points, out var origin, out var normal, out var uAxis))
            {
                return KernelResult<BrepBody>.Failure([Failure("Bounded chamfer corner construction requires planar non-degenerate face loops.", "firmament.chamfer-bounded")]);
            }

            var sid = new SurfaceGeometryId(surfaceId++);
            geometry.AddSurface(sid, SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(uAxis))));
            bindings.AddFaceBinding(new FaceGeometryBinding(faces[faceIndex], sid));
        }

        var builtVertexPoints = vertexIds.ToDictionary(pair => pair.Value, pair => vertexPoints[pair.Key]);
        var body = new BrepBody(builder.Model, geometry, bindings, builtVertexPoints);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static List<VertexToken>? NormalizeFaceCycle(IReadOnlyList<VertexToken> cycle)
    {
        var normalized = new List<VertexToken>(cycle.Count);
        foreach (var vertex in cycle)
        {
            if (normalized.Count == 0 || !normalized[^1].Equals(vertex))
            {
                normalized.Add(vertex);
            }
        }

        if (normalized.Count > 1 && normalized[0].Equals(normalized[^1]))
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return normalized.Count >= 3 ? normalized : null;
    }

    private static bool TryCreatePlane(
        IReadOnlyList<Point3D> points,
        out Point3D origin,
        out Vector3D normal,
        out Vector3D uAxis)
    {
        origin = points[0];
        normal = default;
        uAxis = default;
        for (var i = 1; i < points.Count - 1; i++)
        {
            var v1 = points[i] - points[0];
            var v2 = points[i + 1] - points[0];
            var cross = v1.Cross(v2);
            if (!cross.TryNormalize(out var normalized))
            {
                continue;
            }

            if (!v1.TryNormalize(out var u))
            {
                continue;
            }

            normal = normalized;
            uAxis = u;
            return true;
        }

        return false;
    }

    private static (VertexToken A, VertexToken B) OrderedEdgeKey(VertexToken a, VertexToken b)
        => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    private readonly record struct VertexToken(int Kind, int Value) : IComparable<VertexToken>
    {
        public static VertexToken FromVertex(VertexId vertexId) => new(0, vertexId.Value);
        public static VertexToken FromSplitEdge(EdgeId edgeId) => new(1, edgeId.Value);

        public int CompareTo(VertexToken other)
        {
            var kindCompare = Kind.CompareTo(other.Kind);
            return kindCompare != 0 ? kindCompare : Value.CompareTo(other.Value);
        }
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

    internal static bool TryResolveMaxCornerVertex(BrepBody body, out VertexId vertexId, out Point3D point, out string reason)
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

    internal static bool TryResolveVertexPoint(BrepBody body, VertexId vertexId, out Point3D point)
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

        var triple = edgeVectors[0].Cross(edgeVectors[1]).Dot(edgeVectors[2]);
        return double.Abs(triple) > 1e-9d;
    }

    public static KernelResult<bool> ValidateIncidentEdgePairSelector(
        BrepBody sourceBody,
        BrepBoundedChamferIncidentEdgePairSelector selector)
    {
        if (selector.First == selector.Second)
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Bounded chamfer corner-edge selector requires two distinct incident edge tokens.", Source: "firmament.chamfer-bounded")]);
        }

        if (selector.Corner is not BrepBoundedChamferCorner.XMaxYMaxZMax)
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Bounded chamfer corner-edge selector supports only corner token x_max_y_max_z_max.", Source: "firmament.chamfer-bounded")]);
        }

        if (!BrepBoundedChamferCornerContext.TryResolveMaxCornerVertex(sourceBody, out var cornerVertex, out var cornerPoint, out var reason))
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"Bounded chamfer corner-edge selector rejected: {reason}.", Source: "firmament.chamfer-bounded")]);
        }

        if (!BrepBoundedChamfer.TryResolveCornerIncidentEdgesByToken(sourceBody, cornerVertex, cornerPoint, out var byToken, out reason))
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"Bounded chamfer corner-edge selector rejected: {reason}.", Source: "firmament.chamfer-bounded")]);
        }

        if (!byToken.TryGetValue(selector.First, out var firstEdge))
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"Bounded chamfer corner-edge selector could not resolve selected incident edge '{selector.First}'.", Source: "firmament.chamfer-bounded")]);
        }

        if (!byToken.TryGetValue(selector.Second, out var secondEdge))
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"Bounded chamfer corner-edge selector could not resolve selected incident edge '{selector.Second}'.", Source: "firmament.chamfer-bounded")]);
        }

        var firstIncident = firstEdge.StartVertexId == cornerVertex || firstEdge.EndVertexId == cornerVertex;
        var secondIncident = secondEdge.StartVertexId == cornerVertex || secondEdge.EndVertexId == cornerVertex;
        if (!firstIncident || !secondIncident)
        {
            return KernelResult<bool>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Bounded chamfer corner-edge selector rejected: selected edges do not share the selected corner vertex.", Source: "firmament.chamfer-bounded")]);
        }

        return KernelResult<bool>.Success(true);
    }

    internal static bool TryClassifyCornerIncidentEdge(Vector3D vector, out BrepBoundedChamferCornerIncidentEdge edge)
    {
        edge = default;
        var eps = 1e-8d;
        if (vector.Length <= eps)
        {
            return false;
        }

        var absX = double.Abs(vector.X);
        var absY = double.Abs(vector.Y);
        var absZ = double.Abs(vector.Z);
        var dominant = double.Max(absX, double.Max(absY, absZ));
        if (dominant <= eps)
        {
            return false;
        }

        if (absX >= absY && absX >= absZ && vector.X < 0d)
        {
            edge = BrepBoundedChamferCornerIncidentEdge.XNegative;
            return true;
        }

        if (absY >= absX && absY >= absZ && vector.Y < 0d)
        {
            edge = BrepBoundedChamferCornerIncidentEdge.YNegative;
            return true;
        }

        if (absZ >= absX && absZ >= absY && vector.Z < 0d)
        {
            edge = BrepBoundedChamferCornerIncidentEdge.ZNegative;
            return true;
        }

        return false;
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
    XMaxYMax,
    InnerXMinYMin,
    InnerXMinYMax,
    InnerXMaxYMin,
    InnerXMaxYMax
}

public static class BrepBoundedChamferEdgeExtensions
{
    public static bool IsInternalConcaveToken(this BrepBoundedChamferEdge edge)
        => edge is BrepBoundedChamferEdge.InnerXMinYMin
            or BrepBoundedChamferEdge.InnerXMinYMax
            or BrepBoundedChamferEdge.InnerXMaxYMin
            or BrepBoundedChamferEdge.InnerXMaxYMax;
}

public enum BrepBoundedChamferCornerIncidentEdge
{
    XNegative,
    YNegative,
    ZNegative
}

public readonly record struct BrepBoundedChamferIncidentEdgePairSelector(
    BrepBoundedChamferCorner Corner,
    BrepBoundedChamferCornerIncidentEdge First,
    BrepBoundedChamferCornerIncidentEdge Second);

internal readonly record struct BrepBoundedConcaveChamferContext(
    bool IsPlanarPolyhedralSource,
    bool IsSingleEdgeSelection,
    bool IsEqualDistance,
    bool IsBoundedDistance);
