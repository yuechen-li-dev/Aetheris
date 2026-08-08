using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Compatibility bridge from the parser-owned Firmament V2 primitive AST to the existing lowered primitive records.
/// This intentionally reuses the production primitive executor and STEP back half without making V2 semantics owned by V1 lowering.
/// </summary>
internal static class FirmamentV2BuildLowering
{
    public static KernelResult<FirmamentPrimitiveLoweringPlan> LowerPrimitiveBridge(FirmamentV2Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.ModifyBlocks is { Count: > 0 })
        {
            return Failure("Firmament V2 build/export currently admits only a single primitive solid with no modify blocks.");
        }

        var solid = document.Solid;
        var primitive = ToLoweredPrimitive(solid);
        if (primitive is null)
        {
            return Failure($"Firmament V2 build/export does not admit primitive record '{solid.RecordType}'.");
        }

        return KernelResult<FirmamentPrimitiveLoweringPlan>.Success(new FirmamentPrimitiveLoweringPlan([primitive], [], []));
    }

    private static FirmamentLoweredPrimitive? ToLoweredPrimitive(FirmamentV2SolidBinding solid)
    {
        var lowered = solid.Primitive switch
        {
            FirmamentV2BoxRecord box when box.Size.Count == 3 => (FirmamentLoweredPrimitiveKind.Box, (FirmamentLoweredPrimitiveParameters)new FirmamentLoweredBoxParameters(box.Size[0], box.Size[1], box.Size[2])),
            FirmamentV2CylinderRecord cylinder => (FirmamentLoweredPrimitiveKind.Cylinder, new FirmamentLoweredCylinderParameters(cylinder.Radius, cylinder.Height)),
            FirmamentV2FrustumRecord frustum => (FirmamentLoweredPrimitiveKind.Cone, new FirmamentLoweredConeParameters(frustum.BottomRadius, frustum.TopRadius, frustum.Height)),
            FirmamentV2ConeRecord cone => (FirmamentLoweredPrimitiveKind.Cone, new FirmamentLoweredConeParameters(cone.BottomRadius, cone.TopRadius, cone.Height)),
            FirmamentV2SphereRecord sphere => (FirmamentLoweredPrimitiveKind.Sphere, new FirmamentLoweredSphereParameters(sphere.Radius)),
            FirmamentV2TorusRecord torus => (FirmamentLoweredPrimitiveKind.Torus, new FirmamentLoweredTorusParameters(torus.MajorRadius, torus.MinorRadius)),
            _ => default
        };

        return lowered == default
            ? null
            : new FirmamentLoweredPrimitive(0, solid.Name, lowered.Item1, lowered.Item2, null);
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
