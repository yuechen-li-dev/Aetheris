using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RibNetworkTests
{
    internal static string Program(double draft=0,double secondHeight=10,string finish="",string hole="")=>$$"""
        Model Junction {
          units: mm
          Box Plate { size: [60mm,50mm,5mm] }
          Path Across { start: Point2(-20mm,0mm); heading: 0deg; Line X { length: 40mm } }
          Path Along { start: Point2(0mm,-15mm); heading: 90deg; Line Y { length: 30mm } }
          Modify Plate {
            Rib<Rectangular> A { on: +Z; path: Across; thickness: 4mm; height: 10mm; draft: {{draft.ToString(System.Globalization.CultureInfo.InvariantCulture)}}deg }
            Rib<Rectangular> B { on: +Z; path: Along; thickness: 4mm; height: {{secondHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}}mm; draft: {{draft.ToString(System.Globalization.CultureInfo.InvariantCulture)}}deg }
            {{hole}}
            {{finish}}
          }
        }
        """;

    [Theory]
    [InlineData(0,10,"PrismaticSectionArrangement")]
    [InlineData(0,7,"PrismaticSectionArrangement")]
    [InlineData(2,10,"AffineDraftArrangement")]
    public void CrossJunctionIsOneExactClosedBody(double draft,double height,string strategy)
    {
        var source=Program(draft,height);var built=FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess,string.Join("; ",built.Diagnostics.Select(d=>d.Message)));
        Assert.Equal(strategy,built.Value.RibPlanning!.Strategy);
        Assert.Equal(2,built.Value.MaterializedFeatures.Count);
        Assert.All(built.Value.RuntimeBody!.Topology.Edges,e=>Assert.Equal(2,built.Value.RuntimeBody.Topology.Coedges.Count(c=>c.EdgeId==e.Id)));
        var mass=BrepMassProperties.Evaluate(built.Value.RuntimeBody);
        Assert.True(mass.IsEnclosed&&mass.IsOrientationConsistent,string.Join("; ",mass.Diagnostics));
        var slope=2*Math.Tan(draft*Math.PI/180);
        var expected=15000+70*(4*10-slope*100/2)-(16*10-4*slope*100+slope*slope*1000/3);
        if(height!=10)expected=15000+40*4*10+30*4*height-16*height;
        Assert.InRange(mass.SignedVolume,expected-2,expected+2);
        var read=Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(read.IsSuccess,string.Join("; ",read.Diagnostics.Select(d=>d.Message)));
        Assert.True(BrepMassProperties.Evaluate(read.Value).IsEnclosed);
    }
    [Fact]
    public void UsefulPartCombinesDraftedJunctionsSteppedHolesAndFillet()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Aetheris.slnx")))root=root.Parent;
        var source=File.ReadAllText(Path.Combine(root!.FullName,"fixtures/Canonical/Features/PathFeatures/reinforced-mounting-plate.firmament"));
        var built=FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess,string.Join("; ",built.Diagnostics.Select(d=>d.Message)));
        Assert.Equal(5,built.Value.MaterializedFeatures.Count);
        Assert.Equal("AffineDraftArrangement",built.Value.RibPlanning!.Strategy);
        Assert.True(BrepExportPreflight.Validate(built.Value.RuntimeBody!).IsValid);
    }

    [Theory]
    [InlineData("Point2(0mm,0mm)", "15mm")]
    [InlineData("Point2(20mm,0mm)", "15mm")]
    public void TandLJunctionsRemainClosed(string start, string length)
    {
        var source = Program().Replace("Point2(0mm,-15mm)", start).Replace("length: 30mm", "length: " + length);
        AssertClosed(source);
    }

    [Theory]
    [InlineData("Fillet", "radius: 1mm")]
    [InlineData("Chamfer", "distance: 1mm")]
    public void JunctionsCombineWithEachAdmittedThroughHoleAndPerimeterFinish(string kind, string dimension)
    {
        foreach (var (type, fields) in new[] { ("Shaft", ""), ("Counterbore", "counterboreDiameter: 8mm; counterboreDepth: 2mm"), ("Countersink", "countersinkDiameter: 8mm; countersinkAngle: 90deg") })
            AssertClosed(Program(finish: $"EdgeFinish Rim {{ face: +Z; target: Boundary; kind: {kind}; {dimension} }}",
                hole: $"Hole<{type}> Mount {{ on: +Z; center: Point2(-24mm,-18mm); diameter: 4mm; end: ThroughAll; {fields} }}"));
    }

    [Fact]
    public void ClosedRingCrossingStraightRibRetainsItsSupportIslands()
    {
        var source = Program().Replace("Path Along { start: Point2(0mm,-15mm); heading: 90deg; Line Y { length: 30mm } }",
            "Concept Struct Layout { Circle2 Ring { center: Point2(0mm,0mm); radius: 12mm } }").Replace("path: Along", "path: Layout.Ring");
        AssertClosed(source);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void IsolatedAndJoinedRibsAdmitHolesWithoutRequiringAFinish(bool joined, bool withHoles)
    {
        var hole = withHoles ? """
            Hole<Counterbore> MountA { on: +Z; center: Point2(-24mm,-18mm); diameter: 4mm; end: ThroughAll; counterboreDiameter: 7mm; counterboreDepth: 2mm }
            Hole<Countersink> MountB { on: +Z; center: Point2(24mm,18mm); diameter: 4mm; end: ThroughAll; countersinkDiameter: 7mm; countersinkAngle: 90deg }
            """ : "";
        var source = Program(hole: hole, finish: withHoles ? "" : "EdgeFinish Rim { face: +Z; target: Boundary; kind: Fillet; radius: 1mm }");
        if (!joined) source = string.Join('\n', source.Split('\n').Where(l => !l.Contains("Rib<Rectangular> B")));
        var result = AssertClosed(source);
        Assert.Equal((joined ? 2 : 1) + (withHoles ? 2 : 1), result.MaterializedFeatures.Count);
        Assert.Equal(withHoles ? 0 : 1, result.MaterializedFeatures.Count(f => f.Kind == "EdgeFinish"));
    }

    [Fact]
    public void NoninteractingGrooveCanShareTheReinforcedStock()
    {
        var source = Program().Replace("Modify Plate {", "Path Drain { start: Point2(-15mm,-20mm); heading: 0deg; Line D { length: 30mm } } Modify Plate {")
            .Replace("Rib<Rectangular> A", "Groove<Semicircular> Channel { on: +Z; path: Drain; radius: 1mm } Rib<Rectangular> A");
        AssertClosed(source);
    }

    [Fact]
    public void HoleThroughAJunctionIsRejectedByBothAdmissionPaths()
    {
        var source = Program(hole: "Hole<Shaft> Mount { on: +Z; center: Point2(0mm,0mm); diameter: 4mm; end: ThroughAll }");
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("intersects Hole 'Mount'"));
        Assert.All(built.Diagnostics.Where(d => d.Severity == Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error), d => Assert.NotNull(d.SourceSpan));
        var validation = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(
            FirmamentV2Parser.Parse(source), "rib-hole.firmament", sourceText: source), source);
        Assert.Equal("invalid", validation.Status);
    }

    [Theory]
    [InlineData(2, 7, "no admitted rib plan")]
    [InlineData(15, 10, "draft collapses")]
    public void UnsupportedOrCollapsedDraftHasAnHonestFailure(double draft, double height, string reason)
    {
        var built = FirmamentBuildAndExport.CompileSource(Program(draft, height));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains(reason, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsolatedRibKeepsTheExistingExactGeometry()
    {
        var source = Program().Split('\n').Where(l => !l.Contains("Rib<Rectangular> B"));
        var text = string.Join('\n', source);
        var document = FirmamentV2Parser.Parse(text).Document!;
        var old = Aetheris.Kernel.Core.Brep.Features.BrepPlanarPathFeature.BuildBox(60, 50, 5, [document.PathFeatures.Single().Feature]);
        Assert.True(old.IsSuccess);
        var expected = Aetheris.Kernel.Firmament.Execution.FirmamentPrimitiveExecutionTranslation.TranslateBody(old.Value.Body, new(0, 0, 2.5));
        var actual = AssertClosed(text);
        Assert.Equal("ExistingAnalyticPatch", actual.RibPlanning!.Strategy);
        Assert.Equal(expected.Geometry.Curves.OrderBy(x => x.Key.Value), actual.RuntimeBody!.Geometry.Curves.OrderBy(x => x.Key.Value));
        Assert.Equal(expected.Geometry.Surfaces.OrderBy(x => x.Key.Value), actual.RuntimeBody.Geometry.Surfaces.OrderBy(x => x.Key.Value));
        foreach (var vertex in expected.Topology.Vertices)
        {
            Assert.True(expected.TryGetVertexPoint(vertex.Id, out var a));
            Assert.True(actual.RuntimeBody.TryGetVertexPoint(vertex.Id, out var b));
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void JunctionReorderingPreservesExactStepAndStableNames()
    {
        var source = Program(2);
        var lines = source.Split('\n');
        var a = Array.FindIndex(lines, l => l.Contains("Rib<Rectangular> A"));
        var b = Array.FindIndex(lines, l => l.Contains("Rib<Rectangular> B"));
        (lines[a], lines[b]) = (lines[b], lines[a]);
        var first = AssertClosed(source); var second = AssertClosed(string.Join('\n', lines));
        Assert.Equal(first.StepText, second.StepText);
        Assert.Equal(first.RuntimeCorrespondence!.Descendants.Select(d => d.StableId).Order(), second.RuntimeCorrespondence!.Descendants.Select(d => d.StableId).Order());
    }

    private static FirmamentStepExportResult AssertClosed(string source)
    {
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var body = built.Value.RuntimeBody!;
        Assert.All(body.Topology.Edges, e => Assert.Equal(2, body.Topology.Coedges.Count(c => c.EdgeId == e.Id)));
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent, string.Join("; ", mass.Diagnostics));
        var read = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(read.IsSuccess, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        Assert.True(BrepMassProperties.Evaluate(read.Value).IsEnclosed);
        return built.Value;
    }
}
