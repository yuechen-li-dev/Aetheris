using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfacePatchDescriptorScaffoldTests
{
    [Fact]
    public void SourceSurfaceDescriptor_Box_ProducesSixPlanarDescriptors()
    {
        var descriptors = Enumerable.Range(0, 6)
            .Select(i => new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, $"box-face-{i}", Transform3D.Identity, "cir:box", nameof(Aetheris.Kernel.Core.Cir.CirBoxNode), 0, FacePatchOrientationRole.Forward))
            .ToArray();

        Assert.Equal(6, descriptors.Length);
        Assert.All(descriptors, d => Assert.Equal(SurfacePatchFamily.Planar, d.Family));
        Assert.All(descriptors, d => Assert.False(string.IsNullOrWhiteSpace(d.Provenance)));
    }

    [Fact]
    public void SourceSurfaceDescriptor_Cylinder_ProducesCylindricalAndPlanarDescriptors()
    {
        var side = new SourceSurfaceDescriptor(SurfacePatchFamily.Cylindrical, "side", Transform3D.Identity, "cir:cylinder:side", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Forward);
        var top = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "cap-top", Transform3D.Identity, "cir:cylinder:cap-top", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Forward);
        var bottom = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "cap-bottom", Transform3D.Identity, "cir:cylinder:cap-bottom", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Reversed);

        Assert.Equal(SurfacePatchFamily.Cylindrical, side.Family);
        Assert.Equal(SurfacePatchFamily.Planar, top.Family);
        Assert.Equal(SurfacePatchFamily.Planar, bottom.Family);
    }

    [Fact]
    public void SourceSurfaceDescriptor_Sphere_ProducesSphericalDescriptor()
    {
        var sphere = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", Transform3D.Identity, "cir:sphere", nameof(Aetheris.Kernel.Core.Cir.CirSphereNode), 2, FacePatchOrientationRole.Forward);
        Assert.Equal(SurfacePatchFamily.Spherical, sphere.Family);
    }

    [Fact]
    public void SourceSurfaceDescriptor_Torus_ProducesToroidalDescriptorUnsupportedMaterializer()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Toroidal, "torus", Transform3D.Identity, "cir:torus", nameof(Aetheris.Kernel.Core.Cir.CirTorusNode), 3, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "tool-surface", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.False(eval.IsSuccess);
        Assert.Contains(eval.Rejections, r => r.CandidateName == "surface_family_toroidal" && r.Reason.Contains("deferred", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFamilyRegistry_SelectsPlanarForPlanarPatch()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "plane", Transform3D.Identity, "cir:box", nameof(Aetheris.Kernel.Core.Cir.CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var trim = new TrimCurveDescriptor(TrimCurveFamily.Line, "edge-0", "loop:outer", 0, new ParameterInterval(0, 1), TrimCurveCapability.ExactSupported);
        var patch = new FacePatchDescriptor(source, [trim], [], FacePatchOrientationRole.Forward, "outer", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.True(eval.IsSuccess);
        Assert.Equal("surface_family_planar", eval.Selected?.Name);
    }

    [Fact]
    public void SurfaceFamilyRegistry_RejectsWrongFamilyWithReasons()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", Transform3D.Identity, "cir:sphere", nameof(Aetheris.Kernel.Core.Cir.CirSphereNode), 4, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "outer", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.True(eval.IsSuccess);
        Assert.Equal("surface_family_spherical", eval.Selected?.Name);
        Assert.Contains(eval.Rejections, r => r.CandidateName == "surface_family_planar" && r.Reason.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
    }
}
