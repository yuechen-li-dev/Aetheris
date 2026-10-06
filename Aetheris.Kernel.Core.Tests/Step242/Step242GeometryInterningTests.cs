using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242GeometryInterningTests
{
    [Fact]
    public void ExactGeometrySharesButNamesCoordinatesAndDimensionsRemainDistinct()
    {
        var writer = new Step242TextWriter();
        var point = writer.AddEntity("CARTESIAN_POINT", "''", "(1,2,3)");
        Assert.Equal(point, writer.AddEntity("CARTESIAN_POINT", "''", "(1,2,3)"));
        Assert.NotEqual(point, writer.AddEntity("CARTESIAN_POINT", "'named'", "(1,2,3)"));
        Assert.NotEqual(point, writer.AddEntity("CARTESIAN_POINT", "''", "(1,2,3.0000000000001)"));
        Assert.NotEqual(point, writer.AddEntity("CARTESIAN_POINT", "''", "(1,2)"));
        var direction = writer.AddEntity("DIRECTION", "''", "(1,0,0)");
        var vector = writer.AddEntity("VECTOR", "''", direction, "1");
        Assert.Equal(vector, writer.AddEntity("VECTOR", "''", writer.AddEntity("DIRECTION", "''", "(1,0,0)"), "1"));
    }

    [Theory]
    [InlineData("VERTEX_POINT")]
    [InlineData("EDGE_CURVE")]
    [InlineData("ADVANCED_FACE")]
    [InlineData("PCURVE")]
    [InlineData("SURFACE_CURVE")]
    [InlineData("DEFINITIONAL_REPRESENTATION")]
    [InlineData("PRODUCT")]
    public void TopologyAndAssociationsNeverShareBySerializedEquality(string type)
    {
        var writer = new Step242TextWriter();
        Assert.NotEqual(writer.AddEntity(type, "''", "#1"), writer.AddEntity(type, "''", "#1"));
    }

    [Fact]
    public void WritersOwnTheirCachesAndFirstOccurrenceOrderingIsDeterministic()
    {
        static string Write()
        {
            var writer = new Step242TextWriter();
            var first = writer.AddEntity("CARTESIAN_POINT", "''", "(1,2,3)");
            writer.AddEntity("DIRECTION", "''", "(1,0,0)");
            Assert.Equal("#1", first);
            Assert.Equal(first, writer.AddEntity("CARTESIAN_POINT", "''", "(1,2,3)"));
            Assert.Equal("#3", writer.AddEntity("VERTEX_POINT", "''", first));
            return writer.Build(Step242HeaderMetadata.Deterministic);
        }
        Assert.Equal(Write(), Write());
    }
}
