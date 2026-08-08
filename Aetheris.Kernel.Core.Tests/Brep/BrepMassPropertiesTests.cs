using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepMassPropertiesTests
{
    [Theory]
    [InlineData("cylinder")]
    [InlineData("sphere")]
    [InlineData("torus")]
    public void AnalyticSurfaceFamilies_AreIntegratedFromFinalBrep(string kind)
    {
        var source = kind switch
        {
            "cylinder" => BrepPrimitives.CreateCylinder(2d, 5d).Value,
            "sphere" => BrepPrimitives.CreateSphere(2d).Value,
            "torus" => BrepPrimitives.CreateTorus(5d, 1d).Value,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var step = Step242Exporter.ExportBody(source);
        var body = Assert.IsType<BrepBody>(Step242Importer.ImportBody(step.Value).Value);
        var result = BrepMassProperties.Evaluate(body, new BrepMassPropertiesOptions(1e-4d, double.Pi / 96d, 0.1d));

        Assert.True(result.Status != BrepMassPropertiesStatus.Unavailable, string.Join(" | ", result.Diagnostics));
        Assert.True(result.AbsoluteVolume > 0d);
        Assert.Contains(result.FaceContributions, item => item.SurfaceKind is not null);
        Assert.False(result.IsAuthoritativeForVolumeAssertion);
        Assert.True(result.IsTessellatedSanityEstimate);
    }

    [Fact]
    public void ClosedBox_UsesIndependentTessellatedBoundaryIntegral()
    {
        var body = Assert.IsType<BrepBody>(BrepPrimitives.CreateBox(10d, 6d, 4d).Value);

        var result = BrepMassProperties.Evaluate(body, new BrepMassPropertiesOptions(1e-5d, double.Pi / 96d, 1e-8d));

        Assert.True(result.Status != BrepMassPropertiesStatus.Unavailable, string.Join(" | ", result.Diagnostics));
        Assert.True(result.IsEnclosed);
        Assert.Equal(240d, result.AbsoluteVolume, 6);
        Assert.NotNull(result.Centroid);
        Assert.Equal(0d, result.Centroid!.Value.X, 6);
        Assert.Equal(0d, result.Centroid!.Value.Y, 6);
        Assert.Equal(0d, result.Centroid!.Value.Z, 6);
        Assert.Equal(6, result.FaceContributions.Count);
    }

    [Fact]
    public void HollowFrustum_FullCircularConeTrimsProduceCompleteIndependentEvidence()
    {
        var realization = ThinWalledBodyBRepPlanner.CreateFrustum(32d, 43d, 90d, 2d).Value;
        var body = realization.Body;
        var options = DisplayTessellationOptions.Create(double.Pi / 48d, 1e-4d, 12, 512).Value;

        var first = BrepDisplayTessellator.Tessellate(body, options).Value;
        var second = BrepDisplayTessellator.Tessellate(body, options).Value;
        var coneFaces = body.Topology.Faces
            .Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface?.Cone is not null)
            .OrderBy(face => face.Id.Value)
            .ToArray();

        Assert.Equal(2, coneFaces.Length);
        Assert.True(body.Bindings.GetFaceBinding(coneFaces[0].Id).SameSense);
        Assert.False(body.Bindings.GetFaceBinding(coneFaces[1].Id).SameSense);
        foreach (var face in coneFaces)
        {
            var patch = Assert.Single(first.FacePatches, candidate => candidate.FaceId == face.Id);
            var again = Assert.Single(second.FacePatches, candidate => candidate.FaceId == face.Id);
            Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var faceSurface));
            var cone = faceSurface!.Cone!.Value;
            Assert.NotEmpty(patch.TriangleIndices);
            Assert.Equal(patch.TriangleIndices.Count, again.TriangleIndices.Count);
            Assert.Equal(patch.Positions.Count, again.Positions.Count);
            Assert.All(patch.Positions, point => AssertConeResidual(cone, point));
            Assert.True((patch.Positions[0] - patch.Positions[^1]).Length > 1e-3d); // distinct generator rings
        }

        var result = BrepMassProperties.Evaluate(body);
        Assert.Equal(BrepMassPropertiesStatus.NumericalWithBound, result.Status);
        Assert.True(result.IsEnclosed && result.IsOrientationConsistent);
        Assert.Equal(5, result.FaceContributions.Count);
        Assert.All(result.FaceContributions.Where(item => item.SurfaceKind?.ToString() == "Cone"), item => Assert.True(item.TriangleCount > 0));
        Assert.InRange(result.AbsoluteVolume, 47274.67209734925d - result.ErrorBound!.Value, 47274.67209734925d + result.ErrorBound.Value);
        Assert.InRange(result.Centroid!.Value.Z, 0d, 90d);

        var reimported = Step242Importer.ImportBody(Step242Exporter.ExportBody(body).Value).Value;
        var imported = BrepMassProperties.Evaluate(reimported);
        Assert.True(imported.Status != BrepMassPropertiesStatus.Unavailable, string.Join(" | ", imported.Diagnostics));
        Assert.All(imported.FaceContributions.Where(item => item.SurfaceKind?.ToString() == "Cone"), item => Assert.True(item.TriangleCount > 0));
    }

    private static void AssertConeResidual(ConeSurface cone, Aetheris.Kernel.Core.Math.Point3D point)
    {
        var offset = point - cone.Apex;
        var axial = offset.Dot(cone.Axis.ToVector());
        var radial = (offset - (cone.Axis.ToVector() * axial)).Length;
        Assert.InRange(System.Math.Abs(radial - (axial * System.Math.Tan(cone.SemiAngleRadians))), 0d, 1e-8d);
        Assert.True(axial > 0d);
    }
}
