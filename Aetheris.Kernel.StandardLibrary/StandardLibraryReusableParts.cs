using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.StandardLibrary;

public static class StandardLibraryReusableParts
{
    public const string CubeWithCylindricalHolePartName = "cube_with_cylindrical_hole";

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
