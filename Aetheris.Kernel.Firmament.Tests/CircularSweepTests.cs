using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CircularSweepTests
{
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
        var report = Assert.IsType<FirmamentSweepReport>(result.Value.Sweep);
        Assert.Equal(7, report.SegmentCount);
        Assert.Equal(0.8, report.Diameter, 9);
        Assert.Equal("Standard.Materials.StainlessSteel.304_Annealed", report.Material);
        Assert.Equal(4, report.Cylinders);
        Assert.Equal(3, report.Tori);
        Assert.InRange(report.Bounds[3] - report.Bounds[0], 9.79, 9.81);
        Assert.InRange(report.Bounds[4] - report.Bounds[1], 33.79, 33.81);
        Assert.True(report.CenterlineLength > 100);
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
