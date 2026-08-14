using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Surgery;

/// <summary>
/// Reuses the canonical binding/topology validators and adds the finite vertex
/// geometry invariant required at the Surgery boundary.
/// </summary>
internal static class BrepSurgeryValidation
{
    public static KernelResult<bool> ValidateBody(BrepBody body, bool requireAllEdgeAndFaceBindings = true)
    {
        ArgumentNullException.ThrowIfNull(body);
        var diagnostics = new List<KernelDiagnostic>();
        var bindingValidation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings);
        diagnostics.AddRange(bindingValidation.Diagnostics);

        foreach (var vertex in body.Topology.Vertices)
        {
            if (!body.TryGetVertexPoint(vertex.Id, out var point)
                || !double.IsFinite(point.X)
                || !double.IsFinite(point.Y)
                || !double.IsFinite(point.Z))
            {
                diagnostics.Add(new KernelDiagnostic(
                    KernelDiagnosticCode.ValidationFailed,
                    KernelDiagnosticSeverity.Error,
                    $"Surgery body vertex {vertex.Id.Value} requires a finite geometry point.",
                    "Brep.Surgery.Validation"));
            }
        }

        return diagnostics.Any(diagnostic => diagnostic.Severity == KernelDiagnosticSeverity.Error)
            ? KernelResult<bool>.Failure(diagnostics)
            : KernelResult<bool>.Success(true, diagnostics);
    }
}
