using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class EmittedTokenPairingAnalyzerTests
{
    [Fact]
    public void TokenPairingAnalyzer_ExactlyTwoCompatibleEntries_AreSafePair()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var map = new EmittedTopologyIdentityMap([
            new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", []),
            new("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", [])]);

        var result = EmittedTokenPairingAnalyzer.Analyze([map]);
        Assert.Single(result.SafePairs);
        Assert.Equal(TokenPairingStatus.SafePair, result.SafePairs[0].Status);
        Assert.Contains(result.Diagnostics, d => d.Contains("token-safe-pair", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TokenPairingAnalyzer_SingleEntry_IsMissingMate()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var result = EmittedTokenPairingAnalyzer.Analyze([
            new EmittedTopologyIdentityMap([new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", [])])]);
        Assert.Single(result.MissingMateGroups);
        Assert.Empty(result.SafePairs);
    }

    [Fact]
    public void TokenPairingAnalyzer_MoreThanTwoEntries_IsAmbiguous()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var result = EmittedTokenPairingAnalyzer.Analyze([
            new EmittedTopologyIdentityMap([
                new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", []),
                new("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", []),
                new("edge:3", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalBottomBoundary, "o", [])])]);
        Assert.Single(result.AmbiguousGroups);
        Assert.Empty(result.SafePairs);
    }

    [Fact]
    public void TokenPairingAnalyzer_IncompatibleRoles_NotSafe()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var result = EmittedTokenPairingAnalyzer.Analyze([
            new EmittedTopologyIdentityMap([
                new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", []),
                new("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalBottomBoundary, "o", [])])]);
        Assert.Empty(result.SafePairs);
        Assert.Contains(result.Diagnostics, d => d.Contains("incompatible-roles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TokenPairingAnalyzer_RealBoxCylinderPath_ReportsSafeOrPreciseMissing()
    {
        var root = new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8));
        var planar = new PlanarSurfaceMaterializer().EmitSupportedPlanarPatches(root);
        var gen = FacePatchCandidateGenerator.Generate(root);
        var cylCandidate = gen.Candidates.First(c => c.SourceSurface.Family == SurfacePatchFamily.Cylindrical && c.RetentionRole == FacePatchRetentionRole.ToolBoundaryRetainedInsideBase);
        var ready = new MaterializationReadinessReport(true, EmissionReadiness.EvidenceReadyForEmission, [], [], 0,0,0,0,0,0,0,[],false);
        var cyl = new CylindricalSurfaceMaterializer().EmitRetainedWall(new CylindricalSurfaceMaterializer.RetainedWallEmissionRequest(cylCandidate, ready));

        var result = EmittedTokenPairingAnalyzer.Analyze(planar.Entries.Where(e => e.Emitted).Select(e => e.IdentityMap ?? EmittedTopologyIdentityMap.Empty).Concat(cyl.IdentityMap is null ? [] : [cyl.IdentityMap]));

        Assert.True(result.SafePairs.Count > 0 || result.MissingMateGroups.Count > 0 || result.NullTokenEntries.Count > 0);
        if (result.SafePairs.Count == 0)
            Assert.Contains(result.Diagnostics, d => d.Contains("missing-mate", StringComparison.OrdinalIgnoreCase) || d.Contains("unmapped-entries", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeterministicOrdering()
    {
        var token = new InternalTrimIdentityToken("op", "a", "b", TrimCurveFamily.Circle, "inner", "k1");
        var maps = new[] { new EmittedTopologyIdentityMap([
            new("edge:2", EmittedTopologyKind.Edge, token, EmittedTopologyRole.CylindricalTopBoundary, "o", []),
            new("edge:1", EmittedTopologyKind.Edge, token, EmittedTopologyRole.InnerCircularTrim, "o", [])])};

        var first = EmittedTokenPairingAnalyzer.Analyze(maps);
        var second = EmittedTokenPairingAnalyzer.Analyze(maps);
        Assert.Equal(first.SafePairs.Select(x => x.TokenOrderingKey), second.SafePairs.Select(x => x.TokenOrderingKey));
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }
}
