using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Exact local-frame authority for one Box-hosted blind drilled hole.  It uses the
/// shared ProfileExtrusion plan entities and materializer; only the mechanical
/// shaft/cone topology is new.  No Boolean, clipping, or post-materialization
/// topology discovery participates in this route.
/// </summary>
internal static class BlindDrillPointBRepPlanner
{
    public static ProfileExtrusionPlanResult TryPlan(AirHoleFeature feature, AirConstructionPlaneHolePlacement placement,
        (double XMin, double XMax, double YMin, double YMax, double ZMax) bounds, double shaftDepth, double tipLength)
    {
        const double tol = 1e-9;
        var totalDepth = shaftDepth + tipLength;
        if (feature.Termination is not AirHoleTermination.DrillPoint point || shaftDepth < -tol || tipLength <= tol || totalDepth >= bounds.ZMax - tol)
            return new(false, null, ["HoleDrillPointPlanInvalid: blind shaft/cone interval is invalid."]);

        var frame = new ConstructionPlane(placement.ConstructionPlaneId, placement.SourceConceptPlaneId, placement.FrameOrigin,
            placement.AxisX, placement.AxisY, placement.AxisZ, placement.SourceSpan, placement.Provenance);
        var source = feature.FeatureId;
        var vertices = new List<ProfileExtrusionPlanVertex>();
        var curves = new List<ProfileExtrusionPlanCurve>();
        var edges = new List<ProfileExtrusionPlanEdge>();
        var loops = new List<ProfileExtrusionPlanLoop>();
        var surfaces = new List<ProfileExtrusionPlanSurface>();
        var faces = new List<ProfileExtrusionPlanFace>();
        var nextVertex = 1; var nextCurve = 1; var nextEdge = 1; var nextLoop = 1; var nextCoedge = 1; var nextSurface = 1; var nextFace = 1;

        Point3D World(double x, double y, double z) => frame.ToWorld((x, y), z);
        ProfileExtrusionPlanVertex Vertex(string id, double x, double y, double z, ProfileExtrusionPlanRole role)
        {
            var v = new ProfileExtrusionPlanVertex(id, new VertexId(nextVertex++), World(x, y, z), role, source); vertices.Add(v); return v;
        }
        ProfileExtrusionPlanCurve Curve(string id, CurveGeometry geometry, ParameterInterval trim) { var c = new ProfileExtrusionPlanCurve(id, new CurveGeometryId(nextCurve++), geometry, trim, true, source); curves.Add(c); return c; }
        ProfileExtrusionPlanEdge Edge(string id, ProfileExtrusionPlanVertex a, ProfileExtrusionPlanVertex b, ProfileExtrusionPlanCurve curve, ProfileExtrusionPlanRole role)
        { var e = new ProfileExtrusionPlanEdge(id, new EdgeId(nextEdge++), a.Id, b.Id, curve.Id, source, role); edges.Add(e); return e; }
        ProfileExtrusionPlanEdge Line(string id, ProfileExtrusionPlanVertex a, ProfileExtrusionPlanVertex b, ProfileExtrusionPlanRole role) =>
            Edge(id, a, b, Curve(id + ":curve", CurveGeometry.FromLine(new Line3Curve(a.WorldPoint, Direction3D.Create(b.WorldPoint - a.WorldPoint))), new(0d, (b.WorldPoint - a.WorldPoint).Length)), role);
        ProfileExtrusionPlanEdge Arc(string id, ProfileExtrusionPlanVertex a, ProfileExtrusionPlanVertex b, double z, double start, ProfileExtrusionPlanRole role) =>
            Edge(id, a, b, Curve(id + ":curve", CurveGeometry.FromCircle(new Circle3Curve(World(placement.LocalCenterX, placement.LocalCenterY, z), frame.AxisZ, feature.Shaft.Radius, frame.AxisX)), new(start, start + Math.PI)), role);
        ProfileExtrusionPlanLoop Loop(string id, ProfileExtrusionPlanRole role, params (ProfileExtrusionPlanEdge Edge, bool Reverse)[] uses)
        {
            var material = uses.Select((u, i) => new ProfileExtrusionPlanDirectedEdgeUse(id + $":use:{i}", new CoedgeId(nextCoedge++), u.Edge.Id, u.Reverse,
                DirectedEdgeUse.Resolve(new Edge(u.Edge.Id, u.Edge.StartVertexId, u.Edge.EndVertexId), u.Reverse))).ToArray();
            var loop = new ProfileExtrusionPlanLoop(id, new LoopId(nextLoop++), material, source, role); loops.Add(loop); return loop;
        }
        ProfileExtrusionPlanSurface Surface(string id, SurfaceGeometry geometry, bool sameSense, ProfileExtrusionPlanRole role)
        { var surface = new ProfileExtrusionPlanSurface(id, new SurfaceGeometryId(nextSurface++), geometry, sameSense, source, role); surfaces.Add(surface); return surface; }
        ProfileExtrusionPlanFace Face(string id, ProfileExtrusionPlanSurface surface, bool sameSense, ProfileExtrusionPlanRole role, params ProfileExtrusionPlanLoop[] faceLoops)
        { var face = new ProfileExtrusionPlanFace(id, new FaceId(nextFace++), faceLoops.Select(x => x.Id).ToArray(), surface.Id, sameSense, source, role); faces.Add(face); return face; }

        var b0 = Vertex("plan:box:mouth:0", bounds.XMin, bounds.YMin, 0d, ProfileExtrusionPlanRole.LocalStartVertex);
        var b1 = Vertex("plan:box:mouth:1", bounds.XMax, bounds.YMin, 0d, ProfileExtrusionPlanRole.LocalStartVertex);
        var b2 = Vertex("plan:box:mouth:2", bounds.XMax, bounds.YMax, 0d, ProfileExtrusionPlanRole.LocalStartVertex);
        var b3 = Vertex("plan:box:mouth:3", bounds.XMin, bounds.YMax, 0d, ProfileExtrusionPlanRole.LocalStartVertex);
        var t0 = Vertex("plan:box:far:0", bounds.XMin, bounds.YMin, bounds.ZMax, ProfileExtrusionPlanRole.LocalEndVertex);
        var t1 = Vertex("plan:box:far:1", bounds.XMax, bounds.YMin, bounds.ZMax, ProfileExtrusionPlanRole.LocalEndVertex);
        var t2 = Vertex("plan:box:far:2", bounds.XMax, bounds.YMax, bounds.ZMax, ProfileExtrusionPlanRole.LocalEndVertex);
        var t3 = Vertex("plan:box:far:3", bounds.XMin, bounds.YMax, bounds.ZMax, ProfileExtrusionPlanRole.LocalEndVertex);
        var mouthA = Vertex("plan:drill:mouth:a", placement.LocalCenterX + feature.Shaft.Radius, placement.LocalCenterY, 0d, ProfileExtrusionPlanRole.LocalStartVertex);
        var mouthB = Vertex("plan:drill:mouth:b", placement.LocalCenterX - feature.Shaft.Radius, placement.LocalCenterY, 0d, ProfileExtrusionPlanRole.LocalStartVertex);
        var transitionA = Vertex("plan:drill:shaft-to-point:a", placement.LocalCenterX + feature.Shaft.Radius, placement.LocalCenterY, shaftDepth, ProfileExtrusionPlanRole.LocalEndVertex);
        var transitionB = Vertex("plan:drill:shaft-to-point:b", placement.LocalCenterX - feature.Shaft.Radius, placement.LocalCenterY, shaftDepth, ProfileExtrusionPlanRole.LocalEndVertex);
        var tip = Vertex("plan:drill:tip", placement.LocalCenterX, placement.LocalCenterY, totalDepth, ProfileExtrusionPlanRole.LocalEndVertex);

        var lower = new[] { Line("plan:box:mouth:e0", b0, b1, ProfileExtrusionPlanRole.LocalStartBoundary), Line("plan:box:mouth:e1", b1, b2, ProfileExtrusionPlanRole.LocalStartBoundary), Line("plan:box:mouth:e2", b2, b3, ProfileExtrusionPlanRole.LocalStartBoundary), Line("plan:box:mouth:e3", b3, b0, ProfileExtrusionPlanRole.LocalStartBoundary) };
        var upper = new[] { Line("plan:box:far:e0", t0, t1, ProfileExtrusionPlanRole.LocalEndBoundary), Line("plan:box:far:e1", t1, t2, ProfileExtrusionPlanRole.LocalEndBoundary), Line("plan:box:far:e2", t2, t3, ProfileExtrusionPlanRole.LocalEndBoundary), Line("plan:box:far:e3", t3, t0, ProfileExtrusionPlanRole.LocalEndBoundary) };
        var vertical = new[] { Line("plan:box:vertical:e0", b0, t0, ProfileExtrusionPlanRole.LongitudinalBoundary), Line("plan:box:vertical:e1", b1, t1, ProfileExtrusionPlanRole.LongitudinalBoundary), Line("plan:box:vertical:e2", b2, t2, ProfileExtrusionPlanRole.LongitudinalBoundary), Line("plan:box:vertical:e3", b3, t3, ProfileExtrusionPlanRole.LongitudinalBoundary) };
        var mouthCircleA = Arc("plan:drill:mouth-circle:a", mouthA, mouthB, 0d, 0d, ProfileExtrusionPlanRole.LocalStartBoundary);
        var mouthCircleB = Arc("plan:drill:mouth-circle:b", mouthB, mouthA, 0d, Math.PI, ProfileExtrusionPlanRole.LocalStartBoundary);
        var transitionCircleA = Arc("plan:drill:transition-circle:a", transitionA, transitionB, shaftDepth, 0d, ProfileExtrusionPlanRole.LocalEndBoundary);
        var transitionCircleB = Arc("plan:drill:transition-circle:b", transitionB, transitionA, shaftDepth, Math.PI, ProfileExtrusionPlanRole.LocalEndBoundary);
        var shaftSeam = Line("plan:drill:shaft-seam", mouthA, transitionA, ProfileExtrusionPlanRole.LongitudinalBoundary);
        var coneSeam = Line("plan:drill:point-seam", transitionA, tip, ProfileExtrusionPlanRole.LongitudinalBoundary);

        var mouthOuter = Loop("plan:loop:mouth-outer", ProfileExtrusionPlanRole.LocalStartCapLoop, (lower[0], false), (lower[1], false), (lower[2], false), (lower[3], false));
        var mouthLoop = Loop("plan:loop:mouth", ProfileExtrusionPlanRole.LocalStartCapLoop, (mouthCircleB, true), (mouthCircleA, true));
        var farOuter = Loop("plan:loop:far-outer", ProfileExtrusionPlanRole.LocalEndCapLoop, (upper[0], true), (upper[3], true), (upper[2], true), (upper[1], true));
        var sideLoops = Enumerable.Range(0, 4).Select(i => Loop("plan:loop:box-side:" + i, ProfileExtrusionPlanRole.SideLoop,
            (lower[i], false), (vertical[(i + 1) % 4], false), (upper[i], true), (vertical[i], true))).ToArray();
        var shaftLoop = Loop("plan:loop:shaft-wall", ProfileExtrusionPlanRole.SideLoop, (mouthCircleA, false), (mouthCircleB, false), (shaftSeam, false), (transitionCircleB, true), (transitionCircleA, true), (shaftSeam, true));
        // The cone reaches the singular Tip through a deterministic seam pair; no flat cap or Exit loop exists.
        var pointLoop = Loop("plan:loop:drill-point", ProfileExtrusionPlanRole.SideLoop, (transitionCircleA, false), (transitionCircleB, false), (coneSeam, false), (coneSeam, true));
        var transitionLoop = Loop("plan:loop:shaft-to-drill-point", ProfileExtrusionPlanRole.LocalEndCapLoop, (transitionCircleA, false), (transitionCircleB, false));

        var mouthSurface = Surface("plan:surface:mouth", SurfaceGeometry.FromPlane(new PlaneSurface(World(0, 0, 0), Direction3D.Create(-frame.AxisZ.ToVector()), frame.AxisX)), true, ProfileExtrusionPlanRole.LocalStartCapFace);
        var farSurface = Surface("plan:surface:far", SurfaceGeometry.FromPlane(new PlaneSurface(World(0, 0, bounds.ZMax), frame.AxisZ, frame.AxisX)), true, ProfileExtrusionPlanRole.LocalEndCapFace);
        var sideSurfaces = new[]
        {
            Surface("plan:surface:box-side:0", SurfaceGeometry.FromPlane(new PlaneSurface(b0.WorldPoint, Direction3D.Create(-frame.AxisY.ToVector()), frame.AxisZ)), true, ProfileExtrusionPlanRole.SideFace),
            Surface("plan:surface:box-side:1", SurfaceGeometry.FromPlane(new PlaneSurface(b1.WorldPoint, frame.AxisX, frame.AxisZ)), true, ProfileExtrusionPlanRole.SideFace),
            Surface("plan:surface:box-side:2", SurfaceGeometry.FromPlane(new PlaneSurface(b2.WorldPoint, frame.AxisY, frame.AxisZ)), true, ProfileExtrusionPlanRole.SideFace),
            Surface("plan:surface:box-side:3", SurfaceGeometry.FromPlane(new PlaneSurface(b3.WorldPoint, Direction3D.Create(-frame.AxisX.ToVector()), frame.AxisZ)), true, ProfileExtrusionPlanRole.SideFace)
        };
        var shaftSurface = Surface("plan:surface:shaft", SurfaceGeometry.FromCylinder(new CylinderSurface(World(placement.LocalCenterX, placement.LocalCenterY, 0d), frame.AxisZ, feature.Shaft.Radius, frame.AxisX)), false, ProfileExtrusionPlanRole.SideFace);
        var coneSurface = Surface("plan:surface:drill-point", SurfaceGeometry.FromCone(new ConeSurface(tip.WorldPoint, Direction3D.Create(-frame.AxisZ.ToVector()), point.PointAngleDegrees * Math.PI / 360d, frame.AxisX)), false, ProfileExtrusionPlanRole.SideFace);

        Face("plan:face:mouth", mouthSurface, true, ProfileExtrusionPlanRole.LocalStartCapFace, mouthOuter, mouthLoop);
        Face("plan:face:far", farSurface, true, ProfileExtrusionPlanRole.LocalEndCapFace, farOuter);
        for (var i = 0; i < 4; i++) Face("plan:face:box-side:" + i, sideSurfaces[i], true, ProfileExtrusionPlanRole.SideFace, sideLoops[i]);
        var shaftFace = Face("plan:face:shaft", shaftSurface, false, ProfileExtrusionPlanRole.SideFace, shaftLoop);
        var drillPointFace = Face("plan:face:drill-point", coneSurface, false, ProfileExtrusionPlanRole.SideFace, pointLoop);
        var descendants = new SemanticTopologyDescendant[]
        {
            new($"plan:{source}:mouth-loop", "Loop", SemanticTopologyRole.HoleEntryLoop, source, Loop: mouthLoop.Id, ParentStableId: source),
            new($"plan:{source}:mouth-edge:a", "Edge", SemanticTopologyRole.TopBoundary, source, Edge: mouthCircleA.Id, ParentStableId: source),
            new($"plan:{source}:mouth-edge:b", "Edge", SemanticTopologyRole.TopBoundary, source, Edge: mouthCircleB.Id, ParentStableId: source),
            new($"plan:{source}:shaft-wall", "Face", SemanticTopologyRole.HoleWallFace, source, Face: shaftFace.Id, ParentStableId: source, GeometryPreview: $"radius={feature.Shaft.Radius:R};localZ=[0,{shaftDepth:R}]"),
            new($"plan:{source}:shaft-to-drill-point-loop", "Loop", SemanticTopologyRole.HoleShaftToDrillPointLoop, source, Loop: transitionLoop.Id, ParentStableId: source),
            new($"plan:{source}:shaft-to-drill-point-edge:a", "Edge", SemanticTopologyRole.HoleShaftToDrillPointEdge, source, Edge: transitionCircleA.Id, ParentStableId: source),
            new($"plan:{source}:shaft-to-drill-point-edge:b", "Edge", SemanticTopologyRole.HoleShaftToDrillPointEdge, source, Edge: transitionCircleB.Id, ParentStableId: source),
            new($"plan:{source}:drill-point", "Face", SemanticTopologyRole.HoleDrillPointFace, source, Face: drillPointFace.Id, ParentStableId: source, GeometryPreview: $"includedAngle={point.PointAngleDegrees:R}deg;tipLength={tipLength:R}"),
            new($"plan:{source}:tip", "Vertex", SemanticTopologyRole.HoleTipVertex, source, Vertex: tip.Id, ParentStableId: source, GeometryPreview: $"localZ={totalDepth:R}")
        };
        var shell = new ProfileExtrusionPlanShell("plan:shell:0", new ShellId(1), faces.Select(x => x.Id).ToArray());
        var body = new ProfileExtrusionPlanBody("plan:body:0", new BodyId(1), [shell.Id]);
        var construction = new ProfileExtrusionConstructionAir("air:" + source, source, frame, 0d, bounds.ZMax, [], ["HoleAIR", "DrillPoint", "AuthoritativeBRepPlan"]);
        var correspondence = new SemanticTopologyCorrespondence(feature.TargetBodyId ?? "semantic-hole-host", descendants, ["HoleAIR", "ConstructionPlanePlacement", "LocalFrameHoleBRepPlan", "ConeSurface", "AuthoritativeBRepPlan"]);
        var plan = new ProfileExtrusionBRepPlan("brep-plan:blind-drill-point:" + source + ":" + placement.ConstructionPlaneId, construction, vertices, curves, edges, loops, surfaces, faces, shell, body, correspondence, correspondence.ProvenanceChain,
            ["LocalFrameHoleBRepPlan", "BlindDrillPoint", "CylinderSurface", "ConeSurface", "TipVertex", "AuthoritativeBRepPlan"]);
        return new(true, plan, plan.Diagnostics);
    }
}
