using System.Text.Json;
using Aetheris.ThreeDm;
using Xunit.Sdk;

namespace Aetheris.CLI.Tests;

public sealed class ThreeDmRecoveryLocalTests
{
    [Fact]
    public void ProductRecoveryStudyIsDeterministicAndLeavesTopologyProvisional()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Aetheris repository root was not found.");
        var path = Path.Combine(directory.FullName, "testdata", "3DM", "cartesian-product-metres.3dm");
        if (!File.Exists(path)) throw SkipException.ForSkip("Local Cartesian 3DM recovery fixture is absent.");

        var first = ThreeDmRecovery.Analyze(path);
        var second = ThreeDmRecovery.Analyze(path);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(27, first.Bodies.Count);
        Assert.Equal(0.001, first.SourceToleranceMillimetres);
        Assert.Equal((196, 53, 53, 0),
            (first.SourceRationalEdgeCount, first.StudiedRationalEdgeCount,
                first.QualifiedRecoveredRationalEdgeCount, first.UnresolvedRationalEdgeCount));
        Assert.Equal((104, 21, 83),
            (first.SourceRationalSurfaceCount, first.QualifiedAnalyticRationalSurfaceCount,
                first.UnresolvedRationalSurfaceCount));
        var studied = first.Bodies.SelectMany(body => body.Edges)
            .Where(edge => edge.SourceIsRational && edge.NativeClassification == "Unclassified").ToArray();
        Assert.Equal(16, studied.Count(edge => edge.PreferredCandidate == "CircleSupport"));
        Assert.Equal(37, studied.Count(edge => edge.PreferredCandidate == "NonRationalSameNet"));
        Assert.Contains(studied, edge => edge.PreferredCandidate == "NonRationalSameNet" &&
            edge.Candidates.Any(candidate => candidate.Kind == "CircleSupport" &&
                candidate.Qualification == RecoveryQualification.Approximate));
        Assert.All(studied, edge =>
        {
            Assert.Contains(edge.Candidates, candidate => candidate.Kind == "NonRationalSameNet"
                && candidate.Qualification == RecoveryQualification.WithinSourceTolerance);
            Assert.Contains("topology pending", edge.Status);
            Assert.True(edge.RelativeWeightSpread < 1e-12);
        });
        Assert.StartsWith("No canonical BRep or production STEP is emitted", first.ProductionStatus);
    }
}
