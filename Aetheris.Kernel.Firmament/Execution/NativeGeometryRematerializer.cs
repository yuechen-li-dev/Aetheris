using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class NativeGeometryRematerializer
{
    internal static KernelResult<NativeGeometryState> TryRematerialize(FirmamentPrimitiveLoweringPlan loweringPlan, NativeGeometryState state)
    {
        ArgumentNullException.ThrowIfNull(loweringPlan);
        ArgumentNullException.ThrowIfNull(state);

        if (state.ExecutionMode != NativeGeometryExecutionMode.CirOnly)
        {
            return KernelResult<NativeGeometryState>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Rematerialization requires CirOnly state.", "Firmament.CirRematerializer")]);
        }

        var lowered = FirmamentCirLowerer.Lower(loweringPlan);
        if (!lowered.IsSuccess)
        {
            return KernelResult<NativeGeometryState>.Failure(lowered.Diagnostics);
        }

        var materialized = CirBrepMaterializer.TryMaterialize(new CirBrepMaterializer.CirBrepMaterializerContext(lowered.Value.Root, state.ReplayLog));
        if (!materialized.IsSuccess || materialized.Body is null)
        {
            var diagnostics = materialized.Diagnostics.Count > 0
                ? materialized.Diagnostics
                : [new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, $"CIR rematerialization unsupported for pattern '{materialized.PatternName}': {materialized.Message}", "Firmament.CirRematerializer")];
            return KernelResult<NativeGeometryState>.Failure(diagnostics);
        }

        return KernelResult<NativeGeometryState>.Success(state with
        {
            ExecutionMode = NativeGeometryExecutionMode.BRepActive,
            MaterializationAuthority = NativeGeometryMaterializationAuthority.BRepAuthoritative,
            MaterializedBody = materialized.Body,
            TransitionEvents =
            [
                .. state.TransitionEvents,
                new NativeGeometryTransitionEvent(NativeGeometryExecutionMode.CirOnly, NativeGeometryExecutionMode.BRepActive, state.CirIntentRootReference, null, NativeGeometryTransitionReasonCategory.MaterializationUnsupported, $"CIR rematerialized via pattern '{materialized.PatternName}'.")
            ]
        });
    }
}
