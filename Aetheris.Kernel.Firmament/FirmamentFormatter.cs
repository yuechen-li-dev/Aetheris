using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Formatting;
using Aetheris.Kernel.Firmament.Parsing;
using Aetheris.Kernel.Firmament.Compatibility.V1;

namespace Aetheris.Kernel.Firmament;

public sealed class FirmamentFormatter
{
    public FirmamentFormatResult Format(FirmamentFormatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parseResult = new LegacyFirmamentV1SourceReader().ReadAuto(request.Document.SourceText);
        if (!parseResult.IsSuccess)
        {
            return new FirmamentFormatResult(
                KernelResult<FirmamentFormattedDocument>.Failure(parseResult.Diagnostics));
        }

        var formattedText = new FirmamentV1ToonWriter().Write(parseResult.Value);
        return new FirmamentFormatResult(
            KernelResult<FirmamentFormattedDocument>.Success(
                new FirmamentFormattedDocument(formattedText, parseResult.Value)));
    }
}
