using System.Numerics;
using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferPatchLabTests
{
    [Fact]
    public void Rows_AreDeterministic()
    {
        var a = AirChamferPatchLab.RunAll();
        var b = AirChamferPatchLab.RunAll();
        Assert.Equal(a.Select(Serialize), b.Select(Serialize));
    }

    [Fact]
    public void Canonical_Succeeds_WithExpectedTopologyAndDiagnostics()
    {
        var row = AirChamferPatchLab.Run(AirChamferPatchLab.Canonical(10d, 1d));
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.OffsetCurveAConstructed);
        Assert.True(row.OffsetCurveBConstructed);
        Assert.True(row.Topology.PatchProduced);
        Assert.Equal(4, row.Topology.VertexCount);
        Assert.Equal(4, row.Topology.EdgeCount);
        Assert.Equal(1, row.Topology.FaceCount);
        Assert.Equal(1, row.Topology.PlanarFaceCount);
        Assert.Equal(1, row.Topology.BoundaryLoopCount);
        Assert.Contains("edge-x2-concave-planar-edge-accepted", row.Diagnostics);
        Assert.Contains("edge-x2-offset-curve-a-constructed", row.Diagnostics);
        Assert.Contains("edge-x2-offset-curve-b-constructed", row.Diagnostics);
        Assert.Contains("edge-x2-ruled-chamfer-patch-constructed", row.Diagnostics);
        Assert.Contains("edge-x2-no-3d-boolean-used", row.Diagnostics);
        Assert.NotNull(row.Artifact);
        Assert.True(row.Artifact!.Area > 0d);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void InvalidDistance_Rejects(double distance)
    {
        var row = AirChamferPatchLab.Run(AirChamferPatchLab.Canonical(10d, distance));
        Assert.Equal(LabProfileStatus.Failed, row.Status);
        Assert.Contains("edge-x2-invalid-distance-rejected", row.Diagnostics);
        Assert.False(row.Topology.PatchProduced);
    }

    [Fact]
    public void InvalidFaceAdjacency_RejectsBeforePatch()
    {
        var c = AirChamferPatchLab.Canonical() with { FaceBNormal = new(1f, 0f, 0f) };
        var row = AirChamferPatchLab.Run(c);
        Assert.Equal(LabProfileStatus.Failed, row.Status);
        Assert.Contains(row.Diagnostics, x => x is "edge-x2-invalid-face-adjacency-rejected" or "edge-x2-2-nonorthogonal-patch-rejected:offset-angle-unstable");
        Assert.DoesNotContain("edge-x2-ruled-chamfer-patch-constructed", row.Diagnostics);
    }

    [Fact]
    public void Recommendations_AreFiniteAllowedSet()
    {
        foreach (var row in AirChamferPatchLab.RunAll())
            Assert.Contains(row.Recommendation, AirChamferPatchLab.AllowedRecommendations);
    }

    [Fact]
    public void NonOrthogonalSafe_ConstructsPlanarPositivePatch()
    {
        var c = AirChamferPatchLab.Canonical() with { CaseName = "nonorthogonal-safe", FaceBNormal = Vector3.Normalize(new Vector3(1f, 1f, 0f)) };
        var row = AirChamferPatchLab.Run(c);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.Topology.PatchProduced);
        Assert.Equal(4, row.Topology.CoedgeCount);
        Assert.True(row.Artifact!.Area > 0d);
        Assert.Contains("edge-x2-2-nonorthogonal-offset-curves-constructed", row.Diagnostics);
        Assert.Contains("edge-x2-2-nonorthogonal-patch-planarity-validated", row.Diagnostics);
        Assert.Contains("edge-x2-no-3d-boolean-used", row.Diagnostics);
    }

    private static string Serialize(AirChamferPatchRow row)
        => $"{row.CaseName}|{row.Status}|{row.OffsetCurveAConstructed}|{row.OffsetCurveBConstructed}|{row.Topology.PatchProduced}|{row.Topology.VertexCount}|{row.Topology.EdgeCount}|{row.Topology.FaceCount}|{row.Topology.PlanarFaceCount}|{row.Topology.BoundaryLoopCount}|{row.Topology.CoedgeCount}|{row.Recommendation}|{string.Join(',', row.Diagnostics)}";
}
