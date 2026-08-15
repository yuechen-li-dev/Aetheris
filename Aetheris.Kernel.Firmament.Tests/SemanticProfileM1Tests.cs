using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SemanticProfileM1Tests
{
    [Fact]
    public void Mir_LowersNamedMembersOneToManyWithStableProvenanceAndHash()
    {
        var ir = new SemanticProfileIr("Edge", "profile:Edge", "XY", new(0, 0), 0,
        [
            new SemanticProfileSpanIr("Lead", "profile:Edge.Lead", 10, null, null, null, "source:1"),
            new SemanticProfileChamferIr("EndChamfer", "profile:Edge.EndChamfer", 3, 2, 1, "source:2"),
            new SemanticProfileStepIr("LowerStep", "profile:Edge.LowerStep", 5, 2, -1, "source:3"),
            new SemanticProfileNotchIr("CableNotch", "profile:Edge.CableNotch", 4, 1.5, 1, "source:4"),
            new SemanticProfileCutbackIr("AttachmentCutback", "profile:Edge.AttachmentCutback", 3, 1, -1, "source:5"),
            new SemanticProfileTabIr("MountTab", "profile:Edge.MountTab", 8, 3, -1, "source:6"),
            new SemanticProfileArcTransitionIr("ReturnArc", "profile:Edge.ReturnArc", 2, 90, "source:7")
        ], [], [new("DatumA", "profile:Edge.DatumA", new(0, 0), "authored")], "test");

        var first = SemanticProfileMirResolver.Resolve(ir);
        var second = SemanticProfileMirResolver.Resolve(ir);

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.Equal(first.Profile!.DeterministicHash, second.Profile!.DeterministicHash);
        Assert.Equal(3, first.Profile.Members.Single(member => member.Name == "MountTab").CurveDescendants.Count);
        Assert.Equal(2, first.Profile.Members.Single(member => member.Name == "LowerStep").CurveDescendants.Count);
        Assert.Equal(3, first.Profile.Members.Single(member => member.Name == "CableNotch").CurveDescendants.Count);
        Assert.All(first.Profile.Members.SelectMany(member => member.CurveDescendants), curve => Assert.StartsWith("lowered-from:profile:Edge.", curve.Provenance, StringComparison.Ordinal));
        Assert.Equal(first.Profile.Members.Sum(member => member.CurveDescendants.Count), first.Profile.ExactCurveChain.Count);
    }

    [Fact]
    public void Mir_ConstraintDiagnosticsNameSemanticMembers()
    {
        var ir = new SemanticProfileIr("MirroredTabs", "profile:MirroredTabs", "XY", new(0, 0), 0,
        [
            new SemanticProfileTabIr("LeftTab", "profile:MirroredTabs.LeftTab", 10, 4, 1, "source:left"),
            new SemanticProfileTabIr("RightTab", "profile:MirroredTabs.RightTab", 11.2, 4, -1, "source:right")
        ], [new("TabMirror", "profile:MirroredTabs.TabMirror", "Mirror", ["LeftTab", "RightTab"], "authored")], [], "test");

        var result = SemanticProfileMirResolver.Resolve(ir);

        Assert.False(result.IsSuccess);
        Assert.Contains("semantic-profile-mirror-mismatch:profile:MirroredTabs.TabMirror:LeftTab,RightTab", result.Diagnostics);
    }

    [Fact]
    public void Mir_ExplicitlyLowersToPlanarContourAuthority()
    {
        var ir = new SemanticProfileIr("Rectangle", "profile:Rectangle", "XY", new(0, 0), 0,
        [
            new SemanticProfileSpanIr("Bottom", "profile:Rectangle.Bottom", 20, null, null, null, "source:1"),
            new SemanticProfileSpanIr("Right", "profile:Rectangle.Right", 10, 90, null, null, "source:2"),
            new SemanticProfileSpanIr("Top", "profile:Rectangle.Top", 20, 90, null, null, "source:3"),
            new SemanticProfileCloseIr("Left", "profile:Rectangle.Left", "source:4")
        ], [], [], "test");
        var resolved = SemanticProfileMirResolver.Resolve(ir).Profile!;

        var contour = resolved.LowerToPlanarContour2();

        Assert.True(PlanarContourKernel.Validate(contour).IsValid);
        Assert.Equal(["profile:Rectangle.Bottom", "profile:Rectangle.Right", "profile:Rectangle.Top", "profile:Rectangle.Left"],
            contour.OuterLoop.Segments.Select(segment => segment.Provenance.ConceptStableId));
    }

    [Fact]
    public void FirmamentSemanticMembers_LowerThroughNormalProfileExtrusionPath()
    {
        var parsed = ProfileAuthoringParser.Parse(MountingPlate);

        var profile = Assert.IsType<ResolvedProfile2D>(parsed.Profile);
        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(8, profile.Loops.Single().Segments.Count);
        Assert.Equal(3, profile.Loops.Single().Segments.Count(segment => segment.Provenance.ConceptStableId == "concept-path:Outline.MountTab"));
        Assert.All(profile.Loops.Single().Segments.Where(segment => segment.Provenance.ConceptStableId == "concept-path:Outline.MountTab"),
            segment => Assert.Equal("SemanticProfileMIR:Tab", segment.Provenance.Derivation));
        Assert.True(ResolvedProfile2DValidator.Validate(profile).IsValid);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, ResolvedProfile2DValidator.Extrude(profile, parsed.Height).Status);
    }

    [Fact]
    public void SemanticValue_PathAddressesProfileMemberAndExactDescendants()
    {
        var root = Assert.Single(FirmamentSemanticValues.FromProfilesAndConceptPaths(MountingPlate));
        Assert.Equal("Plate", root.ExposedName);
        Assert.Equal("SemanticProfile", root.Type.Name);
        var reference = new SemanticReference(root, [], new("test", 0, 1));
        Assert.True(SemanticValueValidator.TryResolveMember(reference, new("MountTab", new("test", 1, 8)), out var tab, out var tabDiagnostic), tabDiagnostic?.Message);
        Assert.Equal("Tab", tab!.Value.Type.Name);
        Assert.True(SemanticValueValidator.TryResolveMember(tab, new("Curve02", new("test", 9, 7)), out var curve, out var curveDiagnostic), curveDiagnostic?.Message);
        Assert.Equal("ExactProfileCurve", curve!.Value.Type.Name);
        Assert.True(SemanticValueValidator.TryResolveMember(tab, new("End", new("test", 16, 3)), out var end, out var endDiagnostic), endDiagnostic?.Message);
        Assert.Equal("Point2", end!.Value.Type.Name);
    }

    [Fact]
    public void UserDefinedGenericTemplate_ProducesAddressableSemanticProfile()
    {
        const string source = """
            Model TemplateSemanticProfile {
                Units: mm
                Record PlateSpec { TabWidth: Length TabExtension: Length Thickness: Length }
                Static Spec: PlateSpec = PlateSpec { TabWidth: 12mm TabExtension: 8mm Thickness: 3mm }
                Template < P: PlateSpec >
                Struct MountingPlateTemplate {
                    Concept Path Outline {
                        Start: Point2(0mm, 0mm)
                        Heading: 0deg
                        Span BottomLeft { Length: 44mm }
                        Tab MountTab { Width: P.TabWidth; Extension: P.TabExtension; Side: Right }
                        Span BottomRight { Length: 44mm }
                        Span Right { Turn: 90deg; Length: 60mm }
                        Span Top { Turn: 90deg; Length: 100mm }
                        Close Left
                    }
                    Profile Plate From Outline
                    Extrude Solid { Profile: Plate; From: 0mm; To: P.Thickness }
                }
                Struct Product = MountingPlateTemplate < P: Spec >
            }
            """;

        var diagnostics = new List<string>();
        var values = FirmamentSemanticValues.FromProfilesAndConceptPaths(source, reportedDiagnostics: diagnostics);

        Assert.Empty(diagnostics);
        var profile = Assert.Single(values);
        Assert.Equal("Plate", profile.ExposedName);
        Assert.Equal("Tab", profile.ExposedMembers["MountTab"].Type.Name);
        Assert.Equal(3, profile.ExposedMembers["MountTab"].ExposedMembers.Count(member => member.Key.StartsWith("Curve", StringComparison.Ordinal)));
        Assert.Contains(profile.Provenance, item => item.Stage == "template-specialization");
    }

    [Fact]
    public void RectangleAndCircle_ExposeCanonicalSemanticMembers()
    {
        const string source = """
            Point2 Origin { Position: Point2(0mm, 0mm) }
            Rect2 PlateProfile { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
            Circle2 Vent { Center: Origin; Radius: 5mm }
            """;
        var values = FirmamentSemanticValues.FromProfilesAndConceptPaths(source);
        var rectangle = values.Single(value => value.ExposedName == "PlateProfile");
        var circle = values.Single(value => value.ExposedName == "Vent");

        Assert.Equal("Span", rectangle.ExposedMembers["Right"].Type.Name);
        Assert.Equal("Point2", rectangle.ExposedMembers["TopRight"].Type.Name);
        Assert.Equal("Point2", circle.ExposedMembers["Center"].Type.Name);
        Assert.Equal("Length", circle.ExposedMembers["Radius"].Type.Name);
    }

    [Theory]
    [InlineData("Tab Bad { Width: 0mm; Extension: 2mm; Side: Right }", "semantic-profile-invalid-tab:concept-path:Outline.Bad")]
    [InlineData("Step Bad { Run: 2mm; Rise: -1mm; Side: Left }", "semantic-profile-invalid-step:concept-path:Outline.Bad")]
    [InlineData("Cutback Bad { Run: 2mm; Offset: 1mm }", "semantic-profile-invalid-cutback:concept-path:Outline.Bad")]
    public void InvalidSemanticFeatures_NameTheSemanticPath(string feature, string expected)
    {
        var source = $"Concept Path Outline {{ Start: Point2(0mm, 0mm) Heading: 0deg {feature} }}\nProfile Plate From Outline\nExtrude Solid {{ Profile: Plate; From: 0mm; To: 1mm }}";
        var result = ProfileAuthoringParser.Parse(source);
        Assert.Null(result.Profile);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.StartsWith(expected, StringComparison.Ordinal));
    }

    private const string MountingPlate = """
        Concept Path Outline {
            Start: Point2(0mm, 0mm)
            Heading: 0deg
            Span BottomLeft { Length: 44mm }
            Tab MountTab { Width: 12mm; Extension: 8mm; Side: Right }
            Span BottomRight { Length: 44mm }
            Span Right { Turn: 90deg; Length: 60mm }
            Span Top { Turn: 90deg; Length: 100mm }
            Close Left
        }
        Profile Plate From Outline
        Extrude Solid { Profile: Plate; From: 0mm; To: 3mm }
        """;
}
