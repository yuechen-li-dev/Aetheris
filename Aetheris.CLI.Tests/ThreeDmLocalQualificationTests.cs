using Aetheris.ThreeDm;
using Xunit.Sdk;

namespace Aetheris.CLI.Tests;

public sealed class ThreeDmLocalQualificationTests
{
    [Fact]
    public void CartesianProductInventoryUsesFileUnitsAndNeverClaimsConversion()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Aetheris repository root was not found.");
        var path = Path.Combine(directory.FullName, "testdata", "3DM", "cartesian-product-metres.3dm");
        if (!File.Exists(path)) throw SkipException.ForSkip("Local Cartesian 3DM qualification fixture is absent.");

        var inventory = ThreeDmInspector.Inspect(path);
        Assert.Equal("Meters", inventory.SourceUnit);
        Assert.Equal(1000, inventory.MillimetresPerSourceUnit);
        Assert.Equal(inventory.SourceAbsoluteTolerance * 1000, inventory.AbsoluteToleranceMillimetres);
        Assert.Equal(inventory.ObjectCount, inventory.Objects.Count);
        Assert.Equal(inventory.BrepCount, inventory.Objects.Count(obj => obj.GeometryType == "Brep"));
        Assert.Equal(27, inventory.BrepCount);
        Assert.Equal(0, inventory.MeshCount);
        Assert.Equal((185, 232, 742, 346, 219),
            (inventory.FaceCount, inventory.LoopCount, inventory.TrimCount, inventory.EdgeCount, inventory.VertexCount));
        Assert.Equal((114, 50), (inventory.SeamTrimCount, inventory.SingularTrimCount));
        Assert.Equal((196, 56, 0, 53),
            (inventory.RationalEdgeCount, inventory.RationalArcEdgeCount,
                inventory.RationalEllipticEdgeCount, inventory.RationalGenericEdgeCount));
        Assert.Equal(0.001, inventory.AbsoluteToleranceMillimetres);
        var repeated = ThreeDmInspector.Inspect(path);
        Assert.Equal(inventory.Objects, repeated.Objects);
        Assert.Equal(inventory.BoundsMillimetres, repeated.BoundsMillimetres);
        Assert.All(inventory.Objects.Where(obj => obj.GeometryType == "Brep"),
            obj => Assert.Equal("exact-but-not-yet-mapped", obj.Classification));
    }
}
