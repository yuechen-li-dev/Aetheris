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
    FaceId MouthHostFaceId);

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

        var mapping = source!.FaceMappings.SingleOrDefault(x => x.FaceId == input.MouthHostFaceId && x.Kind == "PrismaticSide");
        if (mapping is null) d.Add("SectionStackBlindDrillMouthFaceProvenanceMissing");
        if (!source.Topology.TryGetFace(input.MouthHostFaceId, out var originalFace) || originalFace is null) d.Add("SectionStackBlindDrillMouthFaceMissing");
        SurfaceGeometry? originalSurface = null;
        if (!source.Bindings.TryGetFaceBinding(input.MouthHostFaceId, out var originalBinding)
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

        var topology = CopyTopologyExceptShells(source.Topology, input.MouthHostFaceId);
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

        var shell = source.Topology.Shells.Single(); topology.AddShell(new Shell(shell.Id, shell.FaceIds.Where(x => x != input.MouthHostFaceId).Append(replacementFace).Append(shaftFace).Append(coneFace).ToArray()));
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
        var mappings = source.FaceMappings.Where(x => x.FaceId != input.MouthHostFaceId).Append(new PrismaticSectionStackFacePlanMapping(replacementFace, "HostFaceReplacement", mapping!.SourceStableId, mapping.ConstructionStableId, mapping.SlabFrom, mapping.SlabTo, mapping.Provenance))
            .Append(new PrismaticSectionStackFacePlanMapping(shaftFace, "BlindDrillShaft", input.Hole.FeatureId, mapping.ConstructionStableId, mapping.SlabFrom, mapping.SlabTo, ["SectionStackBlindDrillCavityPlan", "NoInternalCaps"]))
            .Append(new PrismaticSectionStackFacePlanMapping(coneFace, "BlindDrillPoint", input.Hole.FeatureId, mapping.ConstructionStableId, mapping.SlabFrom, mapping.SlabTo, ["SectionStackBlindDrillCavityPlan", "NoInternalCaps"])) .ToArray();
        var topologyPlan = new PrismaticSectionStackTopologyPlan($"{source.StableId}:blind:{input.Hole.FeatureId}", topology, geometry, bindings, points, mappings, correspondence, correspondence.ProvenanceChain);
        var replacement = new PrismaticSectionStackBrepPlan($"{input.HostPlan.Signature}:blind:{input.Hole.FeatureId}", points.Count, topology.Edges.Count(), topology.Faces.Count(), input.HostPlan.Policy, true, correspondence, topologyPlan);
        d.Add("SectionStackBlindDrillCavityPlanCreated"); d.Add("SectionStackBlindDrillNoInternalCaps"); diagnostics = d;
        return new($"section-stack-blind:{input.Hole.FeatureId}", input.HostPlan, replacement, [new(input.MouthHostFaceId, [replacementFace])], d);
    }

    private static TopologyModel CopyTopologyExceptShells(TopologyModel source, FaceId excludedFace)
    { var copy = new TopologyModel(); foreach (var x in source.Vertices) copy.AddVertex(x); foreach (var x in source.Edges) copy.AddEdge(x); foreach (var x in source.Coedges) copy.AddCoedge(x); foreach (var x in source.Loops) copy.AddLoop(x); foreach (var x in source.Faces.Where(x => x.Id != excludedFace)) copy.AddFace(x); return copy; }
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

        var face = FindMouthFace(hostPlan.TopologyPlan!, placement, feature.Shaft.Radius, d);
        if (face is null) { diagnostics = d; return null; }
        var cavity = SectionStackBlindDrillCavityPlanner.TryPlan(new(stack, hostPlan, feature, placement, corridor, face.Value), out var cavityDiagnostics);
        d.AddRange(cavityDiagnostics);
        if (cavity is null) { diagnostics = d; return null; }
        d.Add("SectionStackBlindDrillComposeBridge");
        diagnostics = d;
        return cavity.ReplacementHostPlan;
    }

    private static FaceId? FindMouthFace(PrismaticSectionStackTopologyPlan plan, AirConstructionPlaneHolePlacement placement, double radius, List<string> diagnostics)
    {
        var mouth = placement.WorldMouthCenter; var axis = placement.AxisZ.ToVector();
        var compatible = plan.FaceMappings.Where(x => x.Kind == "PrismaticSide")
            .Where(x => plan.Bindings.TryGetFaceBinding(x.FaceId, out var binding)
                        && plan.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface)
                        && surface.Kind == SurfaceGeometryKind.Plane
                        && Math.Abs(Math.Abs(surface.Plane!.Value.Normal.ToVector().Dot(axis)) - 1d) <= Tol
                        && Math.Abs((mouth - surface.Plane.Value.Origin).Dot(surface.Plane.Value.Normal.ToVector())) <= Tol)
            .ToArray();
        var candidates = compatible.Where(x => x.SlabFrom is { } from && x.SlabTo is { } to && mouth.Z - radius >= from - Tol && mouth.Z + radius <= to + Tol).ToArray();
        if (candidates.Length != 1)
        {
            diagnostics.Add(candidates.Length == 0 && compatible.Length > 0
                ? "SectionStackBlindDrillMouthCrossesHostPlanningPartition"
                : "SectionStackBlindDrillMouthProvenanceNotFound");
            return null;
        }
        var candidate = candidates[0];
        // The current exact insertion keeps the circular inner loop in one
        // planar host face.  Refuse a seam-crossing mouth rather than creating
        // duplicated arcs or a false one-face ownership claim.
        return candidate.FaceId;
    }
}
