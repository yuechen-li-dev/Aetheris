namespace Aetheris.Kernel.Firmament.ParsedModel;

public sealed record FirmamentParsedDocument(
    FirmamentParsedHeader Firmament,
    FirmamentParsedModelHeader Model,
    FirmamentParsedOpsSection Ops,
    bool HasSchema,
    bool HasPmi);

public sealed record FirmamentParsedHeader(string Version);

public sealed record FirmamentParsedModelHeader(string Name, string Units);

public sealed record FirmamentParsedOpsSection(string Shape);
