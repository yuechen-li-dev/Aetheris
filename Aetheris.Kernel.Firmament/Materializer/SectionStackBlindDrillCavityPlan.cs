using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Construction-owned input for the narrow blind-drill topology operation.  The
/// mouth face is supplied by construction provenance; this planner never
/// searches a materialized body for a geometrically similar face.
/// </summary>
internal sealed record SectionStackBlindDrillCavityInput(
    PrismaticSectionStackConstruction Host,
    PrismaticSectionStackBrepPlan HostPlan,
    AirHoleFeature Hole,
    AirConstructionPlaneHolePlacement Placement,
    BlindDrillToolCorridorEvidence Corridor,
    IReadOnlyList<FaceId> MouthHostFaceIds)
{
    public SectionStackBlindDrillCavityInput(
        PrismaticSectionStackConstruction host,
        PrismaticSectionStackBrepPlan hostPlan,
        AirHoleFeature hole,
        AirConstructionPlaneHolePlacement placement,
        BlindDrillToolCorridorEvidence corridor,
        FaceId mouthHostFaceId)
        : this(host, hostPlan, hole, placement, corridor, [mouthHostFaceId]) { }
}

internal sealed record SectionStackHostFaceReplacement(FaceId OriginalFaceId, IReadOnlyList<FaceId> ReplacementFaceIds);

