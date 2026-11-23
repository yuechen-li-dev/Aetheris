using Aetheris.PlasticShell;
using Xunit;

namespace Aetheris.PlasticShell.Tests;

public sealed class MoldTests
{
    private static string Fixture(string kind, string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", kind, "Mold", name + ".firmament"));

    // The only findings are warnings with these codes.
    private static void OnlyWarnings(MoldCompileResult result, params string[] codes) =>
        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal(PlasticDiagnosticSeverity.Warning, d.Severity);
            Assert.Contains(d.Code, codes);
        });

    [Fact]
    public void DraftedCupReleasesAlongItsAxisWithNoFindings()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "drafted-cup"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        // The floor meets the inner wall in a sharp edge: the one finding, a warning.
        var sharp = Assert.Single(result.Diagnostics);
        Assert.Equal(MoldDiagnosticCodes.SharpInsideCorner, sharp.Code);
        Assert.Equal(PlasticDiagnosticSeverity.Warning, sharp.Severity);
        Assert.Single(result.Report!.InsideCorners!);
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
        // Solid, 12 mm through: thick at the prototype stage is a warning.
        OnlyWarnings(result, MoldDiagnosticCodes.WallTooThick);
        Assert.Single(result.Diagnostics);
        Assert.Equal(12d, result.Report!.Measurements!.MaximumWall, 6);
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
        // Solid, with a Boss and a Pocket: thick sections and the Pocket's and Boss's sharp inside edges.
        OnlyWarnings(result, MoldDiagnosticCodes.WallTooThick, MoldDiagnosticCodes.SharpInsideCorner);
        Assert.Equal(16, result.Report!.InsideCorners!.Count);
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
        OnlyWarnings(result, MoldDiagnosticCodes.SharpInsideCorner);
        var report = result.Report!;
        // The ceiling meets each of the eight inner walls in a sharp edge (angles from the mesh normals on the corner cones).
        Assert.Equal(8, report.InsideCorners!.Count);
        Assert.All(report.InsideCorners!, c => Assert.InRange(c.AngleDegrees, 91.49, 91.51));
        // Each inner wall is parallel to its outer wall, 2 mm away: the wall is exact there.
        var exact = report.Faces.Where(f => f.WallStrength == MoldEvidenceStrength.ExactAnalytic).ToArray();
        Assert.True(exact.Length >= 16, $"{exact.Length} exact walls");
        Assert.All(exact, f => Assert.Equal(2d, f.MinimumWall!.Value, 9));
        Assert.Equal(2d, report.Measurements!.NominalWall, 6);
        Assert.Equal(0d, report.Measurements.MinimumInsideRadius);
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
        OnlyWarnings(result, MoldDiagnosticCodes.SharpInsideCorner);
        Assert.InRange(result.Report!.Measurements!.MinimumWall, 1.99, 2.01);
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
    [InlineData("requirement-sharp-corners", MoldDiagnosticCodes.RequirementFailed)]
    [InlineData("edge-gate-off-parting-line", MoldDiagnosticCodes.GateMisplaced)]
    [InlineData("ejector-on-cavity-half", MoldDiagnosticCodes.EjectorMisplaced)]
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
        OnlyWarnings(result, MoldDiagnosticCodes.SharpInsideCorner);
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

    // Block = cavity + core + part + pins: every solid closes, and the volumes add up.
    private static void AssertInsertsFillTheBlock(MoldCompileResult result)
    {
        var inserts = result.Report!.Inserts!;
        var pins = (inserts.Slides ?? []).Sum(x => x.Volume);
        Assert.Equal(inserts.BlockVolume, inserts.CavityVolume + inserts.CoreVolume + inserts.PartVolume + pins, 1e-6 * inserts.BlockVolume);
        foreach (var step in new[] { result.CavityStep!, result.CoreStep! }.Concat((result.SlideSteps ?? new Dictionary<string, string>()).Values))
        {
            var imported = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(step);
            Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics.Select(d => d.Message)));
            var mass = Aetheris.Kernel.Core.Brep.Verification.BrepMassProperties.Evaluate(imported.Value);
            Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent && mass.SignedVolume > 0);
        }
    }

    [Fact]
    public void ASideActionsPinFillsItsHoleAndIsCappedAcrossItsMouth()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "side-action-latch"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var slide = Assert.Single(result.Report!.Inserts!.Slides!);
        Assert.Equal("pinCore", slide.Name);
        // The hole's wall and its drill point, and the cap across the mouth in the front face.
        Assert.Equal(3, slide.Faces);
        Assert.Equal(result.Report.Actions!.Single().FaceIds, slide.PartFaces);
        Assert.Equal(-1d, slide.Pull.Y, 12);
        Assert.Contains("pinCore", result.SlideSteps!.Keys);
        AssertInsertsFillTheBlock(result);
    }

    [Fact]
    public void ARodAcrossThePullIsCutAlongItsSilhouetteIntoTwoHalves()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "rod-split"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var inserts = result.Report!.Inserts!;
        // The cylinder along its silhouette lines, and both end faces across.
        Assert.Equal(3, inserts.SplitFaces!.Count);
        AssertInsertsFillTheBlock(result);
    }

    [Fact]
    public void WallsCrossingThePartingPlaneAreCutThere()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "block-mid-parting"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var inserts = result.Report!.Inserts!;
        // Four walls, four corner cylinders and the bore; the bore's section at the plane is a shut-off.
        Assert.Equal(9, inserts.SplitFaces!.Count);
        Assert.Equal(1, inserts.ShutOffs);
        AssertInsertsFillTheBlock(result);
    }

    [Fact]
    public void ADraftedWallCutBelowItsTopIsAnUndercutForTheOtherHalf()
    {
        // Drafted to open upwards, the cover's walls cannot leave the core half below a plane through their middle.
        var source = File.ReadAllText(Fixture("Canonical", "drafted-cover")).Replace("parting: Plane { at: face(-Z) }", "parting: Plane { origin: [0mm, 0mm, 5mm] }\n  inserts: 10mm", StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "drafted-cover"));
        Assert.False(result.IsSuccess);
        var refused = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.InsertsUnavailable);
        Assert.Contains("cannot leave the half on its side", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACylinderAcrossThePullCutOffItsAxisIsRefused()
    {
        var source = File.ReadAllText(Fixture("Canonical", "rod-split")).Replace("origin: [0mm, 0mm, 0mm] }", "origin: [0mm, 0mm, 3mm] }", StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "rod-split"));
        var refused = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.InsertsUnavailable);
        Assert.Contains("off its axis", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequirementsAreConceptsOverTheMeasurements()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "cover-requirements"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var requirements = result.Report!.Requirements!;
        Assert.Equal(new[] { "walls", "release" }, requirements.Select(r => r.Name));
        Assert.All(requirements, r => Assert.True(r.Passed, r.Message));
    }

    [Fact]
    public void AFailingRequirementNamesTheCheckThatFailed()
    {
        var result = MoldFirmament.CompileFile(Fixture("Invalid", "requirement-sharp-corners"));
        Assert.False(result.IsSuccess);
        var failed = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.RequirementFailed);
        Assert.Equal(PlasticDiagnosticSeverity.Error, failed.Severity);
        Assert.Contains("HouseCorners.rounded", failed.Message, StringComparison.Ordinal);
        Assert.False(Assert.Single(result.Report!.Requirements!).Passed);
    }

    [Fact]
    public void ARequirementOnATighterWallFailsAtItsLabel()
    {
        var source = File.ReadAllText(Fixture("Canonical", "cover-requirements")).Replace("maximum <= 3mm", "maximum <= 1.8mm", StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "cover-requirements"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.RequirementFailed && d.Message.Contains("HouseWall.thickEnough", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("HouseWall(wall.minimum, wall.maximum)", "HouseWal(wall.minimum, wall.maximum)")]
    [InlineData("HouseWall(wall.minimum, wall.maximum)", "HouseWall(wall.thinnest, wall.maximum)")]
    [InlineData("HouseRelease(draft.minimum, undercuts)", "HouseRelease(wall.minimum, undercuts)")]
    public void AnUnresolvableRequirementIsInvalid(string from, string to)
    {
        var source = File.ReadAllText(Fixture("Canonical", "cover-requirements")).Replace(from, to, StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "cover-requirements"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.RequirementInvalid);
    }

    [Fact]
    public void ASolidSectionIsThickAndAnErrorAtTooling()
    {
        var source = File.ReadAllText(Fixture("Canonical", "drafted-cover")).Replace("stage: Prototype", "stage: Tooling", StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "drafted-cover"));
        Assert.False(result.IsSuccess);
        var thick = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.WallTooThick);
        Assert.Equal(PlasticDiagnosticSeverity.Error, thick.Severity);
    }

    [Fact]
    public void AThinWallIsAFinding()
    {
        // The same cover at 0.8 mm is below ABS's minimum wall.
        var source = File.ReadAllText(Fixture("Canonical", "drafted-hollow-cover")).Replace("wallThickness: 2mm", "wallThickness: 0.8mm", StringComparison.Ordinal);
        var result = MoldFirmament.Compile(source, Fixture("Canonical", "drafted-hollow-cover"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.WallTooThin && d.Severity == PlasticDiagnosticSeverity.Error);
        Assert.Equal(0.8, result.Report!.Measurements!.NominalWall, 6);
    }

    private static MoldCompileResult CompileVariant(string fixture, params (string From, string To)[] edits)
    {
        var path = Fixture("Canonical", fixture);
        var source = File.ReadAllText(path);
        foreach (var (from, to) in edits)
        {
            Assert.Contains(from, source, StringComparison.Ordinal);
            source = source.Replace(from, to, StringComparison.Ordinal);
        }
        return MoldFirmament.Compile(source, path);
    }

    [Fact]
    public void AToolLayoutIsCheckedAndTheFillMeasured()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "cover-tool-layout"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        OnlyWarnings(result, MoldDiagnosticCodes.SharpInsideCorner);
        var report = result.Report!;
        var gate = Assert.Single(report.Gates!);
        Assert.Null(gate.Problem);
        Assert.InRange(gate.DistanceToPartingLine!.Value, 0, 1e-6);
        Assert.All(report.Ejectors!, e => { Assert.Null(e.Problem); Assert.Equal(new[] { e.FaceId!.Value }, e.SeatFaces); });
        var flow = report.Flow!;
        Assert.Equal(0d, flow.UnfilledFraction);
        // From the end of the cover to the far end, over a 2 mm wall.
        Assert.InRange(flow.MaximumFlowRatio, 40, 80);
        Assert.InRange(flow.MaximumFlowLength, 90, 140);
        // The front over the top meets the fronts round the sides, down each side wall; neither reaches the top.
        Assert.Equal(2, flow.KnitLines.Count);
        Assert.All(flow.KnitLines, k => Assert.False(k.Cosmetic));
        Assert.True(Assert.Single(report.Requirements!).Passed);
        Assert.Equal(double.Pi * 8, report.Measurements!.EjectorArea, 9);
    }

    [Fact]
    public void AKnitLineClosesBehindAHole()
    {
        var result = MoldFirmament.CompileFile(Fixture("Canonical", "gated-plate"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        var line = Assert.Single(result.Report!.Flow!.KnitLines);
        // From the hole's trailing edge (x = -6) to the far end of the plate (x = -60), along its centre line.
        Assert.InRange(double.Max(line.From.X, line.To.X), -8, -4);
        Assert.InRange(double.Min(line.From.X, line.To.X), -61, -57);
        Assert.InRange(line.Centroid.Y, -1.5, 1.5);
        Assert.True(line.Cosmetic);
        var warning = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.KnitLineOnCosmetic);
        Assert.Equal(PlasticDiagnosticSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void FrontsFromTwoGatesMeetInAKnitLine()
    {
        var result = CompileVariant("gated-plate",
            ("  Gate main { kind: Edge; at: [60mm, 0mm, 0mm] }", "  Gate east { kind: Edge; at: [60mm, 0mm, 0mm] }\n  Gate west { kind: Edge; at: [-60mm, 0mm, 0mm] }"));
        // Halfway, as the gates are symmetric about the plate's middle; the hole there cuts the meeting line in two.
        var meetings = result.Report!.Flow!.KnitLines.Where(k => k.Gates.Count == 2).ToArray();
        Assert.Equal(2, meetings.Length);
        Assert.All(meetings, m => { Assert.Equal(new[] { "east", "west" }, m.Gates); Assert.InRange(m.Centroid.X, -1, 1); });
        Assert.Equal(-meetings[0].Centroid.Y, meetings[1].Centroid.Y, 6);
        // Two gates halve the flow length.
        Assert.InRange(result.Report.Flow.MaximumFlowLength, 50, 75);
    }

    [Fact]
    public void AFlowPastTheMaterialsRatioIsAFinding()
    {
        // 1.2 mm of PC over 120 mm: past PC's flow ratio of 100.
        var result = CompileVariant("gated-plate", ("to: 2.5mm", "to: 1.2mm"), ("material: ABS", "material: PC"));
        var finding = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.FlowTooLong);
        Assert.Equal(PlasticDiagnosticSeverity.Warning, finding.Severity);
        Assert.True(result.Report!.Flow!.MaximumFlowRatio > 100);
    }

    [Theory]
    [InlineData("Gate main { kind: Edge; at: [60mm, 0mm, 0mm] }", "Gate main { kind: Edge; at: [60mm, 0mm, 9mm] }", "off the part")]
    [InlineData("Gate main { kind: Edge; at: [60mm, 0mm, 0mm] }", "Gate main { kind: Submarine; at: [20mm, 0mm, 2.5mm] }", "tunnels through the −pull half")]
    [InlineData("Gate main { kind: Edge; at: [60mm, 0mm, 0mm] }", "Gate main { kind: Pin; at: [20mm, 0mm, 0mm] }", "feeds the +pull half")]
    [InlineData("Ejector e1 { at: [-40mm, 0mm, 0mm]; diameter: 4mm }", "Ejector e1 { at: [-59mm, 0mm, 0mm]; diameter: 4mm }", "has its footprint")]
    [InlineData("Ejector e1 { at: [-40mm, 0mm, 0mm]; diameter: 4mm }", "Ejector e1 { at: [0mm, 7mm, 0mm]; diameter: 4mm }", "footprint running off the part")]
    public void AMisplacedGateOrPinIsAnError(string from, string to, string because)
    {
        var result = CompileVariant("gated-plate", (from, to));
        Assert.False(result.IsSuccess);
        var finding = Assert.Single(result.Diagnostics, d => d.Code is MoldDiagnosticCodes.GateMisplaced or MoldDiagnosticCodes.EjectorMisplaced);
        Assert.Equal(PlasticDiagnosticSeverity.Error, finding.Severity);
        Assert.Contains(because, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APinTooCloseToAWallBreaksItsClearance()
    {
        // A 4 mm pin 0.3 mm from the hole's edge: the pin fits, its 0.5 mm clearance does not.
        var result = CompileVariant("gated-plate", ("Ejector e1 { at: [-40mm, 0mm, 0mm]; diameter: 4mm }", "Ejector e1 { at: [-8.3mm, 0mm, 0mm]; diameter: 4mm }"));
        var finding = Assert.Single(result.Diagnostics, d => d.Code == MoldDiagnosticCodes.EjectorMisplaced);
        Assert.Contains("clearance", finding.Message, StringComparison.Ordinal);
        Assert.Equal(PlasticDiagnosticSeverity.Warning, finding.Severity);
    }
}
