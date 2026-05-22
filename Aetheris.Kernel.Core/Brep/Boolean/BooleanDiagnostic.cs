using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public enum BooleanDiagnosticCode
{
    AxisNotAligned,
    NotFullySpanning,
    DegenerateBoundarySection,
    RadiusExceedsBoundary,
    TangentContact,
    MultiBodyResult,
    HoleInterference,
    UnsupportedAnalyticSurfaceKind,
    UnsupportedBlindHoleComposition
}

public sealed record BooleanDiagnostic(
    BooleanDiagnosticCode Code,
    string Message,
    string Source)
{
    public KernelDiagnostic ToKernelDiagnostic()
        => new(
            KernelDiagnosticCode.NotImplemented,
            KernelDiagnosticSeverity.Error,
            Message,
            Source);
}
