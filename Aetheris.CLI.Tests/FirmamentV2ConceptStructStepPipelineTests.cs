using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2ConceptStructStepPipelineTests
{
    [Fact]
    public void ConceptStruct_DrivesAirChamferAndExportsReimportableStepWithProvenance()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-concept-expansion-m1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, "concept-bracket.firmament");
        var stepPath = Path.Combine(dir, "concept-bracket.step");
        File.WriteAllText(sourcePath, Source);
        var stdout = new StringWriter(); var stderr = new StringWriter();

        var exit = CliRunner.Run(["build", sourcePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.Equal(0, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        var conceptIr = json.RootElement.GetProperty("conceptIr");
        Assert.Equal("ErasedBeforeFeatureAir", conceptIr.GetProperty("erasureStatus").GetString());
        Assert.False(conceptIr.GetProperty("structs")[0].GetProperty("materialized").GetBoolean());
        var air = json.RootElement.GetProperty("air");
        Assert.Equal("BracketConcept.Bounds", air.GetProperty("feature").GetProperty("provenance").GetProperty("Bounds").GetString());
        Assert.Equal("BracketConcept.TopPlane", air.GetProperty("feature").GetProperty("provenance").GetProperty("Selection").GetString());
        Assert.Equal("AirPrismaticTopFaceBoundaryChamfer", air.GetProperty("materialization").GetProperty("route").GetString());
        Assert.True(air.GetProperty("step").GetProperty("reimportSucceeded").GetBoolean());
        var import = Step242Importer.ImportBody(File.ReadAllText(stepPath, Encoding.UTF8));
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        Assert.Equal(1, json.RootElement.GetProperty("conceptIr").GetProperty("structs").GetArrayLength());
        Assert.Equal(12, import.Value.Topology.Vertices.Count());
        Assert.Equal(20, import.Value.Topology.Edges.Count());
        Assert.Equal(10, import.Value.Topology.Faces.Count());
    }

    private const string Source = """
        Concept MountingFrame {
            Bounds: Box3
            TopPlane: Plane
            CenterAxis: Axis
            MountPoints: Point3[]
        }
        Concept Struct BracketConcept: MountingFrame {
            Bounds: Box3 { Size: [80mm, 50mm, 25mm] }
            TopPlane: Bounds.Face(+Z)
            CenterAxis: Bounds.Center.Axis(+Z)
            MountPoints: Grid {
                Within: Bounds.Face(+Z).Inset(10mm)
                Columns: 2
                Rows: 1
            }
        }
        Struct Bracket: MountingFrame {
            Box Base { Bounds: BracketConcept.Bounds }
            Modify Base {
                EdgeFinish TopBreak {
                    Face: BracketConcept.TopPlane
                    Target: Boundary
                    Kind: Chamfer
                    Distance: 1.5mm
                }
            }
            Expose {
                Bounds: BracketConcept.Bounds
                TopPlane: Base.Top
                CenterAxis: BracketConcept.CenterAxis
                MountPoints: BracketConcept.MountPoints
            }
        }
        """;
}
