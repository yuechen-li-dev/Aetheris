using Aetheris.Kernel.Core.Brep.Boolean;
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
}