internal sealed record SectionStackBlindDrillCavityPlan(
    string StableId,
    PrismaticSectionStackBrepPlan SourceHostPlan,
    PrismaticSectionStackBrepPlan ReplacementHostPlan,
    IReadOnlyList<SectionStackHostFaceReplacement> FaceReplacements,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Exact topology insertion for one proven blind cavity whose circular mouth is
/// wholly inside one construction-provided planar host side face.  This is not
/// a Boolean: the original face is replaced in the plan and the cylinder/cone
/// walls are wired directly into the same shell.
/// </summary>
internal static class SectionStackBlindDrillCavityPlanner
{
    private const double Tol = 1e-7;

    public static SectionStackBlindDrillCavityPlan? TryPlan(SectionStackBlindDrillCavityInput input, out IReadOnlyList<string> diagnostics)
    {
        if (input.MouthHostFaceIds.Count == 2)
            return TryPlanAcrossCoplanarPlanningSeam(input, out diagnostics);
        if (input.MouthHostFaceIds.Count != 1)
        {
            diagnostics = ["SectionStackMouthArcOwnershipAmbiguous"];
            return null;
        }
        var d = new List<string>();
        if (input.Corridor.Classification != BlindDrillToolCorridorClassification.CorridorProven)
            d.Add("SectionStackBlindDrillCorridorNotProven");
        if (input.Corridor.ValidationPolicy != BlindDrillClearancePolicy.FullRadiusThroughTotalDepth)
            d.Add("SectionStackBlindDrillUnsupportedClearancePolicy");
        var point = input.Hole.Termination as AirHoleTermination.DrillPoint;
        if (point is null || input.Hole.EndCondition is AirHoleEndCondition.ThroughAll)
            d.Add("SectionStackBlindDrillRequiresBlindDrillPoint");
        var source = input.HostPlan.TopologyPlan;
        if (source is null)
            d.Add("SectionStackBlindDrillMissingAuthoritativeHostPlan");
        if (d.Count != 0) { diagnostics = d; return null; }

        var mouthHostFaceId = input.MouthHostFaceIds[0];
        var mapping = source!.FaceMappings.SingleOrDefault(x => x.FaceId == mouthHostFaceId && x.Kind == "PrismaticSide");
        if (mapping is null) d.Add("SectionStackBlindDrillMouthFaceProvenanceMissing");
        if (!source.Topology.TryGetFace(mouthHostFaceId, out var originalFace) || originalFace is null) d.Add("SectionStackBlindDrillMouthFaceMissing");
        SurfaceGeometry? originalSurface = null;
        if (!source.Bindings.TryGetFaceBinding(mouthHostFaceId, out var originalBinding)
            || !source.Geometry.TryGetSurface(originalBinding.SurfaceGeometryId, out originalSurface)
            || originalSurface?.Kind != SurfaceGeometryKind.Plane)
            d.Add("SectionStackBlindDrillMouthRequiresPlanarHostSideFace");
        if (d.Count != 0) { diagnostics = d; return null; }

        var axis = input.Placement.AxisZ.ToVector(); var mouth = input.Placement.WorldMouthCenter;
        var normal = originalSurface!.Plane!.Value.Normal.ToVector();
        if (Math.Abs(Math.Abs(axis.Dot(normal)) - 1d) > Tol || Math.Abs((mouth - originalSurface.Plane.Value.Origin).Dot(normal)) > Tol)
            d.Add("SectionStackBlindDrillMouthFaceDoesNotMatchConstructionPlane");
        var shaft = input.Corridor.ShaftDepth; var tipLength = input.Corridor.TipLength;
        if (shaft < -Tol || tipLength <= Tol) d.Add("SectionStackBlindDrillDepthInvalid");
        if (d.Count != 0) { diagnostics = d; return null; }

        var topology = CopyTopologyExceptShells(source.Topology, mouthHostFaceId);
        var geometry = CopyGeometry(source.Geometry); var bindings = CopyBindings(source.Bindings);
        var points = source.VertexPoints.ToDictionary(x => x.Key, x => x.Value);
        var nextVertex = source.Topology.Vertices.Max(x => x.Id.Value) + 1;
        var nextEdge = source.Topology.Edges.Max(x => x.Id.Value) + 1;
        var nextCoedge = source.Topology.Coedges.Max(x => x.Id.Value) + 1;
        var nextLoop = source.Topology.Loops.Max(x => x.Id.Value) + 1;
        var nextFace = source.Topology.Faces.Max(x => x.Id.Value) + 1;
        var nextCurve = source.Geometry.Curves.Max(x => x.Key.Value) + 1;
        var nextSurface = source.Geometry.Surfaces.Max(x => x.Key.Value) + 1;

        VertexId Vertex(Point3D p) { var id = new VertexId(nextVertex++); topology.AddVertex(new Vertex(id)); points[id] = p; return id; }
        EdgeId Edge(VertexId a, VertexId b, CurveGeometry curve, ParameterInterval trim, bool oriented = true)
        { var id = new EdgeId(nextEdge++); var curveId = new CurveGeometryId(nextCurve++); topology.AddEdge(new Edge(id, a, b)); geometry.AddCurve(curveId, curve); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, curveId, trim, oriented)); return id; }
        LoopId Loop(params (EdgeId Edge, bool Reverse)[] uses)
        { var id = new LoopId(nextLoop++); var coedges = uses.Select(_ => new CoedgeId(nextCoedge++)).ToArray(); for (var i = 0; i < uses.Length; i++) topology.AddCoedge(new Coedge(coedges[i], uses[i].Edge, id, coedges[(i + 1) % uses.Length], coedges[(i + uses.Length - 1) % uses.Length], uses[i].Reverse)); topology.AddLoop(new Loop(id, coedges)); return id; }
        FaceId Face(IReadOnlyList<LoopId> loops, SurfaceGeometry surface, bool sameSense)
        { var id = new FaceId(nextFace++); var surfaceId = new SurfaceGeometryId(nextSurface++); topology.AddFace(new Face(id, loops)); geometry.AddSurface(surfaceId, surface); bindings.AddFaceBinding(new FaceGeometryBinding(id, surfaceId, sameSense)); return id; }

        var r = input.Hole.Shaft.Radius; var radial = input.Placement.AxisX.ToVector();
        var mouthA = Vertex(mouth + radial * r); var mouthB = Vertex(mouth - radial * r);
        var transitionCenter = mouth + axis * shaft; var transitionA = Vertex(transitionCenter + radial * r); var transitionB = Vertex(transitionCenter - radial * r);
        var tip = Vertex(transitionCenter + axis * tipLength);
        var circle = new Circle3Curve(mouth, input.Placement.AxisZ, r, input.Placement.AxisX);
        var transitionCircle = new Circle3Curve(transitionCenter, input.Placement.AxisZ, r, input.Placement.AxisX);
        var mouthArcA = Edge(mouthA, mouthB, CurveGeometry.FromCircle(circle), new(0d, Math.PI));
        var mouthArcB = Edge(mouthB, mouthA, CurveGeometry.FromCircle(circle), new(Math.PI, 2d * Math.PI));
        var transitionArcA = Edge(transitionA, transitionB, CurveGeometry.FromCircle(transitionCircle), new(0d, Math.PI));
        var transitionArcB = Edge(transitionB, transitionA, CurveGeometry.FromCircle(transitionCircle), new(Math.PI, 2d * Math.PI));
        var shaftSeam = Edge(mouthA, transitionA, CurveGeometry.FromLine(new Line3Curve(points[mouthA], Direction3D.Create(points[transitionA] - points[mouthA]))), new(0d, shaft));
        var coneSeamLength = (points[tip] - points[transitionA]).Length;
        var coneSeam = Edge(transitionA, tip, CurveGeometry.FromLine(new Line3Curve(points[transitionA], Direction3D.Create(points[tip] - points[transitionA]))), new(0d, coneSeamLength));
        var mouthLoop = Loop((mouthArcB, true), (mouthArcA, true));
        var transitionLoop = Loop((transitionArcA, false), (transitionArcB, false));
        var shaftLoop = Loop((mouthArcA, false), (mouthArcB, false), (shaftSeam, false), (transitionArcB, true), (transitionArcA, true), (shaftSeam, true));
        var coneLoop = Loop((transitionArcA, false), (transitionArcB, false), (coneSeam, false), (coneSeam, true));
        var replacementFace = Face(originalFace!.LoopIds.Concat([mouthLoop]).ToArray(), originalSurface, originalBinding.SameSense);
        var shaftFace = Face([shaftLoop], SurfaceGeometry.FromCylinder(new CylinderSurface(mouth, input.Placement.AxisZ, r, input.Placement.AxisX)), false);
        var coneFace = Face([coneLoop], SurfaceGeometry.FromCone(new ConeSurface(points[tip], Direction3D.Create(-axis), point!.PointAngleDegrees * Math.PI / 360d, input.Placement.AxisX)), false);

        var shell = source.Topology.Shells.Single(); topology.AddShell(new Shell(shell.Id, shell.FaceIds.Where(x => x != mouthHostFaceId).Append(replacementFace).Append(shaftFace).Append(coneFace).ToArray()));
        var body = source.Topology.Bodies.Single(); topology.AddBody(new Body(body.Id, body.ShellIds.ToArray()));
        var descendants = source.Correspondence.Descendants.Concat(new SemanticTopologyDescendant[]
        {
            new($"plan:{input.Hole.FeatureId}:mouth-loop", "Loop", SemanticTopologyRole.HoleEntryLoop, input.Hole.FeatureId, Loop: mouthLoop, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:mouth-edge:a", "Edge", SemanticTopologyRole.TopBoundary, input.Hole.FeatureId, Edge: mouthArcA, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:mouth-edge:b", "Edge", SemanticTopologyRole.TopBoundary, input.Hole.FeatureId, Edge: mouthArcB, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:shaft", "Face", SemanticTopologyRole.HoleWallFace, input.Hole.FeatureId, Face: shaftFace, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:transition-loop", "Loop", SemanticTopologyRole.HoleShaftToDrillPointLoop, input.Hole.FeatureId, Loop: transitionLoop, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:point", "Face", SemanticTopologyRole.HoleDrillPointFace, input.Hole.FeatureId, Face: coneFace, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:tip", "Vertex", SemanticTopologyRole.HoleTipVertex, input.Hole.FeatureId, Vertex: tip, ParentStableId: input.Hole.FeatureId)
        }).ToArray();
        var correspondence = new SemanticTopologyCorrespondence(source.Correspondence.BodyStableId, descendants, source.Correspondence.ProvenanceChain.Concat(["SectionStackBlindDrillCavityPlan", "ConstructionProvenanceMouthFace", "NoInternalCaps"]).ToArray());
        var mappings = source.FaceMappings.Where(x => x.FaceId != mouthHostFaceId).Append(new PrismaticSectionStackFacePlanMapping(replacementFace, "HostFaceReplacement", mapping!.SourceStableId, mapping.ConstructionStableId, mapping.SlabFrom, mapping.SlabTo, mapping.Provenance))
            .Append(new PrismaticSectionStackFacePlanMapping(shaftFace, "BlindDrillShaft", input.Hole.FeatureId, mapping.ConstructionStableId, mapping.SlabFrom, mapping.SlabTo, ["SectionStackBlindDrillCavityPlan", "NoInternalCaps"]))
            .Append(new PrismaticSectionStackFacePlanMapping(coneFace, "BlindDrillPoint", input.Hole.FeatureId, mapping.ConstructionStableId, mapping.SlabFrom, mapping.SlabTo, ["SectionStackBlindDrillCavityPlan", "NoInternalCaps"])) .ToArray();
        var topologyPlan = new PrismaticSectionStackTopologyPlan($"{source.StableId}:blind:{input.Hole.FeatureId}", topology, geometry, bindings, points, mappings, correspondence, correspondence.ProvenanceChain);
        var replacement = new PrismaticSectionStackBrepPlan($"{input.HostPlan.Signature}:blind:{input.Hole.FeatureId}", points.Count, topology.Edges.Count(), topology.Faces.Count(), input.HostPlan.Policy, true, correspondence, topologyPlan);
        d.Add("SectionStackBlindDrillCavityPlanCreated"); d.Add("SectionStackBlindDrillNoInternalCaps"); diagnostics = d;
        return new($"section-stack-blind:{input.Hole.FeatureId}", input.HostPlan, replacement, [new(mouthHostFaceId, [replacementFace])], d);
    }

    /// <summary>
    /// Bounded X1 variant of the same insertion: two adjacent section slabs own
    /// complementary exact arcs of one physical Mouth.  The seam remains a
    /// planning boundary outside the circle and is deliberately absent through
    /// the opening.  This is not a planar-arrangement or Boolean operation.
    /// </summary>
    private static SectionStackBlindDrillCavityPlan? TryPlanAcrossCoplanarPlanningSeam(SectionStackBlindDrillCavityInput input, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string>();
        var point = input.Hole.Termination as AirHoleTermination.DrillPoint;
        var source = input.HostPlan.TopologyPlan;
        if (input.Corridor.Classification != BlindDrillToolCorridorClassification.CorridorProven) d.Add("SectionStackBlindDrillCorridorNotProven");
        if (input.Corridor.ValidationPolicy != BlindDrillClearancePolicy.FullRadiusThroughTotalDepth) d.Add("SectionStackBlindDrillUnsupportedClearancePolicy");
        if (point is null || input.Hole.EndCondition is AirHoleEndCondition.ThroughAll) d.Add("SectionStackBlindDrillRequiresBlindDrillPoint");
        if (source is null) d.Add("SectionStackBlindDrillMissingAuthoritativeHostPlan");
        if (d.Count != 0) { diagnostics = d; return null; }
        if (source is null) { diagnostics = ["SectionStackBlindDrillMissingAuthoritativeHostPlan"]; return null; }

        var faceIds = input.MouthHostFaceIds.Distinct().OrderBy(x => x.Value).ToArray();
        if (faceIds.Length != 2) { diagnostics = ["SectionStackMouthArcOwnershipAmbiguous"]; return null; }
        var mappings = faceIds.Select(id => source.FaceMappings.SingleOrDefault(x => x.FaceId == id && x.Kind == "PrismaticSide")).ToArray();
        if (mappings.Any(x => x is null)) d.Add("SectionStackBlindDrillMouthFaceProvenanceMissing");
        var faces = faceIds.Select(id => source.Topology.TryGetFace(id, out var face) ? face : null).ToArray();
        if (faces.Any(x => x is null)) d.Add("SectionStackBlindDrillMouthFaceMissing");
        var bindings = new FaceGeometryBinding?[2];
        var surfaces = new SurfaceGeometry?[2];
        for (var index = 0; index < faceIds.Length; index++)
        {
            if (source.Bindings.TryGetFaceBinding(faceIds[index], out var binding))
            {
                bindings[index] = binding;
                if (source.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface)) surfaces[index] = surface;
            }
        }
        if (surfaces.Any(x => x?.Kind != SurfaceGeometryKind.Plane)) d.Add("SectionStackMouthSeamNotCoplanar");
        if (d.Count != 0) { diagnostics = d; return null; }

        var axis = input.Placement.AxisZ.ToVector(); var mouth = input.Placement.WorldMouthCenter;
        var normal = surfaces[0]!.Plane!.Value.Normal.ToVector();
        if (surfaces.Any(surface => Math.Abs(Math.Abs(surface!.Plane!.Value.Normal.ToVector().Dot(normal)) - 1d) > Tol
                                      || Math.Abs((surface.Plane.Value.Origin - surfaces[0]!.Plane!.Value.Origin).Dot(normal)) > Tol
                                      || Math.Abs(Math.Abs(axis.Dot(surface.Plane.Value.Normal.ToVector())) - 1d) > Tol
                                      || Math.Abs((mouth - surface.Plane.Value.Origin).Dot(surface.Plane.Value.Normal.ToVector())) > Tol))
            d.Add("SectionStackMouthSeamNotCoplanar");
        // Construction source identity plus adjacent slab extents is the proof
        // that this is a planning partition, not an exterior/material edge.
        if (mappings[0]!.SourceStableId != mappings[1]!.SourceStableId
            || mappings.Any(mapping => mapping!.SlabFrom is null || mapping.SlabTo is null)
            || Math.Abs(mappings[0]!.SlabTo!.Value - mappings[1]!.SlabFrom!.Value) > Tol && Math.Abs(mappings[1]!.SlabTo!.Value - mappings[0]!.SlabFrom!.Value) > Tol)
            d.Add("SectionStackMouthSeamPhysicalBoundary");
        if (d.Count != 0) { diagnostics = d; return null; }

        var loops = faces.Select(face => source.Topology.Loops.Single(loop => loop.Id == face!.LoopIds.Single())).ToArray();
        var edgeSets = loops.Select(loop => loop.CoedgeIds.Select(id => source.Topology.Coedges.Single(c => c.Id == id).EdgeId).ToHashSet()).ToArray();
        var common = edgeSets[0].Intersect(edgeSets[1]).OrderBy(id => id.Value).ToArray();
        if (common.Length != 1) { diagnostics = ["SectionStackMouthSeamPhysicalBoundary"]; return null; }
        var seamId = common[0]; var seam = source.Topology.Edges.Single(edge => edge.Id == seamId);
        if (!source.Bindings.TryGetEdgeBinding(seamId, out var seamBinding)
            || !source.Geometry.TryGetCurve(seamBinding.CurveGeometryId, out var seamCurve)
            || seamCurve is null
            || seamCurve.Kind != CurveGeometryKind.Line3)
        { diagnostics = ["SectionStackMouthSeamPhysicalBoundary"]; return null; }
        var seamStart = source.VertexPoints[seam.StartVertexId]; var seamEnd = source.VertexPoints[seam.EndVertexId];
        var seamVector = seamEnd - seamStart; var seamLength = seamVector.Length;
        if (seamLength <= Tol) { diagnostics = ["SectionStackMouthSeamDegenerate"]; return null; }
        var seamDirection = seamVector / seamLength;
        var relative = seamStart - mouth;
        var projected = relative.Dot(seamDirection);
        var closest = relative - seamDirection * projected;
        var discriminant = input.Hole.Shaft.Radius * input.Hole.Shaft.Radius - closest.LengthSquared;
        if (Math.Abs(discriminant) <= Tol) { diagnostics = ["SectionStackMouthSeamTangent"]; return null; }
        if (discriminant < 0d) { diagnostics = ["SectionStackMouthArcOwnershipAmbiguous"]; return null; }
        var delta = Math.Sqrt(discriminant); var t0 = -projected - delta; var t1 = -projected + delta;
        if (t0 <= Tol || t1 >= seamLength - Tol || t1 - t0 <= Tol) { diagnostics = ["SectionStackMouthArcOwnershipAmbiguous"]; return null; }

        var topology = CopyTopologyExceptShells(source.Topology, faceIds);
        var geometry = CopyGeometry(source.Geometry); var resultBindings = CopyBindings(source.Bindings);
        var points = source.VertexPoints.ToDictionary(x => x.Key, x => x.Value);
        var nextVertex = source.Topology.Vertices.Max(x => x.Id.Value) + 1;
        var nextEdge = source.Topology.Edges.Max(x => x.Id.Value) + 1;
        var nextCoedge = source.Topology.Coedges.Max(x => x.Id.Value) + 1;
        var nextLoop = source.Topology.Loops.Max(x => x.Id.Value) + 1;
        var nextFace = source.Topology.Faces.Max(x => x.Id.Value) + 1;
        var nextCurve = source.Geometry.Curves.Max(x => x.Key.Value) + 1;
        var nextSurface = source.Geometry.Surfaces.Max(x => x.Key.Value) + 1;
        VertexId Vertex(Point3D p) { var id = new VertexId(nextVertex++); topology.AddVertex(new Vertex(id)); points[id] = p; return id; }
        EdgeId Edge(VertexId a, VertexId b, CurveGeometry curve, ParameterInterval trim, bool oriented = true)
        { var id = new EdgeId(nextEdge++); var curveId = new CurveGeometryId(nextCurve++); topology.AddEdge(new Edge(id, a, b)); geometry.AddCurve(curveId, curve); resultBindings.AddEdgeBinding(new EdgeGeometryBinding(id, curveId, trim, oriented)); return id; }
        LoopId Loop(params (EdgeId Edge, bool Reverse)[] uses)
        { var id = new LoopId(nextLoop++); var coedges = uses.Select(_ => new CoedgeId(nextCoedge++)).ToArray(); for (var i = 0; i < uses.Length; i++) topology.AddCoedge(new Coedge(coedges[i], uses[i].Edge, id, coedges[(i + 1) % uses.Length], coedges[(i + uses.Length - 1) % uses.Length], uses[i].Reverse)); topology.AddLoop(new Loop(id, coedges)); return id; }
        FaceId Face(IReadOnlyList<LoopId> faceLoops, SurfaceGeometry surface, bool sameSense)
        { var id = new FaceId(nextFace++); var surfaceId = new SurfaceGeometryId(nextSurface++); topology.AddFace(new Face(id, faceLoops)); geometry.AddSurface(surfaceId, surface); resultBindings.AddFaceBinding(new FaceGeometryBinding(id, surfaceId, sameSense)); return id; }

        // Sorted seam parameters are the deterministic identity and ordering of
        // the two shared physical intersection vertices.
        var i0 = Vertex(seamStart + seamDirection * t0); var i1 = Vertex(seamStart + seamDirection * t1);
        var seamLine = new Line3Curve(seamStart, Direction3D.Create(seamVector));
        var before = Edge(seam.StartVertexId, i0, CurveGeometry.FromLine(seamLine), new(0d, t0));
        var after = Edge(i1, seam.EndVertexId, CurveGeometry.FromLine(seamLine), new(t1, seamLength));
        var circle = new Circle3Curve(mouth, input.Placement.AxisZ, input.Hole.Shaft.Radius, input.Placement.AxisX);
        double Angle(Point3D p) => Math.Atan2((p - mouth).Dot(circle.YAxis.ToVector()), (p - mouth).Dot(circle.XAxis.ToVector()));
        var a0 = Angle(points[i0]); var a1 = Angle(points[i1]); while (a1 <= a0) a1 += 2d * Math.PI;
        var ccwMid = circle.Evaluate((a0 + a1) / 2d);
        var ccwOwner = Array.FindIndex(mappings, mapping => ccwMid.Z >= mapping!.SlabFrom!.Value - Tol && ccwMid.Z <= mapping.SlabTo!.Value + Tol);
        if (ccwOwner < 0 || (ccwMid.Z >= mappings[0]!.SlabFrom!.Value - Tol && ccwMid.Z <= mappings[0]!.SlabTo!.Value + Tol && ccwMid.Z >= mappings[1]!.SlabFrom!.Value - Tol && ccwMid.Z <= mappings[1]!.SlabTo!.Value + Tol))
        { diagnostics = ["SectionStackMouthArcOwnershipAmbiguous"]; return null; }
        var otherOwner = 1 - ccwOwner;
        var ccwArc = Edge(i0, i1, CurveGeometry.FromCircle(circle), new(a0, a1));
        var cwArc = Edge(i1, i0, CurveGeometry.FromCircle(circle), new(a1, a0 + 2d * Math.PI));
        var arcForFace = new[] { ccwArc, cwArc }; arcForFace[ccwOwner] = ccwArc; arcForFace[otherOwner] = cwArc;

        LoopId ReplaceSeam(Loop original, EdgeId arc)
        {
            var forwardArcReverse = arc == cwArc;
            var uses = new List<(EdgeId Edge, bool Reverse)>();
            foreach (var coedgeId in original.CoedgeIds)
            {
                var coedge = source.Topology.Coedges.Single(c => c.Id == coedgeId);
                if (coedge.EdgeId != seamId) uses.Add((coedge.EdgeId, coedge.IsReversed));
                else if (!coedge.IsReversed) { uses.Add((before, false)); uses.Add((arc, forwardArcReverse)); uses.Add((after, false)); }
                else { uses.Add((after, true)); uses.Add((arc, !forwardArcReverse)); uses.Add((before, true)); }
            }
            return Loop(uses.ToArray());
        }
        var hostLoops = new[] { ReplaceSeam(loops[0], arcForFace[0]), ReplaceSeam(loops[1], arcForFace[1]) };
        var replacementFaces = new[] { Face([hostLoops[0]], surfaces[0]!, bindings[0]!.Value.SameSense), Face([hostLoops[1]], surfaces[1]!, bindings[1]!.Value.SameSense) };

        var radial = input.Placement.AxisX.ToVector(); var shaft = input.Corridor.ShaftDepth; var tipLength = input.Corridor.TipLength;
        var transitionCenter = mouth + axis * shaft; var transitionA = Vertex(transitionCenter + radial * input.Hole.Shaft.Radius); var transitionB = Vertex(transitionCenter - radial * input.Hole.Shaft.Radius); var tip = Vertex(transitionCenter + axis * tipLength);
        var transitionCircle = new Circle3Curve(transitionCenter, input.Placement.AxisZ, input.Hole.Shaft.Radius, input.Placement.AxisX);
        var transitionArcA = Edge(transitionA, transitionB, CurveGeometry.FromCircle(transitionCircle), new(0d, Math.PI)); var transitionArcB = Edge(transitionB, transitionA, CurveGeometry.FromCircle(transitionCircle), new(Math.PI, 2d * Math.PI));
        var shaftSeam = Edge(i0, transitionA, CurveGeometry.FromLine(new Line3Curve(points[i0], Direction3D.Create(points[transitionA] - points[i0]))), new(0d, (points[transitionA] - points[i0]).Length));
        var coneSeam = Edge(transitionA, tip, CurveGeometry.FromLine(new Line3Curve(points[transitionA], Direction3D.Create(points[tip] - points[transitionA]))), new(0d, (points[tip] - points[transitionA]).Length));
        var mouthLoop = Loop((ccwArc, false), (cwArc, false));
        var transitionLoop = Loop((transitionArcA, false), (transitionArcB, false));
        var shaftLoop = Loop((ccwArc, false), (cwArc, false), (shaftSeam, false), (transitionArcB, true), (transitionArcA, true), (shaftSeam, true));
        var coneLoop = Loop((transitionArcA, false), (transitionArcB, false), (coneSeam, false), (coneSeam, true));
        var shaftFace = Face([shaftLoop], SurfaceGeometry.FromCylinder(new CylinderSurface(mouth, input.Placement.AxisZ, input.Hole.Shaft.Radius, input.Placement.AxisX)), false);
        var coneFace = Face([coneLoop], SurfaceGeometry.FromCone(new ConeSurface(points[tip], Direction3D.Create(-axis), point!.PointAngleDegrees * Math.PI / 360d, input.Placement.AxisX)), false);
        var shell = source.Topology.Shells.Single(); topology.AddShell(new Shell(shell.Id, shell.FaceIds.Where(id => !faceIds.Contains(id)).Concat(replacementFaces).Append(shaftFace).Append(coneFace).ToArray()));
        var body = source.Topology.Bodies.Single(); topology.AddBody(new Body(body.Id, body.ShellIds.ToArray()));
        var descendants = source.Correspondence.Descendants.Concat(new SemanticTopologyDescendant[]
        {
            new($"plan:{input.Hole.FeatureId}:mouth-loop", "Loop", SemanticTopologyRole.HoleEntryLoop, input.Hole.FeatureId, Loop: mouthLoop, ParentStableId: input.Hole.FeatureId, GeometryPreview:"MultiFaceCoplanar"),
            new($"plan:{input.Hole.FeatureId}:mouth-edge:0", "Edge", SemanticTopologyRole.TopBoundary, input.Hole.FeatureId, Edge: ccwArc, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:mouth-edge:1", "Edge", SemanticTopologyRole.TopBoundary, input.Hole.FeatureId, Edge: cwArc, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:shaft", "Face", SemanticTopologyRole.HoleWallFace, input.Hole.FeatureId, Face: shaftFace, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:transition-loop", "Loop", SemanticTopologyRole.HoleShaftToDrillPointLoop, input.Hole.FeatureId, Loop: transitionLoop, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:point", "Face", SemanticTopologyRole.HoleDrillPointFace, input.Hole.FeatureId, Face: coneFace, ParentStableId: input.Hole.FeatureId),
            new($"plan:{input.Hole.FeatureId}:tip", "Vertex", SemanticTopologyRole.HoleTipVertex, input.Hole.FeatureId, Vertex: tip, ParentStableId: input.Hole.FeatureId)
        }).ToArray();
        var provenance = source.Correspondence.ProvenanceChain.Concat(["SectionStackBlindDrillCavityPlan", "MultiFaceCoplanarMouth", "ExactLineCircleSplit", "NoInternalCaps"]).ToArray();
        var correspondence = new SemanticTopologyCorrespondence(source.Correspondence.BodyStableId, descendants, provenance);
        var faceMappings = source.FaceMappings.Where(mapping => !faceIds.Contains(mapping.FaceId)).Concat(replacementFaces.Select((face, index) => new PrismaticSectionStackFacePlanMapping(face, "HostFaceReplacement", mappings[index]!.SourceStableId, mappings[index]!.ConstructionStableId, mappings[index]!.SlabFrom, mappings[index]!.SlabTo, mappings[index]!.Provenance.Concat(["MultiFaceCoplanarMouth"]).ToArray())))
            .Append(new PrismaticSectionStackFacePlanMapping(shaftFace, "BlindDrillShaft", input.Hole.FeatureId, mappings[0]!.ConstructionStableId, mappings[0]!.SlabFrom, mappings[1]!.SlabTo, ["SectionStackBlindDrillCavityPlan", "NoInternalCaps"]))
            .Append(new PrismaticSectionStackFacePlanMapping(coneFace, "BlindDrillPoint", input.Hole.FeatureId, mappings[0]!.ConstructionStableId, mappings[0]!.SlabFrom, mappings[1]!.SlabTo, ["SectionStackBlindDrillCavityPlan", "NoInternalCaps"])) .ToArray();
        var topologyPlan = new PrismaticSectionStackTopologyPlan($"{source.StableId}:blind:{input.Hole.FeatureId}:seam", topology, geometry, resultBindings, points, faceMappings, correspondence, provenance);
        var replacement = new PrismaticSectionStackBrepPlan($"{input.HostPlan.Signature}:blind:{input.Hole.FeatureId}:seam", points.Count, topology.Edges.Count(), topology.Faces.Count(), input.HostPlan.Policy, true, correspondence, topologyPlan);
        d.AddRange(["SectionStackMouthSeamAdmitted", "SectionStackMouthTwoIntersections", "SectionStackMouthOwnershipMultiFaceCoplanar", "SectionStackMouthSeamRemovedThroughOpening", "SectionStackBlindDrillNoInternalCaps"]);
        diagnostics = d;
        return new($"section-stack-blind:{input.Hole.FeatureId}", input.HostPlan, replacement, faceIds.Zip(replacementFaces).Select(x => new SectionStackHostFaceReplacement(x.First, [x.Second])).ToArray(), d);
    }

    private static TopologyModel CopyTopologyExceptShells(TopologyModel source, params FaceId[] excludedFaces)
    { var excluded = excludedFaces.ToHashSet(); var copy = new TopologyModel(); foreach (var x in source.Vertices) copy.AddVertex(x); foreach (var x in source.Edges) copy.AddEdge(x); foreach (var x in source.Coedges) copy.AddCoedge(x); foreach (var x in source.Loops) copy.AddLoop(x); foreach (var x in source.Faces.Where(x => !excluded.Contains(x.Id))) copy.AddFace(x); return copy; }
    private static BrepGeometryStore CopyGeometry(BrepGeometryStore source) { var copy = new BrepGeometryStore(); foreach (var x in source.Curves) copy.AddCurve(x.Key, x.Value); foreach (var x in source.Surfaces) copy.AddSurface(x.Key, x.Value); return copy; }
    private static BrepBindingModel CopyBindings(BrepBindingModel source) { var copy = new BrepBindingModel(); foreach (var x in source.EdgeBindings) copy.AddEdgeBinding(x); foreach (var x in source.FaceBindings) copy.AddFaceBinding(x); return copy; }
}

