using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Core.Results;

public sealed class KernelResult<T>
{
    private static readonly IReadOnlyList<KernelDiagnostic> EmptyDiagnostics = Array.Empty<KernelDiagnostic>();

    private readonly T? _value;

    private KernelResult(bool isSuccess, T? value, IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        IsSuccess = isSuccess;
        _value = value;
        Diagnostics = diagnostics;
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<KernelDiagnostic> Diagnostics { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value when the result is a failure.");

    public static KernelResult<T> Success(T value, IEnumerable<KernelDiagnostic>? diagnostics = null)
    {
        var materializedDiagnostics = MaterializeDiagnostics(diagnostics);

        if (materializedDiagnostics.Any(d => d.Severity == KernelDiagnosticSeverity.Error))
        {
            throw new ArgumentException("Success results cannot include Error diagnostics.", nameof(diagnostics));
        }

        return new KernelResult<T>(isSuccess: true, value, materializedDiagnostics);
    }

    public static KernelResult<T> Failure(IEnumerable<KernelDiagnostic>? diagnostics = null)
    {
        var materializedDiagnostics = MaterializeDiagnostics(diagnostics);

        if (materializedDiagnostics.Count == 0)
        {
            materializedDiagnostics =
            [
                new KernelDiagnostic(
                    KernelDiagnosticCode.InternalError,
                    KernelDiagnosticSeverity.Error,
                    "Operation failed without explicit diagnostics.")
            ];
        }
        else if (!materializedDiagnostics.Any(d => d.Severity == KernelDiagnosticSeverity.Error))
        {
            materializedDiagnostics =
            [
                .. materializedDiagnostics,
                new KernelDiagnostic(
                    KernelDiagnosticCode.InternalError,
                    KernelDiagnosticSeverity.Error,
                    "Operation failed without an Error diagnostic; an InternalError diagnostic was added.")
            ];
        }

        return new KernelResult<T>(isSuccess: false, value: default, materializedDiagnostics);
    }

    public bool TryGetValue(out T value)
    {
        if (IsSuccess)
        {
            value = _value!;
            return true;
        }

        value = default!;
        return false;
    }

    private static IReadOnlyList<KernelDiagnostic> MaterializeDiagnostics(IEnumerable<KernelDiagnostic>? diagnostics)
    {
        if (diagnostics is null)
        {
            return EmptyDiagnostics;
        }

        var list = diagnostics.ToArray();
        return list;
    }
}
