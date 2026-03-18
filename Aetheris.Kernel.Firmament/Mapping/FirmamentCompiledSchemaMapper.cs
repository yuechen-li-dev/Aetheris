using System.Globalization;
using Aetheris.Kernel.Firmament.CompiledModel;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Mapping;

internal static class FirmamentCompiledSchemaMapper
{
    public static FirmamentCompiledSchema? Map(FirmamentParsedSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        return schema.Process switch
        {
            FirmamentParsedSchemaProcess.Cnc => new FirmamentCompiledSchema(
                FirmamentCompiledSchemaProcess.Cnc,
                new FirmamentCompiledCncSchema(schema.MinimumToolRadius!.Value)),
            FirmamentParsedSchemaProcess.InjectionMolded => new FirmamentCompiledSchema(
                FirmamentCompiledSchemaProcess.InjectionMolded,
                new FirmamentCompiledInjectionMoldedSchema(
                    schema.PartingPlane!,
                    new FirmamentCompiledSchemaGateLocation(
                        ParseRequiredDouble(schema.GateLocation!.XRaw),
                        ParseRequiredDouble(schema.GateLocation.YRaw),
                        ParseRequiredDouble(schema.GateLocation.ZRaw)),
                    schema.DraftAngle!.Value)),
            FirmamentParsedSchemaProcess.Additive => new FirmamentCompiledSchema(
                FirmamentCompiledSchemaProcess.Additive,
                new FirmamentCompiledAdditiveSchema(schema.PrinterResolution!.Value)),
            _ => throw new InvalidOperationException($"Cannot compile unsupported schema process '{schema.Process}'.")
        };
    }

    private static double ParseRequiredDouble(string? raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException("Schema validation should guarantee numeric gate location coordinates.");
        }

        return value;
    }
}
