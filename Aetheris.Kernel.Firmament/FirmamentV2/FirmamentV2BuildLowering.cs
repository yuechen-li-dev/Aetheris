using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal static class FirmamentV2BuildLowering
{
    public static KernelResult<FirmamentPrimitiveLoweringPlan> LowerBoxOnly(FirmamentV2Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.ModifyBlocks is { Count: > 0 })
        {
            return Failure("Firmament V2 build/export currently admits only a single Box solid with no modify blocks.");
        }

        if (document.Solids.Count != 1)
        {
            return Failure("Firmament V2 build/export currently admits exactly one Box solid.");
        }

        var solid = document.Solid;
        if (!string.Equals(solid.RecordType, "Box", StringComparison.Ordinal) || solid.Box.Size.Count != 3)
        {
            return Failure("Firmament V2 build/export currently admits only Box solids.");
        }

        var primitive = new FirmamentLoweredPrimitive(
            OpIndex: 0,
            FeatureId: solid.Name,
            Kind: FirmamentLoweredPrimitiveKind.Box,
            Parameters: new FirmamentLoweredBoxParameters(solid.Box.Size[0], solid.Box.Size[1], solid.Box.Size[2]),
            Placement: null);

        return KernelResult<FirmamentPrimitiveLoweringPlan>.Success(new FirmamentPrimitiveLoweringPlan([primitive], [], []));
    }

    private static KernelResult<FirmamentPrimitiveLoweringPlan> Failure(string message) =>
        KernelResult<FirmamentPrimitiveLoweringPlan>.Failure(
        [
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                FirmamentDiagnosticConventions.Source)
        ]);
}
