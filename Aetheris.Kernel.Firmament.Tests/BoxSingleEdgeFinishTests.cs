using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BoxSingleEdgeFinishTests
{
    private const string Holes = """
        Hole<Shaft> CenterMount { on: +Z; center: [0mm, 0mm]; diameter: 8mm; end: ThroughAll }
        Hole<Countersink> Flush { on: +Z; center: [-10mm, -14mm]; diameter: 6mm; end: ThroughAll; countersinkDiameter: 8mm; countersinkAngle: 90deg }
        """;

    private static string Model(string features) => $$"""
        Model Plate {
          Units: mm
          Box stock { size: [50mm, 40mm, 8mm] }
          {{(features.Length == 0 ? "" : "Modify stock {\n" + features + "\n}")}}
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllTwelveIntactEdgesAreSelectable(bool holes)
    {
        var built = Build(Model(holes ? Holes : ""));
        var selections = SingleEdgeFinishSelection.Index(built.RuntimeCorrespondence!);
        Assert.Equal(12, selections.Count);
        Assert.Equal(12, selections.Values.Select(edge => (edge.Face, edge.Target)).Distinct().Count());
        Assert.All(selections, selection =>
        {
            Assert.True(selection.Value.FilletSupported);
            Assert.True(built.RuntimeBody!.TryGetEdgeCurveGeometry(selection.Key, out var curve));
            Assert.Equal(CurveGeometryKind.Line3, curve!.Kind);
        });
    }

    public static IEnumerable<object[]> StockEdgeFinishes() =>
        from axes in new[] { "XY", "XZ", "YZ" }
        from first in new[] { "+", "-" }
        from second in new[] { "Plus", "Minus" }
        from kind in new[] { "Fillet", "Chamfer" }
        select new object[] { first + axes[0], "SharedEdge" + second + axes[1], kind };

    [Theory]
    [MemberData(nameof(StockEdgeFinishes))]
    public void SingleFinishRetainsThroughHolesInAllDirections(string face, string target, string kind)
    {
        var result = Build(Model(Holes + "\n" + Finish(face, target, kind, "1")));
        var imported = Step242Importer.ImportBody(result.StepText);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        Assert.True(BrepExportPreflight.Validate(imported.Value).IsValid);
        Assert.True(BrepMassProperties.Evaluate(imported.Value).IsEnclosed);
        Assert.Equal(10, imported.Value.Topology.Faces.Count());
        Assert.Single(imported.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Cone);
        Assert.Equal(kind == "Fillet" ? 3 : 2, imported.Value.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
        var replacement = Assert.Single(result.RuntimeCorrespondence!.Descendants, d => d.Role == SemanticTopologyRole.EdgeFinishReplacementFace);
        var surface = result.RuntimeBody!.GetFaceSurface(replacement.Face!.Value);
        var a = Normal(face[1], face[0] == '+' ? 1 : -1);
        var b = Normal(target[^1], target.Contains("Plus", StringComparison.Ordinal) ? 1 : -1);
        if (kind == "Fillet")
        {
            var cylinder = surface.Cylinder!.Value;
            Assert.Equal(1, cylinder.Radius, 9);
            Assert.Equal(0, cylinder.Axis.ToVector().Dot(a), 9);
            Assert.Equal(0, cylinder.Axis.ToVector().Dot(b), 9);
        }
        else
        {
            var normal = surface.Plane!.Value.Normal.ToVector();
            Assert.Equal(Math.Sqrt(.5), normal.Dot(a), 9);
            Assert.Equal(Math.Sqrt(.5), normal.Dot(b), 9);
        }
        Assert.Empty(SingleEdgeFinishSelection.Index(result.RuntimeCorrespondence));
    }

    [Theory]
    [InlineData("Fillet", "3")]
    [InlineData("Fillet", "6")]
    [InlineData("Chamfer", "3")]
    public void FinishThatReachesRecessIsRefused(string kind, string size)
    {
        var product = FirmamentBuildAndExport.CompileSource(Model(Holes + "\n" + Finish("-Y", "SharedEdgePlusZ", kind, size)));
        Assert.False(product.IsSuccess);
        Assert.Contains(product.Diagnostics, d => d.Message.Contains("compose-face-hole:Flush:", StringComparison.Ordinal));
    }

    private static string Finish(string face, string target, string kind, string size) =>
        $"EdgeFinish Edge {{ Face: {face}; Target: {target}; Kind: {kind}; {(kind == "Fillet" ? "Radius" : "Distance")}: {size}mm }}";

    private static FirmamentStepExportResult Build(string source)
    {
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return result.Value;
    }

    private static Vector3D Normal(char axis, int sign) => axis switch
    {
        'X' => new(sign, 0, 0),
        'Y' => new(0, sign, 0),
        _ => new(0, 0, sign)
    };
}
