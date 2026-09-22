using Rhino.FileIO;
using Rhino.Geometry;
using Xunit.Sdk;

namespace Aetheris.CLI.Tests;

public sealed class ThreeDmLampBaseIntentLocalTests
{
    [Fact]
    public void SelectedBaseAndPrimaryIntentMeasurementsMatchLocalSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Aetheris repository root was not found.");
        var path = Path.Combine(directory.FullName, "testdata", "3DM", "cartesian-product-metres.3dm");
        if (!File.Exists(path)) throw SkipException.ForSkip("Local Cartesian 3DM fixture is absent.");

        using var file = File3dm.Read(path);
        Assert.NotNull(file);
        Brep? brep = null;
        Guid sourceId = Guid.Empty;
        var objectIndex = 0;
        foreach (var item in file.Objects)
        {
            if (objectIndex++ != 5) continue;
            sourceId = item.Attributes.ObjectId;
            brep = Assert.IsType<Brep>(item.Geometry);
            break;
        }
        Assert.Equal(Guid.Parse("764c1644-10c1-44ec-a51b-3335716bda0b"), sourceId);
        Assert.NotNull(brep);
        Assert.True(brep.IsSolid);
        Assert.Equal((54, 133, 314), (brep.Faces.Count, brep.Edges.Count, brep.Trims.Count));

        var points = brep.Edges.SelectMany(edge => Enumerable.Range(0, 129).Select(i =>
            edge.PointAt(edge.Domain.T0 + (edge.Domain.T1 - edge.Domain.T0) * i / 128d))).ToArray();
        Assert.InRange(points.Min(point => point.X * 1000), -107.50001, -107.49999);
        Assert.InRange(points.Max(point => point.X * 1000), 107.49999, 107.50001);
        Assert.InRange(points.Min(point => point.Y * 1000), -75.00001, -74.99999);
        Assert.InRange(points.Max(point => point.Y * 1000), 74.99999, 75.00001);
        Assert.InRange(points.Min(point => point.Z * 1000), -0.00001, 0.00001);
        Assert.InRange(points.Max(point => point.Z * 1000), 11.99999, 12.00001);

        AssertCircle(brep.Edges[130], 38, 0, 61.6);
        AssertCircle(brep.Edges[105], -78, 0, 7);
        AssertCircle(brep.Edges[107], -44, -25, 3.5);
    }

    private static void AssertCircle(BrepEdge edge, double x, double y, double radius)
    {
        Assert.True(edge.EdgeCurve.TryGetCircle(out var circle));
        Assert.InRange(circle.Center.X * 1000, x - 0.00001, x + 0.00001);
        Assert.InRange(circle.Center.Y * 1000, y - 0.00001, y + 0.00001);
        Assert.InRange(circle.Radius * 1000, radius - 0.00001, radius + 0.00001);
    }
}
