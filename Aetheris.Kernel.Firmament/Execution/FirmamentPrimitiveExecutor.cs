using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentPrimitiveExecutor
{
    public static KernelResult<FirmamentPrimitiveExecutionResult> Execute(FirmamentPrimitiveLoweringPlan loweringPlan)
    {
        ArgumentNullException.ThrowIfNull(loweringPlan);

        var executedPrimitives = new List<FirmamentExecutedPrimitive>(loweringPlan.Primitives.Count);
        var executedBooleans = new List<FirmamentExecutedBoolean>(loweringPlan.Booleans.Count);
        var bodiesByFeatureId = new Dictionary<string, BrepBody>(StringComparer.Ordinal);

        foreach (var primitive in loweringPlan.Primitives.OrderBy(p => p.OpIndex))
        {
            var bodyResult = ExecutePrimitive(primitive);
            if (!bodyResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(bodyResult.Diagnostics);
            }

            executedPrimitives.Add(new FirmamentExecutedPrimitive(
                OpIndex: primitive.OpIndex,
                FeatureId: primitive.FeatureId,
                Kind: primitive.Kind,
                Body: bodyResult.Value));
            bodiesByFeatureId[primitive.FeatureId] = bodyResult.Value;
        }

        foreach (var boolean in loweringPlan.Booleans.OrderBy(b => b.OpIndex))
        {
            if (!bodiesByFeatureId.TryGetValue(boolean.PrimaryReferenceFeatureId, out var baseBody))
            {
                continue;
            }

            var toolResult = ExecuteTool(boolean.Tool);
            if (!toolResult.IsSuccess)
            {
                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(toolResult.Diagnostics);
            }

            var booleanResult = ExecuteBoolean(boolean.Kind, baseBody, toolResult.Value);
            if (!booleanResult.IsSuccess)
            {
                if (booleanResult.Diagnostics.All(d => d.Code == KernelDiagnosticCode.NotImplemented))
                {
                    continue;
                }

                return KernelResult<FirmamentPrimitiveExecutionResult>.Failure(booleanResult.Diagnostics);
            }

            executedBooleans.Add(new FirmamentExecutedBoolean(
                OpIndex: boolean.OpIndex,
                FeatureId: boolean.FeatureId,
                Kind: boolean.Kind,
                Body: booleanResult.Value));
            bodiesByFeatureId[boolean.FeatureId] = booleanResult.Value;
        }

        return KernelResult<FirmamentPrimitiveExecutionResult>.Success(new FirmamentPrimitiveExecutionResult(executedPrimitives, executedBooleans));
    }

    private static KernelResult<BrepBody> ExecuteTool(FirmamentLoweredToolOp tool)
    {
        if (string.Equals(tool.OpName, "box", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("size", out var sizeRaw) || string.IsNullOrWhiteSpace(sizeRaw))
            {
                return KernelResult<BrepBody>.Failure([
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        "Boolean execution expected validated nested field 'with.size' for tool op 'box'.")]);
            }

            var parameters = FirmamentPrimitiveToolParsing.ParseBox(sizeRaw);
            return BrepPrimitives.CreateBox(parameters.SizeX, parameters.SizeY, parameters.SizeZ);
        }

        if (string.Equals(tool.OpName, "cylinder", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("radius", out var radiusRaw) || string.IsNullOrWhiteSpace(radiusRaw)
                || !tool.RawFields.TryGetValue("height", out var heightRaw) || string.IsNullOrWhiteSpace(heightRaw))
            {
                return KernelResult<BrepBody>.Failure([
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        "Boolean execution expected validated nested fields 'with.radius' and 'with.height' for tool op 'cylinder'.")]);
            }

            var radius = FirmamentPrimitiveToolParsing.ParseScalar(radiusRaw);
            var height = FirmamentPrimitiveToolParsing.ParseScalar(heightRaw);
            return BrepPrimitives.CreateCylinder(radius, height);
        }

        if (string.Equals(tool.OpName, "sphere", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("radius", out var radiusRaw) || string.IsNullOrWhiteSpace(radiusRaw))
            {
                return KernelResult<BrepBody>.Failure([
                    new KernelDiagnostic(
                        KernelDiagnosticCode.ValidationFailed,
                        KernelDiagnosticSeverity.Error,
                        "Boolean execution expected validated nested field 'with.radius' for tool op 'sphere'.")]);
            }

            var radius = FirmamentPrimitiveToolParsing.ParseScalar(radiusRaw);
            return BrepPrimitives.CreateSphere(radius);
        }

        return KernelResult<BrepBody>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                $"Boolean execution supports nested tool ops 'box', 'cylinder', and 'sphere' only. Got '{tool.OpName}'.")]);
    }

    private static KernelResult<BrepBody> ExecuteBoolean(FirmamentLoweredBooleanKind kind, BrepBody left, BrepBody right)
    {
        return kind switch
        {
            FirmamentLoweredBooleanKind.Add => BrepBoolean.Union(left, right),
            FirmamentLoweredBooleanKind.Subtract => BrepBoolean.Subtract(left, right),
            FirmamentLoweredBooleanKind.Intersect => BrepBoolean.Intersect(left, right),
            _ => KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    $"Boolean execution for kind '{kind}' is not implemented.")])
        };
    }

    private static KernelResult<BrepBody> ExecutePrimitive(FirmamentLoweredPrimitive primitive)
    {
        return primitive.Kind switch
        {
            FirmamentLoweredPrimitiveKind.Box => ExecuteBox(primitive),
            FirmamentLoweredPrimitiveKind.Cylinder => ExecuteCylinder(primitive),
            FirmamentLoweredPrimitiveKind.Sphere => ExecuteSphere(primitive),
            _ => KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    $"Primitive execution for kind '{primitive.Kind}' is not implemented.")])
        };
    }

    private static KernelResult<BrepBody> ExecuteBox(FirmamentLoweredPrimitive primitive)
    {
        var parameters = (FirmamentLoweredBoxParameters)primitive.Parameters;
        return BrepPrimitives.CreateBox(parameters.SizeX, parameters.SizeY, parameters.SizeZ);
    }

    private static KernelResult<BrepBody> ExecuteCylinder(FirmamentLoweredPrimitive primitive)
    {
        var parameters = (FirmamentLoweredCylinderParameters)primitive.Parameters;
        return BrepPrimitives.CreateCylinder(parameters.Radius, parameters.Height);
    }

    private static KernelResult<BrepBody> ExecuteSphere(FirmamentLoweredPrimitive primitive)
    {
        var parameters = (FirmamentLoweredSphereParameters)primitive.Parameters;
        return BrepPrimitives.CreateSphere(parameters.Radius);
    }
}

internal static class FirmamentPrimitiveToolParsing
{
    public static FirmamentLoweredBoxParameters ParseBox(string sizeRaw)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(sizeRaw);
        var elements = doc.RootElement.EnumerateArray().ToArray();

        return new FirmamentLoweredBoxParameters(
            ParseScalar(elements[0].ToString()),
            ParseScalar(elements[1].ToString()),
            ParseScalar(elements[2].ToString()));
    }

    public static double ParseScalar(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            trimmed = trimmed[1..^1];
        }

        return double.Parse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }
}
