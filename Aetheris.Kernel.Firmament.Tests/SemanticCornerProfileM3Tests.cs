using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SemanticCornerProfileM3Tests
{
    [Fact]
    public void Firmament_RectangleComposesEdgeFragmentAndTwoSharedCornersThenExtrudes()
    {
        const string source = """
            Rect2 PlateBase { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
            EdgeProfile PlateBase.Bottom {
                Tab MountTab { CenteredAt: 50mm; Width: 12mm; Extension: 6mm; Side: Right }
            }
            CornerProfile PlateBase.BottomRight {
                Chamfer CableClearance { SetbackA: 8mm; SetbackB: 5mm; }
            }
            CornerProfile PlateBase.TopLeft {
                NotchCorner LocatingStep { SetbackA: 7mm; SetbackB: 4mm; }
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
        Assert.Single(segments, x => x.Provenance.ConceptStableId == "concept-path:PlateBase.BottomRight.CableClearance");
        Assert.Equal(2, segments.Count(x => x.Provenance.ConceptStableId == "concept-path:PlateBase.TopLeft.LocatingStep"));
        Assert.Equal(3, segments.Count(x => x.Provenance.ConceptStableId == "concept-path:PlateBase.Bottom.MountTab"));

        var root = FirmamentSemanticValues.FromProfilesAndConceptPaths(source).Single(x => x.ExposedName == "Plate");
        var reference = new SemanticReference(root, [], new("test", 0, 1));
        Assert.True(SemanticValueValidator.TryResolveMember(reference, new("BottomRight", new("test", 0, 1)), out var corner, out var cornerDiagnostic), cornerDiagnostic?.Message);
        Assert.Equal("SemanticCorner", corner!.Value.Type.Name);
        Assert.True(SemanticValueValidator.TryResolveMember(corner, new("CableClearance", new("test", 0, 1)), out var chamfer, out var chamferDiagnostic), chamferDiagnostic?.Message);
        Assert.Equal("Chamfer", chamfer!.Value.Type.Name);
    }

    [Fact]
    public void Parser_RejectsCornerConsumptionThatOverlapsNamedEdgeFragment()
    {
        const string source = """
            Rect2 PlateBase { Center: [0mm, 0mm]; Size: [100mm, 60mm] }
            EdgeProfile PlateBase.Bottom {
                Tab EndTab { FromEnd: 2mm; Width: 5mm; Extension: 2mm; Side: Right }
            }
            CornerProfile PlateBase.BottomRight {
                Chamfer EndChamfer { Setback: 8mm; }
            }
            Profile Plate From PlateBase
            Extrude Solid { Profile: Plate; From: 0mm; To: 3mm }
            """;

        var parsed = ProfileAuthoringParser.Parse(source);

        Assert.Null(parsed.Profile);
        Assert.Contains("semantic-corner-edge-fragment-conflict:PlateBase.BottomRight:PlateBase.Bottom.EndTab:owner=PlateBase.Bottom", parsed.Diagnostics);
    }

    [Fact]
    public void Resolver_SupportsNonOrthogonalEdgesAndUsesNoCandidateSelection()
    {
        var source = new SemanticCornerProfileIr(
            "Plate.ObliqueCorner", "Plate.ObliqueCorner", "Plate.EdgeA", "Plate.EdgeB",
            new(-10, 0), new(0, 0), new(5, 10),
            new SemanticCornerChamferIr("ExactChamfer", "Plate.ObliqueCorner.ExactChamfer", 2, 3, "test"),
            "u/v", "test");

        var first = SemanticCornerProfileResolver.Resolve(source);
        var second = SemanticCornerProfileResolver.Resolve(source);

        Assert.True(first.IsSuccess, string.Join('\n', first.Diagnostics));
        Assert.Single(first.Corner!.CurveDescendants);
        Assert.Equal(first.Corner.DeterministicHash, second.Corner!.DeterministicHash);
        Assert.Equal(new SemanticProfilePoint(-2, 0), first.Corner.EdgeAEndpoint);
        Assert.Equal(3, Math.Sqrt(first.Corner.EdgeBEndpoint.X * first.Corner.EdgeBEndpoint.X + first.Corner.EdgeBEndpoint.Y * first.Corner.EdgeBEndpoint.Y), 8);
    }

    [Fact]
    public void Resolver_TrimsBothCarrierEndpointsAndOmitsZeroLengthSpans()
    {
        var edge = new SemanticEdgeProfileIr("Plate.Bottom", "Plate.Bottom", new(0, 0), new(100, 0), [], "u/v", "test");

        var result = SemanticEdgeProfileResolver.Resolve(edge, new(6, 8, "Plate.BottomLeft", "Plate.BottomRight"));

        Assert.True(result.IsSuccess, string.Join('\n', result.Diagnostics));
        var carrier = Assert.Single(result.Profile!.OrderedMembers);
        Assert.Equal(6, carrier.StartU);
        Assert.Equal(92, carrier.EndU);
        var line = Assert.IsType<LineArcLineSegment2D>(Assert.Single(carrier.CurveDescendants).Geometry);
        Assert.Equal((6d, 0d), line.Start);
        Assert.Equal((92d, 0d), line.End);
    }
}
