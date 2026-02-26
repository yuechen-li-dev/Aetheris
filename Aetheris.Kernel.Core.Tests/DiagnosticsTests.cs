using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Core.Tests;

public sealed class DiagnosticsTests
{
    [Fact]
    public void CanCreateDiagnostic_WithCodeSeverityMessageAndSource()
    {
        var diagnostic = new KernelDiagnostic(
            KernelDiagnosticCode.InvalidArgument,
            KernelDiagnosticSeverity.Warning,
            "Input radius was clamped.",
            "FakeOperation");

        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Input radius was clamped.", diagnostic.Message);
        Assert.Equal("FakeOperation", diagnostic.Source);
    }

    [Fact]
    public void DiagnosticIsImmutable_RecordWithExpressionCreatesNewInstance()
    {
        var original = new KernelDiagnostic(
            KernelDiagnosticCode.Unknown,
            KernelDiagnosticSeverity.Info,
            "Original message");

        var changed = original with { Message = "Changed message" };

        Assert.Equal("Original message", original.Message);
        Assert.Equal("Changed message", changed.Message);
        Assert.NotEqual(original, changed);
    }
}
