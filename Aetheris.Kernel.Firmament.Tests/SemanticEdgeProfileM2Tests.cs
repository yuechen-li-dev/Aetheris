using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SemanticEdgeProfileM2Tests
{
    [Fact]
    public void Firmament_RectangleEdgeProgramsComposeAndExtrudeWithoutManualCarrierSpans()
    {
        const string source = """
            Rect2 PlateBase { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
            EdgeProfile PlateBase.Bottom {
                Notch CableNotch { FromStart: 18mm; Width: 8mm; Depth: 4mm; Side: Left }
                Tab MountTab { CenteredAt: 50mm; Width: 12mm; Extension: 6mm; Side: Right }
            }
            EdgeProfile PlateBase.Top {
                Cutback HandlingCutback { FromEnd: 10mm; Run: 8mm; Offset: 3mm; Side: Right }
            }
            Profile Plate From PlateBase
            Extrude Solid { Profile: Plate; From: 0mm; To: 3mm }
            """;

        var parsed = ProfileAuthoringParser.Parse(source);

        Assert.Empty(parsed.Diagnostics);
        Assert.NotNull(parsed.Profile);
        Assert.True(ResolvedProfile2DValidator.Validate(parsed.Profile!).IsValid);
        Assert.Equal(LineArcProfileExtrudeStatus.Succeeded, ResolvedProfile2DValidator.Extrude(parsed.Profile!, parsed.Height).Status);
        var segments = parsed.Profile.Loops.Single().Segments;
        Assert.Equal(3, segments.Count(x => x.Provenance.ConceptStableId == "concept-path:PlateBase.Bottom.MountTab"));
        Assert.Contains(segments, x => x.Provenance.ConceptStableId == "concept-path:PlateBase.Bottom.Carrier00");
        Assert.Equal(2, segments.Count(x => x.Provenance.ConceptStableId == "concept-path:PlateBase.Top.HandlingCutback"));

        var root = FirmamentSemanticValues.FromProfilesAndConceptPaths(source).Single(x => x.ExposedName == "Plate");
        var reference = new SemanticReference(root, [], new("test", 0, 1));
        Assert.True(SemanticValueValidator.TryResolveMember(reference, new("Bottom", new("test", 0, 1)), out var bottom, out var bottomDiagnostic), bottomDiagnostic?.Message);
        Assert.True(SemanticValueValidator.TryResolveMember(bottom!, new("MountTab", new("test", 0, 1)), out var tab, out var tabDiagnostic), tabDiagnostic?.Message);
        Assert.Equal("Tab", tab!.Value.Type.Name);
    }

    [Fact]
    public void Resolver_OrdersIndependentAnchorsAndGeneratesCarrierSpans()
    {
        var ir = Edge(
            new SemanticEdgeNotchIr("RightNotch", "Plate.Bottom.RightNotch", new(SemanticEdgeAnchorKind.FromEnd, 10), 8, 3, 1, "test"),
            new SemanticEdgeTabIr("MountTab", "Plate.Bottom.MountTab", new(SemanticEdgeAnchorKind.CenteredAt, 50), 12, 6, -1, "test"));

        var resolved = SemanticEdgeProfileResolver.Resolve(ir);

        Assert.True(resolved.IsSuccess, string.Join('\n', resolved.Diagnostics));
        Assert.Equal(["Carrier", "Tab", "Carrier", "Notch", "Carrier"], resolved.Profile!.OrderedMembers.Select(x => x.Kind));
        Assert.Equal(3, resolved.Profile.OrderedMembers.Single(x => x.StableId == "Plate.Bottom.MountTab").CurveDescendants.Count);
        Assert.All(resolved.Profile.OrderedMembers.Where(x => x.IsGeneratedCarrier), x => Assert.StartsWith("Plate.Bottom.Carrier", x.StableId));
    }

    [Fact]
    public void GenericTemplate_PreservesNestedEdgeFragmentPathAcrossSpecialization()
    {
        const string source = """
            Model TemplateEdgeAttachment {
                Units: mm
                Record Spec { Width: Length TabWidth: Length TabDepth: Length Thickness: Length }
                Static Chosen: Spec = Spec { Width: 100mm TabWidth: 12mm TabDepth: 6mm Thickness: 3mm }
                Template < P: Spec >
                Struct PlateTemplate {
                    Rect2 PlateBase { Center: [0mm, 0mm]; Size: [P.Width, 60mm] }
                    EdgeProfile PlateBase.Bottom {
                        Tab MountTab { CenteredAt: 50mm; Width: P.TabWidth; Extension: P.TabDepth; Side: Right }
                    }
                    Profile Plate From PlateBase
                    Extrude Solid { Profile: Plate; From: 0mm; To: P.Thickness }
                }
                Struct Product = PlateTemplate < P: Chosen >
            }
            """;
        var diagnostics = new List<string>();

        var root = FirmamentSemanticValues.FromProfilesAndConceptPaths(source, reportedDiagnostics: diagnostics).Single(x => x.ExposedName == "Plate");

        Assert.Empty(diagnostics);
        var reference = new SemanticReference(root, [], new("test", 0, 1));
        Assert.True(SemanticValueValidator.TryResolveMember(reference, new("Bottom", new("test", 0, 1)), out var bottom, out _));
        Assert.True(SemanticValueValidator.TryResolveMember(bottom!, new("MountTab", new("test", 0, 1)), out var tab, out _));
        Assert.Equal("Tab", tab!.Value.Type.Name);
        Assert.Contains(root.Provenance, x => x.Stage == "template-specialization");
    }

    [Fact]
    public void Resolver_DeclarationOrderDoesNotAffectGeometryOrHash()
    {
        var a = new SemanticEdgeTabIr("A", "Plate.Bottom.A", new(SemanticEdgeAnchorKind.FromStart, 10), 10, 4, -1, "test");
        var b = new SemanticEdgeCutbackIr("B", "Plate.Bottom.B", new(SemanticEdgeAnchorKind.FromEnd, 10), 10, 3, 1, "test");

        var first = SemanticEdgeProfileResolver.Resolve(Edge(a, b)).Profile!;
        var second = SemanticEdgeProfileResolver.Resolve(Edge(b, a)).Profile!;

        Assert.Equal(first.DeterministicHash, second.DeterministicHash);
        Assert.Equal(first.OrderedMembers.Select(x => x.StableId), second.OrderedMembers.Select(x => x.StableId));
    }

    [Fact]
    public void Resolver_ReportsBothOverlappingSemanticPaths()
    {
        var result = SemanticEdgeProfileResolver.Resolve(Edge(
            new SemanticEdgeTabIr("Tab", "Plate.Bottom.Tab", new(SemanticEdgeAnchorKind.FromStart, 10), 30, 4, -1, "test"),
            new SemanticEdgeNotchIr("Notch", "Plate.Bottom.Notch", new(SemanticEdgeAnchorKind.FromStart, 25), 20, 3, 1, "test")));

        Assert.False(result.IsSuccess);
        Assert.Contains("semantic-edge-fragment-overlap:Plate.Bottom:Plate.Bottom.Tab:Plate.Bottom.Notch", result.Diagnostics);
    }

    [Fact]
    public void Firmament_BadOwnerMemberFailsBeforeContourMaterialization()
    {
        const string source = """
            Rect2 PlateBase { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
            EdgeProfile PlateBase.Diagonal { Tab Bad { FromStart: 10mm; Width: 5mm; Extension: 2mm; Side: Right } }
            Profile Plate From PlateBase
            Extrude Solid { Profile: Plate; From: 0mm; To: 3mm }
            """;

        var parsed = ProfileAuthoringParser.Parse(source);

        Assert.Null(parsed.Profile);
        Assert.Contains("semantic-edge-owner-member-missing:PlateBase.Diagonal:available=Bottom,Right,Top,Left", parsed.Diagnostics);
    }

    [Theory]
    [InlineData(-1, "semantic-edge-fragment-out-of-bounds")]
    [InlineData(96, "semantic-edge-fragment-out-of-bounds")]
    public void Resolver_RejectsOutOfBoundsFragments(double offset, string code)
    {
        var result = SemanticEdgeProfileResolver.Resolve(Edge(
            new SemanticEdgeTabIr("Tab", "Plate.Bottom.Tab", new(SemanticEdgeAnchorKind.FromStart, offset), 5, 2, -1, "test")));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.StartsWith(code, StringComparison.Ordinal));
    }

    [Fact]
    public void Resolver_EdgeLengthChangePreservesFragmentPathsAndAdjustsCarrier()
    {
        var fragment = new SemanticEdgeTabIr("Tab", "Plate.Bottom.Tab", new(SemanticEdgeAnchorKind.FromEnd, 10), 10, 2, -1, "test");
        var shortEdge = SemanticEdgeProfileResolver.Resolve(Edge(fragment)).Profile!;
        var longEdge = SemanticEdgeProfileResolver.Resolve(EdgeTo(120, fragment)).Profile!;

        Assert.Contains(shortEdge.OrderedMembers, x => x.StableId == fragment.StableId);
        Assert.Contains(longEdge.OrderedMembers, x => x.StableId == fragment.StableId);
        Assert.Equal(80, shortEdge.OrderedMembers.First(x => x.IsGeneratedCarrier).EndU, 8);
        Assert.Equal(100, longEdge.OrderedMembers.First(x => x.IsGeneratedCarrier).EndU, 8);
    }

    private static SemanticEdgeProfileIr Edge(params SemanticEdgeFragmentIr[] fragments) => EdgeTo(100, fragments);
    private static SemanticEdgeProfileIr EdgeTo(double length, params SemanticEdgeFragmentIr[] fragments) =>
        new("Plate.Bottom", "Plate.Bottom", new(0, 0), new(length, 0), fragments, "Plate.Bottom[u,v]", "test");
}
