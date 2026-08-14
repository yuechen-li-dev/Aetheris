using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Core.Tests.Brep.Surgery;

public sealed class BrepSurgeryRecipeParityTests
{
    [Fact]
    public void MigratedOrthogonalUnion_RetainsCanonicalTopologyAndStep()
    {
        var body = BrepBooleanOrthogonalUnionBuilder.BuildFromCells([
            new AxisAlignedBoxExtents(0d, 2d, 0d, 2d, 0d, 1d),
            new AxisAlignedBoxExtents(2d, 4d, 0d, 2d, 0d, 1d),
        ]).Value;

        AssertCanonical(body, 8, 12, 6, "09e6b8d838d08748d60fe6f08f0bfcb09282eb983d26d6fc51956cf5e8828de2");
    }

    [Fact]
    public void MigratedCylinderRootKeyway_RetainsCanonicalTopologyAndStep()
    {
        var shaft = BrepPrimitives.CreateCylinder(15d, 80d).Value;
        var tool = BrepBooleanBoxRecognition.CreateBoxFromExtents(
            new AxisAlignedBoxExtents(5d, 15d, -3d, 3d, -45d, 45d)).Value;
        var body = BrepBoolean.Subtract(shaft, tool).Value;

        AssertCanonical(body, 8, 12, 6, "b91443673ab595a0449e91359aa697ae597a03efdb90d7394ac937107e25ae98");
    }

    [Fact]
    public void MigratedPolygonalThroughCut_RetainsCanonicalTopologyAndStep()
    {
        var root = StandardLibraryPrimitives.CreateRoundedCornerBox(24d, 18d, 20d, 4d).Value;
        var tool = StandardLibraryPrimitives.CreateSlotCut(10d, 4d, 24d, 2d).Value;
        var body = BrepBoolean.Subtract(root, tool).Value;

        AssertCanonical(body, 128, 192, 66, "8554faf173a41abeb15facbeb2bd3cceb4f2ea486d6aa1e1b11c0740b922fe7d");
    }

    private static void AssertCanonical(BrepBody body, int vertices, int edges, int faces, string expectedStepSha256)
    {
        Assert.Equal(vertices, body.Topology.Vertices.Count());
        Assert.Equal(edges, body.Topology.Edges.Count());
        Assert.Equal(faces, body.Topology.Faces.Count());
        Assert.Equal(edges, body.Geometry.Curves.Count());
        Assert.Equal(faces, body.Geometry.Surfaces.Count());
        Assert.Equal(edges, body.Bindings.EdgeBindings.Count());
        Assert.Equal(faces, body.Bindings.FaceBindings.Count());
        Assert.True(BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true).IsSuccess);

        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(expectedStepSha256, Sha256(export.Value));

        var reimport = Step242Importer.ImportBody(export.Value);
        Assert.True(reimport.IsSuccess, string.Join(Environment.NewLine, reimport.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.True(BrepBindingValidator.Validate(reimport.Value, requireAllEdgeAndFaceBindings: true).IsSuccess);
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
