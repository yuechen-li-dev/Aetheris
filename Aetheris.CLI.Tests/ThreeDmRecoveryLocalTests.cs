using System.Text.Json;
using Aetheris.ThreeDm;

namespace Aetheris.CLI.Tests;

public sealed class ThreeDmRecoveryLocalTests
{
    [LocalThreeDmFact]
    public void ProductRationalSupportsRecoverAtConfigurableEngineeringTolerance()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "testdata", "3DM", "cartesian-product-metres.3dm");

        var ordinary = ThreeDmRecovery.Analyze(path, 0.1);
        var tighter = ThreeDmRecovery.Analyze(path, 0.05);
        Assert.Equal((53, 83), (ordinary.RecoveredCurveCount, ordinary.RecoveredSurfaceCount));
        Assert.Equal((53, 83), (tighter.RecoveredCurveCount, tighter.RecoveredSurfaceCount));
        Assert.Equal(0.1, ordinary.RecoveryToleranceMillimetres);
        Assert.Equal(0.05, tighter.RecoveryToleranceMillimetres);

        var ordinaryCandidates = ordinary.Bodies.SelectMany(body =>
            body.Edges.Select(edge => edge.GenericRecovery).Concat(body.Faces.Select(face => face.GenericRecovery)))
            .Where(candidate => candidate is not null).ToArray();
        var tighterCandidates = tighter.Bodies.SelectMany(body =>
            body.Edges.Select(edge => edge.GenericRecovery).Concat(body.Faces.Select(face => face.GenericRecovery)))
            .Where(candidate => candidate is not null).ToArray();
        Assert.All(ordinaryCandidates, candidate =>
        {
            Assert.Equal(RecoveryQualification.WithinRecoveryTolerance, candidate!.Qualification);
            Assert.InRange(candidate.Residual.MaxMillimetres, 0, 0.1);
            Assert.True(candidate.Residual.SampleCount >= 65);
        });
        Assert.All(tighterCandidates, candidate =>
        {
            Assert.Equal(RecoveryQualification.WithinRecoveryTolerance, candidate!.Qualification);
            Assert.InRange(candidate.Residual.MaxMillimetres, 0, 0.05);
        });
        Assert.True(tighterCandidates.Sum(candidate => candidate!.Parameters["controlPointsU"] *
            double.Max(1, candidate.Parameters["controlPointsV"])) >=
            ordinaryCandidates.Sum(candidate => candidate!.Parameters["controlPointsU"] *
                double.Max(1, candidate.Parameters["controlPointsV"])));
        Assert.StartsWith("No canonical BRep", ordinary.ProductionStatus);
    }

    [LocalThreeDmFact]
    public void TightUnsupportedSupportsDoNotEraseOtherSourceBodies()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var report = ThreeDmRecovery.Analyze(Path.Combine(directory.FullName,
            "testdata", "3DM", "cartesian-product-metres.3dm"), 0.00001);

        Assert.Equal(27, report.Bodies.Count);
        Assert.True(report.RecoveredSurfaceCount > 0);
        Assert.True(report.UnrecoveredSurfaceCount > 0);
        Assert.Contains(report.Bodies, body => body.Faces.Any(face =>
            face.GenericRecovery?.Qualification == RecoveryQualification.Unresolved &&
            face.GenericRecovery.Limitation.Length > 0));
    }

    [LocalThreeDmFact]
    public void ProductRecoveryStudyIsDeterministicAndLeavesTopologyProvisional()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Aetheris repository root was not found.");
        var path = Path.Combine(directory.FullName, "testdata", "3DM", "cartesian-product-metres.3dm");
        Assert.True(File.Exists(path), "Local Cartesian 3DM fixture disappeared after test discovery.");

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
