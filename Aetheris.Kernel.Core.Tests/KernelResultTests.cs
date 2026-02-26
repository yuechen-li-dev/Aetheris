using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Tests;

public sealed class KernelResultTests
{
    [Fact]
    public void SuccessResult_ExposesValueAndIsSuccessTrue()
    {
        var result = FakeOperation(success: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void SuccessResult_CanCarryWarningOrInfoDiagnostics()
    {
        var result = FakeOperation(
            success: true,
            diagnostics:
            [
                new KernelDiagnostic(KernelDiagnosticCode.Unknown, KernelDiagnosticSeverity.Info, "FYI"),
                new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning, "Something to review"),
            ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Error);
    }

    [Fact]
    public void FailureResult_HasIsSuccessFalseAndErrorDiagnostic()
    {
        var result = FakeOperation(
            success: false,
            diagnostics:
            [
                new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, "Input invalid")
            ]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Error);
    }

    [Fact]
    public void FailureResult_ValueAccessThrowsAndTryGetValueReturnsFalse()
    {
        var result = FakeOperation(success: false);

        var exception = Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        Assert.Equal("Cannot access Value when the result is a failure.", exception.Message);

        var gotValue = result.TryGetValue(out var value);

        Assert.False(gotValue);
        Assert.Equal(default, value);
    }

    [Fact]
    public void DiagnosticsCollection_CannotBeMutatedExternally()
    {
        var sourceDiagnostics = new List<KernelDiagnostic>
        {
            new(KernelDiagnosticCode.Unknown, KernelDiagnosticSeverity.Warning, "before")
        };

        var result = KernelResult<int>.Success(1, sourceDiagnostics);
        sourceDiagnostics.Add(new KernelDiagnostic(KernelDiagnosticCode.Unknown, KernelDiagnosticSeverity.Warning, "after"));

        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void CreatingSuccessWithErrorDiagnostic_IsRejected()
    {
        var diagnostics = new[]
        {
            new KernelDiagnostic(KernelDiagnosticCode.InternalError, KernelDiagnosticSeverity.Error, "nope"),
        };

        var exception = Assert.Throws<ArgumentException>(() => KernelResult<int>.Success(1, diagnostics));

        Assert.Equal("diagnostics", exception.ParamName);
    }

    [Fact]
    public void CreatingFailureWithNoDiagnostics_AutoPopulatesInternalError()
    {
        var result = KernelResult<int>.Failure();

        Assert.False(result.IsSuccess);
        Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InternalError, result.Diagnostics[0].Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, result.Diagnostics[0].Severity);
    }

    [Fact]
    public void CreatingFailureWithoutErrorDiagnostic_NormalizesByAppendingInternalError()
    {
        var result = KernelResult<int>.Failure(
        [
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning, "warning only")
        ]);

        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, d => d.Severity == KernelDiagnosticSeverity.Error && d.Code == KernelDiagnosticCode.InternalError);
    }

    private static KernelResult<int> FakeOperation(bool success, IReadOnlyList<KernelDiagnostic>? diagnostics = null)
    {
        if (success)
        {
            return KernelResult<int>.Success(42, diagnostics);
        }

        return KernelResult<int>.Failure(diagnostics);
    }
}
