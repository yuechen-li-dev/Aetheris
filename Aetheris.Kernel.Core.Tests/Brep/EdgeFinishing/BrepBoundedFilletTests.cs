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
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("No bounded single-edge fillet candidate", StringComparison.Ordinal));
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
}
