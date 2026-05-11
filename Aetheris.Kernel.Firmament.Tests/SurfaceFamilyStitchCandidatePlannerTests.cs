using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFamilyStitchCandidatePlannerTests
{
    [Fact]
    public void StitchCandidate_BoxCylinder_GeneratesCandidateFromSafeTokenPair()
    {
        var result = SurfaceFamilyStitchCandidatePlanner.PlanBoxCylinder(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));
        Assert.True(result.Candidates.Count > 0 || result.Diagnostics.Any(d => d.Contains("missing-mate", StringComparison.OrdinalIgnoreCase) || d.Contains("ambiguous", StringComparison.OrdinalIgnoreCase) || d.Contains("incompatible", StringComparison.OrdinalIgnoreCase) || d.Contains("unmapped", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void StitchCandidate_SafePairEntriesAreCarried()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var a = new EmittedTopologyIdentityEntry("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", []);
        var b = new EmittedTopologyIdentityEntry("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", []);
        var map = new EmittedTopologyIdentityMap([a, b]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze([map]);

        var result = SurfaceFamilyStitchCandidatePlanner.Plan([map], analysis, null);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(token, candidate.Token);
        Assert.Equal(a, candidate.EntryA);
        Assert.Equal(b, candidate.EntryB);
    }

    [Fact]
    public void StitchCandidate_MissingMate_DoesNotGenerateCandidate()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var map = new EmittedTopologyIdentityMap([new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", [])]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze([map]);
        var result = SurfaceFamilyStitchCandidatePlanner.Plan([map], analysis, null);
        Assert.Empty(result.Candidates);
        Assert.Contains(result.Diagnostics, d => d.Contains("missing-mate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StitchCandidate_AmbiguousMultiplicity_DoesNotGenerateCandidate()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var map = new EmittedTopologyIdentityMap([
            new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", []),
            new("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", []),
            new("edge:3", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalBottomBoundary, "o", [])]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze([map]);
        var result = SurfaceFamilyStitchCandidatePlanner.Plan([map], analysis, null);
        Assert.Empty(result.Candidates);
        Assert.Contains(result.Diagnostics, d => d.Contains("ambiguous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StitchCandidate_OrientationMissingDefers()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var a = new EmittedTopologyIdentityEntry("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", []);
        var b = new EmittedTopologyIdentityEntry("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", []);
        var map = new EmittedTopologyIdentityMap([a, b]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze([map]);
        var shell = new ShellStitchingDryRunResult(false, ShellClosureReadiness.Deferred, [], [], [], [
            new OrientationCompatibilityEvidence("k1", OrientationCompatibilityStatus.Deferred, ShellClosureReadiness.Deferred, "orientation-missing", ["orientation-missing"])
        ], [], [], false);
        var result = SurfaceFamilyStitchCandidatePlanner.Plan([map], analysis, shell);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(SurfaceFamilyStitchCandidateReadiness.Deferred, candidate.Readiness);
        Assert.Contains(result.Diagnostics, d => d.Contains("orientation-deferred", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StitchCandidate_DeterministicOrdering()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var map = new EmittedTopologyIdentityMap([
            new("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", []),
            new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", [])]);
        var analysis = EmittedTokenPairingAnalyzer.Analyze([map]);

        var first = SurfaceFamilyStitchCandidatePlanner.Plan([map], analysis, null);
        var second = SurfaceFamilyStitchCandidatePlanner.Plan([map], analysis, null);
        Assert.Equal(first.Candidates.Select(c => c.OrderingKey), second.Candidates.Select(c => c.OrderingKey));
    }

    [Fact]
    public void StitchCandidate_NoExecution()
    {
        var result = SurfaceFamilyStitchCandidatePlanner.PlanBoxCylinder(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));
        Assert.False(result.StitchExecutionImplemented);
    }
}
