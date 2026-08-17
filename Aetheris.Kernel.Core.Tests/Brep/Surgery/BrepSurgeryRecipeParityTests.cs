using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Verification;
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

        AssertCanonical(body, 8, 12, 6, "39692ac0ea3c48d9f2f6fb1155e3b494c1b946bf56cd8dd2ade2c9005cd38048", expectClosedManifold: true);
    }

    [Fact]
    public void MigratedCylinderRootKeyway_RetainsCanonicalTopologyAndStep()
    {
        var shaft = BrepPrimitives.CreateCylinder(15d, 80d).Value;
        var tool = BrepBooleanBoxRecognition.CreateBoxFromExtents(
            new AxisAlignedBoxExtents(5d, 15d, -3d, 3d, -45d, 45d)).Value;
        var body = BrepBoolean.Subtract(shaft, tool).Value;

        AssertCanonical(body, 8, 12, 6, "7eb11479eaa1e14a51d1d60d8fc16f0521d80bd05203cf4aad400846298b605b", expectClosedManifold: true);
    }

    [Fact]
    public void MigratedPolygonalThroughCut_RetainsCanonicalTopologyAndStep()
    {
        var root = StandardLibraryPrimitives.CreateRoundedCornerBox(24d, 18d, 20d, 4d).Value;
        var tool = StandardLibraryPrimitives.CreateSlotCut(10d, 4d, 24d, 2d).Value;
        var body = BrepBoolean.Subtract(root, tool).Value;

        AssertCanonical(body, 128, 192, 66, "8554faf173a41abeb15facbeb2bd3cceb4f2ea486d6aa1e1b11c0740b922fe7d");
    }

    private static void AssertCanonical(BrepBody body, int vertices, int edges, int faces, string expectedStepSha256, bool expectClosedManifold = false)
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
        if (expectClosedManifold)
        {
            var mass = BrepMassProperties.Evaluate(reimport.Value);
            Assert.True(mass.IsEnclosed, string.Join(Environment.NewLine, mass.Diagnostics));
            Assert.True(mass.IsOrientationConsistent, string.Join(Environment.NewLine, mass.Diagnostics));
        }
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
