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
    /// <summary>An already mapped span in the authored input. Unlike SourceSpan,
    /// presentation must not remap this through a different frontend's expansion.</summary>
    public KernelDiagnosticSourceSpan? OriginalSourceSpan { get; init; }
}

public sealed record KernelDiagnosticSourceSpan(int Start, int Length)
{
    public string? LocationNote { get; init; }
}
