using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentFormatResult(KernelResult<FirmamentFormattedDocument> Formatting);

public sealed record FirmamentFormattedDocument(string Text, ParsedModel.FirmamentParsedDocument ParsedDocument);
