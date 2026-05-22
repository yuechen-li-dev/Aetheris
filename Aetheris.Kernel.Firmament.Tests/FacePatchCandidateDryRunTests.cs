using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FacePatchCandidateDryRunTests
{
    [Fact]
    public void LoopGrouping_BoxMinusCylinder_GroupsBaseAndToolLoops()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));

        var result = FacePatchCandidateGenerator.Generate(root);

        var baseCandidates = result.Candidates.Where(c => c.CandidateRole == "base-surface-candidate" && c.RetentionRole == FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool).ToArray();
        var toolCandidate = Assert.Single(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        Assert.NotEmpty(baseCandidates);
        Assert.All(baseCandidates, c => Assert.NotEmpty(c.RetainedRegionLoopGroups));
        Assert.All(baseCandidates.SelectMany(c => c.RetainedRegionLoopGroups), g => Assert.Equal(RetainedRegionLoopOrientationPolicy.UseCandidateOrientation, g.OrientationPolicy));
        Assert.All(toolCandidate.RetainedRegionLoopGroups.Where(g => g.Readiness is RetainedRegionLoopGroupReadiness.ExactReady or RetainedRegionLoopGroupReadiness.SpecialCaseReady), g => Assert.Equal(RetainedRegionLoopOrientationPolicy.ReverseForToolCavity, g.OrientationPolicy));
        Assert.False(result.TopologyAssemblyImplemented);
    }

    [Fact]
    public void LoopScaffold_BoxMinusCylinder_ProducesTrimLoopDescriptors()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Candidates, c => c.CandidateRole == "base-surface-candidate" && c.RetentionRole == FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool);
        Assert.Contains(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        Assert.Contains(result.Candidates, c => c.RetentionStatus == FacePatchRetentionStatus.KnownTrimmedSurface);
        Assert.Contains(result.Candidates, c => c.CandidateRole == "base-surface-candidate" && c.RetainedRegionLoops.Any(l => l.SourceSurfaceFamily == SurfacePatchFamily.Planar));
        Assert.Contains(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetainedRegionLoops.Any(l => l.OppositeSurfaceFamily == SurfacePatchFamily.Planar));
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.TrimCurveFamily is TrimCurveFamily.Line or TrimCurveFamily.Circle or TrimCurveFamily.Ellipse);
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.Status is RetainedRegionLoopStatus.ExactReady or RetainedRegionLoopStatus.SpecialCaseReady);
        Assert.False(result.TopologyAssemblyImplemented);
    }

    [Fact]
    public void LoopScaffold_BoxMinusSphere_ProducesCircularTrimLoopDescriptors()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirSphereNode(3));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Candidates, c =>
            c.SourceSurface.Family == SurfacePatchFamily.Spherical
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase
            && c.RetentionStatus == FacePatchRetentionStatus.KnownTrimmedSurface);
        Assert.Contains(result.TrimCapabilitySummaries, t =>
            ((t.FamilyA == SurfacePatchFamily.Planar && t.FamilyB == SurfacePatchFamily.Spherical)
             || (t.FamilyA == SurfacePatchFamily.Spherical && t.FamilyB == SurfacePatchFamily.Planar))
            && t.Classification == TrimCapabilityClassification.ExactSupported
            && t.CurveFamilies.Contains(TrimCurveFamily.Circle));
        Assert.Contains(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Spherical && c.RetainedRegionLoops.Any(l => l.TrimCurveFamily == TrimCurveFamily.Circle && l.Status == RetainedRegionLoopStatus.ExactReady));
        Assert.Contains(result.Candidates, c => c.SourceSurface.Family == SurfacePatchFamily.Spherical && c.RetainedRegionLoopGroups.Any(g => g.OrientationPolicy == RetainedRegionLoopOrientationPolicy.ReverseForToolCavity && g.Readiness == RetainedRegionLoopGroupReadiness.ExactReady));
        Assert.DoesNotContain(result.Candidates, c => c.Diagnostics.Any(d => d.Contains("generic unsupported", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void LoopScaffold_BoxMinusTorus_ProducesDeferredLoopDiagnostics()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirTorusNode(4, 1));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Candidates, c =>
            c.SourceSurface.Family == SurfacePatchFamily.Toroidal
            && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase
            && c.Readiness == FacePatchCandidateReadiness.TrimDeferred
            && c.RetentionStatus == FacePatchRetentionStatus.Deferred);
        Assert.Contains(result.Candidates.SelectMany(c => c.Diagnostics), d => d.Contains("quartic/algebraic", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Candidates, c =>
            c.SourceSurface.Family == SurfacePatchFamily.Toroidal
            && c.RetainedRegionLoops.Any(l => l.Status == RetainedRegionLoopStatus.Deferred));
    }

    [Fact]
    public void LoopScaffold_NonSubtract_DoesNotInventLoops()
    {
        var root = new CirUnionNode(new CirBoxNode(10, 10, 10), new CirSphereNode(2));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.False(result.IsSuccess);
        Assert.All(result.Candidates, c => Assert.Equal(FacePatchRetentionRole.NotApplicable, c.RetentionRole));
        Assert.All(result.Candidates, c => Assert.Equal(FacePatchRetentionStatus.Deferred, c.RetentionStatus));
        Assert.All(result.Candidates, c => Assert.Empty(c.RetainedRegionLoops));
        Assert.All(result.Candidates, c => Assert.True(c.RetainedRegionLoopGroups.Count == 0 || c.RetainedRegionLoopGroups.Any(g => g.GroupKind == RetainedRegionLoopGroupKind.NotApplicable)));
    }

    [Fact]
    public void LoopGrouping_DeterministicOrdering()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var first = FacePatchCandidateGenerator.Generate(root);
        var second = FacePatchCandidateGenerator.Generate(root);

        var firstKeys = first.Candidates.SelectMany(c => c.RetainedRegionLoopGroups.Select(g => g.OrderingKey)).ToArray();
        var secondKeys = second.Candidates.SelectMany(c => c.RetainedRegionLoopGroups.Select(g => g.OrderingKey)).ToArray();
        Assert.Equal(firstKeys, secondKeys);
    }

    [Fact]
    public void FacePatchDryRun_UsesSourceSurfaceExtractor()
    {
        var root = new CirBoxNode(3, 4, 5);
        var extracted = SourceSurfaceExtractor.Extract(root);

        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(root, new CirSphereNode(1)));

        Assert.Equal(extracted.Descriptors.Count, result.SourceSurfaces.Count(d => d.OwningCirNodeKind == nameof(CirBoxNode)));
    }

    [Fact]
    public void FacePatchDryRun_DoesNotClaimTopologyAssembly()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));

        var result = FacePatchCandidateGenerator.Generate(root);

        Assert.False(result.TopologyAssemblyImplemented);
        Assert.Contains(result.Diagnostics, d => d.Contains("topology-assembly-not-implemented", StringComparison.Ordinal));
    }

    [Fact]
    public void RetainedLoopBinding_BoxMinusCylinder_RealPipeline_BindsInnerCircle()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var result = FacePatchCandidateGenerator.Generate(root);

        var bound = result.Candidates
            .Where(c => c.RetentionRole == FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool)
            .SelectMany(c => c.RetainedRegionLoops)
            .Where(l => l.LoopKind == RetainedRegionLoopKind.InnerTrim && l.TrimCurveFamily == TrimCurveFamily.Circle && l.CircularGeometry is not null)
            .Select(l => l.CircularGeometry!.Value)
            .ToArray();

        Assert.NotEmpty(bound);
        Assert.Contains(bound.Select(b => b.Diagnostic), d => d.Contains("canonical planar/cylindrical inner-circle geometry bound", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RetainedLoopBinding_PlanarCylinder_CreatesInnerCircleGeometry_WithCylindricalEvidence()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, null, BoundedPlanarPatchGeometry.CreateRectangle(new Point3D(-1, -1, 0), new Point3D(1, -1, 0), new Point3D(1, 1, 0), new Point3D(-1, 1, 0), new Vector3D(0, 0, 1)), null, Transform3D.Identity, "source", nameof(CirBoxNode), 1, FacePatchOrientationRole.Forward);
        var opposite = new SourceSurfaceDescriptor(SurfacePatchFamily.Cylindrical, "side", null, new CylindricalSurfaceGeometryEvidence(new Point3D(0, 0, -4), new Vector3D(0, 0, 8), 2d, 8d, new Point3D(0, 0, -4), new Point3D(0, 0, 4)), Transform3D.Identity, "tool", nameof(CirCylinderNode), 2, FacePatchOrientationRole.Forward);

        var ok = RetainedLoopGeometryBinder.TryBindCircularLoop(source, opposite, TrimCurveFamily.Circle, RetainedRegionLoopStatus.SpecialCaseReady, isBase: true, out _, out var circular);

        Assert.True(ok);
        Assert.NotNull(circular);
        Assert.Equal(2d, circular.Value.Radius, 9);
        Assert.Equal(RetainedRegionLoopOrientationPolicy.ReverseForToolCavity, circular.Value.OrientationPolicy);
    }

    [Fact]
    public void RetainedLoopBinding_RequiresPerpendicularPlaneCylinder()
    {
        var source = new SourceSurfaceDescriptor(
            SurfacePatchFamily.Planar,
            null,
            BoundedPlanarPatchGeometry.CreateRectangle(new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 1, 0), new Point3D(0, 1, 0), new Vector3D(0, 0, 1)),
            null,
            Transform3D.Identity,
            "source",
            nameof(CirBoxNode),
            1,
            FacePatchOrientationRole.Forward);
        var opposite = new SourceSurfaceDescriptor(SurfacePatchFamily.Cylindrical, null, null, null, Transform3D.CreateRotationX(Math.PI * 0.5d), "tool", nameof(CirCylinderNode), 2, FacePatchOrientationRole.Forward);

        var ok = RetainedLoopGeometryBinder.TryBindCircularLoop(source, opposite, TrimCurveFamily.Circle, RetainedRegionLoopStatus.ExactReady, isBase: true, out var diagnostic, out var circular);

        Assert.False(ok);
        Assert.Null(circular);
        Assert.Contains("requires plane normal parallel", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RetainedLoopBinding_DoesNotBypassReadiness()
    {
        var source = new SourceSurfaceDescriptor(SurfacePatchFamily.Planar, null, BoundedPlanarPatchGeometry.CreateRectangle(new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 1, 0), new Point3D(0, 1, 0), new Vector3D(0, 0, 1)), null, Transform3D.Identity, "source", nameof(CirBoxNode), 1, FacePatchOrientationRole.Forward);
        var opposite = new SourceSurfaceDescriptor(SurfacePatchFamily.Cylindrical, null, null, null, Transform3D.Identity, "tool", nameof(CirCylinderNode), 2, FacePatchOrientationRole.Forward);

        var ok = RetainedLoopGeometryBinder.TryBindCircularLoop(source, opposite, TrimCurveFamily.Circle, RetainedRegionLoopStatus.Deferred, isBase: true, out _, out var circular);

        Assert.False(ok);
        Assert.Null(circular);
    }
}
