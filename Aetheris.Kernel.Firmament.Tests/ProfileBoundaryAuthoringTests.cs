using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ProfileBoundaryAuthoringTests
{
    private const string Panel = """
        Model Test {
          Units: mm
          Rect2 Blank { Center: [0mm,0mm]; Size: [120mm,80mm] }
          Profile Original { Loop Outer { Blank |> TraceLoop } }
          Profile Panel {
            From: Blank
            Replace Grip {
              On: Blank.Right
              Range: [20mm,60mm]
              Through: [[48mm,-10mm], [48mm,10mm]]
              Join: Tangent
            }
          }
          Extrude Body { Profile: Panel From: 0mm To: 3mm }
        }
        """;

    [Fact]
    public void DerivationPreservesSeedAndMaterializesThroughTheRealCompiler()
    {
        var original = ProfileAuthoringParser.ResolveNamedProfile(Panel, "Original", out var originalErrors);
        var derived = ProfileAuthoringParser.ResolveNamedProfile(Panel, "Panel", out var errors);
        Assert.Empty(originalErrors); Assert.Empty(errors); Assert.NotNull(derived);
        Assert.Equal(9600, ResolvedProfile2DValidator.Validate(original!).SignedArea, 7);
        Assert.True(ResolvedProfile2DValidator.Validate(derived).SignedArea < 9600);
        Assert.Equal(8, derived.Loops[0].Segments.Count);
        var curves = derived.Loops[0].Segments.Where(s => s.Geometry is LineArcCubicBezier2D).ToArray();
        Assert.Equal(3, curves.Length);
        var first = (LineArcCubicBezier2D)curves[0].Geometry;
        Assert.Equal((60d, -20d), first.Start);
        Assert.Equal(first.Start.X, first.Control1.X);
        Assert.Single(derived.BoundaryEdits!);
        var build = FirmamentBuildAndExport.CompileSource(Panel);
        Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics));
    }

    [Theory]
    [InlineData("Range: [20mm,60mm]", "Range: [-1mm,60mm]", "profile-edit-range-invalid")]
    [InlineData("On: Blank.Right", "On: Blank.Missing", "profile-edit-target-missing")]
    [InlineData("Join: Tangent", "Join: Tangent; Join: Position", "profile-edit-field-invalid")]
    [InlineData("Join: Tangent", "StartTangent: [0,0]", "profile-edit-tangent-invalid")]
    [InlineData("Through: [[48mm,-10mm], [48mm,10mm]]", "Through: [[60mm,-20mm]]", "profile-edit-knots-invalid")]
    [InlineData("Through: [[48mm,-10mm], [48mm,10mm]]", "Through: [[48deg,-10mm]]", "profile-edit-vector-invalid")]
    [InlineData("Join: Tangent", "Equation: t => t", "profile-edit-field-invalid")]
    [InlineData("From: Blank", "From: Panel", "concept-profile-dependency-cycle")]
    [InlineData("Through: [[48mm,-10mm], [48mm,10mm]]", "Through: [[-100mm,0mm]]", "profile-edit-self-intersection")]
    [InlineData("Range: [20mm,60mm]", "Range: [0mm,80mm]", "profile-edit-join-mismatch")]
    public void InvalidEditsFailBeforeMaterialization(string before, string after, string diagnostic)
    {
        var profile = ProfileAuthoringParser.ResolveNamedProfile(Panel.Replace(before, after), "Panel", out var errors);
        Assert.Null(profile); Assert.Contains(errors, e => e.Contains(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void OverlapIsRejectedAndDisjointEditOrderDoesNotChangeGeometry()
    {
        const string second = "Replace Other { On: Blank.Right; Range: [30mm,70mm]; Through: [[50mm,0mm]] }";
        var source = Panel.Replace("From: Blank", "From: Blank\n" + second);
        Assert.Null(ProfileAuthoringParser.ResolveNamedProfile(source, "Panel", out var errors));
        Assert.Contains(errors, e => e.StartsWith("profile-edit-overlap:"));
        const string disjoint = "Replace LeftGrip { On: Blank.Left; Through: [[-50mm,0mm]] }";
        var a = ProfileAuthoringParser.ResolveNamedProfile(Panel.Replace("From: Blank", "From: Blank\n" + disjoint), "Panel", out var ae);
        var b = ProfileAuthoringParser.ResolveNamedProfile(Panel.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("Join: Tangent\n    }", "Join: Tangent\n    }\n" + disjoint), "Panel", out var be);
        Assert.Empty(ae); Assert.Empty(be);
        Assert.Equal(a!.Loops[0].Segments.Select(s => s.Geometry), b!.Loops[0].Segments.Select(s => s.Geometry));
    }

    [Fact]
    public void AppliedDeltaAndFurtherDerivationKeepNamedDescendants()
    {
        const string source = """
            Rect2 Blank { Center: [0mm,0mm]; Size: [120mm,80mm] }
            ProfileDelta ServiceTab {
              On: Blank.Top; Anchor: CenteredAt 60mm; Side: Outward;
              Level Carrier { Offset: 0mm; }
              Level Extended { Offset: 8mm; }
              Transition Enter { Kind: Step; To: Extended; }
              Span Crown { Run: 24mm; At: Extended; }
              Transition Exit { Kind: Step; To: Carrier; }
            }
            Profile Panel { From: Blank; Apply ServiceTab }
            Profile Finished { From: Panel; Replace Rounded {
              On: Panel.ServiceTab.Crown; Through: [[0mm,52mm]]
            } }
            """;
        var result = ProfileAuthoringParser.ResolveNamedProfile(source, "Finished", out var errors);
        Assert.Empty(errors); Assert.NotNull(result);
        Assert.Contains(result.Loops[0].Segments, s => s.Name == "Rounded_Span0");
        Assert.DoesNotContain(result.Loops[0].Segments, s => s.Name == "ServiceTab.Crown");
        var panel = ProfileAuthoringParser.ResolveNamedProfile(source, "Panel", out var panelErrors);
        Assert.Empty(panelErrors);
        Assert.Equal(9600 + 24 * 8, ResolvedProfile2DValidator.Validate(panel!).SignedArea, 7);
    }

    [Fact]
    public void ExplicitDerivativesConstructExactCubics()
    {
        var source = Panel.Replace("Join: Tangent", "Derivatives: [[0mm,20mm], [-10mm,15mm], [10mm,15mm], [0mm,20mm]]");
        var result = ProfileAuthoringParser.ResolveNamedProfile(source, "Panel", out var errors);
        Assert.Empty(errors); Assert.NotNull(result);
        var cubic = Assert.IsType<LineArcCubicBezier2D>(result.Loops[0].Segments.Single(s => s.Name == "Grip_Span0").Geometry);
        Assert.Equal(-20 + 20d / 3, cubic.Control1.Y, 10);
        Assert.Equal("ExplicitHermite", result.BoundaryEdits![0].Construction);
    }

    [Theory]
    [InlineData("[[-270mm,300mm],[270mm,300mm]]", "profile-edit-self-intersection")]
    [InlineData("[[0mm,200mm],[0mm,200mm]]", "profile-edit-cusp")]
    public void IndividualCubicLoopsAndRetracingAreRejected(string derivatives, string diagnostic)
    {
        var source = Panel.Replace("Through: [[48mm,-10mm], [48mm,10mm]]", "Through: []")
            .Replace("Join: Tangent", "Derivatives: " + derivatives);
        Assert.Null(ProfileAuthoringParser.ResolveNamedProfile(source, "Panel", out var errors));
        Assert.Contains(errors, e => e.StartsWith(diagnostic + ":"));
    }

    [Fact]
    public void LibraryTemplateFixtureBuildsAndSeedCommentsAreAccepted()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Boundary2/edited-panel.firmament");
        var source = File.ReadAllText(path);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "Panel", out var errors);
        Assert.Empty(errors); Assert.NotNull(profile);
        Assert.Equal(2, profile.BoundaryEdits!.Count);
        var compiled = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
        Assert.NotNull(ProfileAuthoringParser.ResolveNamedProfile(Panel.Replace("From: Blank", "// closed seed\n From: Blank"), "Panel", out errors));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Circle2 Blank { Center: [0mm,0mm]; Radius: 10mm }")]
    [InlineData("Ellipse2 Blank { Center: [0mm,0mm]; AxisLengths: [20mm,30mm] }")]
    public void PeriodicSeedsAreValuesButCurvedReplacementIsNotAdmitted(string seed)
    {
        Assert.NotNull(ProfileAuthoringParser.ResolveNamedProfile(seed + "\nProfile P { From: Blank }", "P", out var errors));
        Assert.Empty(errors);
        Assert.Null(ProfileAuthoringParser.ResolveNamedProfile(seed + "\nProfile P { From: Blank; Replace X { On: Blank.Boundary; Through: [[1mm,1mm]] } }", "P", out errors));
        Assert.Contains(errors, e => e.StartsWith("profile-edit-curve-not-qualified:"));
    }
}
