using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
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

        AssertCanonical(body, 8, 12, 6, "0a1e73060c516eb608c1aef10a4f669b988052d806da441d8fed39bff73d5aed", expectClosedManifold: true);
    }

    [Fact]
    public void MigratedCylinderRootKeyway_RetainsCanonicalTopologyAndStep()
    {
        var shaft = BrepPrimitives.CreateCylinder(15d, 80d).Value;
        var tool = BrepBooleanBoxRecognition.CreateBoxFromExtents(
            new AxisAlignedBoxExtents(5d, 15d, -3d, 3d, -45d, 45d)).Value;
        var body = BrepBoolean.Subtract(shaft, tool).Value;

        // Partial circular edges now reference legal CIRCLE bases; geometry is unchanged.
        AssertCanonical(body, 8, 12, 6, "9e3ae6915a10875e081e9eaa9f676f4e8e7bda8ed05a1872ab81da0dbf709fb0", expectClosedManifold: true);
    }

    [Fact]
    public void MigratedPolygonalThroughCut_RetainsCanonicalTopologyAndStep()
    {
        var root = StandardLibraryPrimitives.CreateRoundedCornerBox(24d, 18d, 20d, 4d).Value;
        var tool = StandardLibraryPrimitives.CreateSlotCut(10d, 4d, 24d, 2d).Value;
        var body = BrepBoolean.Subtract(root, tool).Value;

        AssertCanonical(body, 128, 192, 66, "a7b0cbd24843291d472417b8f6b752dbd2b3f1f361149a853cb829b7de6915fc");
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
        Assert.EndsWith("\r\n", export.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", export.Value.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        var actualStepSha256 = Sha256(export.Value);
        Assert.True(string.Equals(expectedStepSha256, actualStepSha256, StringComparison.Ordinal), actualStepSha256);

        var reimport = Step242Importer.ImportBody(export.Value);
        Assert.True(reimport.IsSuccess, string.Join(Environment.NewLine, reimport.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.True(BrepBindingValidator.Validate(reimport.Value, requireAllEdgeAndFaceBindings: true).IsSuccess);
        var sourceSamples = SampleEdges(body);
        var reimportedSamples = SampleEdges(reimport.Value);
        Assert.Equal(sourceSamples.Length, reimportedSamples.Length);
        foreach (var sample in sourceSamples)
            Assert.True(reimportedSamples.Min(point => (point - sample).Length) < 1e-7d);
        foreach (var sample in reimportedSamples)
            Assert.True(sourceSamples.Min(point => (point - sample).Length) < 1e-7d);
        if (expectClosedManifold)
        {
            var mass = BrepMassProperties.Evaluate(reimport.Value);
            Assert.True(mass.IsEnclosed, string.Join(Environment.NewLine, mass.Diagnostics));
            Assert.True(mass.IsOrientationConsistent, string.Join(Environment.NewLine, mass.Diagnostics));
        }
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static Point3D[] SampleEdges(BrepBody body) => body.Bindings.EdgeBindings.SelectMany(binding =>
    {
        var curve = body.GetEdgeCurve(binding.EdgeId);
        var interval = binding.TrimInterval!.Value;
        return Enumerable.Range(0, 5).Select(i =>
        {
            var t = interval.Start + (interval.End - interval.Start) * i / 4d;
            return curve.Kind switch
            {
                CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(t),
                CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(t),
                _ => throw new InvalidOperationException($"Unexpected recipe curve {curve.Kind}."),
            };
        });
    }).ToArray();
}
