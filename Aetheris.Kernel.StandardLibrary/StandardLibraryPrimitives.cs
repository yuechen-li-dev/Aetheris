using Aetheris.Forge;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.StandardLibrary;

public static class StandardLibraryPrimitives
{
    public static KernelResult<BrepBody> CreateRoundedCornerBox(double width, double depth, double height, double cornerRadius, int cornerSegments = 8)
    {
        var profile = ForgeAtomics.RoundedRectangle(width, depth, cornerRadius, cornerSegments);
        if (!profile.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(profile.Diagnostics);
        }

        return ForgeAtomics.ExtrudeCentered(profile.Value, height);
    }
}
