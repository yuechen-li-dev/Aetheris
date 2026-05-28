using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class ProfileChamferExtrudeLabTests
{
    private static string Stable(ProfileChamferExtrudeRow r) => string.Join("|",
        r.CaseName,
        r.Status,
        r.Succeeded,
        r.Topology.VertexCount,
        r.Topology.EdgeCount,
        r.Topology.FaceCount,
        r.Topology.PlanarFaceCount,
        r.Topology.CylindricalFaceCount,
        r.Topology.SideFaceCount,
        r.Topology.ChamferFaceCount,
        r.Topology.LoopCount,
        r.Topology.CoedgeCount,
        r.Topology.Bounds,
        string.Join(",", r.Step.PresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", r.Step.MissingRequiredMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", r.Step.AbsentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", r.Step.UnexpectedPresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", r.Diagnostics.OrderBy(x => x, StringComparer.Ordinal)),
        r.Recommendation);

    [Fact]
    public void ProfileChamferExtrudeLab_IsDeterministic()
    {
        var first = ProfileChamferExtrudeLab.RunAll();
        var second = ProfileChamferExtrudeLab.RunAll();

        Assert.Equal(first.Select(Stable), second.Select(Stable));
    }

    [Theory]
    [InlineData("canonical-centered-box", "[-5,-4,0]..[5,4,6]")]
    [InlineData("larger-valid-chamfer", "[-5,-4,0]..[5,4,6]")]
    [InlineData("non-square-rectangle", "[-6,-2.5,0]..[6,2.5,7]")]
    public void ProfileChamferExtrudeLab_ValidCasesEmitExpectedTopology(string caseName, string expectedBounds)
    {
        var row = ProfileChamferExtrudeLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.True(row.Succeeded);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.Topology.BodyProduced);
        Assert.Equal(10, row.Topology.VertexCount);
        Assert.Equal(15, row.Topology.EdgeCount);
        Assert.Equal(7, row.Topology.FaceCount);
        Assert.Equal(7, row.Topology.PlanarFaceCount);
        Assert.Equal(0, row.Topology.CylindricalFaceCount);
        Assert.Equal(5, row.Topology.SideFaceCount);
        Assert.Equal(1, row.Topology.ChamferFaceCount);
        Assert.Equal(7, row.Topology.LoopCount);
        Assert.Equal(30, row.Topology.CoedgeCount);
        Assert.Equal(expectedBounds, row.Topology.Bounds);
        Assert.Equal("profile-chamfer-extrude-ready-for-production-evaluation", row.Recommendation);
    }

    [Fact]
    public void ProfileChamferExtrudeLab_StepSmokePasses()
    {
        var row = ProfileChamferExtrudeLab.RunAll().Single(x => x.CaseName == "canonical-centered-box");

        Assert.True(row.Step.Exported);
        Assert.Empty(row.Step.MissingRequiredMarkers);
        Assert.Empty(row.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", row.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", row.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", row.Step.PresentMarkers);
        Assert.Contains("PLANE", row.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", row.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", row.Step.AbsentMarkers);
        Assert.Contains("edge-profile-x1-step-smoke-succeeded", row.Diagnostics);
    }

    [Fact]
    public void ProfileChamferExtrudeLab_CandidatePathExcludesEdgeSweepChamferMutationAndBoolean()
    {
        var row = ProfileChamferExtrudeLab.RunAll().Single(x => x.CaseName == "canonical-centered-box");

        Assert.Contains("edge-profile-x1-no-air-edge-sweep-used", row.Diagnostics);
        Assert.Contains("edge-profile-x1-no-brep-bounded-chamfer-used", row.Diagnostics);
        Assert.Contains("edge-profile-x1-no-topology-graft-used", row.Diagnostics);
        Assert.Contains("edge-profile-x1-no-3d-boolean-used", row.Diagnostics);
        Assert.Contains("edge-profile-x1-chamfer-face-identified", row.Diagnostics);
    }

    [Theory]
    [InlineData("invalid-zero-chamfer-distance", "edge-profile-x1-invalid-chamfer-distance-rejected")]
    [InlineData("invalid-too-large-chamfer-distance", "edge-profile-x1-chamfer-distance-too-large-rejected")]
    [InlineData("invalid-width", "edge-profile-x1-invalid-dimensions-rejected")]
    [InlineData("invalid-depth", "edge-profile-x1-invalid-dimensions-rejected")]
    [InlineData("invalid-height", "edge-profile-x1-invalid-dimensions-rejected")]
    [InlineData("invalid-non-finite-width", "edge-profile-x1-invalid-dimensions-rejected")]
    public void ProfileChamferExtrudeLab_InvalidCasesRejectBeforeExtrusion(string caseName, string expectedDiagnostic)
    {
        var row = ProfileChamferExtrudeLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.False(row.Succeeded);
        Assert.False(row.Topology.BodyProduced);
        Assert.Contains(expectedDiagnostic, row.Diagnostics);
        Assert.DoesNotContain("edge-profile-x1-profile-extrude-attempted", row.Diagnostics);
        Assert.Equal("profile-chamfer-extrude-invalid-rejected", row.Recommendation);
    }

    [Fact]
    public void ProfileChamferExtrudeLab_RecommendationsAreFinite()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "profile-chamfer-extrude-ready-for-production-evaluation",
            "profile-chamfer-extrude-needs-profile-validation-hardening",
            "profile-chamfer-extrude-needs-emitter-parity-work",
            "profile-chamfer-extrude-invalid-rejected",
            "profile-chamfer-extrude-deferred",
        };

        foreach (var row in ProfileChamferExtrudeLab.RunAll())
        {
            Assert.Contains(row.Recommendation, allowed);
        }
    }
}
