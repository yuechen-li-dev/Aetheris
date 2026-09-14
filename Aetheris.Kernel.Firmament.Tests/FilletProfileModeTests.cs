using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FilletProfileModeTests
{
    [Fact]
    public void ExistingConcaveFilletRouteCarriesSmoothProfileToStep()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/Fillet/concave-curvature-continuous.firmament");
        var smooth = CompatibilityBuild(source);
        Assert.True(smooth.IsSuccess, string.Join("\n", smooth.Diagnostics.Select(d => d.Message)));
        Assert.Contains("B_SPLINE_SURFACE_WITH_KNOTS", smooth.Value.Export.StepText);
        var circular = CompatibilityBuild(source.Replace("profile: CurvatureContinuous", "profile: Circular"));
        Assert.True(circular.IsSuccess, string.Join("\n", circular.Diagnostics.Select(d => d.Message)));
        Assert.Contains("CYLINDRICAL_SURFACE", circular.Value.Export.StepText);
        Assert.DoesNotContain("B_SPLINE_SURFACE_WITH_KNOTS", circular.Value.Export.StepText);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("1")]
    public void InvalidProfileDoesNotSilentlyUseCircularMode(string mode)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/Fillet/concave-curvature-continuous.firmament")
            .Replace("profile: CurvatureContinuous", "profile: " + mode);
        var result = CompatibilityBuild(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("BoundedFilletProfileUnsupported"));
    }

    [Fact]
    public void ProfileBoundaryRouteDoesNotPretendToSupportSmoothContactShells()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Invalid/Fillet/smooth-loop-contact-shell.firmament");
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "Section", out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.NotNull(profile);
        Assert.False(ProfileBoundaryChamferSourceBinder.TryBindFillet(source, profile!, "Body", out _, out _, out _, out var diagnostic));
        Assert.Equal("ProfileBoundaryFilletProfileModeUnsupported:CurvatureContinuous:polynomial-contact-shell-required", diagnostic);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
    }
    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentBuildAndExportResult> CompatibilityBuild(string source)
    {
        var directory = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "artifacts", "local", "fillet-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "input.firmament");
        File.WriteAllText(file, source);
        return FirmamentBuildAndExport.Run(file, Path.Combine(directory, "result.step"));
    }
}
