using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SourceSurfaceAndTrimCapabilityTests
{
    [Fact]
    public void SourceSurfaceExtractor_Box_ProducesSixPlanarDescriptors()
    {
        var result = SourceSurfaceExtractor.Extract(new CirBoxNode(10, 20, 30));
        Assert.Equal(6, result.Descriptors.Count);
        Assert.All(result.Descriptors, d => Assert.Equal(SurfacePatchFamily.Planar, d.Family));
        Assert.Contains(result.Descriptors, d => d.ParameterPayloadReference == "top");
        Assert.Contains(result.Descriptors, d => d.ParameterPayloadReference == "bottom");
    }

    [Fact]
    public void SourceSurfaceExtractor_Cylinder_ProducesSideAndCaps()
    {
        var result = SourceSurfaceExtractor.Extract(new CirCylinderNode(5, 20));
        Assert.Single(result.Descriptors.Where(d => d.Family == SurfacePatchFamily.Cylindrical));
        Assert.Equal(2, result.Descriptors.Count(d => d.Family == SurfacePatchFamily.Planar));
    }

    [Fact]
    public void SourceSurfaceExtractor_Sphere_ProducesSpherical()
    {
        var result = SourceSurfaceExtractor.Extract(new CirSphereNode(5));
        var descriptor = Assert.Single(result.Descriptors);
        Assert.Equal(SurfacePatchFamily.Spherical, descriptor.Family);
    }

    [Fact]
    public void SourceSurfaceExtractor_Torus_ProducesToroidalDeferredMaterialization()
    {
        var result = SourceSurfaceExtractor.Extract(new CirTorusNode(10, 2));
        var descriptor = Assert.Single(result.Descriptors);
        Assert.Equal(SurfacePatchFamily.Toroidal, descriptor.Family);
        Assert.Contains(result.Diagnostics, d => d.Code == "torus-materialization-deferred");
    }

    [Fact]
    public void SourceSurfaceExtractor_BooleanTree_ExtractsPrimitiveSourceSurfaces()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 10));
        var result = SourceSurfaceExtractor.Extract(root);
        Assert.Equal(9, result.Descriptors.Count);
        Assert.Contains(result.Diagnostics, d => d.Code == "retention-deferred");
    }

    [Fact]
    public void SourceSurfaceExtractor_Transform_ComposesIntoDescriptors()
    {
        var root = new CirTransformNode(new CirSphereNode(5), Transform3D.CreateTranslation(new Vector3D(1, 2, 3)));
        var result = SourceSurfaceExtractor.Extract(root);
        var descriptor = Assert.Single(result.Descriptors);
        Assert.Equal(new Point3D(1, 2, 3), descriptor.Transform.Apply(Point3D.Origin));
    }

    [Fact]
    public void TrimCapabilityMatrix_PlanarSphere_IsCircleExact()
    {
        var result = TrimCapabilityMatrix.Evaluate(SurfacePatchFamily.Planar, SurfacePatchFamily.Spherical);
        Assert.Equal(TrimCapabilityClassification.ExactSupported, result.Classification);
        Assert.Contains(TrimCurveFamily.Circle, result.CurveFamilies);
    }

    [Fact]
    public void TrimCapabilityMatrix_PlanarTorus_IsDeferred()
    {
        var result = TrimCapabilityMatrix.Evaluate(SurfacePatchFamily.Planar, SurfacePatchFamily.Toroidal);
        Assert.Equal(TrimCapabilityClassification.Deferred, result.Classification);
    }

    [Fact]
    public void TrimCapabilityMatrix_IsSymmetricForKnownPairs()
    {
        var ab = TrimCapabilityMatrix.Evaluate(SurfacePatchFamily.Cylindrical, SurfacePatchFamily.Planar);
        var ba = TrimCapabilityMatrix.Evaluate(SurfacePatchFamily.Planar, SurfacePatchFamily.Cylindrical);
        Assert.Equal(ab.Classification, ba.Classification);
        Assert.Equal(ab.CurveFamilies, ba.CurveFamilies);
    }
}
