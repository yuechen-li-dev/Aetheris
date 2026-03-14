using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Parsing;

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

        return new FirmamentCompileResult(
            KernelResult<FirmamentCompilationArtifact>.Success(
                new FirmamentCompilationArtifact(ParsedDocument: parseResult.Value)));
    }
}
