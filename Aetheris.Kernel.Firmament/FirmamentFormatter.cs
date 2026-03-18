using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Formatting;
using Aetheris.Kernel.Firmament.Parsing;

namespace Aetheris.Kernel.Firmament;

public sealed class FirmamentFormatter
{
    public FirmamentFormatResult Format(FirmamentFormatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parseResult = FirmamentTopLevelParser.Parse(request.Document.SourceText);
        if (!parseResult.IsSuccess)
        {
            return new FirmamentFormatResult(
                KernelResult<FirmamentFormattedDocument>.Failure(parseResult.Diagnostics));
        }

        var formattedText = FirmamentCanonicalFormatter.Format(parseResult.Value);
        return new FirmamentFormatResult(
            KernelResult<FirmamentFormattedDocument>.Success(
                new FirmamentFormattedDocument(formattedText, parseResult.Value)));
    }
}
