using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentPrimitiveExecutor
{
    public static KernelResult<FirmamentPrimitiveExecutionResult> Execute(FirmamentPrimitiveLoweringPlan loweringPlan)
    {
        ArgumentNullException.ThrowIfNull(loweringPlan);

        var executedPrimitives = new List<FirmamentExecutedPrimitive>(loweringPlan.Primitives.Count);

        foreach (var primitive in loweringPlan.Primitives.OrderBy(p => p.OpIndex))
        {
            var bodyResult = ExecutePrimitive(primitive);
            if (!bodyResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(bodyResult.Diagnostics);
            }

            executedPrimitives.Add(new FirmamentExecutedPrimitive(
                OpIndex: primitive.OpIndex,
                FeatureId: primitive.FeatureId,
                Kind: primitive.Kind,
                Body: bodyResult.Value));
        }

        return KernelResult<FirmamentPrimitiveExecutionResult>.Success(new FirmamentPrimitiveExecutionResult(executedPrimitives));
    }

    private static KernelResult<BrepBody> ExecutePrimitive(FirmamentLoweredPrimitive primitive)
    {
        return primitive.Kind switch
        {
            FirmamentLoweredPrimitiveKind.Box => ExecuteBox(primitive),
            FirmamentLoweredPrimitiveKind.Cylinder => ExecuteCylinder(primitive),
            FirmamentLoweredPrimitiveKind.Sphere => ExecuteSphere(primitive),
            _ => KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    $"Primitive execution for kind '{primitive.Kind}' is not implemented.")])
        };
    }

    private static KernelResult<BrepBody> ExecuteBox(FirmamentLoweredPrimitive primitive)
    {
        var parameters = (FirmamentLoweredBoxParameters)primitive.Parameters;
        return BrepPrimitives.CreateBox(parameters.SizeX, parameters.SizeY, parameters.SizeZ);
    }

    private static KernelResult<BrepBody> ExecuteCylinder(FirmamentLoweredPrimitive primitive)
    {
        var parameters = (FirmamentLoweredCylinderParameters)primitive.Parameters;
        return BrepPrimitives.CreateCylinder(parameters.Radius, parameters.Height);
    }

    private static KernelResult<BrepBody> ExecuteSphere(FirmamentLoweredPrimitive primitive)
    {
        var parameters = (FirmamentLoweredSphereParameters)primitive.Parameters;
        return BrepPrimitives.CreateSphere(parameters.Radius);
    }
}
