using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FaceRecessDirectionTests
{
    private static string Original {
        get {
            var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament");
            return source[..source.IndexOf("  Modify", StringComparison.Ordinal)] + "}";
        }
    }
    [Theory]
    [InlineData("Vertex2_Vertex3")]
    [InlineData("Vertex3_Vertex4")]
    [InlineData("Vertex5_Vertex0")]
    public void RecessOpensOnPickedFace_AndExtendsInward_OnBothNormalSigns(string side)
    {
        var baseline = FirmamentBuildAndExport.CompileSource(Original).Value;
        var target = baseline.RuntimeCorrespondence!.Descendants.Single(d => d.Face is not null && d.FirmamentSelector?.Contains(side, StringComparison.Ordinal) == true);
        var frame = PlanarFaceThroughHole.Support(baseline.RuntimeBody!, target.Face!.Value)!;
        var u = frame.Boundary.Average(p => p[0]); var v = frame.Boundary.Average(p => p[1]);
        var normal = new Vector3D(frame.Normal[0], frame.Normal[1], frame.Normal[2]);
        var mouth = new Point3D(frame.Origin[0], frame.Origin[1], frame.Origin[2])
            + new Vector3D(frame.X[0], frame.X[1], frame.X[2]) * u + new Vector3D(frame.Y[0], frame.Y[1], frame.Y[2]) * v;
        foreach (var variant in new[] { "Counterbore", "Countersink" })
        {
            var fields = variant == "Counterbore" ? "CounterboreDiameter: 7mm; CounterboreDepth: 1mm" : "CountersinkDiameter: 7mm; CountersinkAngle: 90deg";
            var source = Original[..Original.LastIndexOf('}')] + FormattableString.Invariant($"Modify bracket {{ Hole<{variant}> Recess {{ On: {target.FirmamentSelector}; Center: Point2({u}mm, {v}mm); Diameter: 4mm; {fields}; End: ThroughAll }} }} }}");
            var result = FirmamentBuildAndExport.CompileSource(source);
            Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
            var body = result.Value.RuntimeBody!; var correspondence = result.Value.RuntimeCorrespondence!;
            var entryFace = correspondence.Descendants.Single(d => d.Face is not null && d.FirmamentSelector == target.FirmamentSelector);
            var entry = PlanarFaceThroughHole.Support(body, entryFace.Face!.Value)!;
            Assert.Equal(3.5, Assert.Single(entry.ExcludedDisks).Radius);
            var exits = correspondence.Descendants.Where(d => d.Face is not null && d.FirmamentSelector is not null)
                .Select(d => PlanarFaceThroughHole.Support(body, d.Face!.Value)).Where(f => f is not null && f.ExcludedDisks.Count == 1
                    && new Vector3D(f.Normal[0], f.Normal[1], f.Normal[2]).Dot(normal) < -.99).ToArray();
            Assert.Equal(2d, Assert.Single(exits)!.ExcludedDisks[0].Radius);
            var shaftFace = correspondence.Descendants.Single(d => d.Role == SemanticTopologyRole.HoleWallFace);
            var shaft = body.GetFaceSurface(shaftFace.Face!.Value).Cylinder!.Value;
            Assert.Equal(variant == "Counterbore" ? -1d : -1.5d, (shaft.Origin - mouth).Dot(normal), 8);
            if (variant == "Counterbore") {
                var shoulder = correspondence.Descendants.Single(d => d.Role == SemanticTopologyRole.HoleCounterboreShoulderFace);
                Assert.Equal(-1d, (body.GetFaceSurface(shoulder.Face!.Value).Plane!.Value.Origin - mouth).Dot(normal), 8);
            } else {
                var cone = correspondence.Descendants.Single(d => d.Role == SemanticTopologyRole.HoleCountersinkWallFace);
                Assert.True((body.GetFaceSurface(cone.Face!.Value).Cone!.Value.Apex - mouth).Dot(normal) < 0);
            }
            var imported = Step242Importer.ImportBody(result.Value.StepText); Assert.True(imported.IsSuccess);
            foreach (var checkedBody in new[] { body, imported.Value }) {
                var mass = BrepMassProperties.Evaluate(checkedBody);
                Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
                Assert.InRange(Math.Abs(mass.AbsoluteVolume - (4020 - (variant == "Counterbore" ? 20.25 : 17.625) * Math.PI)), 0, Math.Max(mass.ErrorBound ?? 0, 1e-6));
            }
        }
    }

    [Fact]
    public void FullThicknessCounterbore_ReportsOneActionableError()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-multiple-face-holes.firmament")
            .Replace("CounterboreDepth: 1mm", "CounterboreDepth: 3mm", StringComparison.Ordinal);
        var result = FirmamentBuildAndExport.CompileSource(source); Assert.False(result.IsSuccess);
        var presented = FirmamentBuildAdmissionValidation.Present(Assert.Single(result.Diagnostics), "bracket.firmament", source);
        Assert.Equal("compose-face-hole", presented.Code);
        Assert.Contains("depth is 3 mm", presented.Message); Assert.Contains("face has 3 mm", presented.Message);
        Assert.Contains("2.99 mm", presented.Message); Assert.Contains("Reduce CounterboreDepth", presented.Message);
        Assert.DoesNotContain("authoritative-section-stack", presented.Message);
    }

    [Theory]
    [InlineData("counterbore")]
    [InlineData("countersink")]
    public void PaletteDefaults_FitTheThreeMillimeterUpright(string id)
    {
        var item = FirmamentLanguagePaletteService.Palette().Items.Single(i => i.Id == id);
        var feature = item.Source.Replace("On: +Z", "On: face(bracket.Side(\"profile:section.Outer.Vertex3_Vertex4\"))", StringComparison.Ordinal)
            .Replace("Center: Point2(0mm, 0mm)", "Center: Point2(10mm, 21mm)", StringComparison.Ordinal);
        var source = Original[..Original.LastIndexOf('}')] + $"Modify bracket {{ {feature} }} }}";
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
    }
}
