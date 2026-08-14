using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Formatting;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Parsing;

namespace Aetheris.Kernel.Firmament.Compatibility.V1;

/// <summary>Strict reader for the historical Firmament V1 TOON serialization.</summary>
public sealed class FirmamentV1ToonReader
{
    public KernelResult<FirmamentParsedDocument> Read(string sourceText) => FirmamentTopLevelParser.ReadToon(sourceText);
}

/// <summary>Strict reader for the historical Firmament V1 JSON serialization.</summary>
public sealed class FirmamentV1JsonReader
{
    public KernelResult<FirmamentParsedDocument> Read(string sourceText) => FirmamentTopLevelParser.ReadJson(sourceText);
}

/// <summary>
/// Historical compatibility adapter for callers that did not record whether their V1
/// source was JSON or TOON. Automatic sniffing is not a Firmament V2 source feature.
/// </summary>
public sealed class LegacyFirmamentV1SourceReader
{
    public KernelResult<FirmamentParsedDocument> ReadAuto(string sourceText) => FirmamentTopLevelParser.ReadAuto(sourceText);
}

/// <summary>Deterministic LF-only writer for the historical Firmament V1 TOON form.</summary>
public sealed class FirmamentV1ToonWriter
{
    public string Write(FirmamentParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return FirmamentCanonicalFormatter.Format(document);
    }
}
