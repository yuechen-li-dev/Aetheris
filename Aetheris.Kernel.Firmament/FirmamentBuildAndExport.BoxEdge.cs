using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private const string BoxSingleEdgeRoute = "BoxSingleEdgeFinish";

    private static (BrepBody? Body, double RemovedVolume, IReadOnlyList<string> Diagnostics) BuildBoxSingleEdgeStock(
        FirmamentV2Document document, FirmamentV2SolidBinding solid, FirmamentV2BoxRecord box,
        FirmamentV2EdgeFinishDecl finish, BoxEdgeFacePair pair)
    {
        var frame = pair.Frame(box.Size);
        var span = new AirSourceSpan(finish.SourceSpan.Start, finish.SourceSpan.Length, document.ModelName);
        var feature = $"{solid.Name}.{finish.Name}";
        var fillet = finish.Kind == "Fillet";
        var (local, diagnostics) = fillet ? Fillet() : Chamfer();
        var removedArea = finish.Distance * finish.Distance * (fillet ? 1d - Math.PI / 4d : .5d);
        return (local is null ? null : FirmasmAssemblyExecutor.TransformBody(local, frame.ToWorld), removedArea * frame.Length, diagnostics);

        (BrepBody?, IReadOnlyList<string>) Fillet()
        {
            var result = AirLocalizedTangentBlendFilletCompiler.Compile(new(solid.Name, feature, finish.Name, frame.Width,
                frame.Length, frame.Height, "+X", "+Z", finish.Kind, finish.Distance, span));
            return (result.Succeeded ? result.Body : null, result.Diagnostics);
        }

        (BrepBody?, IReadOnlyList<string>) Chamfer()
        {
            var result = AirLocalizedPlanarReplacementChamferCompiler.Compile(new(solid.Name, feature, finish.Name, frame.Width,
                frame.Length, frame.Height, "+X", "+Z", finish.Kind, finish.Distance, span));
            return (result.Succeeded ? result.Body : null, result.Diagnostics);
        }
    }

    private static SemanticTopologyDescendant BoxSingleEdgeReplacement(FirmamentV2EdgeFinishDecl finish) =>
        // The localized emitter creates both end faces, then the swept sides in cross-section order.
        new($"edgefinish:{finish.Name}:replacement", "Face", SemanticTopologyRole.EdgeFinishReplacementFace,
            $"edgefinish:{finish.Name}", Face: new FaceId(5), ParentStableId: finish.Name);

    private static KernelResult<FirmamentStepExportResult> ExportV2BoxSingleEdge(
        FirmamentV2Document document, FirmamentV2SolidBinding solid, FirmamentV2BoxRecord box,
        FirmamentV2EdgeFinishDecl finish, BoxEdgeFacePair pair)
    {
        var (body, _, diagnostics) = BuildBoxSingleEdgeStock(document, solid, box, finish, pair);
        if (body is null)
            return CombinedFailure("BoxSingleEdgeSizeRejected: The finish must fit both faces at the selected corner.", diagnostics, finish.SourceSpan);
        var proof = BrepExportPreflight.Validate(body);
        if (!proof.IsValid || !FirmamentManifoldChecker.IsManifold(body))
            return CombinedFailure("BoxSingleEdgePreflightFailed: The finished Box failed analytic validation.", proof.Diagnostics.Select(d => d.Message), finish.SourceSpan);
        var support = ValidateV2PmiExportSupport(document);
        if (!support.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(support.Diagnostics);
        var step = Step242Exporter.ExportBody(body, BuildV2SemanticPmi(document, [], solid.Name, body: body));
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var imported = Step242Importer.ImportBody(step.Value);
        if (!imported.IsSuccess || !FirmamentManifoldChecker.IsManifold(imported.Value))
            return CombinedFailure("BoxSingleEdgeRoundtripFailed: STEP did not retain one closed solid.", imported.Diagnostics.Select(d => d.Message), finish.SourceSpan);
        return KernelResult<FirmamentStepExportResult>.Success(new(step.Value, document.ModelName, 0, "box-single-edge-finish", BoxSingleEdgeRoute)
        {
            RuntimeBody = body,
            RuntimeCorrespondence = new(solid.Name, [BoxSingleEdgeReplacement(finish)], [BoxSingleEdgeRoute]),
            MaterializedFeatures = FirmamentFeatureAccounting.Receipts(solid.Name, "EdgeFinish", finish.Name),
            EngineeringFeatures = [new(finish.Name, "EdgeFinish", $"edgefinish:{finish.Name}", solid.Name,
                finish.FaceAxis, pair.Selector, finish.Distance, finish.Kind == "Fillet" ? "Radius" : "Distance", "Remove",
                PolicySource: finish.Kind, MaterializationRoute: BoxSingleEdgeRoute)]
        });
    }

    private static SemanticTopologyCorrespondence? BoxThroughHolePickEdges(SemanticTopologyCorrespondence? correspondence,
        BrepBody body, FirmamentV2SolidBinding solid, FirmamentV2BoxRecord box, IReadOnlyList<AirHoleFeature> holes)
    {
        if (holes.Any(hole => hole.EndCondition is not AirHoleEndCondition.ThroughAll || hole.Axis.Direction.Z < 1d - 1e-9))
            return correspondence;
        var top = FirmamentSelectorResolver.ResolveSupportPlaneFaces(body, "top_face");
        var bottom = FirmamentSelectorResolver.ResolveSupportPlaneFaces(body, "bottom_face");
        if (top.Count != 1 || bottom.Count != 1 || body.Topology.GetFace(top[0]).LoopIds.Count != holes.Count + 1
            || body.Topology.GetFace(bottom[0]).LoopIds.Count != holes.Count + 1)
            return correspondence;
        var stock = BoxConstructionCorrespondence.RetainedCaps(solid.Name, bottom[0], top[0], body);
        if (stock.Count != 6) return correspondence;
        correspondence ??= new(solid.Name, stock, ["RetainedBoxCaps"]);
        return BoxConstructionCorrespondence.WithStockEdges(correspondence, body, box.Size);
    }
}
