using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static KernelResult<FirmamentStepExportResult> ExportV2BoxFaceBoundaryFinish(
        FirmamentV2Document document, FirmamentV2SolidBinding solid, FirmamentV2EdgeFinishDecl finish, FirmamentV2BoxRecord box)
    {
        var x = box.Size[0]; var y = box.Size[1]; var z = box.Size[2];
        // Proper signed permutations preserve support/curve parameters and shell
        // orientation. Canonical +Z keeps its established byte-stable route.
        (double Width, double Depth, double Height, double[]? Matrix) frame = finish.FaceAxis switch
        {
            "-Z" => (x, y, z, new double[] { 1,0,0,0, 0,-1,0,0, 0,0,-1,0, 0,0,z,1 }),
            "+X" => (y, z, x, new double[] { 0,1,0,0, 0,0,1,0, 1,0,0,0, -x/2,0,z/2,1 }),
            "-X" => (y, z, x, new double[] { 0,-1,0,0, 0,0,1,0, -1,0,0,0, x/2,0,z/2,1 }),
            "+Y" => (x, z, y, new double[] { -1,0,0,0, 0,0,1,0, 0,1,0,0, 0,-y/2,z/2,1 }),
            "-Y" => (x, z, y, new double[] { 1,0,0,0, 0,0,1,0, 0,-1,0,0, 0,y/2,z/2,1 }),
            _ => default
        };
        if (frame.Matrix is null)
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
}
