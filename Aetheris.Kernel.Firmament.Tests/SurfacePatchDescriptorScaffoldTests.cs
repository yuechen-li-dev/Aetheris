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
            .Select(i => new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, $"box-face-{i}", null, null, Transform3D.Identity, "cir:box", nameof(Aetheris.Kernel.Core.Cir.CirBoxNode), 0, FacePatchOrientationRole.Forward))
            .ToArray();

        Assert.Equal(6, descriptors.Length);
        Assert.All(descriptors, d => Assert.Equal(SurfacePatchFamily.Planar, d.Family));
        Assert.All(descriptors, d => Assert.False(string.IsNullOrWhiteSpace(d.Provenance)));
    }

    [Fact]
    public void SourceSurfaceDescriptor_Cylinder_ProducesCylindricalAndPlanarDescriptors()
    {
        var side = new SourceSurfaceDescriptor(SurfacePatchFamily.Cylindrical, "side", null, null, Transform3D.Identity, "cir:cylinder:side", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Forward);
        var top = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "cap-top", null, null, Transform3D.Identity, "cir:cylinder:cap-top", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Forward);
        var bottom = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "cap-bottom", null, null, Transform3D.Identity, "cir:cylinder:cap-bottom", nameof(Aetheris.Kernel.Core.Cir.CirCylinderNode), 1, FacePatchOrientationRole.Reversed);

        Assert.Equal(SurfacePatchFamily.Cylindrical, side.Family);
        Assert.Equal(SurfacePatchFamily.Planar, top.Family);
        Assert.Equal(SurfacePatchFamily.Planar, bottom.Family);
    }

    [Fact]
    public void SourceSurfaceDescriptor_Sphere_ProducesSphericalDescriptor()
    {
        var sphere = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, null, Transform3D.Identity, "cir:sphere", nameof(Aetheris.Kernel.Core.Cir.CirSphereNode), 2, FacePatchOrientationRole.Forward);
        Assert.Equal(SurfacePatchFamily.Spherical, sphere.Family);
    }

    [Fact]
    public void SourceSurfaceDescriptor_Torus_ProducesToroidalDescriptorUnsupportedMaterializer()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Toroidal, "torus", null, null, Transform3D.Identity, "cir:torus", nameof(Aetheris.Kernel.Core.Cir.CirTorusNode), 3, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "tool-surface", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.False(eval.IsSuccess);
        Assert.Contains(eval.Rejections, r => r.CandidateName == "surface_family_toroidal" && r.Reason.Contains("deferred", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SurfaceFamilyRegistry_SelectsPlanarForPlanarPatch()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "plane", null, null, Transform3D.Identity, "cir:box", nameof(Aetheris.Kernel.Core.Cir.CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var trim = new TrimCurveDescriptor(TrimCurveFamily.Line, "edge-0", "loop:outer", 0, new ParameterInterval(0, 1), TrimCurveCapability.ExactSupported);
        var patch = new FacePatchDescriptor(source, [trim], [], FacePatchOrientationRole.Forward, "outer", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.True(eval.IsSuccess);
        Assert.Equal("surface_family_planar", eval.Selected?.Name);
    }

    [Fact]
    public void SurfaceFamilyRegistry_RejectsWrongFamilyWithReasons()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, null, Transform3D.Identity, "cir:sphere", nameof(Aetheris.Kernel.Core.Cir.CirSphereNode), 4, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "outer", []);

        var eval = SurfaceFamilyMaterializerRegistry.Evaluate(patch);

        Assert.True(eval.IsSuccess);
        Assert.Equal("surface_family_spherical", eval.Selected?.Name);
        Assert.Contains(eval.Rejections, r => r.CandidateName == "surface_family_planar" && r.Reason.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_UntrimmedBoxFace_EmitsPlanarTopology()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:-1,-1,0;1,-1,0;1,1,0;-1,1,0", null, null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
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
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:0,0,0;1,0,0;1,1,0;0,1,0", null, null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
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
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, null, Transform3D.Identity, "cir:sphere", nameof(CirSphereNode), 0, FacePatchOrientationRole.Forward);
        var patch = new FacePatchDescriptor(source, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("only supports planar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RejectsTrimmedCircularPatch()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:0,0,0;1,0,0;1,1,0;0,1,0", null, null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var trim = new TrimCurveDescriptor(TrimCurveFamily.Circle, "circle", "outer", 0, new ParameterInterval(0, 2 * double.Pi), TrimCurveCapability.ExactSupported);
        var patch = new FacePatchDescriptor(source, [trim], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("supports only rectangular untrimmed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_ReportsInnerCircularLoopPolicy()
    {
        var policy = PlanarSurfaceMaterializer.GetLoopEmissionPolicy();
        Assert.True(policy.SupportsOuterRectangle);
        Assert.True(policy.SupportsOuterCircle);
        Assert.True(policy.SupportsInnerCircle);
        Assert.False(policy.SupportsMultipleInnerLoops);
        Assert.Equal(PlanarSurfaceMaterializer.PlanarLoopSupportStatus.Supported, policy.Status);
        Assert.Contains("supports one rectangular outer loop plus one canonical retained inner circular loop", policy.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanarSurfaceMaterializer_TrimmedCircularInnerLoop_RespectsPolicy()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:-2,-2,0;2,-2,0;2,2,0;-2,2,0", null, null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var inner = new TrimCurveDescriptor(TrimCurveFamily.Circle, "inner-circle", "inner", 1, new ParameterInterval(0, 2 * double.Pi), TrimCurveCapability.ExactSupported);
        var patch = new FacePatchDescriptor(source, [], [[inner]], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);
        Assert.False(result.Success);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("bounded rectangle-with-inner-circle emission path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RejectsMultipleInnerLoops()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "rect3d:-2,-2,0;2,-2,0;2,2,0;-2,2,0", null, null, Transform3D.Identity, "cir:box:face", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var innerA = new TrimCurveDescriptor(TrimCurveFamily.Circle, "inner-circle-a", "inner", 1, new ParameterInterval(0, 2 * double.Pi), TrimCurveCapability.ExactSupported);
        var innerB = new TrimCurveDescriptor(TrimCurveFamily.Circle, "inner-circle-b", "inner", 2, new ParameterInterval(0, 2 * double.Pi), TrimCurveCapability.ExactSupported);
        var patch = new FacePatchDescriptor(source, [], [[innerA], [innerB]], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);

        Assert.False(result.Success);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("multiple inner loops", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RectangleWithInnerCircle_FromRealBoxCylinderEvidence_EmitsTrimmedFace()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var generation = FacePatchCandidateGenerator.Generate(root);
        var candidate = generation.Candidates.First(c =>
            c.RetentionRole == FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool
            && c.SourceSurface.BoundedPlanarGeometry is { Kind: BoundedPlanarPatchGeometryKind.Rectangle }
            && c.RetainedRegionLoops.Count(l => l.LoopKind == RetainedRegionLoopKind.InnerTrim && l.CircularGeometry is not null) == 1);
        var inner = candidate.RetainedRegionLoops.Single(l => l.LoopKind == RetainedRegionLoopKind.InnerTrim && l.CircularGeometry is not null).CircularGeometry!.Value;
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);

        var result = new PlanarSurfaceMaterializer().EmitRectangleWithInnerCircle(new(candidate.SourceSurface, inner, null, readiness));
        Assert.True(result.Success);
        Assert.NotNull(result.Body);
        Assert.Single(result.Body!.Topology.Faces);
        Assert.Equal(2, result.Body.Topology.Loops.Count());
        Assert.Equal(5, result.Body.Topology.Edges.Count());
        var face = result.Body.Topology.Faces.Single();
        Assert.Equal(2, face.LoopIds.Count);
        var edgeBinding = result.Body.Bindings.EdgeBindings.Single(b => b.TrimInterval?.End == 2d * double.Pi);
        Assert.True(result.Body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve));
        Assert.Equal(CurveGeometryKind.Circle3, curve!.Kind);
        Assert.Contains(result.Diagnostics, d => d.Contains("inner loop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RectangleWithInnerCircle_RejectsDeferredReadiness()
    {
        var source = SourceSurfaceExtractor.Extract(new CirBoxNode(10, 10, 10)).Descriptors.First(d => d.BoundedPlanarGeometry?.Kind == BoundedPlanarPatchGeometryKind.Rectangle);
        var circle = new RetainedCircularLoopGeometry(new Point3D(0, 0, 0), new Vector3D(0, 0, 1), 1d, RetainedRegionLoopOrientationPolicy.ReverseForToolCavity, "token", "diag");
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.Deferred, [EmissionBlockingReason.TopologyPlanning], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().EmitRectangleWithInnerCircle(new(source, circle, null, readiness));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("no readiness, no emission", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RectangleWithInnerCircle_RejectsMissingInnerCircleGeometry()
    {
        var source = SourceSurfaceExtractor.Extract(new CirBoxNode(10, 10, 10)).Descriptors.First(d => d.BoundedPlanarGeometry?.Kind == BoundedPlanarPatchGeometryKind.Rectangle);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().EmitRectangleWithInnerCircle(new(source, null, null, readiness));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("inner-circle-missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RectangleWithInnerCircle_RejectsMultipleInnerLoops()
    {
        var source = SourceSurfaceExtractor.Extract(new CirBoxNode(10, 10, 10)).Descriptors.First(d => d.BoundedPlanarGeometry?.Kind == BoundedPlanarPatchGeometryKind.Rectangle);
        var circleA = new RetainedCircularLoopGeometry(new Point3D(0, 0, 0), new Vector3D(0, 0, 1), 1d, RetainedRegionLoopOrientationPolicy.ReverseForToolCavity, "a", "diag");
        var circleB = new RetainedCircularLoopGeometry(new Point3D(1, 0, 0), new Vector3D(0, 0, 1), 1d, RetainedRegionLoopOrientationPolicy.ReverseForToolCavity, "b", "diag");
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().EmitRectangleWithInnerCircle(new(source, null, [circleA, circleB], readiness));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("multiple inner loops", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarPayloadBuilder_RejectsNonPlanarSource()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Spherical, "sphere", null, null, Transform3D.Identity, "cir:sphere", nameof(CirSphereNode), 0, FacePatchOrientationRole.Forward);
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
    public void SourceSurfaceExtractor_CylinderSide_HasCylindricalEvidence()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirCylinderNode(5, 20));
        var side = Assert.Single(extraction.Descriptors.Where(d => d.Family == SurfacePatchFamily.Cylindrical));
        var evidence = Assert.IsType<CylindricalSurfaceGeometryEvidence>(side.CylindricalGeometryEvidence!.Value);
        Assert.Equal(new Point3D(0, 0, -10), evidence.AxisOrigin);
        Assert.Equal(5d, evidence.Radius, 8);
        Assert.Equal(20d, evidence.Height, 8);
        Assert.Equal(new Point3D(0, 0, -10), evidence.BottomCenter);
        Assert.Equal(new Point3D(0, 0, 10), evidence.TopCenter);
    }

    [Fact]
    public void SourceSurfaceExtractor_CylinderSide_TranslationTransform_PreservesEvidence()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirTransformNode(new CirCylinderNode(5, 20), Transform3D.CreateTranslation(new Vector3D(2, -3, 7))));
        var side = Assert.Single(extraction.Descriptors.Where(d => d.Family == SurfacePatchFamily.Cylindrical));
        var evidence = Assert.IsType<CylindricalSurfaceGeometryEvidence>(side.CylindricalGeometryEvidence!.Value);
        Assert.Equal(new Point3D(2, -3, -3), evidence.AxisOrigin);
        Assert.Equal(new Point3D(2, -3, -3), evidence.BottomCenter);
        Assert.Equal(new Point3D(2, -3, 17), evidence.TopCenter);
        Assert.Equal(5d, evidence.Radius, 8);
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
    public void PlanarSurfaceMaterializer_CylinderTopCap_FromSourceSurface_EmitsCircularTopology()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirCylinderNode(3, 8));
        var top = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-top"));

        var patch = new FacePatchDescriptor(top, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);

        Assert.True(result.Success);
        Assert.NotNull(result.Body);
        Assert.Single(result.Body!.Topology.Faces);
        Assert.Single(result.Body.Topology.Loops);
        Assert.Single(result.Body.Topology.Edges);
        Assert.Single(result.Body.Topology.Vertices);
        var edge = Assert.Single(result.Body.Topology.Edges);
        Assert.Equal(edge.StartVertexId, edge.EndVertexId);
        var edgeBinding = Assert.Single(result.Body.Bindings.EdgeBindings);
        Assert.Equal(0d, edgeBinding.TrimInterval!.Value.Start);
        Assert.Equal(2d * double.Pi, edgeBinding.TrimInterval!.Value.End);
        Assert.True(result.Body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve));
        Assert.Equal(CurveGeometryKind.Circle3, curve!.Kind);
        Assert.Contains(result.Diagnostics, d => d.Contains("circular", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanarSurfaceMaterializer_CylinderBottomCap_FromSourceSurface_EmitsCircularTopology()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirCylinderNode(3, 8));
        var bottom = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-bottom"));

        var patch = new FacePatchDescriptor(bottom, [], [], FacePatchOrientationRole.Reversed, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);

        Assert.True(result.Success);
        Assert.NotNull(result.Body);
        var surfaceBinding = Assert.Single(result.Body!.Bindings.FaceBindings);
        Assert.True(result.Body.Geometry.TryGetSurface(surfaceBinding.SurfaceGeometryId, out var surface));
        Assert.Equal(SurfaceGeometryKind.Plane, surface!.Kind);
        Assert.True(surface.Plane!.Value.Normal.Z < 0d);
    }

    [Fact]
    public void PlanarSurfaceMaterializer_RejectsCircularCap_WhenReadinessDeferred()
    {
        var extraction = SourceSurfaceExtractor.Extract(new CirCylinderNode(3, 8));
        var top = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-top"));

        var patch = new FacePatchDescriptor(top, [], [], FacePatchOrientationRole.Forward, "outer", []);
        var readiness = new MaterializationReadinessReport(true, EmissionReadiness.Deferred, [EmissionBlockingReason.TopologyPlanning], [], 1, 1, 1, 0, 0, 0, 0, [], false);
        var result = new PlanarSurfaceMaterializer().Emit(patch, readiness);

        Assert.False(result.Success);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics, d => d.Contains("no readiness, no emission", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void SourceSurfaceExtractor_DefersShearedCylinderCapCircularGeometry_AndEmitterRejectsMissingCircle()
    {
        var nonUniformScale = Transform3D.CreateScale(new Vector3D(2d, 1d, 1d));
        var extraction = SourceSurfaceExtractor.Extract(new CirTransformNode(new CirCylinderNode(3, 8), nonUniformScale));
        var top = Assert.Single(extraction.Descriptors.Where(c => c.ParameterPayloadReference == "cap-top"));

        Assert.Null(top.BoundedPlanarGeometry);
        Assert.Contains(extraction.Diagnostics, d => d.Code == "cylinder-cap-circular-geometry-deferred");

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
        var roleOnly = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, "top", null, null, Transform3D.Identity, "cir:box:top", nameof(CirBoxNode), 0, FacePatchOrientationRole.Forward);
        var success = PlanarPatchPayloadBuilder.TryBuildRectanglePayload(roleOnly, out _, out var diagnostic);
        Assert.False(success);
        Assert.Contains("does not encode bounded rectangle corners", diagnostic, StringComparison.OrdinalIgnoreCase);
    }
}
