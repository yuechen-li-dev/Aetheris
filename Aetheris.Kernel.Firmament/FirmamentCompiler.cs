using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament;

public sealed class FirmamentCompiler
{
    public FirmamentCompileResult Compile(FirmamentCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostic = new KernelDiagnostic(
            KernelDiagnosticCode.NotImplemented,
            KernelDiagnosticSeverity.Error,
            "Firmament compilation is not implemented yet (pre-M0 scaffold).",
            Source: FirmamentDiagnosticConventions.Source);

        return new FirmamentCompileResult(
            KernelResult<FirmamentCompilationArtifact>.Failure([diagnostic]));
    }
}
