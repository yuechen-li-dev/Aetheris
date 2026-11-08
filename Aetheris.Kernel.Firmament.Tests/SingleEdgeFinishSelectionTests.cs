using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SingleEdgeFinishSelectionTests
{
    private static string Bracket {
        get {
            var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament");
            return source[..source.IndexOf("  Modify", StringComparison.Ordinal)] + "}";
        }
    }

    [Theory]
    [InlineData("Top", "Fillet")]
    [InlineData("Bottom", "Fillet")]
    [InlineData("Top", "Chamfer")]
    [InlineData("Bottom", "Chamfer")]
    public void LegacyCapFeature_RemainsBuildable_WithoutAdvertisingAFullEdgeFillet(string side, string kind)
    {
        var original = FirmamentBuildAndExport.CompileSource(Bracket).Value;
        var correspondence = original.RuntimeCorrespondence!;
        var role = side == "Top" ? SemanticTopologyRole.TopBoundary : SemanticTopologyRole.BottomBoundary;
        var descendant = Assert.Single(correspondence.Descendants, d => d.Role == role && d.SourceStableId == "profile:section.Outer.Vertex2_Vertex3");
        var selected = SingleEdgeFinishSelection.Resolve(correspondence, descendant.Edge!.Value)!;
        Assert.Equal(new("bracket", side, "section.Outer.Vertex2_Vertex3", "Extrusion end-face edge", FilletSupported: false), selected);
        var field = kind == "Fillet" ? "Radius" : "Distance";
        var source = Bracket[..Bracket.LastIndexOf('}')] + $"Modify {selected.Body} {{ EdgeFinish Picked {{ Face: {selected.Face} Target: {selected.Target} Kind: {kind} {field}: 0.5mm }} }} }}";
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var mass = BrepMassProperties.Evaluate(result.Value.RuntimeBody!);
        Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
        Assert.InRange(mass.AbsoluteVolume, 4000, 4019.99);
        var imported = Step242Importer.ImportBody(result.Value.StepText); Assert.True(imported.IsSuccess);
        var reimported = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(reimported.IsEnclosed); Assert.True(reimported.IsOrientationConsistent);
        Assert.True(Math.Abs(mass.AbsoluteVolume - reimported.AbsoluteVolume) <= Math.Max(1e-6, (mass.ErrorBound ?? 0) + (reimported.ErrorBound ?? 0)),
            $"Native {mass.AbsoluteVolume} +/- {mass.ErrorBound} ({mass.EvaluationMethod}); STEP {reimported.AbsoluteVolume} +/- {reimported.ErrorBound} ({reimported.EvaluationMethod})");
        if (kind == "Fillet") Assert.Single(result.Value.RuntimeBody!.Geometry.Surfaces, s => s.Value.Cylinder is not null);
    }

    [Fact]
    public void SplitOrUnmappedEdges_DoNotBecomeWholeSegmentSelections()
    {
        var map = FirmamentBuildAndExport.CompileSource(Bracket).Value.RuntimeCorrespondence!;
        var edge = map.Descendants.First(d => d.Role == SemanticTopologyRole.TopBoundary && d.Edge is not null);
        var split = map with { Descendants = [.. map.Descendants, edge with { StableId = "split", Edge = new EdgeId(9999) }] };
        Assert.Null(SingleEdgeFinishSelection.Resolve(split, edge.Edge!.Value));
        Assert.Null(SingleEdgeFinishSelection.Resolve(map, new EdgeId(9999)));
    }

    [Theory]
    [InlineData("Fillet")]
    [InlineData("Chamfer")]
    public void BoxFacePair_UsesTheExistingLocalizedSingleEdgeRoute(string kind)
    {
        const string stock = "Box Stock { Size: [20mm, 15mm, 10mm] }";
        var original = FirmamentBuildAndExport.CompileSource($"Model Part {{ Units: mm {stock} }}");
        Assert.True(original.IsSuccess, string.Join(";", original.Diagnostics.Select(d => d.Message)));
        var map = original.Value.RuntimeCorrespondence!;
        var edge = Assert.Single(map.Descendants, d => d.StableId == "Stock.edge(TopRight)");
        var selection = SingleEdgeFinishSelection.Resolve(map, edge.Edge!.Value)!;
        Assert.Equal("+X", selection.Face); Assert.Equal("SharedEdgePlusZ", selection.Target);
        var field = kind == "Fillet" ? "Radius" : "Distance";
        var result = FirmamentBuildAndExport.CompileSource($"Model Part {{ Units: mm {stock} Modify Stock {{ EdgeFinish Picked {{ Face: {selection.Face} Target: {selection.Target} Kind: {kind} {field}: 1mm }} }} }}");
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(result.Value.StepText); Assert.True(imported.IsSuccess);
        Assert.Equal(7, imported.Value.Topology.Faces.Count());
        var originalPoints = original.Value.RuntimeBody!.Topology.Vertices.Select(v => {
            Assert.True(original.Value.RuntimeBody.TryGetVertexPoint(v.Id, out var point)); return point;
        }).ToArray();
        var finishedPoints = imported.Value.Topology.Vertices.Select(v => {
            Assert.True(imported.Value.TryGetVertexPoint(v.Id, out var point)); return point;
        }).ToArray();
        Assert.Equal(originalPoints.Min(p => p.Z), finishedPoints.Min(p => p.Z), 9);
        Assert.Equal(originalPoints.Max(p => p.Z), finishedPoints.Max(p => p.Z), 9);
    }

    [Fact]
    public void OrientedBracketWithPickedFaceHole_AdmitsSeparatedFinishHistory()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament");
        source = source.Insert(source.LastIndexOf("  }", StringComparison.Ordinal),
            "EdgeFinish EdgeFillet { Face: Longitudinal Target: section.Outer.Vertex3_Vertex4.Start Kind: Fillet Radius: 0.5mm }\n");
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var accepted=FirmamentBuildAndExport.CompileSource(FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament"));
        Assert.Null(SingleEdgeFinishSelection.UnavailableReason(accepted.Value.RuntimeCorrespondence!));
        Assert.Contains(accepted.Value.RuntimeCorrespondence!.Descendants, d => d.Role == SemanticTopologyRole.ReflexLongitudinalEdge);
    }
}
