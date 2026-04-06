namespace Aetheris.Kernel.Firmament.CompiledModel;

public sealed record FirmamentCompiledSchema(
    FirmamentCompiledSchemaProcess Process,
    FirmamentCompiledSchemaPayload Payload);

public enum FirmamentCompiledSchemaProcess
{
    Cnc,
    InjectionMolded,
    Additive
}

public abstract record FirmamentCompiledSchemaPayload;

public sealed record FirmamentCompiledCncSchema(double MinimumToolRadius, double MinimumWallThickness)
    : FirmamentCompiledSchemaPayload;

public sealed record FirmamentCompiledInjectionMoldedSchema(
    string PartingPlane,
    FirmamentCompiledSchemaGateLocation GateLocation,
    double DraftAngle)
    : FirmamentCompiledSchemaPayload;

public sealed record FirmamentCompiledAdditiveSchema(double PrinterResolution)
    : FirmamentCompiledSchemaPayload;

public sealed record FirmamentCompiledSchemaGateLocation(double X, double Y, double Z);
