using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Formatting;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Parsing;

namespace Aetheris.Kernel.Firmament.Compatibility.V1;

/// <summary>Strict reader for the historical Firmament V1 TOON serialization.</summary>
public sealed class FirmamentV1ToonReader
{
    public KernelResult<FirmamentParsedDocument> Read(string sourceText) => FirmamentV1CodecContract.RequireV1(FirmamentTopLevelParser.ReadToon(sourceText));
}

/// <summary>Strict reader for the historical Firmament V1 JSON serialization.</summary>
public sealed class FirmamentV1JsonReader
{
    public KernelResult<FirmamentParsedDocument> Read(string sourceText) => FirmamentV1CodecContract.RequireV1(FirmamentTopLevelParser.ReadJson(sourceText));
}

/// <summary>
/// Historical compatibility adapter for callers that did not record whether their V1
/// source was JSON or TOON. Automatic sniffing is not a Firmament V2 source feature.
/// </summary>
public sealed class LegacyFirmamentV1SourceReader
{
    public KernelResult<FirmamentParsedDocument> ReadAuto(string sourceText) => FirmamentV1CodecContract.RequireV1(FirmamentTopLevelParser.ReadAuto(sourceText));
}

internal static class FirmamentV1CodecContract
{
    public static KernelResult<FirmamentParsedDocument> RequireV1(KernelResult<FirmamentParsedDocument> decoded)
    {
        if (!decoded.IsSuccess || string.Equals(decoded.Value.Firmament.Version, "1", StringComparison.Ordinal)) return decoded;
        return KernelResult<FirmamentParsedDocument>.Failure([
            new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(
                Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
                Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                $"Firmament V1 compatibility input must declare version '1'; found '{decoded.Value.Firmament.Version}'.",
                "FirmamentV1.CompatibilityReader")
        ]);
    }
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
