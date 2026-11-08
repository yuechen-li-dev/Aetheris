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
}
