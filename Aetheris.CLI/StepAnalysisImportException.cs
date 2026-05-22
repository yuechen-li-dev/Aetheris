using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.CLI;

public sealed class StepAnalysisImportException : InvalidOperationException
{
    public StepAnalysisImportException(string stepPath, IReadOnlyList<KernelDiagnostic> diagnostics)
        : base(FormatMessage(stepPath, diagnostics))
    {
        StepPath = stepPath;
        Diagnostics = diagnostics;
    }

    public string StepPath { get; }

    public IReadOnlyList<KernelDiagnostic> Diagnostics { get; }

    private static string FormatMessage(string stepPath, IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        var lines = diagnostics.Select(d => $"[{d.Severity}] {d.Source}: {d.Message}");
        return $"Failed to analyze STEP '{stepPath}':{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }
}
