using Aetheris.PlasticShell;
using Xunit;

namespace Aetheris.PlasticShell.Tests;

public sealed class MoldTests
{
    private static string Fixture(string kind, string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", kind, "Mold", name + ".firmament"));

    [Fact]
    public void DraftedCupReleasesAlongItsAxisWithNoFindings()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "drafted-cup"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics);
        var report = result.Report!;
        // The walls are cones about the pull: their draft is exact, the cone's semi-angle.
        var cones = report.Faces.Where(f => f.Surface == "Cone").ToArray();
        Assert.Equal(2, cones.Length);
        Assert.All(cones, f => Assert.Equal(double.Atan(1.397 / 40) * 180 / double.Pi, f.MinimumDraftDegrees, 9));
        Assert.All(cones, f => Assert.Equal(MoldEvidenceStrength.ExactAnalytic, f.Strength));
        Assert.Contains(cones, f => f.Side == MoldFaceSide.Positive);
        Assert.Contains(cones, f => f.Side == MoldFaceSide.Negative);
        Assert.True(report.PartingLine.OnDeclaredPlane);
        Assert.Equal(40d, report.PartingLine.MinimumHeight, 6);
        // Projected along the pull the cup is its rim disk.
        Assert.InRange(report.Summary.ProjectedArea, double.Pi * 31.397 * 31.397 * (1 - 1e-3), double.Pi * 31.397 * 31.397 * (1 + 1e-3));
    }

    [Fact]
    public void DraftedExtrudeReleasesWithExactWallsAndNoFindings()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "drafted-cover"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics);
        var report = result.Report!;
        // Nine walls (four planes, four corner cones, the bore's cone) and the top cap form the cavity half.
        var walls = report.Faces.Where(f => f.MinimumDraftDegrees < 90).ToArray();
        Assert.Equal(9, walls.Length);
        Assert.All(walls, f => Assert.Equal(1.5, f.MinimumDraftDegrees, 9));
        Assert.All(walls, f => Assert.Equal(MoldFaceSide.Positive, f.Side));
        Assert.All(walls, f => Assert.Equal(MoldEvidenceStrength.ExactAnalytic, f.Strength));
        Assert.Equal(0, report.Summary.DraftFailures);
        // Along the pull the cover is its bottom outline less the bore at its narrowest, at the parting face.
        var outline = 80d * 50d - (4d - double.Pi) * 36d - double.Pi * 16d;
        Assert.InRange(report.Summary.ProjectedArea, outline * (1 - 1e-3), outline * (1 + 1e-3));
    }

    [Fact]
    public void DraftedComposeBossAndPocketReleaseWithNoFindings()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "drafted-compose-block"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics);
        var report = result.Report!;
        // The Base's walls (split where the Pocket's floor starts), the Boss's and the Pocket's: all +pull at 2°.
        var walls = report.Faces.Where(f => f.MinimumDraftDegrees < 90).ToArray();
        Assert.Equal(32, walls.Length);
        Assert.All(walls, f => Assert.Equal(2d, f.MinimumDraftDegrees, 9));
        Assert.All(walls, f => Assert.Equal(MoldFaceSide.Positive, f.Side));
        Assert.All(walls, f => Assert.Equal(MoldEvidenceStrength.ExactAnalytic, f.Strength));
        Assert.Equal(0, report.Summary.DraftFailures);
        Assert.Equal(0, report.Summary.UndercutFaces);
    }

    [Fact]
    public void DraftedHollowCoverReleasesInsideAndOut()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "drafted-hollow-cover"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics);
        var report = result.Report!;
        var walls = report.Faces.Where(f => f.MinimumDraftDegrees < 90).ToArray();
        // Outer walls face the cavity half; the inner walls face down into the core.
        Assert.Equal(24, walls.Length);
        Assert.All(walls, f => Assert.Equal(1.5, f.MinimumDraftDegrees, 9));
        Assert.Equal(16, walls.Count(f => f.Side == MoldFaceSide.Positive));
        Assert.Equal(8, walls.Count(f => f.Side == MoldFaceSide.Negative));
        Assert.Equal(0, report.Summary.DraftFailures);
        Assert.Equal(0, report.Summary.UndercutFaces);
    }

    [Fact]
    public void DraftedHousingWithBossAndPocketReleases()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "drafted-housing"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics);
        var report = result.Report!;
        Assert.All(report.Faces.Where(f => f.MinimumDraftDegrees < 90), f => Assert.Equal(1.5, f.MinimumDraftDegrees, 9));
        Assert.Equal(0, report.Summary.DraftFailures);
        Assert.Equal(0, report.Summary.UndercutFaces);
    }

    [Fact]
    public void VerticalWallsAreShortOfDraftButPullClean()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "tray-concept"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var report = result.Report!;
        Assert.Equal(0, report.Summary.UndercutFaces);
        Assert.Equal(24, report.Summary.DraftFailures);
        Assert.All(result.Diagnostics, d => Assert.Equal(PlasticDiagnosticSeverity.Warning, d.Severity));
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.DraftInsufficient);
        // The parting line is the outer edge of the rim, on the declared plane.
        Assert.Equal(8, report.PartingLine.EdgeIds.Count);
        Assert.True(report.PartingLine.OnDeclaredPlane);
        var outline = 120d * 80d - (4d - double.Pi) * 144d;
        Assert.InRange(report.Summary.ProjectedArea, outline * (1 - 1e-3), outline * (1 + 1e-3));
    }

    [Fact]
    public void ToolingStageTurnsFindingsIntoErrors()
    {
        var source = File.ReadAllText(Fixture("Canonical", "tray-concept")).Replace("stage: Concept", "stage: Tooling", StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "tray-concept"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.DraftInsufficient && d.Severity == PlasticDiagnosticSeverity.Error);
    }

    [Fact]
    public void PolicyAndTextureRaiseTheRequiredDraftOnTheTexturedHalf()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "cup-textured-policy"));
        Assert.True(result.IsSuccess);
        var report = result.Report!;
        Assert.Equal(1.5, report.Intent.Policy.MinimumDraftDegrees, 12);
        var failing = Assert.Single(report.Faces, f => !f.DraftSatisfied);
        Assert.Equal(MoldFaceSide.Negative, failing.Side);
        Assert.Equal(2.5, failing.RequiredDraftDegrees, 9);
        Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.DraftInsufficient);
    }

    [Theory]
    [InlineData("bottle-shoulder-undercut", MoldDiagnosticCodes.Undercut)]
    [InlineData("pulley-groove-undercut", MoldDiagnosticCodes.Undercut)]
    [InlineData("unknown-material", MoldDiagnosticCodes.MaterialUnknown)]
    [InlineData("side-hole-undercut", MoldDiagnosticCodes.Undercut)]
    [InlineData("action-releases-nothing", MoldDiagnosticCodes.ActionReleasesNothing)]
    public void RefusalsNameTheirCause(string fixture, string code)
    {
        var result = MoldFirmament.CompileFile(Fixture("Invalid", fixture));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == PlasticDiagnosticSeverity.Error);
    }

    [Fact]
    public void PulleyGrooveIsHiddenFromBothHalves()
    {
        var report = MoldFirmament.CompileFile(Fixture("Invalid", "pulley-groove-undercut")).Report!;
        // The groove: its upper and lower faces and its floor. The flanges, bore and ends release.
        var undercuts = report.Faces.Where(f => f.UndercutFraction > 0).ToArray();
        Assert.Equal(3, undercuts.Length);
        Assert.All(undercuts, f => Assert.Equal(1d, f.UndercutFraction));
        Assert.Equal(["Cylinder", "Plane", "Plane"], undercuts.Select(f => f.Surface).Order());
        Assert.All(undercuts, f => Assert.InRange(double.Hypot(f.Representative.X, f.Representative.Y), 13.9, 20.1));
    }

    [Fact]
    public void AGrooveAroundTheAxisReleasesAlongNoSideDirection()
    {
        var result = MoldFirmament.CompileFile(Fixture("Invalid", "pulley-groove-undercut"));
        var group = Assert.Single(result.Report!.Undercuts!);
        Assert.Equal(3, group.FaceIds.Count);
        Assert.Empty(group.ReleasingDirections);
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.Undercut && d.Message.Contains("lifter", StringComparison.Ordinal));
    }

    [Fact]
    public void ASideHoleIsAnUndercutThatNamesTheDirectionThatReleasesIt()
    {
        var result = MoldFirmament.CompileFile(Fixture("Invalid", "side-hole-undercut"));
        var group = Assert.Single(result.Report!.Undercuts!);
        // The hole's wall and its drill point.
        Assert.Equal(["Cone", "Cylinder"], result.Report.Faces.Where(f => group.FaceIds.Contains(f.FaceId)).Select(f => f.Surface).Order());
        Assert.Equal(["-Y"], group.ReleasingDirections);
        Assert.Null(group.Action);
        var undercut = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.Undercut);
        Assert.Contains("declare an Action", undercut.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredActionFormsTheUndercutItReleases()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "side-action-latch"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var report = result.Report!;
        Assert.Equal("pinCore", Assert.Single(report.Undercuts!).Action);
        Assert.Equal(0, report.Summary.UndercutFaces);
        Assert.Equal(0, report.Summary.StraddlingFaces);
        Assert.Equal(2, report.Summary.ActionFaces);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code is MoldDiagnosticCodes.Undercut or MoldDiagnosticCodes.StraddlingFace);
        // The pin's wall is a cylinder along its pull: exactly 0° of draft against it, which the review reports.
        var action = Assert.Single(report.Actions!);
        Assert.Equal(0d, action.MinimumDraftDegrees, 9);
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.DraftInsufficient && d.Message.StartsWith("Action pinCore", StringComparison.Ordinal));
        // The action's faces are no part of either half's projected area or parting line.
        Assert.Equal(40d * 30d, report.Summary.ProjectedArea, 6);
        Assert.Single(report.PartingLine.Loops!);
    }

    [Fact]
    public void AFaceSplitByThePartingPlaneThroughItsSilhouetteIsNotAFinding()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "rod-split"));
        var cylinder = Assert.Single(result.Report!.Faces, f => f.Side == MoldFaceSide.Straddling);
        Assert.Equal(0d, cylinder.SilhouetteHeight!.Value, 12);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code is MoldDiagnosticCodes.StraddlingFace or MoldDiagnosticCodes.PartingLineOffPlane);
        Assert.True(result.Report.PartingLine.OnDeclaredPlane);
        // Projected along the pull the rod is its 40 × 16 shadow.
        Assert.InRange(result.Report.Summary.ProjectedArea, 640 * (1 - 1e-3), 640 * (1 + 1e-3));

        var moved = File.ReadAllText(Fixture("Canonical", "rod-split")).Replace("origin: [0mm, 0mm, 0mm] }", "origin: [0mm, 0mm, 3mm] }", StringComparison.Ordinal);
        var off = MoldFirmament.Compile(moved, Fixture("Canonical", "rod-split"));
        var straddle = Assert.Single(off.Diagnostics, d => d.Code == MoldDiagnosticCodes.StraddlingFace);
        Assert.Contains("silhouette at 0 mm, off the parting plane at 3 mm", straddle.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreAndCavityInsertsFillTheBlockAroundThePart()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "housing-inserts"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics);
        var report = result.Report!;
        // The vent's exit inside the shell is a shut-off; the rim is the parting line.
        Assert.Equal(1, report.PartingLine.Loops!.Count(l => l.Kind == MoldPartingLoopKind.ShutOff));
        Assert.Equal(1, report.PartingLine.Loops!.Count(l => l.Kind == MoldPartingLoopKind.Parting));
        var inserts = report.Inserts!;
        Assert.Equal(1, inserts.ShutOffs);
        // Block = cavity + core + part. The inserts' volumes are measured on meshes, as is the part's.
        Assert.Equal(inserts.BlockVolume, inserts.CavityVolume + inserts.CoreVolume + inserts.PartVolume, 1e-4 * inserts.BlockVolume);
        foreach (var step in new[] { result.CavityStep!, result.CoreStep! })
        {
            var imported = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(step);
            Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => d.Message)));
            var mass = Aetheris.Kernel.Core.Brep.Verification.BrepMassProperties.Evaluate(imported.Value);
            Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent && mass.SignedVolume > 0);
        }
        // The block reaches the margin beyond the part on every side.
        Assert.Equal(-60d, inserts.BlockMinimum.X, 6);
        Assert.Equal(-10d, inserts.BlockMinimum.Z, 6);
        Assert.Equal(41d, inserts.BlockMaximum.Z, 6);
    }

    [Fact]
    public void InsertsAreRefusedWhenTheToolNeedsMoreThanTwoPlates()
    {
        var source = File.ReadAllText(Fixture("Canonical", "side-action-latch")).Replace("  Action pinCore", "  inserts: 10mm\n  Action pinCore", StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "side-action-latch"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.InsertsUnavailable && d.Message.Contains("side actions", StringComparison.Ordinal));
    }
}
