using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfacePatchDescriptorScaffoldTests
{
    [Fact]
    public void SourceSurfaceDescriptor_Box_ProducesSixPlanarDescriptors()
    {
        var descriptors = Enumerable.Range(0, 6)
            .Select(i => new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, $"box-face-{i}", null, Transform3D.Identity, "cir:box", nameof(Aetheris.Kernel.Core.Cir.CirBoxNode), 0, FacePatchOrientationRole.Forward))
            .ToArray();

        Assert.Equal(6, descriptors.Length);
        Assert.All(descriptors, d => Assert.Equal(SurfacePatchFamily.Planar, d.Family));
        Assert.All(descriptors, d => Assert.False(string.IsNullOrWhiteSpace(d.Provenance)));
    }

    [Fact]
    public void SourceSurfaceDescriptor_Cylinder_ProducesCylindricalAndPlanarDescriptors()
    {
        var side = new SourceSurfaceDescriptor(SurfacePatchFamily.Cylindrical, "side", null, Transform3D.Identity, "cir:cylinder:side", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Forward);
        var top = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "cap-top", null, Transform3D.Identity, "cir:cylinder:cap-top", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Forward);
        var bottom = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "cap-bottom", null, Transform3D.Identity, "cir:cylinder:cap-bottom", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Reversed);

        Assert.Equal(SurfacePatchFamily.Cylindrical, side.Family);
        Assert.Equal(SurfacePatchFamily.Planar, top.Family);
        Assert.Equal(SurfacePatchFamily.Planar, bottom.Family);
    }

    [Fact]
    public void SourceSurfaceDescriptor_Sphere_ProducesSphericalDescriptor()
    {
        var sphere = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, Transform3D.Identity, "cir:sphere", nameof(Aetheris.Kernel.Core.Cir.CirSphereNode), 2, FacePatchOrientationRole.Forward);
        Assert.Equal(SurfacePatchFamily.Spherical, sphere.Family);
    }

    [Fact]
    public void SourceSurfaceDescriptor_Torus_ProducesToroidalDescriptorUnsupportedMaterializer()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Toroidal, "torus", null, Transform3D.Identity, "cir:torus", nameof(Aetheris.Kernel.Core.Cir.CirTorusNode), 3, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "tool-surface", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.False(eval.IsSuccess);
        Assert.Contains(eval.Rejections, r => r.CandidateName == "surface_family_toroidal" && r.Reason.Contains("deferred", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFamilyRegistry_SelectsPlanarForPlanarPatch()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "plane", null, Transform3D.Identity, "cir:box", nameof(Aetheris.Kernel.Core.Cir.CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var trim = new TrimCurveDescriptor(TrimCurveFamily.Line, "edge-0", "loop:outer", 0, new ParameterInterval(0, 1), TrimCurveCapability.ExactSupported);
        var patch = new FacePatchDescriptor(source, [trim], [], FacePatchOrientationRole.Forward, "outer", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.True(eval.IsSuccess);
        Assert.Equal("surface_family_planar", eval.Selected?.Name);
    }

    [Fact]
    public void SurfaceFamilyRegistry_RejectsWrongFamilyWithReasons()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, Transform3D.Identity, "cir:sphere", nameof(Aetheris.Kernel.Core.Cir.CirSphereNode), 4, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "outer", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.True(eval.IsSuccess);
        Assert.Equal("surface_family_spherical", eval.Selected?.Name);
        Assert.Contains(eval.Rejections, r => r.CandidateName == "surface_family_planar" && r.Reason.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_UntrimmedBoxFace_EmitsPlanarTopology()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:-1,-1,0;1,-1,0;1,1,0;-1,1,0", null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);

        Assert.True(result.Success);
        Assert.NotNull(result.Body);
        Assert.Single(result.Body!.Topology.Faces);
        Assert.Single(result.Body.Topology.Loops);
        Assert.Equal(4, result.Body.Topology.Edges.Count());
        Assert.Equal(4, result.Body.Topology.Vertices.Count());
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RejectsDeferredReadiness()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:0,0,0;1,0,0;1,1,0;0,1,0", null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.Deferred, [EmissionBlockingReason.TopologyPlanning], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.False(result.Success);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("no readiness, no emission", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RejectsNonPlanarPatch()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, Transform3D.Identity, "cir:sphere", nameof(CirSphereNode), 0, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("only supports planar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RejectsTrimmedCircularPatch()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:0,0,0;1,0,0;1,1,0;0,1,0", null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var trim = new TrimCurveDescriptor(TrimCurveFamily.Circle, "circle", "outer", 0, new ParameterInterval(0, 2 * double.Pi), TrimCurveCapability.ExactSupported);
        var patch = new FacePatchDescriptor(source, [trim], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("supports only rectangular untrimmed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarPayloadBuilder_RejectsNonPlanarSource()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, Transform3D.Identity, "cir:sphere", nameof(CirSphereNode), 0, FacePatchOrientationRole.Forward);
        var success = PlanarPatchPayloadBuilder.TryBuildRectanglePayload(source, out _, out var diagnostic);
        Assert.False(success);
        Assert.Contains("not planar", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceSurfaceExtractor_BoxFaces_HaveBoundedPlanarGeometry()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirBoxNode(10, 6, 4));
        var planar = extraction.Descriptors.Where(d => d.Family == SurfacePatchFamily.Planar && d.OwningCirNodeKind == nameof(CirBoxNode)).ToArray();
        Assert.Equal(6, planar.Length);
        Assert.Equal(["top", "bottom", "left", "right", "front", "back"], planar.Select(d => d.ParameterPayloadReference).ToArray());
        Assert.All(planar, d => Assert.NotNull(d.BoundedPlanarGeometry));
        Assert.All(planar, d =>
        {
            var g = d.BoundedPlanarGeometry!.Value;
            Assert.Equal(BoundedPlanarPatchGeometryKind.Rectangle, g.Kind);
            Assert.True((g.Corner10 - g.Corner00).Length > 0d);
            Assert.True((g.Corner01 - g.Corner00).Length > 0d);
            Assert.True(g.Normal.Length > 0d);
        });
    }

    [Fact]
    public void SourceSurfaceExtractor_CylinderCaps_HaveCircularBoundedGeometry()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirCylinderNode(5, 20));
        Assert.Single(extraction.Descriptors.Where(d => d.Family == SurfacePatchFamily.Cylindrical));
        var caps = extraction.Descriptors.Where(d => d.Family == SurfacePatchFamily.Planar && d.ParameterPayloadReference is "cap-top" or "cap-bottom").ToArray();
        Assert.Equal(2, caps.Length);

        var top = Assert.Single(caps.Where(c => c.ParameterPayloadReference == "cap-top"));
        var bottom = Assert.Single(caps.Where(c => c.ParameterPayloadReference == "cap-bottom"));
        Assert.Equal(BoundedPlanarPatchGeometryKind.Circle, top.BoundedPlanarGeometry!.Value.Kind);
        Assert.Equal(BoundedPlanarPatchGeometryKind.Circle, bottom.BoundedPlanarGeometry!.Value.Kind);
        Assert.Equal(new Point3D(0, 0, 10), top.BoundedPlanarGeometry!.Value.Center);
        Assert.Equal(new Point3D(0, 0, -10), bottom.BoundedPlanarGeometry!.Value.Center);
        Assert.True(top.BoundedPlanarGeometry!.Value.Normal.Z > 0);
        Assert.True(bottom.BoundedPlanarGeometry!.Value.Normal.Z < 0);
        Assert.Equal(5d, top.BoundedPlanarGeometry!.Value.Radius, 8);
        Assert.Equal(5d, bottom.BoundedPlanarGeometry!.Value.Radius, 8);
    }

    [Fact]
    public void SourceSurfaceExtractor_CylinderCaps_RespectTranslationTransform()
    {
        var node = new CirTransformNode(new CirCylinderNode(5, 20), Transform3D.CreateTranslation(new Vector3D(2, -3, 7)));
        var extraction = SourceSurfaceExtractor.Extract(node);
        var top = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-top"));
        var bottom = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-bottom"));
        Assert.Equal(new Point3D(2, -3, 17), top.BoundedPlanarGeometry!.Value.Center);
        Assert.Equal(new Point3D(2, -3, -3), bottom.BoundedPlanarGeometry!.Value.Center);
        Assert.Equal(5d, top.BoundedPlanarGeometry!.Value.Radius, 8);
        Assert.Equal(5d, bottom.BoundedPlanarGeometry!.Value.Radius, 8);
    }

    [Fact]
    public void PlanarPayloadBuilder_RejectsCircularCapGeometry()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirCylinderNode(3, 8));
        var top = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-top"));
        var success = PlanarPatchPayloadBuilder.TryBuildRectanglePayload(top, out _, out var diagnostic);
        Assert.False(success);
        Assert.Contains("circular planar cap emission is deferred", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanarSurfaceMaterializer_DoesNotEmitCircularCapYet()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirCylinderNode(3, 8));
        var top = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-top"));
        var ready = PlanarPatchPayloadBuilder.TryBuildRectanglePayload(top, out var payload, out _);
        Assert.False(ready);
        Assert.Null(payload);

        var patch = new FacePatchDescriptor(top, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.False(result.Success);
        Assert.Null(result.Body);
    }

    [Fact]
    public void PlanarPayloadBuilder_BoxTopFace_DerivesRect3d()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirBoxNode(10, 6, 4));
        var top = Assert.Single(extraction.Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var success = PlanarPatchPayloadBuilder.TryBuildRectanglePayload(top, out _, out var diagnostic);
        Assert.True(success);
        Assert.Contains("derived rect3d payload from bounded planar source geometry", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanarSurfaceMaterializer_BoxTopFace_FromSourceSurface_EmitsTopology()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirBoxNode(10, 6, 4));
        var top = Assert.Single(extraction.Descriptors.Where(d => d.ParameterPayloadReference == "top"));
        var ready = PlanarPatchPayloadBuilder.TryBuildRectanglePayload(top, out var payload, out var payloadDiagnostic);
        Assert.True(ready);
        Assert.StartsWith("rect3d:", payload);
        Assert.Contains("derived rect3d payload", payloadDiagnostic, StringComparison.OrdinalIgnoreCase);

        var patch = new FacePatchDescriptor(top with { ParameterPayloadReference = payload }, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.True(result.Success);
        Assert.NotNull(result.Body);
        Assert.Single(result.Body!.Topology.Faces);
        Assert.Single(result.Body.Topology.Loops);
        Assert.Equal(4, result.Body.Topology.Edges.Count());
        Assert.Equal(4, result.Body.Topology.Vertices.Count());
    }

    [Fact]
    public void PlanarPayloadBuilder_RejectsDescriptorWithoutBoundedGeometry()
    {
        var roleOnly = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "top", null, Transform3D.Identity, "cir:box:top", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var success = PlanarPatchPayloadBuilder.TryBuildRectanglePayload(roleOnly, out _, out var diagnostic);
        Assert.False(success);
        Assert.Contains("does not encode bounded rectangle corners", diagnostic, StringComparison.OrdinalIgnoreCase);
    }
}