/// <summary>
/// The sole compose/V2 feature bridge for the admitted conservative blind-drill
/// lane.  It consumes construction-owned section-stack plan provenance; it
/// never searches a materialized body or STEP topology for an entry face.
/// </summary>
public static class SectionStackBlindDrillComposeBridge
{
    private const double Tol = 1e-7;

    public static PrismaticSectionStackBrepPlan? TryApply(
        PrismaticSectionStackConstruction stack,
        PrismaticSectionStackBrepPlan hostPlan,
        out IReadOnlyList<string> diagnostics,
        out BlindDrillToolCorridorEvidence? evidence)
    {
        var d = new List<string>();
        var declarations = stack.Feature.ConstructionPlaneBlindDrills ?? [];
        evidence = null;
        if (declarations.Count == 0) { diagnostics = d; return hostPlan; }
        if (declarations.Count != 1) { diagnostics = ["SectionStackBlindDrillOnlyOneFeatureIsAdmitted"]; return null; }
        var declaration = declarations[0];
        var placement = new AirConstructionPlaneHolePlacement(declaration.ConstructionPlaneId, declaration.SourceConceptPlaneId,
            declaration.FrameOrigin, declaration.AxisX, declaration.AxisY, declaration.AxisZ, declaration.LocalCenterX, declaration.LocalCenterY,
            declaration.SourceSpan, declaration.Provenance);
        var end = declaration.DeclaredDepthIsTotal
            ? (AirHoleEndCondition)new AirHoleEndCondition.TotalDepth(declaration.DeclaredDepth)
            : new AirHoleEndCondition.ShaftDepth(declaration.DeclaredDepth);
        var feature = AirHoleFeature.CreateConstructionPlaneSimpleShaft(declaration.Name, declaration.StableId, stack.Feature.Name, placement,
            new AirHoleShaft(declaration.Diameter), end,
            new AirProvenance("COMPOSED-HOST-BLIND-DRILL-CLEARANCE-X1", "Profile/Compose Construction Plane Hole", declaration.Name,
                declaration.StableId, nameof(AirConstructionPlaneHolePlacement), AirSelectionClass.None, AirRuleKind.None,
                "FullRadiusThroughTotalDepth", true, ["construction-plane:" + declaration.ConstructionPlaneId, "source-concept-plane:" + declaration.SourceConceptPlaneId]),
            new AirHoleTermination.DrillPoint(declaration.PointAngleDegrees));
        var corridor = TransverseBlindDrillToolCorridor.Prove(feature, stack, placement);
        evidence = corridor;
        if (corridor.Classification != BlindDrillToolCorridorClassification.CorridorProven)
        { d.AddRange(corridor.Diagnostics); d.Add("SectionStackBlindDrillClearanceContractRejected"); diagnostics = d; return null; }

        var faces = FindMouthFaces(hostPlan.TopologyPlan!, placement, feature.Shaft.Radius, d);
        if (faces is null) { diagnostics = d; return null; }
        var cavity = SectionStackBlindDrillCavityPlanner.TryPlan(new(stack, hostPlan, feature, placement, corridor, faces), out var cavityDiagnostics);
        d.AddRange(cavityDiagnostics);
        if (cavity is null) { diagnostics = d; return null; }
        d.Add("SectionStackBlindDrillComposeBridge");
        diagnostics = d;
        return cavity.ReplacementHostPlan;
    }

