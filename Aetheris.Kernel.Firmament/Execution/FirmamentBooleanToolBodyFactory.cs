using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentBooleanToolBodyFactory
{
    public static KernelResult<BrepBody> CreateBody(FirmamentLoweredToolOp tool)
    {
        if (string.Equals(tool.OpName, "box", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("size", out var sizeRaw) || string.IsNullOrWhiteSpace(sizeRaw))
            {
                return KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Boolean execution expected validated nested field 'with.size' for tool op 'box'.")]);
            }

            var parameters = FirmamentPrimitiveToolParsing.ParseBox(sizeRaw);
            return BrepPrimitives.CreateBox(parameters.SizeX, parameters.SizeY, parameters.SizeZ);
        }

        if (string.Equals(tool.OpName, "cylinder", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("radius", out var radiusRaw) || string.IsNullOrWhiteSpace(radiusRaw)
                || !tool.RawFields.TryGetValue("height", out var heightRaw) || string.IsNullOrWhiteSpace(heightRaw))
            {
                return KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Boolean execution expected validated nested fields 'with.radius' and 'with.height' for tool op 'cylinder'.")]);
            }

            return BrepPrimitives.CreateCylinder(FirmamentPrimitiveToolParsing.ParseScalar(radiusRaw), FirmamentPrimitiveToolParsing.ParseScalar(heightRaw));
        }

        if (string.Equals(tool.OpName, "sphere", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("radius", out var radiusRaw) || string.IsNullOrWhiteSpace(radiusRaw))
            {
                return KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Boolean execution expected validated nested field 'with.radius' for tool op 'sphere'.")]);
            }

            return BrepPrimitives.CreateSphere(FirmamentPrimitiveToolParsing.ParseScalar(radiusRaw));
        }

        if (string.Equals(tool.OpName, "cone", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("bottom_radius", out var bottomRadiusRaw) || string.IsNullOrWhiteSpace(bottomRadiusRaw)
                || !tool.RawFields.TryGetValue("top_radius", out var topRadiusRaw) || string.IsNullOrWhiteSpace(topRadiusRaw)
                || !tool.RawFields.TryGetValue("height", out var coneHeightRaw) || string.IsNullOrWhiteSpace(coneHeightRaw))
            {
                return KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Boolean execution expected validated nested fields 'with.bottom_radius', 'with.top_radius', and 'with.height' for tool op 'cone'.")]);
            }

            return FirmamentPrimitiveExecutor.ExecuteCone(new FirmamentLoweredConeParameters(
                FirmamentPrimitiveToolParsing.ParseScalar(bottomRadiusRaw),
                FirmamentPrimitiveToolParsing.ParseScalar(topRadiusRaw),
                FirmamentPrimitiveToolParsing.ParseScalar(coneHeightRaw)));
        }

        if (string.Equals(tool.OpName, "torus", StringComparison.Ordinal))
        {
            if (!tool.RawFields.TryGetValue("major_radius", out var majorRadiusRaw) || string.IsNullOrWhiteSpace(majorRadiusRaw)
                || !tool.RawFields.TryGetValue("minor_radius", out var minorRadiusRaw) || string.IsNullOrWhiteSpace(minorRadiusRaw))
            {
                return KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Boolean execution expected validated nested fields 'with.major_radius' and 'with.minor_radius' for tool op 'torus'.")]);
            }

            return BrepPrimitives.CreateTorus(
                FirmamentPrimitiveToolParsing.ParseScalar(majorRadiusRaw),
                FirmamentPrimitiveToolParsing.ParseScalar(minorRadiusRaw));
        }

        if (FirmamentPrismFamilyTools.IsPrismTool(tool.OpName))
        {
            return FirmamentPrismFamilyTools.TryCreateBody(tool);
        }

        return KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, $"Boolean execution supports nested tool ops 'box', 'cylinder', 'sphere', 'cone', 'torus', 'triangular_prism', 'hexagonal_prism', 'straight_slot', and 'slot_cut' only. Got '{tool.OpName}'.")]);
    }
}
