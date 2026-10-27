using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class EngineeringRimFinishTests
{
    [Theory]
    [InlineData("-Z")] [InlineData("+X")] [InlineData("-X")]
    [InlineData("+Y")] [InlineData("-Y")]
    public void DisplayFilletRollsRespectTheirEllipticalMiterTrims(string face)
    {
        var built = Build($$"""
            Model Block {
                units: mm
                Box Stock { size: [60mm, 40mm, 12mm] }
                Modify Stock { EdgeFinish<Fillet> Rim { face: {{face}}; target: Boundary; radius: 1mm } }
            }
            """);
        foreach (var body in new[] { built.RuntimeBody!, Step242Importer.ImportBody(built.StepText).Value })
        {
            var options = DisplayTessellationOptions.Default with { ChordTolerance = .01 };
            var display = BrepDisplayTessellator.Tessellate(body, options);
            Assert.True(display.IsSuccess, string.Join(';', display.Diagnostics.Select(d => d.Message)));
            Assert.Equal(body.Topology.Faces.Count(), display.Value.FacePatches.Count);
            foreach (var binding in body.Bindings.EdgeBindings.Where(b => body.GetEdgeCurve(b.EdgeId).Ellipse3.HasValue))
            {
                var ellipse = body.GetEdgeCurve(binding.EdgeId).Ellipse3!.Value;
                var trim = binding.TrimInterval!.Value;
                var samples = Assert.Single(display.Value.EdgePolylines, e => e.EdgeId == binding.EdgeId).Points;
                for (var i = 0; i < samples.Count - 1; i++)
                {
                    var exact = ellipse.Evaluate(trim.Start + (trim.End - trim.Start) * (i + .5) / (samples.Count - 1));
                    var chord = samples[i] + (samples[i + 1] - samples[i]) * .5;
                    Assert.InRange((exact - chord).Length, 0, options.ChordTolerance);
                }
            }
            var cap = face == "+X" ? 30 : face == "-X" ? -30 : face == "+Y" ? 20 : face == "-Y" ? -20 : 0;
            var points = display.Value.FacePatches.SelectMany(p => p.Positions)
                .Where(p => Math.Abs((face[1] == 'X' ? p.X : face[1] == 'Y' ? p.Y : p.Z) - cap) < 1e-7).ToArray();
            Assert.NotEmpty(points);
            // A rectangular UV bounding patch incorrectly extends each roll to
            // the sharp stock corner. At the finished cap every axis is inset.
            Assert.All(points, p => {
                if (face[1] != 'X') Assert.InRange(p.X, -29.000001, 29.000001);
                if (face[1] != 'Y') Assert.InRange(p.Y, -19.000001, 19.000001);
                if (face[1] != 'Z') Assert.InRange(p.Z, .999999, 11.000001);
            });
        }
    }

    [Fact]
    public void RealHeliosCodexTaskRetainsItsFourHolesAndRim()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/EdgeFinishRobustness/helios-codex-rounded-plate.firmament");
        var built = Build(source);
        Assert.Equal(4, built.Features!.Count);
        Assert.Equal(new[] { (-14d, -9d), (-14d, 9d), (14d, -9d), (14d, 9d) },
            built.Features.Select(f => (f.LocalU, f.LocalV)).Order().ToArray());
        Assert.All(built.Features, f => Assert.Equal(4d, f.Diameter));
        Assert.Contains(built.EngineeringFeatures!, f => f.Kind == "EdgeFinish");
        Assert.Equal(34, built.RuntimeBody!.Topology.Faces.Count());
        Assert.Equal(4, built.RuntimeBody.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Torus));
        Assert.DoesNotContain(built.RuntimeBody.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots);
        Check(Step242Importer.ImportBody(built.StepText).Value);
    }

    [Theory]
    [InlineData("+X", false)] [InlineData("-X", false)]
    [InlineData("+Y", false)] [InlineData("-Y", false)]
    [InlineData("+Z", false)] [InlineData("-Z", false)]
    [InlineData("+X", true)] [InlineData("-X", true)]
    [InlineData("+Y", true)] [InlineData("-Y", true)]
    [InlineData("+Z", true)] [InlineData("-Z", true)]
    public void BoxLoopFinishUsesTheRequestedFace(string face, bool fillet)
    {
        var source = $$"""
            Model Block {
                units: mm
                Box Stock { size: [60mm, 40mm, 12mm] }
                Modify Stock { EdgeFinish<{{(fillet ? "Fillet" : "Chamfer")}}> Rim {
                    face: {{face}}; target: Boundary; {{(fillet ? "radius" : "distance")}}: 1mm
                } }
            }
            """;
        var built = Build(source);
        var body = Step242Importer.ImportBody(built.StepText).Value;
        Check(body);
        var points = body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var p)); return p; }).ToArray();
        Assert.Equal(-30, points.Min(p => p.X), 8); Assert.Equal(30, points.Max(p => p.X), 8);
        Assert.Equal(-20, points.Min(p => p.Y), 8); Assert.Equal(20, points.Max(p => p.Y), 8);
        Assert.Equal(0, points.Min(p => p.Z), 8); Assert.Equal(12, points.Max(p => p.Z), 8);
        var axis = face[1]; var sign = face[0] == '+' ? 1 : -1;
        double Along(Aetheris.Kernel.Core.Math.Point3D p) => axis == 'X' ? p.X : axis == 'Y' ? p.Y : p.Z;
        var plane = sign > 0 ? points.Max(Along) : points.Min(Along);
        var cap = Assert.Single(body.Topology.Faces.Where(f => body.GetFaceSurface(f.Id).Plane is { } p
            && Math.Abs((axis == 'X' ? p.Normal.ToVector().X : axis == 'Y' ? p.Normal.ToVector().Y : p.Normal.ToVector().Z) - sign) < 1e-8
            && Math.Abs(Along(p.Origin) - plane) < 1e-8));
        var capPoints = cap.LoopIds.SelectMany(l => body.Topology.GetLoop(l).CoedgeIds)
            .Select(c => body.Topology.GetEdge(body.Topology.GetCoedge(c).EdgeId).StartVertexId)
            .Select(v => { Assert.True(body.TryGetVertexPoint(v, out var p)); return p; }).ToArray();
        // All four corners of the requested cap are inset; its opposite face
        // retains the original rectangle. Bounds alone cannot prove orientation.
        if (axis != 'X') Assert.Equal(58, capPoints.Max(p => p.X) - capPoints.Min(p => p.X), 8);
        if (axis != 'Y') Assert.Equal(38, capPoints.Max(p => p.Y) - capPoints.Min(p => p.Y), 8);
        if (axis != 'Z') Assert.Equal(10, capPoints.Max(p => p.Z) - capPoints.Min(p => p.Z), 8);
        Assert.Contains(built.EngineeringFeatures!, f => f.Kind == "EdgeFinish" && f.Support == face);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void RoundedRimRetainsAllThroughCavitiesAndExplicitPmi(bool bottom, bool chamfer)
    {
        var source = Source;
        if (bottom) source = source.Replace("target: Boundary; on: +Z", "target: Outline.Outer; on: Bottom");
        if (chamfer) source = source.Replace("EdgeFinish<Fillet>", "EdgeFinish<Chamfer>").Replace("radius: 1mm", "distance: 1mm");
        var built = Build(source);
        var reference = Build(source.Replace(source.Split('\n').Single(l => l.Contains("EdgeFinish<")), ""));
        var body = built.RuntimeBody!;
        var imported = Step242Importer.ImportBody(built.StepText);
        Assert.True(imported.IsSuccess, string.Join(';', imported.Diagnostics.Select(d => d.Message)));
        Check(body); Check(imported.Value);
        Assert.Equal(3, built.Features!.Count);
        var correspondence = built.RuntimeCorrespondence!;
        Assert.Equal(3, correspondence.Descendants.Count(d => d.Role is SemanticTopologyRole.HoleEntryLoop or SemanticTopologyRole.HoleCounterboreMouthLoop));
        Assert.Equal(3, correspondence.Descendants.Count(d => d.Role == SemanticTopologyRole.HoleExitLoop));
        foreach (var role in new[] { SemanticTopologyRole.HoleWallFace, SemanticTopologyRole.HoleCountersinkWallFace,
            SemanticTopologyRole.HoleCounterboreWallFace, SemanticTopologyRole.HoleCounterboreShaftWallFace,
            SemanticTopologyRole.HoleEntryLoop, SemanticTopologyRole.HoleCounterboreMouthLoop,
            SemanticTopologyRole.HoleCounterboreShoulderLoop, SemanticTopologyRole.HoleExitLoop })
            Assert.Equal(reference.RuntimeCorrespondence!.Descendants.Where(d => d.Role == role).Select(d => d.StableId).Order(),
                correspondence.Descendants.Where(d => d.Role == role).Select(d => d.StableId).Order());
        foreach (var wall in reference.RuntimeCorrespondence!.Descendants.Where(d => d.Face.HasValue &&
            d.Role is SemanticTopologyRole.HoleWallFace or SemanticTopologyRole.HoleCountersinkWallFace
                or SemanticTopologyRole.HoleCounterboreWallFace or SemanticTopologyRole.HoleCounterboreShaftWallFace))
        {
            var retained = Assert.Single(correspondence.Descendants, d => d.StableId == wall.StableId);
            // Compare exact analytic supports, including cylinder axes, cone
            // origins, radii and orientation, rather than just face counts.
            Assert.Equal(reference.RuntimeBody!.GetFaceSurface(wall.Face!.Value), body.GetFaceSurface(retained.Face!.Value));
        }
        var sinkCones = imported.Value.Geometry.Surfaces.Where(s => s.Value.Cone is { PlacementOrigin.X: 15 }).ToArray();
        Assert.Equal(4, sinkCones.Length); // Four qualified quarter patches.
        Assert.All(sinkCones, c => Assert.Equal(Math.PI / 4, c.Value.Cone!.Value.SemiAngleRadians, 10));
        var settings = new BrepMassPropertiesOptions(.01, Math.PI / 90, .01, MinimumSegments: 64, MaximumSegments: 256);
        var mass = BrepMassProperties.Evaluate(imported.Value, settings);
        var referenceMass = BrepMassProperties.Evaluate(reference.RuntimeBody!, settings);
        Assert.True(mass.IsEnclosed); Assert.True(referenceMass.IsEnclosed);
        Assert.True(mass.IsOrientationConsistent);
        var perimeter = 2 * (60 + 40) - 2 * (4 - Math.PI) * 6;
        var removed = chamfer ? perimeter / 2 - Math.PI / 3
            : perimeter * (1 - Math.PI / 4) - Math.PI * (5d / 3 - Math.PI / 2);
        Assert.InRange(Math.Abs(referenceMass.AbsoluteVolume - mass.AbsoluteVolume - removed), 0, .2);
        var capZ = bottom ? 0d : 8d;
        var cap = Assert.Single(body.Topology.Faces, f => body.GetFaceSurface(f.Id).Plane is { } p
            && Math.Abs(p.Origin.Z - capZ) < 1e-8 && Math.Abs(p.Normal.ToVector().Z) > .99);
        var capPoints = cap.LoopIds.SelectMany(l => body.Topology.GetLoop(l).CoedgeIds)
            .Select(c => body.Topology.GetEdge(body.Topology.GetCoedge(c).EdgeId).StartVertexId)
            .Select(v => { Assert.True(body.TryGetVertexPoint(v, out var p)); return p; }).ToArray();
        Assert.Equal(58, capPoints.Max(p => p.X) - capPoints.Min(p => p.X), 8);
        Assert.Equal(38, capPoints.Max(p => p.Y) - capPoints.Min(p => p.Y), 8);
        var pmi = Step242SemanticPmiInspector.Inspect(built.StepText);
        Assert.True(pmi.Success); Assert.Equal(1, pmi.DatumCount); Assert.Equal(3, pmi.DimensionCount);
        Assert.Equal(built.StepText, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
    }

    [Theory]
    [InlineData("center: Point2(-15mm, 0mm)", "center: Point2(-15mm, 17mm)", "CavityInterference")]
    [InlineData("radius: 1mm", "radius: 7mm", "")]
    [InlineData("target: Boundary; on: +Z", "target: Boundary; on: +X", "FaceNotCap")]
    [InlineData("radius: 1mm", "raduis: 1mm", "")]
    public void UnsupportedIntentCannotSilentlyBuildUnfinishedStock(string from, string to, string diagnostic)
    {
        var source = Source.Replace(from, to);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains(diagnostic, StringComparison.Ordinal));
        if (diagnostic == "CavityInterference")
        {
            var shown = built.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, "plate.firmament", source));
            // The presenter resolves the named colliding Hole, which is more
            // precise than the enclosing EdgeFinish fallback span.
            Assert.All(shown, d => Assert.Equal(9, d.Line));
        }
        Assert.Equal("invalid", FirmamentBuildAdmissionValidation.Admit(
            FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source).Status);
    }

    private static string Source => FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/EdgeFinishRobustness/rounded-plate-through-cavities.firmament");
    private static FirmamentStepExportResult Build(string source)
    {
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(';', built.Diagnostics.Select(d => d.Message)));
        Assert.Equal("valid", FirmamentBuildAdmissionValidation.Admit(
            FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source).Status);
        return built.Value;
    }
    private static void Check(BrepBody body)
    {
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        foreach (var edge in body.Topology.Edges)
        {
            var uses = body.Topology.Coedges.Where(c => c.EdgeId == edge.Id).ToArray();
            Assert.Equal(2, uses.Length);
        }
        // STEP normalizes face orientation independently from wire direction;
        // raw coedge flags alone are not a shell-orientation invariant.
        Assert.True(BrepMassProperties.Evaluate(body).IsEnclosed);
    }
}