    private static IReadOnlyList<FaceId>? FindMouthFaces(PrismaticSectionStackTopologyPlan plan, AirConstructionPlaneHolePlacement placement, double radius, List<string> diagnostics)
    {
        var mouth = placement.WorldMouthCenter; var axis = placement.AxisZ.ToVector();
        var compatible = plan.FaceMappings.Where(x => x.Kind == "PrismaticSide")
            .Where(x => plan.Bindings.TryGetFaceBinding(x.FaceId, out var binding)
                         && plan.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface)
                         && surface is { Kind: SurfaceGeometryKind.Plane, Plane: { } plane }
                         && Math.Abs(Math.Abs(plane.Normal.ToVector().Dot(axis)) - 1d) <= Tol
                         && Math.Abs((mouth - plane.Origin).Dot(plane.Normal.ToVector())) <= Tol)
            .ToArray();
        var candidates = compatible.Where(x => x.SlabFrom is { } from && x.SlabTo is { } to && mouth.Z - radius >= from - Tol && mouth.Z + radius <= to + Tol).ToArray();
        if (candidates.Length == 1)
        {
            var tangent = compatible.Any(x => x.FaceId != candidates[0].FaceId
                && ((x.SlabTo is { } to && Math.Abs(mouth.Z - radius - to) <= Tol)
                    || (x.SlabFrom is { } from && Math.Abs(mouth.Z + radius - from) <= Tol)));
            if (tangent)
            {
                diagnostics.Add("SectionStackMouthSeamTangent");
                return null;
            }
            return [candidates[0].FaceId];
        }
        var crossed = compatible.Where(x => x.SlabFrom is { } from && x.SlabTo is { } to && mouth.Z + radius > from + Tol && mouth.Z - radius < to - Tol).OrderBy(x => x.FaceId.Value).ToArray();
        if (crossed.Length == 2)
        {
            return crossed.Select(x => x.FaceId).ToArray();
        }
        diagnostics.Add(candidates.Length == 0 && compatible.Length > 0
            ? "SectionStackBlindDrillMouthCrossesHostPlanningPartition"
            : "SectionStackBlindDrillMouthProvenanceNotFound");
        return null;
    }
}
