using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPerforationTests
{
    private const string GridSource = """
        Model PerforatedPanel {
          Units: mm
          Box Body { Size: [30mm, 30mm, 2mm] }
          Modify Body {
            Perforation Vent {
              On: +Z
              Diameter: 6mm
              Layout: Grid
              Pitch: 12mm
              Margin: 3mm
            }
          }
        }
        """;

    [Fact]
    public void Grid_BuildsManifoldStepAndPerInstanceWallCorrespondence()
    {
        var built = FirmamentBuildAndExport.CompileSource(GridSource);
        Assert.True(built.IsSuccess, string.Join(" | ", built.Diagnostics.Select(d => d.Message)));
        var report = Assert.IsType<FirmamentPerforationReport>(built.Value.Perforation);
        Assert.Equal(4, report.InstanceCount);
        Assert.True(report.Manifold && report.StepReimportedManifold);
        Assert.Equal(4, built.Value.RuntimeCorrespondence!.Descendants.Count);
        Assert.All(built.Value.RuntimeCorrespondence.Descendants, descendant =>
        {
            Assert.NotNull(descendant.Face);
            Assert.Equal("Body.Vent", descendant.ParentStableId);
        });
        Assert.Contains("Body.Vent", built.Value.RuntimeCorrespondence.SourceSpans!.Keys);
        Assert.Equal("Body.Vent.Row0.Col0", report.InstanceIds[0]);
        Assert.NotEmpty(built.Value.StepText);
    }

    [Fact]
    public void Hex_StaggersRowsAndClipsFullOpenings()
    {
        var parsed = FirmamentV2Parser.Parse(GridSource.Replace("Layout: Grid", "Layout: Hex", StringComparison.Ordinal));
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics));
        var feature = Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).Perforations!);
        var plan = FirmamentPerforationPlanner.Plan(30, 30, feature);
        Assert.True(plan.Succeeded, plan.Diagnostic);
        Assert.Equal(-6, plan.Instances[0].X, 8);
        Assert.Equal(0, plan.Instances.First(i => i.Row == 1).X, 8);
        Assert.All(plan.Instances, instance =>
        {
            Assert.InRange(instance.X, -9, 9);
            Assert.InRange(instance.Y, -9, 9);
        });
    }

    [Fact]
    public void InvalidPitchAndNoRoomFailWithSpecificDiagnostics()
    {
        var overlap = FirmamentBuildAndExport.CompileSource(GridSource.Replace("Pitch: 12mm", "Pitch: 6mm", StringComparison.Ordinal));
        Assert.False(overlap.IsSuccess);
        Assert.Contains(overlap.Diagnostics, d => d.Message.Contains("perforation-hole-overlap", StringComparison.Ordinal));
        var noRoom = FirmamentBuildAndExport.CompileSource(GridSource.Replace("Margin: 3mm", "Margin: 15mm", StringComparison.Ordinal));
        Assert.False(noRoom.IsSuccess);
        Assert.Contains(noRoom.Diagnostics, d => d.Message.Contains("perforation-support-too-small", StringComparison.Ordinal));
    }

    [Fact]
    public void CylinderSupport_ReportsExactTopologyBoundaryWithoutExportingUncutStock()
    {
        const string source = """
            Model Sleeve {
              Units: mm
              Cylinder Tube { Radius: 20mm; Height: 60mm }
              Modify Tube {
                Perforation Vents { On: Wall; Diameter: 6mm; Layout: Hex; Pitch: 9mm; Margin: 8mm }
              }
            }
            """;
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("perforation-cylindrical-topology-unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public void SchemaAndLanguageService_ExposeCanonicalFields()
    {
        var schema = Assert.IsType<FirmamentConstructSchema>(FirmamentSemanticSchemas.Get("Perforation"));
        Assert.Contains(schema.Fields, field => field.Name == "Diameter" && field.Required);
        Assert.Contains(schema.Fields, field => field.Name == "Layout" && field.Choices.Contains("Hex"));
        const string source = "Model X { Units: mm Box Body { Size: [30mm,30mm,2mm] } Modify Body { Perforation Vent {\n Dia";
        var completion = FirmamentLanguageService.Complete(source, "x", "r1", source.Length);
        Assert.Equal("Perforation", completion.Context);
        Assert.Contains(completion.Fields, field => field.Name == "Diameter");
    }
}
