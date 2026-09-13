using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GearLibraryX0Tests
{
    [Fact]
    public void SpurGearOwnsStandardGeometryAndDeterministicStep()
    {
        var source = Fixture("Canonical", "Gears", "spur-basic.firmament");
        var document = GearAuthoring.Parse(File.ReadAllText(source));
        var gear = Assert.Single(document.Gears);
        Assert.True(document.IsSuccess); Assert.Equal(48d, gear.PitchDiameterMm); Assert.Equal(52d, gear.AddendumDiameterMm);
        Assert.Equal(43d, gear.RootDiameterMm); Assert.Equal(48d * Math.Cos(20d * Math.PI / 180d), gear.BaseDiameterMm!.Value, 10);

        var first = FirmamentBuildAndExport.CompileSource(File.ReadAllText(source));
        var second = FirmamentBuildAndExport.CompileSource(File.ReadAllText(source));
        Assert.True(first.IsSuccess); Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Gear!.StepSha256, second.Value!.Gear!.StepSha256);
        Assert.True(first.Value.Gear.EnclosedManifold); Assert.True(first.Value.Gear.StepReimportedManifold);
        Assert.Equal(96, first.Value.Gear.Gears[0].InvoluteSpanCount);
        Assert.True(first.Value.Gear.Gears[0].MaximumInvoluteApproximationError < .002d);
        Assert.Contains("B_SPLINE_CURVE_WITH_KNOTS", first.Value.StepText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("internal-spur-basic.firmament", "InternalSpurGear", 100)]
    [InlineData("bevel-basic.firmament", "BevelGear", 45)]
    [InlineData("miter-basic.firmament", "MiterGear", 20)]
    [InlineData("ratchet-basic.firmament", "RatchetGear", 60)]
    [InlineData("pawl-basic.firmament", "Pawl", null)]
    public void CanonicalFamiliesMaterializeAndReimport(string file, string family, int? teeth)
    {
        var source = File.ReadAllText(Fixture("Canonical", "Gears", file));
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        var report = result.Value!.Gear!; var item = Assert.Single(report.Gears);
        Assert.Equal(family, item.Family); Assert.Equal(teeth, item.Teeth);
        Assert.True(report.EnclosedManifold); Assert.True(report.StepReimportSucceeded); Assert.True(report.StepReimportedManifold);
        if (family is "BevelGear" or "MiterGear") { Assert.True(report.Cones > 0); Assert.True(report.BSplineSurfaces > 0); }
        if (family == "InternalSpurGear") Assert.True(item.MaximumInvoluteApproximationError < .00001d);
    }

    [Theory]
    [InlineData("spur-pair.firmament", "ExternalExternal", 72d, -1, 0.5d)]
    [InlineData("internal-spur-pair.firmament", "ExternalInternal", 20d, 1, 1d / 3d)]
    [InlineData("bevel-pair.firmament", "Bevel", null, -1, .5d)]
    [InlineData("miter-pair.firmament", "Bevel", null, -1, 1d)]
    [InlineData("ratchet-pawl.firmament", "RatchetPawl", null, 0, null)]
    public void InterfacesExposeCompatibilityMath(string file, string kind, double? center, int sign, double? ratio)
    {
        var document = GearAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "GearInterfaces", file)));
        Assert.True(document.IsSuccess, string.Join(Environment.NewLine, document.Diagnostics));
        var mesh = Assert.Single(document.Interfaces); Assert.True(mesh.Compatible); Assert.Equal(kind, mesh.Kind);
        Assert.Equal(center, mesh.ExpectedCenterDistanceMm); Assert.Equal(sign, mesh.RotationSign); Assert.Equal(ratio, mesh.Ratio);
        if (kind == "Bevel") { Assert.Equal(90d, mesh.ShaftAngleDegrees); Assert.Equal("Intersecting:90deg", mesh.AxisRelation); }
        else if (kind == "RatchetPawl") { Assert.Equal("Clockwise", mesh.AllowedDirection); Assert.Equal(2d, mesh.EngagementPhaseDegrees); Assert.Equal("EngagementPlane", mesh.AxisRelation); }
        else Assert.Equal("Parallel", mesh.AxisRelation);
    }

    [Theory]
    [InlineData("zero-module.firmament", "firmament-gear-module-invalid")]
    [InlineData("zero-teeth.firmament", "firmament-gear-teeth-invalid")]
    [InlineData("bore-too-large.firmament", "firmament-gear-bore-too-large")]
    [InlineData("internal-ring-too-small.firmament", "firmament-gear-internal-outside-diameter-too-small")]
    [InlineData("incompatible-pair.firmament", "module-mismatch")]
    [InlineData("zero-face-width.firmament", "face-width-invalid")]
    [InlineData("incompatible-pressure-angle.firmament", "pressure-angle-mismatch")]
    [InlineData("impossible-internal-pair.firmament", "internal-tooth-count-not-greater")]
    [InlineData("invalid-bevel-pair.firmament", "pitch-cones-do-not-close")]
    [InlineData("invalid-ratchet.firmament", "ratchet-teeth-below-qualified-range")]
    public void InvalidFixturesHaveTypedDiagnostics(string file, string fragment)
    {
        var document = GearAuthoring.Parse(File.ReadAllText(Fixture("Invalid", "Gears", file)));
        Assert.False(document.IsSuccess); Assert.Contains(document.Diagnostics, item => item.Contains(fragment, StringComparison.Ordinal));
    }

    private static string Fixture(params string[] path) => Path.Combine([RepoRoot(), "fixtures", .. path]);
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
