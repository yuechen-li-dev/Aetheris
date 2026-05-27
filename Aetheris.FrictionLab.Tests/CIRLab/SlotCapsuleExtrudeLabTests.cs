using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class SlotCapsuleExtrudeLabTests
{
    [Fact]
    public void DeterministicAcrossRuns()
    {
        var a = SlotCapsuleExtrudeLab.RunAll();
        var b = SlotCapsuleExtrudeLab.RunAll();
        Assert.Equal(string.Join(";", a.Select(x => $"{x.CaseName}:{x.Status}:{x.Recommendation}:{string.Join(',', x.Diagnostics)}")), string.Join(";", b.Select(x => $"{x.CaseName}:{x.Status}:{x.Recommendation}:{string.Join(',', x.Diagnostics)}")));
    }

    [Theory]
    [InlineData("valid-slot-centered-horizontal")]
    [InlineData("valid-slot-offcenter-horizontal")]
        [InlineData("valid-slot-reversed-input")]
    public void ValidSlotProfilesValidateAndExtrudeIsAttemptedButBlocked(string caseName)
    {
        var row = SlotCapsuleExtrudeLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.Equal(LabProfileStatus.Deferred, row.Status);
        Assert.Contains("v2-x6-slot-profile-validated", row.Diagnostics);
        Assert.Contains("v2-x6-slot-extrude-attempted", row.Diagnostics);
        Assert.Contains("v2-x6-slot-extrude-blocked:current-emitter-assumes-full-circle-hole-loops", row.Diagnostics);
        Assert.Equal("slot-capsule-extrude-needs-emitter-support", row.Recommendation);
    }

    [Theory]
    [InlineData("invalid-slot-outside")]
    [InlineData("invalid-slot-touches-boundary")]
    [InlineData("invalid-slot-crosses-boundary")]
    [InlineData("invalid-slot-radius")]
    [InlineData("invalid-slot-length")]
    public void InvalidCasesRejectedBeforeBrepEmission(string caseName)
    {
        var row = SlotCapsuleExtrudeLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.Equal(LabProfileStatus.Failed, row.Status);
        Assert.False(row.ExtrusionAttempted);
        Assert.Equal("slot-capsule-invalid-rejected", row.Recommendation);
    }

    [Theory]
    [InlineData("deferred-slot-degenerate-circle", "v2-x6-slot-degenerate-circle-deferred")]
    [InlineData("deferred-slot-rotated", "v2-x6-slot-rotated-deferred")]
    [InlineData("deferred-slot-vertical", "v2-x6-slot-vertical-deferred")]
    public void DeferredCasesAreExplicit(string caseName, string diagnostic)
    {
        var row = SlotCapsuleExtrudeLab.RunAll().Single(x => x.CaseName == caseName);
        Assert.Equal(LabProfileStatus.Deferred, row.Status);
        Assert.Contains(diagnostic, row.Diagnostics);
        Assert.False(row.ExtrusionAttempted);
    }

    [Fact]
    public void RecommendationsAreFiniteAllowedSet()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "slot-capsule-profile-valid",
            "slot-capsule-extrude-ready-for-production-evaluation",
            "slot-capsule-extrude-needs-emitter-support",
            "slot-capsule-invalid-rejected",
            "slot-capsule-deferred-topology"
        };

        foreach (var row in SlotCapsuleExtrudeLab.RunAll()) Assert.Contains(row.Recommendation, allowed);
    }
}
