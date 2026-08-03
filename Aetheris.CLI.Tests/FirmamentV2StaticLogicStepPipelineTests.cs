using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentV2StaticLogicStepPipelineTests
{
    [Fact]
    public void EnumMatchVariants_ProduceDistinctValidBranchFreeStepGeometry()
    {
        var compact = Build("Compact");
        var standard = Build("Standard");

        Assert.Equal(new[] { 60d, 40d, 20d }, compact.Size);
        Assert.Equal(new[] { 80d, 50d, 25d }, standard.Size);
        Assert.Equal(1d, compact.Distance);
        Assert.Equal(1.5d, standard.Distance);
        Assert.NotEqual(compact.Hash, standard.Hash);
        Assert.Equal("Compact", compact.SelectedBoundsArm);
        Assert.Equal("Standard", standard.SelectedBoundsArm);
        Assert.Equal(1, compact.BodyCount);
        Assert.Equal(1, standard.BodyCount);
    }

    private static Result Build(string variant)
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-static-logic-m3", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, variant + ".firmament");
        var stepPath = Path.Combine(dir, variant + ".step");
        File.WriteAllText(sourcePath, Source(variant));
        var stdout = new StringWriter(); var stderr = new StringWriter();

        var exit = CliRunner.Run(["build", sourcePath, "--out", stepPath, "--json"], stdout, stderr);

        Assert.Equal(0, exit);
        using var report = JsonDocument.Parse(stdout.ToString());
        var conceptIr = report.RootElement.GetProperty("conceptIr");
        Assert.Equal("ErasedBeforeFeatureAir", conceptIr.GetProperty("erasureStatus").GetString());
        Assert.False(conceptIr.GetProperty("structs")[0].GetProperty("materialized").GetBoolean());
        Assert.DoesNotContain("\"Match\"", report.RootElement.GetProperty("air").GetRawText(), StringComparison.Ordinal);
        var selections = conceptIr.GetProperty("staticSelections").EnumerateArray().ToArray();
        var boundsSelection = selections.Single(s => s.GetProperty("member").GetString() == "BracketConcept.Bounds");
        var distanceSelection = selections.Single(s => s.GetProperty("member").GetString() == "BracketConcept.ChamferDistance");
        Assert.Equal(variant, distanceSelection.GetProperty("selectedArm").GetString());
        var import = Step242Importer.ImportBody(File.ReadAllText(stepPath, Encoding.UTF8));
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        var points = import.Value.Topology.Vertices.Select(v => import.Value.TryGetVertexPoint(v.Id, out var point) ? point : throw new InvalidOperationException()).ToArray();
        var size = new[] { points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y), points.Max(p => p.Z) - points.Min(p => p.Z) };
        Assert.Equal(12, points.Length);
        Assert.Equal(20, import.Value.Topology.Edges.Count());
        Assert.Equal(10, import.Value.Topology.Faces.Count());
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(stepPath)));
        var bodyCount = StepAnalyzer.Analyze(stepPath).Summary.BodyCount;
        return new(size, report.RootElement.GetProperty("air").GetProperty("feature").GetProperty("distance").GetDouble(), hash,
            boundsSelection.GetProperty("selectedArm").GetString()!, bodyCount);
    }

    private static string Source(string variant) => $$"""
        Enum BracketVariant {
            Compact
            Standard
        }
        Concept Struct BracketConcept {
            Variant: BracketVariant = {{variant}}
            Bounds: Match Variant {
                Compact => Box3 { Size: [60mm, 40mm, 20mm] }
                Standard => Box3 { Size: [80mm, 50mm, 25mm] }
            }
            ChamferDistance: Match Variant {
                Compact => 1mm
                Standard => 1.5mm
            }
        }
        Struct Bracket {
            Box Base { Bounds: BracketConcept.Bounds }
            Modify Base {
                EdgeFinish TopBreak {
                    Face: +Z
                    Target: Boundary
                    Kind: Chamfer
                    Distance: BracketConcept.ChamferDistance
                }
            }
        }
        """;

    private sealed record Result(double[] Size, double Distance, string Hash, string SelectedBoundsArm, int BodyCount);
}
