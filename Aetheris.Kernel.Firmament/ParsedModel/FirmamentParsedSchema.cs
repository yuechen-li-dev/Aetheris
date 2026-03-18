namespace Aetheris.Kernel.Firmament.ParsedModel;

public sealed record FirmamentParsedSchema(
    bool IsObjectLike,
    string? ProcessRaw,
    FirmamentParsedSchemaProcess Process,
    string? MinimumToolRadiusRaw = null,
    double? MinimumToolRadius = null,
    string? PartingPlane = null,
    bool HasGateLocation = false,
    bool GateLocationIsObjectLike = false,
    FirmamentParsedSchemaGateLocation? GateLocation = null,
    string? DraftAngleRaw = null,
    double? DraftAngle = null,
    string? PrinterResolutionRaw = null,
    double? PrinterResolution = null);

public enum FirmamentParsedSchemaProcess
{
    Cnc,
    InjectionMolded,
    Additive,
    Unknown
}

public sealed record FirmamentParsedSchemaGateLocation(string? XRaw, string? YRaw, string? ZRaw);
