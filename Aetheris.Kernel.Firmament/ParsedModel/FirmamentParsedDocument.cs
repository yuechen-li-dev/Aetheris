namespace Aetheris.Kernel.Firmament.ParsedModel;

public sealed record FirmamentParsedDocument(
    FirmamentParsedHeader Firmament,
    FirmamentParsedModelHeader Model,
    FirmamentParsedOpsSection Ops,
    FirmamentParsedSchema? Schema,
    bool HasPmi);

public sealed record FirmamentParsedHeader(string Version);

public sealed record FirmamentParsedModelHeader(string Name, string Units);

public sealed record FirmamentParsedOpsSection(IReadOnlyList<FirmamentParsedOpEntry> Entries);

public sealed record FirmamentParsedOpEntry(
    string OpName,
    FirmamentKnownOpKind KnownKind,
    FirmamentOpFamily Family,
    IReadOnlyDictionary<string, string> RawFields,
    FirmamentParsedPlacement? Placement = null,
    IReadOnlyDictionary<string, string>? ClassifiedFields = null);
