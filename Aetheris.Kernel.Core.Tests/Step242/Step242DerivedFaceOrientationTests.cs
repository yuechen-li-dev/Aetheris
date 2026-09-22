using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242DerivedFaceOrientationTests
{
    [Fact]
    public void ClosedBox_OneHostileSameSenseFlag_DerivesIdenticalCanonicalBodyAndReportsMismatch()
    {
        var trusted = Step242Importer.ImportBody(Step242FixtureCorpus.CanonicalBoxGolden);
        var hostileText = FlipAdvancedFaceSameSense(Step242FixtureCorpus.CanonicalBoxGolden, all: false);
        var hostile = Step242Importer.ImportBody(hostileText);

        Assert.True(trusted.IsSuccess);
        Assert.True(hostile.IsSuccess);
        Assert.Equal(CanonicalOrientations(trusted.Value), CanonicalOrientations(hostile.Value));
        Assert.Equal(
            BrepMassProperties.Evaluate(trusted.Value).SignedVolume,
            BrepMassProperties.Evaluate(hostile.Value).SignedVolume,
            precision: 10);
        Assert.Equal(1, hostile.Value.FaceOrientationReport!.SourceMismatchCount);
        Assert.Contains(hostile.Diagnostics, diagnostic => diagnostic.Source == "Importer.StepOrientation.SourceOrientationMismatch");
    }

    [Fact]
    public void ClosedBox_AllHostileSameSenseFlags_DerivesIdenticalCanonicalBody()
    {
        var trusted = Step242Importer.ImportBody(Step242FixtureCorpus.CanonicalBoxGolden);
        var hostile = Step242Importer.ImportBody(FlipAdvancedFaceSameSense(Step242FixtureCorpus.CanonicalBoxGolden, all: true));

        Assert.True(trusted.IsSuccess);
        Assert.True(hostile.IsSuccess);
        Assert.Equal(CanonicalOrientations(trusted.Value), CanonicalOrientations(hostile.Value));
        Assert.Equal(hostile.Value.Topology.Faces.Count(), hostile.Value.FaceOrientationReport!.SourceMismatchCount);
        Assert.All(hostile.Value.FaceOrientationReport.Shells, shell =>
            Assert.Equal(FaceOrientationQualification.DerivedQualified, shell.Qualification));
    }

    [Fact]
    public void OpenShell_ResolvesLocalOrientationButDoesNotClaimGlobalOutwardness()
    {
        var import = Step242Importer.ImportBody(Step242FixtureCorpus.PlanarFaceWithRectangularHole);

        Assert.True(import.IsSuccess);
        var shell = Assert.Single(import.Value.FaceOrientationReport!.Shells);
        Assert.False(shell.IsClosedManifold);
        Assert.Equal(FaceOrientationQualification.DerivedLocallyConsistentGlobalUnknown, shell.Qualification);
        Assert.Contains(import.Diagnostics, diagnostic => diagnostic.Source == "Importer.StepOrientation.GlobalOrientationUnknown");
    }

    [Fact]
    public void NonManifoldClosedShell_IsRejectedRatherThanGuessed()
    {
        var duplicatedFace = Regex.Replace(
            Step242FixtureCorpus.CanonicalBoxGolden,
            @"(CLOSED_SHELL\([^;]*\()(#[0-9]+)((?:,#[0-9]+)*)(\)\s*\);)",
            match => $"{match.Groups[1].Value}{match.Groups[2].Value}{match.Groups[3].Value},{match.Groups[2].Value}{match.Groups[4].Value}",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        var import = Step242Importer.ImportBody(duplicatedFace);

        Assert.False(import.IsSuccess);
        Assert.Contains(import.Diagnostics, diagnostic => diagnostic.Source == "Importer.StepOrientation.NonManifoldOrientation");
    }

    [Fact]
    public void BrepWithVoid_ProjectsOuterPositiveAndInnerNegativeShellOrientation()
    {
        var outer = BrepPrimitives.CreateBox(20d, 18d, 16d);
        var inner = BrepPrimitives.CreateSphere(3d);
        Assert.True(outer.IsSuccess);
        Assert.True(inner.IsSuccess);
        var cavity = BrepBoolean.Subtract(outer.Value, inner.Value);
        Assert.True(cavity.IsSuccess);
        var export = Step242Exporter.ExportBody(cavity.Value);
        Assert.True(export.IsSuccess);

        var import = Step242Importer.ImportBody(export.Value);

        Assert.True(import.IsSuccess);
        var report = Assert.IsType<FaceOrientationReport>(import.Value.FaceOrientationReport);
        Assert.Equal(2, report.Shells.Count);
        Assert.All(report.Shells, shell => Assert.Equal(FaceOrientationQualification.DerivedQualified, shell.Qualification));
        Assert.True(report.Shells.Single(shell => !shell.IsInnerVoid).SignedVolumeAfterGlobalProjection > 0d);
        var innerShell = report.Shells.Single(shell => shell.IsInnerVoid);
        Assert.NotEqual(0d, innerShell.SignedVolumeBeforeGlobalProjection);
        Assert.True(innerShell.SignedVolumeAfterGlobalProjection < 0d);
        var innerFaces = import.Value.Topology.GetShell(innerShell.ShellId).FaceIds.ToHashSet();
        Assert.All(report.Faces.Where(face => innerFaces.Contains(face.FaceId)), face =>
            Assert.False(face.Orientation.IsAlignedWithSurface));
    }

    private static IReadOnlyList<bool> CanonicalOrientations(BrepBody body)
        => body.Topology.Faces.OrderBy(face => face.Id.Value)
            .Select(face => body.Bindings.GetFaceBinding(face.Id).Orientation.IsAlignedWithSurface)
            .ToArray();

    private static string FlipAdvancedFaceSameSense(string step, bool all)
    {
        var count = 0;
        return Regex.Replace(
            step,
            @"(?m)(ADVANCED_FACE\([^;]*,)(\.T\.|\.F\.)(\)\s*;)",
            match =>
            {
                if (!all && count++ > 0) return match.Value;
                var flipped = match.Groups[2].Value == ".T." ? ".F." : ".T.";
                return $"{match.Groups[1].Value}{flipped}{match.Groups[3].Value}";
            },
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }
}
