namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2Document(string ModelName, string Units, FirmamentV2SolidBinding Solid);
public sealed record FirmamentV2SolidBinding(string Name, string RecordType, FirmamentV2BoxRecord Box);
public sealed record FirmamentV2BoxRecord(IReadOnlyList<double> Size);
public sealed record FirmamentV2ParseResult(bool IsSuccess, FirmamentV2Document? Document, IReadOnlyList<string> Diagnostics)
{
    public static FirmamentV2ParseResult Success(FirmamentV2Document document, IReadOnlyList<string> diagnostics) => new(true, document, diagnostics);
    public static FirmamentV2ParseResult Failure(IReadOnlyList<string> diagnostics) => new(false, null, diagnostics);
}
