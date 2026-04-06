namespace Aetheris.Kernel.Firmament.ParsedModel;

public sealed record FirmamentParsedPmiSection(
    bool IsPresent,
    IReadOnlyList<FirmamentParsedPmiEntry> Entries);

public sealed record FirmamentParsedPmiEntry(
    string KindRaw,
    FirmamentParsedPmiKind Kind,
    IReadOnlyDictionary<string, string> RawFields);

public enum FirmamentParsedPmiKind
{
    Unknown = 0,
    Hole = 1,
    Datum = 2,
    Note = 3
}
