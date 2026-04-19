using Aetheris.Forge;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class ForgeRoundedRectangleTests
{
    [Fact]
    public void RoundedRectangle_ValidParameters_Succeeds()
    {
        var profileResult = ForgeAtomics.RoundedRectangle(width: 40d, depth: 20d, cornerRadius: 3d);

        Assert.True(profileResult.IsSuccess);
        var polylineResult = profileResult.Value.ToPolylineProfile();
        Assert.True(polylineResult.IsSuccess);
        Assert.Equal(32, polylineResult.Value.Vertices.Count);
    }

    [Fact]
    public void RoundedRectangle_InvalidRadius_Fails()
    {
        var profileResult = ForgeAtomics.RoundedRectangle(width: 10d, depth: 6d, cornerRadius: 4d);

        Assert.False(profileResult.IsSuccess);
        Assert.Contains(profileResult.Diagnostics, diagnostic => diagnostic.Message.Contains("min(width, depth) / 2", StringComparison.Ordinal));
    }

    [Fact]
    public void RoundedCornerBox_StandardLibrary_Succeeds()
    {
        var result = StandardLibraryPrimitives.CreateRoundedCornerBox(width: 40d, depth: 20d, height: 12d, cornerRadius: 3d);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Topology.Faces.Count() >= 10);
        Assert.True(result.Value.Topology.Edges.Count() >= 24);
    }

    [Fact]
    public void SlotCut_StandardLibrary_Succeeds()
    {
        var result = StandardLibraryPrimitives.CreateSlotCut(length: 40d, width: 12d, height: 10d, cornerRadius: 6d);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Topology.Faces.Count() >= 10);
        Assert.True(result.Value.Topology.Edges.Count() >= 24);
    }
}
