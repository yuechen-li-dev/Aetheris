using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Verification;

namespace Aetheris.Kernel.Core.Tests.Brep.Verification;

public sealed class BrepSolidInterferenceTests
{
    [Fact]
    public void OverlappingConvexPlanarSolids_ProducePositiveVolumeProof()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new(0, 2, 0, 2, 0, 2)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new(1, 3, 1, 3, 1, 3)).Value;

        var result = BrepSolidInterference.Analyze(left, right);

        Assert.True(result.Status == BrepSolidInterferenceStatus.Interfering, result.Evidence);
        Assert.True(result.PenetrationWitnessMm > 0);
        Assert.True(result.WitnessTetrahedronVolumeMm3 > 0);
        Assert.True(result.IntersectionVertexCount >= 8);
    }

    [Fact]
    public void FaceContact_IsAdmissibleAndHasNoSolidInterference()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new(0, 2, 0, 2, 0, 2)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new(2, 4, 0, 2, 0, 2)).Value;

        var result = BrepSolidInterference.Analyze(left, right);

        Assert.True(result.Status == BrepSolidInterferenceStatus.DisjointOrTouching, result.Evidence);
    }

    [Fact]
    public void CurvedPair_IsOutsideBoundedProofInsteadOfUsingBoundingBoxGuess()
    {
        var sphere = BrepPrimitives.CreateSphere(2).Value;
        var box = BrepBooleanBoxRecognition.CreateBoxFromExtents(new(-1, 1, -1, 1, -1, 1)).Value;

        var result = BrepSolidInterference.Analyze(sphere, box);

        Assert.Equal(BrepSolidInterferenceStatus.Unsupported, result.Status);
        Assert.Contains("outside the convex-planar proof subset", result.Evidence, StringComparison.Ordinal);
    }
}
