namespace Aetheris.Kernel.Core.Diagnostics;

public sealed record KernelDiagnostic(
    KernelDiagnosticCode Code,
    KernelDiagnosticSeverity Severity,
    string Message,
    string? Source = null);
