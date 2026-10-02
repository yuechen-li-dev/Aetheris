using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FunctionalPickupTests
{
    private static string Source => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Feature/pickup-functional.firmament"));

    [Fact]
    public void PickupHasEightNamedBossesPrivateProfilesAndOneExactClosedBody()
    {
        var source = Source;
        var parsed = FirmamentV2Parser.Parse(source.Replace("schema Mechanical", ""));
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal(8, parsed.Document!.Bosses!.Count);
        Assert.Equal(3, parsed.Document.FeatureInvocations!.Count);
        Assert.Equal(new[] { "UpperCoil", "LowerCoil", "PoleSeed" }, parsed.Document.FeatureInvocations.Select(i => i.ResultIdentity));
        var pattern = Assert.Single(parsed.Document.StaticAuthoring!.LinearPatterns!);
        Assert.Equal("PoleSeed", pattern.Source);
        Assert.Equal(6, pattern.Count);
        Assert.Equal(10.2, pattern.Spacing);
        Assert.Equal(2, parsed.Document.StaticAuthoring.PlanarAxes!.Count);
        Assert.Contains(parsed.Document.Bosses, b => b.Name == "UpperCoil" && b.On == "BaseTop");
        Assert.Contains(parsed.Document.Bosses, b => b.Name == "LowerCoil" && b.On == "BaseTop");
        Assert.Equal("UpperCoil__Section", parsed.Document.Bosses.Single(b => b.Name == "UpperCoil").Profile);
        Assert.Equal("LowerCoil__Section", parsed.Document.Bosses.Single(b => b.Name == "LowerCoil").Profile);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics));
        var import = Step242Importer.ImportBody(build.Value!.StepText);
        Assert.True(import.IsSuccess, string.Join("\n", import.Diagnostics));
        var mass = BrepMassProperties.Evaluate(import.Value!);
        Assert.True(mass.IsEnclosed, string.Join("\n", mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent, string.Join("\n", mass.Diagnostics));
        var expected = (88 * 46 - (4 - Math.PI) * 16) * 4
            + 2 * (70 * 17 - (4 - Math.PI) * 9) * 5 + 6 * Math.PI * 2.2 * 2.2;
        Assert.Equal(expected, mass.SignedVolume, 5);
    }

    [Fact]
    public void NestedNamedCallsKeepPrivateConstructionNamesDistinct()
    {
        var source = """
            Model NestedProfiles {
                Units: mm
                Feature Inner() -> Boss {
                    Circle2 Outline { Center: [0mm,0mm]; Radius: 2mm }
                    Profile Section { Loop Outer { Outline |> TraceLoop } }
                    return Boss { On: Top; Profile: Section; Height: 2mm }
                }
                Feature Outer() -> Boss {
                    Circle2 Outline { Center: [0mm,0mm]; Radius: 3mm }
                    Profile Section { Loop Outer { Outline |> TraceLoop } }
                    return Inner()
                }
                Concept Struct Layout On XY { Rect2 Stock { Center: [0mm,0mm]; Size: [20mm,20mm] } }
                Profile BaseProfile Using Layout { Loop Outer { Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Close } }
                Struct Product { Compose Body {
                    Base Stock { Profile: BaseProfile; From: 0mm; To: 4mm; Role: Stock }
                    Feature Pad = Outer()
                } }
            }
            """;
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics));
        var diagnostics = new List<string>();
        var expansion = FirmamentV2FeatureExpansion.Expand(source, diagnostics);
        Assert.NotNull(expansion);
        Assert.Empty(diagnostics);
        Assert.Contains("Profile Pad__Section", expansion.Source);
        Assert.Contains("Profile Pad__Call1__Section", expansion.Source);
        Assert.Contains("Boss  Pad ", expansion.Source);
    }

    [Theory]
    [InlineData("Source: PoleSeed", "Source: UpperCoil", "firmament-feature-linear-source-profile-not-qualified")]
    [InlineData("Count: 6", "Count: 0", "firmament-feature-linear-count-invalid")]
    [InlineData("Count: 6", "Count: 1025", "firmament-feature-linear-count-invalid")]
    [InlineData("Spacing: Spec.PolePitch", "Spacing: 0mm", "firmament-feature-linear-spacing-invalid")]
    [InlineData("Direction: [1,0]", "Direction: [0,0]", "firmament-feature-linear-axis-invalid")]
    [InlineData("Layout.Longitudinal.Direction", "Layout.Unknown.Direction", "firmament-feature-linear-axis-unresolved")]
    [InlineData("Up: [0,1,0]", "Up: [1,0,0]", "feature-support-plane-not-qualified")]
    [InlineData("Count: 6", "Count: 6; Extra: 1", "firmament-feature-linear-fields-invalid")]
    [InlineData("Normal: [0,0,1]", "Normal: [0,1,0]", "feature-support-plane-not-qualified")]
    [InlineData("Spec.BaseHeight + Spec.CoilHeight", "100mm", "firmament-boss-disconnected-from-host")]
    [InlineData("Point2 LowerCenter", "Point2 WrongCenter", "firmament-boundary2-invalid-dimensions")]
    public void InvalidPickupStopsAtTheOwningSemanticBoundary(string before, string after, string diagnostic)
    {
        var result = FirmamentBuildAndExport.CompileSource(Source.Replace(before, after));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void WithOverrideChangesPoleSpacingAndExpansionRemainsDeterministic()
    {
        var source = Source.Replace("Humbucker<Spec: StandardPickup>", "Humbucker<Spec: BridgePickupSpec>")
            .Replace("with { PolePitch: 10.2mm }", "with { PolePitch: 10.4mm }");
        var first = FirmamentBuildAndExport.CompileSource(source);
        var second = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(first.IsSuccess, string.Join("\n", first.Diagnostics));
        Assert.True(second.IsSuccess, string.Join("\n", second.Diagnostics));
        Assert.Equal(first.Value!.StepText, second.Value!.StepText);
        var parsed = FirmamentV2Parser.Parse(source.Replace("schema Mechanical", ""));
        Assert.Equal(10.4, Assert.Single(parsed.Document!.StaticAuthoring!.LinearPatterns!).Spacing);
    }
}
