using System.Globalization;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentSchemaValidator
{
    public static KernelResult<bool> Validate(FirmamentParsedDocument parsedDocument)
    {
        ArgumentNullException.ThrowIfNull(parsedDocument);

        var schema = parsedDocument.Schema;
        if (schema is null)
        {
            return KernelResult<bool>.Success(true);
        }

        if (!schema.IsObjectLike)
        {
            return InvalidFieldTypeOrShape("schema", "expected an object-like mapping block");
        }

        if (string.IsNullOrWhiteSpace(schema.ProcessRaw))
        {
            return MissingField("process");
        }

        if (schema.Process == FirmamentParsedSchemaProcess.Unknown)
        {
            return UnknownProcess(schema.ProcessRaw!);
        }

        return schema.Process switch
        {
            FirmamentParsedSchemaProcess.Cnc => ValidateCnc(schema),
            FirmamentParsedSchemaProcess.InjectionMolded => ValidateInjectionMolded(schema),
            FirmamentParsedSchemaProcess.Additive => ValidateAdditive(schema),
            _ => UnknownProcess(schema.ProcessRaw!)
        };
    }

    private static KernelResult<bool> ValidateCnc(FirmamentParsedSchema schema)
    {
        if (string.IsNullOrWhiteSpace(schema.MinimumToolRadiusRaw))
        {
            return MissingField("minimum_tool_radius");
        }

        if (schema.MinimumToolRadius is null)
        {
            return InvalidFieldTypeOrShape("minimum_tool_radius", "expected a numeric scalar");
        }

        if (schema.MinimumToolRadius <= 0)
        {
            return InvalidFieldValue("minimum_tool_radius", "expected a numeric value greater than 0");
        }

        if (string.IsNullOrWhiteSpace(schema.MinimumWallThicknessRaw))
        {
            return MissingField("minimum_wall_thickness");
        }

        if (schema.MinimumWallThickness is null)
        {
            return InvalidFieldTypeOrShape("minimum_wall_thickness", "expected a numeric scalar");
        }

        if (schema.MinimumWallThickness <= 0)
        {
            return InvalidFieldValue("minimum_wall_thickness", "expected a numeric value greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateInjectionMolded(FirmamentParsedSchema schema)
    {
        if (string.IsNullOrWhiteSpace(schema.PartingPlane))
        {
            return MissingField("parting_plane");
        }

        if (schema.PartingPlane is not ("xy" or "yz" or "xz"))
        {
            return InvalidFieldValue("parting_plane", "expected one of 'xy', 'yz', or 'xz'");
        }

        if (!schema.HasGateLocation)
        {
            return MissingField("gate_location");
        }

        if (!schema.GateLocationIsObjectLike || schema.GateLocation is null)
        {
            return InvalidFieldTypeOrShape("gate_location", "expected an object-like mapping with numeric fields 'x', 'y', and 'z'");
        }

        if (!TryParseRequiredNumeric(schema.GateLocation.XRaw, out _))
        {
            return InvalidFieldTypeOrShape("gate_location.x", "expected a numeric scalar");
        }

        if (!TryParseRequiredNumeric(schema.GateLocation.YRaw, out _))
        {
            return InvalidFieldTypeOrShape("gate_location.y", "expected a numeric scalar");
        }

        if (!TryParseRequiredNumeric(schema.GateLocation.ZRaw, out _))
        {
            return InvalidFieldTypeOrShape("gate_location.z", "expected a numeric scalar");
        }

        if (string.IsNullOrWhiteSpace(schema.DraftAngleRaw))
        {
            return MissingField("draft_angle");
        }

        if (schema.DraftAngle is null)
        {
            return InvalidFieldTypeOrShape("draft_angle", "expected a numeric scalar");
        }

        if (schema.DraftAngle <= 0)
        {
            return InvalidFieldValue("draft_angle", "expected a numeric value greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static KernelResult<bool> ValidateAdditive(FirmamentParsedSchema schema)
    {
        if (string.IsNullOrWhiteSpace(schema.PrinterResolutionRaw))
        {
            return MissingField("printer_resolution");
        }

        if (schema.PrinterResolution is null)
        {
            return InvalidFieldTypeOrShape("printer_resolution", "expected a numeric scalar");
        }

        if (schema.PrinterResolution <= 0)
        {
            return InvalidFieldValue("printer_resolution", "expected a numeric value greater than 0");
        }

        return KernelResult<bool>.Success(true);
    }

    private static bool TryParseRequiredNumeric(string? raw, out double value)
    {
        value = default;
        return !string.IsNullOrWhiteSpace(raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static KernelResult<bool> UnknownProcess(string process) =>
        KernelResult<bool>.Failure([
            CreateDiagnostic(
                FirmamentDiagnosticCodes.SchemaUnknownProcess,
                $"Schema has unknown process '{process}'.")]);

    private static KernelResult<bool> MissingField(string fieldName) =>
        KernelResult<bool>.Failure([
            CreateDiagnostic(
                FirmamentDiagnosticCodes.SchemaMissingRequiredField,
                $"Schema is missing required field '{fieldName}'.")]);

    private static KernelResult<bool> InvalidFieldTypeOrShape(string fieldName, string expectation) =>
        KernelResult<bool>.Failure([
            CreateDiagnostic(
                FirmamentDiagnosticCodes.SchemaInvalidFieldTypeOrShape,
                $"Schema field '{fieldName}' has invalid type/shape; {expectation}.")]);

    private static KernelResult<bool> InvalidFieldValue(string fieldName, string expectation) =>
        KernelResult<bool>.Failure([
            CreateDiagnostic(
                FirmamentDiagnosticCodes.SchemaInvalidFieldValue,
                $"Schema field '{fieldName}' has invalid value; {expectation}.")]);

    private static KernelDiagnostic CreateDiagnostic(FirmamentDiagnosticCode code, string message) =>
        new(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] {message}",
            Source: FirmamentDiagnosticConventions.Source);
}
