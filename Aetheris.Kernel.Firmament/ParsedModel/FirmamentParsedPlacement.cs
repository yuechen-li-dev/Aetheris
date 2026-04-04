namespace Aetheris.Kernel.Firmament.ParsedModel;

public sealed record FirmamentParsedPlacement(
    FirmamentParsedPlacementAnchor? On,
    IReadOnlyList<double> Offset,
    string? OnFace,
    string? CenteredOn,
    string? AroundAxis,
    double? RadialOffset,
    double? AngleDegrees,
    IReadOnlyList<string> UnknownFields);

public abstract record FirmamentParsedPlacementAnchor;

public sealed record FirmamentParsedPlacementOriginAnchor : FirmamentParsedPlacementAnchor;

public sealed record FirmamentParsedPlacementSelectorAnchor(string Selector) : FirmamentParsedPlacementAnchor;
