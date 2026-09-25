using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Core.Brep.Tessellation;
using System.Diagnostics;
using System.Text.Json;

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

    private const string CylindricalSource = """
        Struct PerforatedSleeve {
          Cylinder<Hollow> Body {
            Radius: 56mm
            Height: 142mm
            WallThickness: 0.8mm
            Openings: [Top]
          }
          Modify Body {
            Perforation Vents {
              On: OuterWall
              Diameter: 9mm
              Layout: CylindricalGrid
              Pitch: 15mm
              CircumferentialPitch: 18mm
              MarginTop: 12mm
              MarginBottom: 20mm
              MinimumLigament: 2mm
              StartAngle: 9deg
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
    public void CylindricalPlanner_WrapsWithoutDuplicateAndKeepsStableGridIdentity()
    {
        var parsed = FirmamentV2Parser.Parse(CylindricalSource);
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics));
        var feature = Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).Perforations!);
        var plan = FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8, feature);
        Assert.True(plan.Succeeded, plan.Diagnostic);
        Assert.Equal(plan.Rows * plan.Columns, plan.Instances.Count);
        Assert.Equal(plan.Instances.Count, plan.Instances.Select(instance => instance.StableId("Body.Vents")).Distinct().Count());
        Assert.All(plan.Instances, instance => Assert.InRange(instance.AngleRadians, 0, 2 * Math.PI));
        Assert.All(plan.Instances, instance => Assert.InRange(instance.Z, 20 + 4.5, 142 - 12 - 4.5));
        Assert.Equal(2 * Math.PI / plan.Columns, plan.AngularPitchRadians, 10);
        Assert.Equal("Body.Vents.Row0.Col0", plan.Instances[0].StableId("Body.Vents"));
        var again = FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8, feature);
        Assert.Equal(plan.Instances, again.Instances);
        var wrappedPhase = FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8,
            feature with { StartAngleDegrees = feature.StartAngleDegrees + 360 });
        Assert.Equal(plan.Instances, wrappedPhase.Instances);
    }

    [Fact]
    public void CylindricalPlanner_StaggerAndInvalidClearanceAreDeterministic()
    {
        var parsed = FirmamentV2Parser.Parse(CylindricalSource.Replace("CylindricalGrid", "CylindricalStaggered", StringComparison.Ordinal));
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics));
        var feature = Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).Perforations!);
        var plan = FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8, feature);
        Assert.True(plan.Succeeded, plan.Diagnostic);
        Assert.Equal(plan.AngularPitchRadians / 2, plan.Instances[plan.Columns].AngleRadians - plan.Instances[0].AngleRadians, 10);
        Assert.Equal("perforation-hole-overlap", FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8,
            feature with { Pitch = 9 }).Diagnostic);
        Assert.Equal("perforation-no-admissible-holes", FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8,
            feature with { MarginTop = 75, MarginBottom = 75 }).Diagnostic);
        Assert.Equal("perforation-hole-overlap", FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8,
            feature with { CircumferentialPitch = 9 }).Diagnostic);
        Assert.Equal("perforation-cylindrical-tangent-or-oversize", FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8,
            feature with { Diameter = 111 }).Diagnostic);
        Assert.Equal("perforation-cylindrical-offset-unsupported", FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8,
            feature with { OffsetX = 1 }).Diagnostic);
    }

    [Fact]
    public void CylindricalPlanner_ParameterEditsRetainPatternCoordinates()
    {
        var parsed = FirmamentV2Parser.Parse(CylindricalSource);
        var feature = Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).Perforations!);
        var original = FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8, feature);
        var smallerHole = FirmamentCylindricalPerforationPlanner.Plan(56, 142, 0.8, feature with { Diameter = 8 });
        var thickerWall = FirmamentCylindricalPerforationPlanner.Plan(56, 142, 1.2, feature);
        Assert.True(smallerHole.Succeeded && thickerWall.Succeeded);
        Assert.Equal(original.Instances.Select(i => (i.Row, i.Column)),
            smallerHole.Instances.Select(i => (i.Row, i.Column)));
        Assert.Equal(original.Instances.Select(i => (i.Row, i.Column)),
            thickerWall.Instances.Select(i => (i.Row, i.Column)));
    }

    [Fact]
    public void CylindricalPerforation_DiameterAndThicknessEditsRebuildStep()
    {
        var diameter = FirmamentBuildAndExport.CompileSource(CylindricalSource.Replace("Diameter: 9mm", "Diameter: 8mm", StringComparison.Ordinal));
        var thickness = FirmamentBuildAndExport.CompileSource(CylindricalSource.Replace("WallThickness: 0.8mm", "WallThickness: 1.2mm", StringComparison.Ordinal));
        Assert.True(diameter.IsSuccess, string.Join(" | ", diameter.Diagnostics.Select(d => d.Message)));
        Assert.True(thickness.IsSuccess, string.Join(" | ", thickness.Diagnostics.Select(d => d.Message)));
        Assert.Equal(133, diameter.Value.Perforation!.InstanceCount);
        Assert.Equal(133, thickness.Value.Perforation!.InstanceCount);
        Assert.NotEqual(diameter.Value.Perforation.StepSha256, thickness.Value.Perforation.StepSha256);
        Assert.Equal(diameter.Value.Perforation.InstanceIds, thickness.Value.Perforation.InstanceIds);
    }

    [Fact]
    public void CylindricalPerforation_ExportsOneManifoldPatternWithStableWalls()
    {
        var buildStart = Stopwatch.GetTimestamp();
        var built = FirmamentBuildAndExport.CompileSource(CylindricalSource);
        var buildMilliseconds = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;
        Assert.True(built.IsSuccess, string.Join(" | ", built.Diagnostics.Select(d => d.Message)));
        var report = Assert.IsType<FirmamentPerforationReport>(built.Value.Perforation);
        Assert.Equal(report.Rows * report.Columns, report.InstanceCount);
        Assert.Equal(133, report.InstanceCount);
        Assert.Equal(report.InstanceCount * 3, built.Value.RuntimeCorrespondence!.Descendants.Count);
        var first = built.Value.RuntimeCorrespondence.Descendants.Where(d => d.SourceStableId == "perforation:Body.Vents.Row0.Col0").ToArray();
        Assert.Contains(first, d => d.Role == Aetheris.Kernel.Firmament.Materializer.SemanticTopologyRole.HoleWallFace && d.Face is not null);
        Assert.Contains(first, d => d.Role == Aetheris.Kernel.Firmament.Materializer.SemanticTopologyRole.HoleEntryLoop && d.Loop is not null);
        Assert.Contains(first, d => d.Role == Aetheris.Kernel.Firmament.Materializer.SemanticTopologyRole.HoleExitLoop && d.Loop is not null);
        Assert.Equal(report.InstanceCount + 5, report.Faces);
        Assert.True(report.Manifold && report.StepReimportedManifold);
        Assert.NotEmpty(built.Value.StepText);
        var output = Environment.GetEnvironmentVariable("AETHERIS_CYLINDRICAL_ORDNING_OBJ_OUTPUT");
        var displayOptions = string.IsNullOrWhiteSpace(output) ? DisplayTessellationOptions.Default
            : new DisplayTessellationOptions(System.Math.PI / 24, 0.01, 24, 1024);
        var displayStart = Stopwatch.GetTimestamp();
        var display = BrepDisplayTessellator.TessellateBounded(built.Value.RuntimeBody!, displayOptions,
            executionTimeout: TimeSpan.FromSeconds(string.IsNullOrWhiteSpace(output) ? 60 : 120));
        var displayMilliseconds = Stopwatch.GetElapsedTime(displayStart).TotalMilliseconds;
        Assert.True(display.IsSuccess, string.Join(" | ", display.Diagnostics.Select(d => d.Message)));
        Assert.Equal(report.Faces, display.Value.FacePatches.Count);
        var perfOutput = Environment.GetEnvironmentVariable("AETHERIS_CYLINDRICAL_PERF_REPORT_OUTPUT");
        if (!string.IsNullOrWhiteSpace(perfOutput))
            File.WriteAllText(perfOutput, JsonSerializer.Serialize(new
            {
                buildExportReimportMs = buildMilliseconds, displayTessellationMs = displayMilliseconds,
                displayOptions, stepBytes = System.Text.Encoding.UTF8.GetByteCount(built.Value.StepText),
                report.InstanceCount, report.Faces, report.Edges, report.Vertices,
                report.MaximumCurveBoundMm, report.MaximumPcurveBoundMm
            }, new JsonSerializerOptions { WriteIndented = true }));
        if (!string.IsNullOrWhiteSpace(output))
        {
            var lines = new List<string>(); var offset = 1;
            foreach (var patch in display.Value.FacePatches)
            {
                lines.Add($"g Face{patch.FaceId.Value}");
                lines.AddRange(patch.Positions.Select(p => FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}")));
                for (var k = 0; k < patch.TriangleIndices.Count; k += 3)
                    lines.Add($"f {offset + patch.TriangleIndices[k]} {offset + patch.TriangleIndices[k + 1]} {offset + patch.TriangleIndices[k + 2]}");
                offset += patch.Positions.Count;
            }
            File.WriteAllLines(output, lines);
        }
    }

    [Fact]
    public void BlendedOrdningBlank_PreservesOneHundredThirtyThreeHoles()
    {
        var source = CylindricalSource.Replace("WallThickness: 0.8mm", "WallThickness: 0.8mm; BottomBlendRadius: 8mm", StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(" | ", built.Diagnostics.Select(d => d.Message)));
        var report = Assert.IsType<FirmamentPerforationReport>(built.Value.Perforation);
        Assert.Equal(133, report.InstanceCount);
        Assert.Equal(140, report.Faces);
        Assert.True(report.Manifold && report.StepReimportedManifold);
        Assert.Equal(2, built.Value.RuntimeBody!.Geometry.Surfaces.Count(s => s.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Torus));
        var output = Environment.GetEnvironmentVariable("AETHERIS_HOLLOW_BLEND_OBJ_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        var display = BrepDisplayTessellator.TessellateBounded(built.Value.RuntimeBody,
            new DisplayTessellationOptions(System.Math.PI / 24, 0.01, 24, 1024), executionTimeout: TimeSpan.FromSeconds(120));
        Assert.True(display.IsSuccess, string.Join(" | ", display.Diagnostics.Select(d => d.Message)));
        Assert.Equal(140, display.Value.FacePatches.Count);
        var lines = new List<string>(); var offset = 1;
        foreach (var patch in display.Value.FacePatches)
        {
            lines.Add($"g Face{patch.FaceId.Value}");
            lines.AddRange(patch.Positions.Select(p => FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}")));
            for (var k = 0; k < patch.TriangleIndices.Count; k += 3)
                lines.Add($"f {offset + patch.TriangleIndices[k]} {offset + patch.TriangleIndices[k + 1]} {offset + patch.TriangleIndices[k + 2]}");
            offset += patch.Positions.Count;
        }
        File.WriteAllLines(output, lines);
    }

    [Fact]
    public void HollowBottomBlend_RejectsInvalidRadiusAndUnit()
    {
        var source = CylindricalSource.Replace("WallThickness: 0.8mm", "WallThickness: 0.8mm; BottomBlendRadius: 0.8mm", StringComparison.Ordinal);
        var radius = FirmamentV2Parser.Parse(source);
        Assert.False(radius.IsSuccess);
        Assert.Contains("firmament-v2-hollow-bottom-blend-invalid", radius.Diagnostics);
        var unit = FirmamentV2Parser.Parse(source.Replace("BottomBlendRadius: 0.8mm", "BottomBlendRadius: 8cm", StringComparison.Ordinal));
        Assert.False(unit.IsSuccess);
        Assert.Contains("firmament-v2-hollow-bottom-blend-invalid", unit.Diagnostics);
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
        var cylindricalFieldOnPanel = FirmamentBuildAndExport.CompileSource(GridSource.Replace("Margin: 3mm", "Margin: 3mm; CircumferentialPitch: 12mm", StringComparison.Ordinal));
        Assert.False(cylindricalFieldOnPanel.IsSuccess);
        Assert.Contains(cylindricalFieldOnPanel.Diagnostics, d => d.Message.Contains("perforation-planar-cylindrical-field-unsupported", StringComparison.Ordinal));
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
        Assert.Contains(schema.Fields, field => field.Name == "Layout" && field.Choices.Contains("CylindricalStaggered"));
        Assert.Contains(schema.Fields, field => field.Name == "CircumferentialPitch");
        const string source = "Model X { Units: mm Box Body { Size: [30mm,30mm,2mm] } Modify Body { Perforation Vent {\n Dia";
        var completion = FirmamentLanguageService.Complete(source, "x", "r1", source.Length);
        Assert.Equal("Perforation", completion.Context);
        Assert.Contains(completion.Fields, field => field.Name == "Diameter");
        const string cylindricalSource = "Struct X { Cylinder<Hollow> Body { Radius: 20mm Height: 60mm WallThickness: 0.8mm Openings: [Top] } Modify Body { Perforation Vent {\n Circ";
        var cylindricalCompletion = FirmamentLanguageService.Complete(cylindricalSource, "x", "r1", cylindricalSource.Length);
        Assert.Equal("Perforation", cylindricalCompletion.Context);
        Assert.Contains(cylindricalCompletion.Fields, field => field.Name == "CircumferentialPitch");
    }
}
