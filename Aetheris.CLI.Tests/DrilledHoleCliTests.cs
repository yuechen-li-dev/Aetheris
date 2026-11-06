using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

/// <summary>Blind holes are drilled holes: a 118° point unless stated otherwise, the depth measured to full diameter,
/// and side holes drilled from planes derived from source (Construction Planes or Path steps), never face references.</summary>
public sealed class DrilledHoleCliTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static double Tip(double radius, double angle) => radius / Math.Tan(angle * Math.PI / 360d);

    [Theory]
    [InlineData("blind-drillpoint-default.firmament", 118d)]
    [InlineData("blind-drillpoint-angle.firmament", 135d)]
    public void TopFaceBlindHoleEndsInADrillPointBelowTheFullDiameterDepth(string fixture, double angle)
    {
        var body = BuildAndImport("fixtures/Regression/Hole/valid/" + fixture);
        var cone = Assert.Single(Cones(body));
        // Box z 0..20, 10 mm full-diameter depth, Ø8: the apex sits one tip length below z = 10 on the hole axis.
        Assert.Equal(-15d, cone.Apex.X, 9);
        Assert.Equal(5d, cone.Apex.Y, 9);
        Assert.Equal(10d - Tip(4d, angle), cone.Apex.Z, 9);
        Assert.Equal(angle / 2d, cone.SemiAngleRadians * 180d / Math.PI, 9);
    }

    [Fact]
    public void FlatTerminationKeepsAFlatFloor()
    {
        var body = BuildAndImport("fixtures/Regression/Hole/valid/blind-flat-floor.firmament");
        Assert.Empty(Cones(body));
        Assert.Contains(body.Topology.Faces, f => body.TryGetFaceSurface(f.Id, out var s) && s?.Plane is { } p
                                                   && Math.Abs(Math.Abs(p.Normal.Z) - 1d) < 1e-12 && Math.Abs(p.Origin.Z - 10d) < 1e-9);
    }

    [Fact]
    public void PathStepPlanesDrillIntoTheirOwnCoplanarFaces()
    {
        var body = BuildAndImport("fixtures/Regression/Hole/valid/path-step-plane-drills.firmament");
        var tip = Tip(2d, 118d);
        var apexes = Cones(body).Select(c => (Math.Round(c.Apex.X, 9), Math.Round(c.Apex.Y, 9), Math.Round(c.Apex.Z, 9))).OrderBy(a => a).ToArray();
        (double, double, double)[] expected =
        [
            (-20d, Math.Round(-15d + 8d + tip, 9), -5d),
            (0d, Math.Round(15d - 8d - tip, 9), -5d),
            (20d, Math.Round(-15d + 8d + tip, 9), -5d),
        ];
        Assert.Equal(expected.OrderBy(a => a).ToArray(), apexes);
    }

    [Fact]
    public void ANamedProfileEdgeSweepsTheDrillPlane()
    {
        var body = BuildAndImport("fixtures/Regression/Hole/valid/profile-edge-plane-drill.firmament");
        var cone = Assert.Single(Cones(body));
        Assert.Equal(10d, cone.Apex.X, 9);
        Assert.Equal(-15d + 10d + Tip(2d, 118d), cone.Apex.Y, 9);
        Assert.Equal(-4d, cone.Apex.Z, 9);
    }

    [Theory]
    [InlineData("fixtures/Regression/Hole/valid/blind-drillpoint-default.firmament")]
    [InlineData("fixtures/Regression/Hole/valid/path-step-plane-drills.firmament")]
    [InlineData("fixtures/Regression/JunctionFillet/drilled-boss-foot.firmament")]
    public void DrillPointsMeshWatertight(string fixture)
    {
        // A drill point is a cone closed at its apex: its loop walks the seam out and back, and its rim may be split
        // where the hole wall is. The last fixture also passes through a junction fillet, which rebuilds the body.
        var stl = Path.Combine(Path.GetTempPath(), "aetheris-drilled-holes", Guid.NewGuid().ToString("N") + ".stl");
        Directory.CreateDirectory(Path.GetDirectoryName(stl)!);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["mesh", Path.Combine(RepoRoot, fixture), "--format", "stl", "--output", stl], stdout, stderr);
        Assert.True(exit == 0, $"{fixture}: {stderr}{stdout}");
        Assert.Contains("watertight: True", stdout.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("drill-envelopes-touch.firmament", "SectionStackBlindDrillToolEnvelopesIntersect")]
    [InlineData("plane-drill-flat.firmament", "a flat floor from a plane is not supported")]
    public void UnsupportedDrillsAreRefused(string fixture, string expected)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["validate", Path.Combine(RepoRoot, "fixtures/Invalid/Hole", fixture), "--json"], stdout, stderr);
        Assert.NotEqual(0, exit);
        using var document = JsonDocument.Parse(stdout.ToString());
        var report = document.RootElement.TryGetProperty("firmamentV2Validation", out var nested) ? nested : document.RootElement;
        var diagnostics = report.GetProperty("diagnostics").EnumerateArray()
            .Select(d => d.GetProperty("code").GetString() + " " + d.GetProperty("message").GetString()).ToArray();
        Assert.Contains(diagnostics, d => d.Contains(expected, StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Contains("CavityPlanCreated", StringComparison.Ordinal));
    }

    private static IEnumerable<Aetheris.Kernel.Core.Geometry.Surfaces.ConeSurface> Cones(BrepBody body)
        => body.Topology.Faces.Select(f => body.TryGetFaceSurface(f.Id, out var s) ? s?.Cone : null).OfType<Aetheris.Kernel.Core.Geometry.Surfaces.ConeSurface>();

    private static BrepBody BuildAndImport(string fixture)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-drilled-holes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var step = Path.Combine(directory, Path.GetFileNameWithoutExtension(fixture) + ".step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.True(CliRunner.Run(["build", Path.Combine(RepoRoot, fixture), "--out", step], stdout, stderr) == 0, $"{fixture}: {stderr}{stdout}");
        var import = Step242Importer.ImportBody(File.ReadAllText(step));
        Assert.True(import.IsSuccess, string.Join("; ", import.Diagnostics.Select(d => d.Message)));
        return import.Value;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
