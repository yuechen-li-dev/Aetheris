using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.EdgeFinishing;

namespace Aetheris.Kernel.Core.Tests.Brep.EdgeFinishing;

public sealed class BrepBoundedChamferCornerJudgmentTests
{
    [Fact]
    public void ChamferAxisAlignedBoxSingleCorner_Reports_Judgment_Rejection_Reasons_When_No_Geometry_Candidate_Is_Admissible()
    {
        var box = new AxisAlignedBoxExtents(0d, 10d, 0d, 10d, 0d, 1d);
        var result = BrepBoundedChamfer.ChamferAxisAlignedBoxSingleCorner(
            box,
            BrepBoundedChamferCorner.XMaxYMaxZMax,
            distance: 2d);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("No bounded corner-resolution candidate was admissible", StringComparison.Ordinal));
    }

    [Fact]
    public void ChamferTrustedPolyhedralSingleCorner_Builds_NonOrthogonalTriangularPrismCorner()
    {
        var prism = BrepPrimitives.CreateTriangularPrism(baseWidth: 8d, baseDepth: 6d, height: 10d);
        Assert.True(prism.IsSuccess);

        var result = BrepBoundedChamfer.ChamferTrustedPolyhedralSingleCorner(
            prism.Value,
            BrepBoundedChamferCorner.XMaxYMaxZMax,
            distance: 1d);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(6, result.Value.Topology.Faces.Count());
        Assert.Equal(12, result.Value.Topology.Edges.Count());
        Assert.Equal(8, result.Value.Topology.Vertices.Count());
    }
}
