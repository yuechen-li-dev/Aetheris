namespace Aetheris.Kernel.Firmament.ParsedModel;

public sealed record FirmamentParsedPlacement(
    FirmamentParsedPlacementAnchor On,
    IReadOnlyList<double> Offset);

public abstract record FirmamentParsedPlacementAnchor;

public sealed record FirmamentParsedPlacementOriginAnchor : FirmamentParsedPlacementAnchor;

public sealed record FirmamentParsedPlacementSelectorAnchor(string Selector) : FirmamentParsedPlacementAnchor;
