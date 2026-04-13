using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.EdgeFinishing;

namespace Aetheris.Kernel.Core.Tests.Brep.EdgeFinishing;

public sealed class BrepBoundedConcaveChamferPreflightTests
{
    [Fact]
    public void ResolveInternalConcaveVerticalEdge_Succeeds_ForCanonicalLRoot_WithBoundedDistance()
    {
        var composition = CreateLRootComposition();

        var result = BrepBoundedConcaveChamferPreflight.ResolveInternalConcaveVerticalEdge(
            composition,
            BrepBoundedChamferEdge.InnerXMaxYMax,
            distance: 1d);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.True(result.Value.MaxAllowedDistance > 1d);
        Assert.Equal(0d, result.Value.MinZ, 9);
        Assert.Equal(1d, result.Value.MaxZ, 9);
    }

    [Fact]
    public void ChamferTrustedPolyhedralSingleInternalConcaveEdge_UsesJudgmentAndReturnsExplicitConstructionBlocker()
    {
        var body = CreateLRootBody();

        var result = BrepBoundedChamfer.ChamferTrustedPolyhedralSingleInternalConcaveEdge(
            body,
            BrepBoundedChamferEdge.InnerXMaxYMax,
            distance: 1d);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("local loop rewrite construction is not yet implemented", StringComparison.Ordinal));
    }

    private static SafeBooleanComposition CreateLRootComposition()
    {
        var cells = new[]
        {
            new AxisAlignedBoxExtents(0d, 2d, 0d, 2d, 0d, 1d),
            new AxisAlignedBoxExtents(2d, 4d, 0d, 2d, 0d, 1d),
            new AxisAlignedBoxExtents(0d, 2d, 2d, 4d, 0d, 1d),
        };

        return new SafeBooleanComposition(
            new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 1d),
            [],
            SafeBooleanRootDescriptor.FromBox(new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 1d)),
            OccupiedCells: cells);
    }

    private static BrepBody CreateLRootBody()
    {
        var composition = CreateLRootComposition();
        var built = BrepBooleanOrthogonalUnionBuilder.BuildFromCells(composition.OccupiedCells!);
        Assert.True(built.IsSuccess);
        return new BrepBody(
            built.Value.Topology,
            built.Value.Geometry,
            built.Value.Bindings,
            built.Value.Topology.Vertices
                .Where(v => built.Value.TryGetVertexPoint(v.Id, out _))
                .ToDictionary(v => v.Id, v =>
                {
                    built.Value.TryGetVertexPoint(v.Id, out var point);
                    return point;
                }),
            composition,
            built.Value.ShellRepresentation);
    }
}
