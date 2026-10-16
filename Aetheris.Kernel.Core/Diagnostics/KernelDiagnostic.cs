namespace Aetheris.Kernel.Core.Diagnostics;

public sealed record KernelDiagnostic(
    KernelDiagnosticCode Code,
    KernelDiagnosticSeverity Severity,
    string Message,
    string? Source = null)
{
    /// <summary>Offsets in the producing stage's source. Presentation must map
    /// expanded source to the authored document before displaying a location.</summary>
    public KernelDiagnosticSourceSpan? SourceSpan { get; init; }
}

public sealed record KernelDiagnosticSourceSpan(int Start, int Length);
