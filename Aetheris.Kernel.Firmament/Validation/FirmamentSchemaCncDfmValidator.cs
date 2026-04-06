using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.CompiledModel;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentSchemaCncDfmValidator
{
    public static KernelResult<bool> Validate(
        FirmamentCompiledSchema? compiledSchema,
        FirmamentPrimitiveExecutionResult? primitiveExecutionResult)
    {
        if (compiledSchema?.Process != FirmamentCompiledSchemaProcess.Cnc
            || compiledSchema.Payload is not FirmamentCompiledCncSchema cncSchema)
        {
            return KernelResult<bool>.Success(true);
        }

        var terminalBody = ResolveTerminalBody(primitiveExecutionResult);
        if (terminalBody is null)
        {
            return KernelResult<bool>.Success(true);
        }

        var schemaResult = BrepCncManufacturabilitySchema.Evaluate(
            terminalBody,
            new CncManufacturabilitySchemaInput(cncSchema.MinimumToolRadius, cncSchema.MinimumWallThickness));

        if (schemaResult.IsPass)
        {
            return KernelResult<bool>.Success(true);
        }

        var diagnostics = schemaResult.Issues
            .Where(issue => issue.Kind == CncManufacturabilityIssueKind.Violation)
            .Select(CreateIssueDiagnostic)
            .ToArray();

        if (diagnostics.Length == 0)
        {
            return KernelResult<bool>.Success(true);
        }

        return KernelResult<bool>.Failure(diagnostics);
    }

    private static BrepBody? ResolveTerminalBody(FirmamentPrimitiveExecutionResult? execution)
    {
        if (execution is null)
        {
            return null;
        }

        var terminalBoolean = execution.ExecutedBooleans
            .OrderByDescending(entry => entry.OpIndex)
            .FirstOrDefault();
        if (terminalBoolean is not null)
        {
            return terminalBoolean.Body;
        }

        return execution.ExecutedPrimitives
            .OrderByDescending(entry => entry.OpIndex)
            .FirstOrDefault()
            ?.Body;
    }

    private static KernelDiagnostic CreateIssueDiagnostic(CncManufacturabilitySchemaIssue issue)
    {
        var code = issue.Kind == CncManufacturabilityIssueKind.Unsupported
            ? FirmamentDiagnosticCodes.SchemaCncManufacturabilityUnsupported
            : FirmamentDiagnosticCodes.SchemaCncManufacturabilityViolated;

        var measured = issue.MeasuredValue.HasValue
            ? issue.MeasuredValue.Value.ToString("0.0###############", CultureInfo.InvariantCulture)
            : "n/a";
        var required = issue.RequiredThreshold.ToString("0.0###############", CultureInfo.InvariantCulture);

        return new KernelDiagnostic(
            KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error,
            $"[{code.Value}] CNC rule '{issue.RuleId}' failed at {issue.Location}: measured={measured}, required>={required}. {issue.Message}",
            Source: FirmamentDiagnosticConventions.Source);
    }
}
