using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static (double Width, double Depth, double Height, double[] Matrix)? BoxBoundaryFrame(string face, FirmamentV2BoxRecord box)
    {
        var x = box.Size[0]; var y = box.Size[1]; var z = box.Size[2];
        // Proper signed permutations preserve support/curve parameters and shell
        // orientation. Canonical +Z keeps its established byte-stable route.
        return face switch
        {
            "-Z" => (x, y, z, new double[] { 1,0,0,0, 0,-1,0,0, 0,0,-1,0, 0,0,z,1 }),
            "+X" => (y, z, x, new double[] { 0,1,0,0, 0,0,1,0, 1,0,0,0, -x/2,0,z/2,1 }),
            "-X" => (y, z, x, new double[] { 0,-1,0,0, 0,0,1,0, -1,0,0,0, x/2,0,z/2,1 }),
            "+Y" => (x, z, y, new double[] { -1,0,0,0, 0,0,1,0, 0,1,0,0, 0,-y/2,z/2,1 }),
            "-Y" => (x, z, y, new double[] { 1,0,0,0, 0,0,1,0, 0,-1,0,0, 0,y/2,z/2,1 }),
            _ => null
        };
    }

    private static KernelResult<FirmamentStepExportResult> ExportV2BoxFaceBoundaryFinish(
        FirmamentV2Document document, FirmamentV2SolidBinding solid, FirmamentV2EdgeFinishDecl finish, FirmamentV2BoxRecord box)
    {
        if (BoxBoundaryFrame(finish.FaceAxis, box) is not { } frame)
            return CombinedFailure($"EdgeFinishFaceUnsupported: '{finish.FaceAxis}' is not a principal Box face.", sourceSpan: finish.SourceSpan);
        var kind = finish.Kind == "Fillet" ? TopBoundaryEdgeFinishKind.Fillet : TopBoundaryEdgeFinishKind.Chamfer;
        var plan = new CombinedTopBoundaryEdgeFinishThroughHolePlan(
            $"edgefinish:{solid.Name}:{finish.Name}", $"stock:{solid.Name}", [finish.Name],
            frame.Width, frame.Depth, 0, frame.Height, finish.Distance, [], kind);
        var built = CombinedTopBoundaryEdgeFinishThroughHoleBuilder.Build(plan);
        if (!built.IsSuccess)
            return CombinedFailure("EdgeFinishFaceBoundaryRejected: Finish size must fit the selected face and its stock depth.",
                built.Diagnostics.Select(d => d.Message), finish.SourceSpan);
        var body = FirmasmAssemblyExecutor.TransformBody(built.Value, Transform3D.FromRowMajor(frame.Matrix));
        if (!FirmamentManifoldChecker.IsManifold(body))
            return CombinedFailure("EdgeFinishFaceBoundaryNonManifold", sourceSpan: finish.SourceSpan);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid)
            return CombinedFailure("EdgeFinishFaceBoundaryPreflightFailed", preflight.Diagnostics.Select(d => d.Message), finish.SourceSpan);
        var support = ValidateV2PmiExportSupport(document);
        if (!support.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(support.Diagnostics);
        var step = Step242Exporter.ExportBody(body, BuildV2SemanticPmi(document, [], solid.Name, body: body));
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var imported = Step242Importer.ImportBody(step.Value);
        if (!imported.IsSuccess || !FirmamentManifoldChecker.IsManifold(imported.Value))
            return CombinedFailure("EdgeFinishFaceBoundaryRoundtripFailed", imported.Diagnostics.Select(d => d.Message), finish.SourceSpan);
        return KernelResult<FirmamentStepExportResult>.Success(new(step.Value, document.ModelName, 0,
            "box-face-boundary-finish", "BoxFaceBoundaryFinish")
        {
            RuntimeBody = body,
            MaterializedFeatures = FirmamentFeatureAccounting.Receipts(solid.Name, "EdgeFinish", finish.Name),
            EngineeringFeatures = [new(finish.Name, "EdgeFinish", $"edgefinish:{finish.Name}", solid.Name,
                finish.FaceAxis, "Boundary", finish.Distance, finish.Kind == "Fillet" ? "Radius" : "Distance", "Remove",
                PolicySource: finish.Kind, MaterializationRoute: "BoxFaceBoundaryFinish")]
        });
    }

    /// <summary>The oriented rim owns the outer shell. Insert circles on the retained world-Z
    /// supports using the same planar through-hole planner as a composed extrusion.</summary>
    private static PrismaticSectionStackEmissionResult BuildOrientedBoxRimWithHoles(
        CombinedTopBoundaryEdgeFinishThroughHolePlan plan, Transform3D toWorld, string bodyName, string finishName,
        double stockHeight, IReadOnlyList<AirHoleFeature> holes)
    {
        var stock = CombinedTopBoundaryEdgeFinishThroughHoleBuilder.Build(plan with { Holes = [] });
        if (!stock.IsSuccess) return new(null, null, stock.Diagnostics.Select(d => d.Message).ToArray());
        var body = FirmasmAssemblyExecutor.TransformBody(stock.Value, toWorld);
        return AttachBoxTopHoles(body, plan.PlanId, bodyName, stockHeight, holes, BoxRimFinishFaces(finishName), "BoxFaceBoundaryFinish");
    }

    private static PrismaticSectionStackEmissionResult AttachBoxTopHoles(BrepBody body, string planId, string bodyName,
        double stockHeight, IReadOnlyList<AirHoleFeature> holes, IEnumerable<SemanticTopologyDescendant> finishFaces,
        string finishRoute, bool allowConvexRoundedCorner = false)
    {
        // The oriented Box emitter produces one cap for each world-Z direction. A fillet
        // trim may reference a larger circle whose untrimmed extent lies outside the stock.
        var tops = FirmamentSelectorResolver.ResolveUniquePlanarFace(body, "top_face");
        var bottoms = FirmamentSelectorResolver.ResolveUniquePlanarFace(body, "bottom_face");
        if (tops.Count != 1 || bottoms.Count != 1)
            return new(null, null, ["BoxFinishHoleSupportLost: Expected one retained top and bottom plane."]);

        var caps = BoxConstructionCorrespondence.RetainedCaps(bodyName, bottoms[0], tops[0], body);
        var entry = caps.Single(face => face.Face == tops[0]);
        var descendants = caps.Select(face => face == entry ? face with { SupportOrigin = [0d, 0d, stockHeight] } : face);
        var correspondence = new SemanticTopologyCorrespondence(bodyName,
            [.. descendants, .. finishFaces], [finishRoute, "OrientedBoxStock"]);
        var mapping = new PrismaticSectionStackFacePlanMapping(tops[0], "PrismaticSide", entry.StableId, entry.StableId,
            null, null, correspondence.ProvenanceChain);
        var points = body.Topology.Vertices.ToDictionary(vertex => vertex.Id, vertex =>
            body.TryGetVertexPoint(vertex.Id, out var point) ? point : throw new InvalidOperationException("Missing Box rim vertex."));
        var topology = new PrismaticSectionStackTopologyPlan(planId, body.Topology, body.Geometry, body.Bindings, points,
            [mapping], correspondence, correspondence.ProvenanceChain);
        var emitted = new PrismaticSectionStackEmissionResult(body, new(planId, points.Count, body.Topology.Edges.Count(),
            body.Topology.Faces.Count(), finishRoute, true, correspondence, topology), [], correspondence);
        foreach (var hole in holes)
        {
            var counterbore = hole.Stack.Components.OfType<AirHoleCounterboreComponent>().SingleOrDefault();
            var countersink = hole.Stack.Components.OfType<AirHoleCountersinkComponent>().SingleOrDefault();
            emitted = PlanarFaceThroughHole.Apply(emitted, new(hole.Name, $"hole:{hole.FeatureId}", entry.StableId,
                hole.Placement.U, hole.Placement.V, hole.Shaft.Diameter, hole.Stack.Kind == AirHoleStackKind.SimpleShaft ? "Shaft" : hole.Stack.Kind.ToString(),
                countersink?.EntryDiameter ?? counterbore?.Radius * 2d, counterbore?.Depth, countersink?.AngleDegrees),
                allowConvexRoundedCorner);
            if (emitted.Body is null) return emitted;
        }
        return emitted;
    }

    private static IEnumerable<SemanticTopologyDescendant> BoxRimFinishFaces(string name) =>
        Enumerable.Range(0, 4).Select(side => new SemanticTopologyDescendant($"combined:edgefinish:{name}:replacement:{side}", "Face",
            SemanticTopologyRole.EdgeFinishReplacementFace, $"edgefinish:{name}", Face: new FaceId(4 + 2 * side), ParentStableId: name));
}
