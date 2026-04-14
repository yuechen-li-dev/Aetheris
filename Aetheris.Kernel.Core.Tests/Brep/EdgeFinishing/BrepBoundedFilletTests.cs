using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.EdgeFinishing;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Tests.Brep.EdgeFinishing;

public sealed class BrepBoundedFilletTests
{
    [Fact]
    public void FilletTrustedPolyhedralSingleInternalConcaveEdge_Builds_CylindricalFace_ForCanonicalSelection()
    {
        var source = CreatePlanarSourceWithLRootComposition();
        var preflight = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdge(
            source.SafeBooleanComposition!,
            BrepBoundedManufacturingFilletEdge.InnerXMaxYMax,
            radius: 1.5d);
        Assert.True(preflight.IsSuccess);

        var result = BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(source, preflight.Value, 1.5d);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Contains(
            result.Value.Bindings.FaceBindings,
            face => result.Value.Geometry.GetSurface(face.SurfaceGeometryId).Kind == SurfaceGeometryKind.Cylinder);
    }

    [Fact]
    public void FilletTrustedPolyhedralSingleInternalConcaveEdge_Rejects_NonPlanarSource_ThroughJudgmentPath()
    {
        var cylinder = BrepPrimitives.CreateCylinder(5d, 10d);
        Assert.True(cylinder.IsSuccess);

        var source = new BrepBody(
            cylinder.Value.Topology,
            cylinder.Value.Geometry,
            cylinder.Value.Bindings,
            vertexPoints: null,
            safeBooleanComposition: CreateCanonicalComposition());
        var preflight = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdge(
            source.SafeBooleanComposition!,
            BrepBoundedManufacturingFilletEdge.InnerXMaxYMax,
            radius: 1.5d);
        Assert.True(preflight.IsSuccess);

        var result = BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(source, preflight.Value, 1.5d);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("No bounded single-edge/chained fillet candidate was admissible", StringComparison.Ordinal));
    }

    [Fact]
    public void FilletTrustedPolyhedralSingleInternalConcaveEdge_Builds_ChainedSameRadiusCylindricalFillets_ForAdjacentPair()
    {
        var source = CreatePlanarSourceWithNotchComposition();
        var preflight = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdges(
            source.SafeBooleanComposition!,
            [BrepBoundedManufacturingFilletEdge.InnerXMaxYMin, BrepBoundedManufacturingFilletEdge.InnerXMaxYMax],
            radius: 1d);
        Assert.True(preflight.IsSuccess, string.Join(Environment.NewLine, preflight.Diagnostics.Select(d => d.Message)));

        var result = BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(source, preflight.Value, 1d);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.True(result.Value.Bindings.FaceBindings.Count(binding => result.Value.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.Cylinder) >= 2);
    }

    [Fact]
    public void FilletTrustedPolyhedralSingleInternalConcaveEdge_Builds_ChainedCylindricalTermination_ForSameRadiusFollowOnPair()
    {
        var source = CreatePlanarSourceWithNotchComposition();
        var preflight = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdges(
            source.SafeBooleanComposition!,
            [BrepBoundedManufacturingFilletEdge.InnerXMaxYMin, BrepBoundedManufacturingFilletEdge.InnerXMaxYMax],
            radius: 1d);
        Assert.True(preflight.IsSuccess);

        var firstPass = BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(source, preflight.Value, 1d);
        Assert.True(firstPass.IsSuccess, string.Join(Environment.NewLine, firstPass.Diagnostics.Select(d => d.Message)));

        var secondPass = BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(firstPass.Value, preflight.Value, 1d);

        Assert.True(secondPass.IsSuccess, string.Join(Environment.NewLine, secondPass.Diagnostics.Select(d => d.Message)));
        Assert.Contains(
            secondPass.Value.Bindings.FaceBindings,
            binding => secondPass.Value.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.Cylinder);
    }

    [Fact]
    public void FilletTrustedPolyhedralSingleInternalConcaveEdge_Rejects_ChainedCylindricalTermination_ForMismatchedRadius()
    {
        var source = CreatePlanarSourceWithNotchComposition();
        var firstPreflight = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdge(
            source.SafeBooleanComposition!,
            BrepBoundedManufacturingFilletEdge.InnerXMaxYMin,
            radius: 0.5d);
        Assert.True(firstPreflight.IsSuccess);

        var firstPass = BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(source, firstPreflight.Value, 0.5d);
        Assert.True(firstPass.IsSuccess, string.Join(Environment.NewLine, firstPass.Diagnostics.Select(d => d.Message)));

        var chainedPreflight = BrepBoundedManufacturingFilletPreflight.ResolveInternalConcaveVerticalEdges(
            source.SafeBooleanComposition!,
            [BrepBoundedManufacturingFilletEdge.InnerXMaxYMin, BrepBoundedManufacturingFilletEdge.InnerXMaxYMax],
            radius: 1d);
        Assert.True(chainedPreflight.IsSuccess);

        var secondPass = BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge(firstPass.Value, chainedPreflight.Value, 1d);
        Assert.False(secondPass.IsSuccess);
        Assert.Contains(
            secondPass.Diagnostics,
            d => d.Message.Contains("chained_same_radius_fillet_with_cylindrical_termination", StringComparison.Ordinal)
                && d.Message.Contains("hasMatchingRadiusVerticalCylinder=False", StringComparison.Ordinal));
    }

    private static BrepBody CreatePlanarSourceWithLRootComposition()
    {
        var box = BrepPrimitives.CreateBox(30d, 20d, 10d);
        Assert.True(box.IsSuccess);
        return new BrepBody(
            box.Value.Topology,
            box.Value.Geometry,
            box.Value.Bindings,
            vertexPoints: null,
            safeBooleanComposition: CreateCanonicalComposition());
    }

    private static SafeBooleanComposition CreateCanonicalComposition()
        => new(
            new AxisAlignedBoxExtents(0d, 30d, 0d, 20d, 0d, 10d),
            [],
            OccupiedCells:
            [
                new AxisAlignedBoxExtents(0d, 30d, 0d, 10d, 0d, 10d),
                new AxisAlignedBoxExtents(0d, 10d, 10d, 20d, 0d, 10d)
            ]);

    private static BrepBody CreatePlanarSourceWithNotchComposition()
    {
        var box = BrepPrimitives.CreateBox(40d, 30d, 10d);
        Assert.True(box.IsSuccess);
        return new BrepBody(
            box.Value.Topology,
            box.Value.Geometry,
            box.Value.Bindings,
            vertexPoints: null,
            safeBooleanComposition: new SafeBooleanComposition(
                new AxisAlignedBoxExtents(0d, 40d, 0d, 30d, 0d, 10d),
                [],
                OccupiedCells:
                [
                    new AxisAlignedBoxExtents(0d, 30d, 0d, 30d, 0d, 10d),
                    new AxisAlignedBoxExtents(30d, 40d, 0d, 10d, 0d, 10d),
                    new AxisAlignedBoxExtents(30d, 40d, 20d, 30d, 0d, 10d)
                ]));
    }
}
