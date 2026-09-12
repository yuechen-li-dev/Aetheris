using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CircularSweepTests
{
    [Fact]
    public void PaperclipDisplayMesh_PreservesBoundedBendsAndClosedTopology()
    {
        var compiled = FirmamentBuildAndExport.CompileSource(PaperclipTemplateLibrary.Source);
        Assert.True(compiled.IsSuccess);
        var body = Step242Importer.ImportBody(compiled.Value.StepText).Value;
        var options = Aetheris.Kernel.Core.Brep.Tessellation.DisplayTessellationOptions.Default;
        Assert.True(Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshIrTessellator.TryBuild(body,
            Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshPolicy.FromDisplayOptions(options), out var mesh));
        var bends = mesh.Patches.Where(patch => patch.Support.Kind == Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshSupportKind.Torus).ToArray();
        Assert.Equal(3, bends.Length);
        Assert.All(bends, patch => { Assert.False(patch.HasPeriodicUSeam); Assert.True(patch.HasPeriodicVSeam); });
        Assert.True(Aetheris.Kernel.Core.Brep.Tessellation.SurfaceMeshIrTessellator.TryLowerToTriangleMesh(mesh, out _, out var topology));
        Assert.True(topology.IsWatertight);
        var byId = mesh.Vertices.ToDictionary(vertex => vertex.Id);
        // The centerline bend joins all occur at y >= 14 mm, except the lower
        // return at y = 0. No bend may complete its un-authored other half.
        foreach (var patch in bends)
        {
            var centerY = patch.Support.Torus!.Value.Center.Y;
            foreach (var id in patch.Cells.SelectMany(cell => cell.VertexIds).Distinct())
            {
                var y = byId[id].Position.Y;
                if (centerY > 1) Assert.True(y >= centerY - 0.500001);
                else Assert.True(y <= centerY + 0.500001);
            }
        }
    }

    [Fact]
    public void ConceptPathCircularSweep_ProducesAnalyticEnclosedStepRoundTrip()
    {
        var result = FirmamentBuildAndExport.CompileSource(SimpleSweep);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message)));
        var report = Assert.IsType<FirmamentSweepReport>(result.Value.Sweep);
        Assert.Equal(2, report.Cylinders);
        Assert.Equal(1, report.Tori);
        Assert.Equal(2, report.Planes);
        Assert.True(report.EnclosedManifold);
        Assert.True(report.StepReimportSucceeded);
        Assert.True(report.StepReimportedManifold);
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Contains(imported.Value.Topology.Faces, face => imported.Value.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Torus);
    }

    [Fact]
    public void StandardPaperclipTemplate_SpecializesIntoRecognizableManufacturableSweep()
    {
        var result = FirmamentBuildAndExport.CompileSource(PaperclipTemplateLibrary.Source);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message)));
        var report = Assert.IsType<FirmamentWireFormReport>(result.Value.WireForm);
        Assert.Equal(7, report.OperationCount);
        Assert.Equal(1, report.DiameterMm, 9);
        Assert.Equal("Standard.Materials.StainlessSteel.304_Annealed", report.Material);
        Assert.Equal(4, report.Cylinders);
        Assert.Equal(3, report.Tori);
        Assert.Equal(11, report.Bounds[3] - report.Bounds[0], 9);
        Assert.Equal(25, report.Bounds[4] - report.Bounds[1], 9);
        Assert.Equal(1, report.Bounds[5] - report.Bounds[2], 9);
        Assert.Equal(58 + 12 * Math.PI, report.TotalWireLengthMm, 9);
        Assert.Equal(95.6991118431, report.TotalWireLengthMm, 9);
        Assert.Equal(0, report.RationalProductSurfaces);
        Assert.Equal(0, report.FacetedFallback);
        Assert.True(report.EnclosedManifold);
        Assert.True(report.StepReimportSucceeded);
        Assert.True(report.StepReimportedManifold);
        Assert.True(report.MassKilograms > 0);
    }

    [Theory]
    [InlineData("Diameter: 0mm", "firmament-sweep-section-invalid")]
    [InlineData("Radius: 0.4mm; Turn: 90deg", "firmament-sweep-bend-radius-too-small")]
    public void InvalidSweep_FailsWithEngineeringDiagnostic(string replacement, string diagnostic)
    {
        var source = replacement.StartsWith("Diameter", StringComparison.Ordinal)
            ? SimpleSweep.Replace("Diameter: 1mm", replacement, StringComparison.Ordinal)
            : SimpleSweep.Replace("Radius: 5mm; Turn: 90deg", replacement, StringComparison.Ordinal);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void AirValidation_IdentifiesDisconnectedSegmentPair()
    {
        var parsed = CircularSweepAuthoring.Parse(SimpleSweep);
        Assert.True(parsed.IsSuccess);
        var first = parsed.Value.Path.Segments[0];
        var broken = parsed.Value.Path.Segments[1] with { Geometry = new LineArcLineSegment2D((100, 100), (110, 100)) };
        var feature = parsed.Value with { Path = parsed.Value.Path with { Segments = [first, broken] } };
        var result = CircularSweepBRepMaterializer.Build(feature);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Message.Contains("firmament-sweep-path-disconnected", StringComparison.Ordinal));
    }

    private const string SimpleSweep = """
        Model BentWire {
            Units: mm
            Concept Path WirePath {
                Start: Point2(0mm, 0mm)
                Heading: 0deg
                Line Lead { Length: 20mm }
                Arc Bend { Radius: 5mm; Turn: 90deg }
                Line Tail { Length: 15mm }
            }
            Sweep Wire {
                Path: WirePath
                Diameter: 1mm
                Material: Standard.Materials.StainlessSteel.304_Annealed
            }
        }
        """;
}
