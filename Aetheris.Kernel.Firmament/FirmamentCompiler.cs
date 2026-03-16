using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;
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

        var validationValidationResult = FirmamentValidationRequiredFieldValidator.Validate(parseResult.Value);
        if (!validationValidationResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(validationValidationResult.Diagnostics));
        }

        var targetShapeValidationResult = FirmamentValidationTargetShapeValidator.Validate(parseResult.Value);
        if (!targetShapeValidationResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(targetShapeValidationResult.Diagnostics));
        }

        var documentCoherenceValidationResult = FirmamentDocumentCoherenceValidator.Validate(targetShapeValidationResult.Value);
        if (!documentCoherenceValidationResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(documentCoherenceValidationResult.Diagnostics));
        }

        var validatedDocument = documentCoherenceValidationResult.Value;

        var primitiveLoweringResult = FirmamentPrimitiveLowerer.Lower(validatedDocument);
        if (!primitiveLoweringResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(primitiveLoweringResult.Diagnostics));
        }

        var primitiveExecutionResult = FirmamentPrimitiveExecutor.Execute(primitiveLoweringResult.Value);
        if (!primitiveExecutionResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(primitiveExecutionResult.Diagnostics));
        }

        var validationExecutionResult = FirmamentValidationExecutor.Execute(validatedDocument, primitiveExecutionResult.Value);
        if (!validationExecutionResult.IsSuccess)
        {
            return new FirmamentCompileResult(
                KernelResult<FirmamentCompilationArtifact>.Failure(validationExecutionResult.Diagnostics));
        }

        return new FirmamentCompileResult(
            KernelResult<FirmamentCompilationArtifact>.Success(
                new FirmamentCompilationArtifact(
                    ArtifactKind: "firmament-topology-exists-validation-executed",
                    ParsedDocument: validatedDocument,
                    PrimitiveLoweringPlan: primitiveLoweringResult.Value,
                    PrimitiveExecutionResult: primitiveExecutionResult.Value,
                    ValidationExecutionResult: validationExecutionResult.Value)));
    }
}
