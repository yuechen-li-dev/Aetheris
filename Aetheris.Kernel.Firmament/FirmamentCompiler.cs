using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Parsing;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament;

public sealed class FirmamentCompiler
{
    public FirmamentCompileResult Compile(FirmamentCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parseResult = FirmamentTopLevelParser.Parse(request.Document.SourceText);
        if (!parseResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(parseResult.Diagnostics));
        }

        var primitiveValidationResult = FirmamentPrimitiveRequiredFieldValidator.Validate(parseResult.Value);
        if (!primitiveValidationResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(primitiveValidationResult.Diagnostics));
        }

        var booleanValidationResult = FirmamentBooleanRequiredFieldValidator.Validate(parseResult.Value);
        if (!booleanValidationResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(booleanValidationResult.Diagnostics));
        }

        return new FirmamentCompileResult(
            KernelResult<FirmamentCompilationArtifact>.Success(
                new FirmamentCompilationArtifact(
                    ArtifactKind: "firmament-ops-primitive-and-boolean-required-fields-validated",
                    ParsedDocument: parseResult.Value)));
    }
}
