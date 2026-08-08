using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.StandardLibrary;

public static class StandardLibraryReusableParts
{
    public const string CubeWithCylindricalHolePartName = "cube_with_cylindrical_hole";
    public const string HexBoltPartName = "HexBolt";

    public static KernelResult<BrepBody> TryCreate(string partName)
    {
        return string.Equals(partName, CubeWithCylindricalHolePartName, StringComparison.Ordinal)
            ? CreateCubeWithCylindricalHole()
            : KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"StandardLibrary part '{partName}' was not found.")
            ]);
    }

    public static KernelResult<StandardLibraryPartDefinition> TryCreate(
        string partName,
        IReadOnlyDictionary<string, string> parameters)
    {
        if (string.Equals(partName, CubeWithCylindricalHolePartName, StringComparison.Ordinal))
        {
            var cube = CreateCubeWithCylindricalHole();
            return cube.IsSuccess
                ? KernelResult<StandardLibraryPartDefinition>.Success(new(cube.Value, null))
                : KernelResult<StandardLibraryPartDefinition>.Failure(cube.Diagnostics);
        }

        if (!string.Equals(partName, HexBoltPartName, StringComparison.Ordinal))
            return KernelResult<StandardLibraryPartDefinition>.Failure([new KernelDiagnostic(
                KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error,
                $"StandardLibrary part '{partName}' was not found.")]);

        var parsed = HexBoltParameterBinding.Bind(parameters);
        if (!parsed.IsSuccess) return KernelResult<StandardLibraryPartDefinition>.Failure(parsed.Diagnostics);
        var bodyStableId = parameters.TryGetValue("StableId", out var authoredId) && !string.IsNullOrWhiteSpace(authoredId)
            ? HexBoltParameterBinding.Text(authoredId)
            : "HexBolt";
        var bolt = HexBoltBuilder.Create(parsed.Value, bodyStableId);
        return bolt.IsSuccess
            ? KernelResult<StandardLibraryPartDefinition>.Success(new(bolt.Value.Body, bolt.Value))
            : KernelResult<StandardLibraryPartDefinition>.Failure(bolt.Diagnostics);
    }

    public static KernelResult<BrepBody> CreateCubeWithCylindricalHole()
    {
        const double cubeSize = 20d;
        const double holeRadius = 3d;
        const double holeHeight = 24d;

        var cubeResult = BrepPrimitives.CreateBox(cubeSize, cubeSize, cubeSize);
        if (!cubeResult.IsSuccess)
        {
            return cubeResult;
        }

        var holeResult = BrepPrimitives.CreateCylinder(holeRadius, holeHeight);
        if (!holeResult.IsSuccess)
        {
            return holeResult;
        }

        return BrepBoolean.Subtract(cubeResult.Value, holeResult.Value);
    }
}

public sealed record StandardLibraryPartDefinition(BrepBody Body, HexBoltDefinition? HexBolt);
