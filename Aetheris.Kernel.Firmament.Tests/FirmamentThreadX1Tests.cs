using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentThreadX1Tests
{
    private static string CylinderThread(double radius = 4, double major = 8, double pitch = 1.25,
        double length = 5, double height = 9, double start = 1, string support = "face(Shank.OuterWall)",
        string hand = "Right") => $$"""
        Model ThreadWitness {
            Units: mm
            Cylinder Shank {
                Radius: {{radius}}mm
                Height: {{height}}mm
            }
            Thread MainThread {
                Surface: {{support}}
                MajorDiameter: {{major}}mm
                Pitch: {{pitch}}mm
                Length: {{length}}mm
                StartOffset: {{start}}mm
                Hand: {{hand}}
            }
        }
        """;

    [Fact]
    public void CylinderThreadProducesClosedManifoldStepAndSourceMappedRoles()
    {
        var source = CylinderThread();
        var first = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(first.IsSuccess, string.Join(" | ", first.Diagnostics.Select(d => d.Message)));
        var report = Assert.IsType<FirmamentThreadReport>(first.Value.Thread);
        Assert.Equal(8, report.MajorDiameterMm, 9);
        Assert.Equal(8 - 2 * 17 * Math.Sqrt(3) * 1.25 / 48, report.RootDiameterMm, 9);
        Assert.Equal(1.25, report.PitchMm, 9);
        Assert.Equal(report.PitchMm, report.LeadMm);
        Assert.Equal("Right", report.Hand);
        Assert.Equal("metric-60-degree-truncated", report.ProfileFamily);
        Assert.Equal(5, report.LengthMm);
        Assert.Equal(4, report.Turns);
        var body = Assert.IsType<BrepBody>(first.Value.RuntimeBody);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed, string.Join(" | ", mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent);
        Assert.Equal(report.Faces, body.Topology.Faces.Count());
        Assert.Equal(report.Edges, body.Topology.Edges.Count());
        Assert.Equal(report.Vertices, body.Topology.Vertices.Count());
        var display = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: TimeSpan.FromSeconds(30));
        Assert.True(display.IsSuccess, string.Join(" | ", display.Diagnostics.Select(d => d.Message)));
        Assert.Equal(report.Faces, display.Value.FacePatches.Count);
        Assert.All(display.Value.FacePatches, patch => Assert.NotEmpty(patch.TriangleIndices));

        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(first.Value.RuntimeCorrespondence);
        var sourceMap = new GeometrySourceMap(correspondence);
        Assert.True(sourceMap.TryGetSourceSpan(report.FeatureId, out var span));
        Assert.Contains("Thread MainThread", source.Substring(span.Start, span.Length));
        foreach (var role in new[] { SemanticTopologyRole.ThreadLeadingFlank,
                     SemanticTopologyRole.ThreadTrailingFlank, SemanticTopologyRole.ThreadCrest,
                     SemanticTopologyRole.ThreadRootFace, SemanticTopologyRole.ThreadStartCap,
                     SemanticTopologyRole.ThreadEndCap })
        {
            var face = Assert.Single(correspondence.Descendants.Where(item => item.Role == role && item.Face.HasValue).Take(1));
            Assert.True(sourceMap.TryGetByBrepFace(face.Face!.Value, out var mapped));
            Assert.Equal(report.FeatureId, mapped.SourceStableId);
        }
        var edge = Assert.Single(correspondence.Descendants.Where(item => item.Role == SemanticTopologyRole.ThreadBoundaryEdge && item.Edge.HasValue).Take(1));
        Assert.True(sourceMap.TryGetByBrepEdge(edge.Edge!.Value, out _));

        var imported = Step242Importer.ImportBody(first.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        var roundtripMass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(roundtripMass.IsEnclosed, string.Join(" | ", roundtripMass.Diagnostics));
        Assert.True(roundtripMass.IsOrientationConsistent);

        var second = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(second.IsSuccess);
        Assert.Equal(report.StepSha256, second.Value.Thread!.StepSha256);
    }

    [Fact]
    public void PitchLengthAndDiameterEditsUpdateSemanticGeometry()
    {
        var baseline = FirmamentBuildAndExport.CompileSource(CylinderThread());
        var pitch = FirmamentBuildAndExport.CompileSource(CylinderThread(pitch: 1, length: 5));
        var length = FirmamentBuildAndExport.CompileSource(CylinderThread(length: 6.25, height: 10));
        var diameter = FirmamentBuildAndExport.CompileSource(CylinderThread(radius: 5, major: 10));
        Assert.True(baseline.IsSuccess);
        Assert.True(pitch.IsSuccess, string.Join(" | ", pitch.Diagnostics.Select(d => d.Message)));
        Assert.True(length.IsSuccess, string.Join(" | ", length.Diagnostics.Select(d => d.Message)));
        Assert.True(diameter.IsSuccess, string.Join(" | ", diameter.Diagnostics.Select(d => d.Message)));
        Assert.Equal(4, baseline.Value.Thread!.Turns);
        Assert.Equal(5, pitch.Value.Thread!.Turns);
        Assert.True(pitch.Value.Thread.RootDiameterMm > baseline.Value.Thread.RootDiameterMm);
        Assert.Equal(5, length.Value.Thread!.Turns);
        Assert.Equal(6.25, length.Value.Thread.LengthMm);
        Assert.Equal(10, diameter.Value.Thread!.MajorDiameterMm);
        Assert.Equal(baseline.Value.Thread.RootDiameterMm + 2, diameter.Value.Thread.RootDiameterMm, 8);
    }

    [Fact]
    public void OmittedStartOffsetDerivesAStockMargin()
    {
        var source = CylinderThread().Replace("StartOffset: 1mm", "", StringComparison.Ordinal);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(1 + 5d * 1.25d / 12d, result.Value.Thread!.StartOffsetMm, 9);
    }

    [Theory]
    [InlineData(4, 8, 0, 5, 9, 1, "face(Shank.OuterWall)", "Right", "thread-pitch-invalid")]
    [InlineData(4, 8, -1, 5, 9, 1, "face(Shank.OuterWall)", "Right", "thread-pitch-invalid")]
    [InlineData(4, 8, 1.25, 0, 9, 1, "face(Shank.OuterWall)", "Right", "thread-length-invalid")]
    [InlineData(4, 8, 1.25, 8, 9, 1, "face(Shank.OuterWall)", "Right", "thread-support-bounds")]
    [InlineData(4, 9, 1.25, 5, 9, 1, "face(Shank.OuterWall)", "Right", "thread-major-diameter-incompatible")]
    [InlineData(4, 8, 8, 8, 20, 1, "face(Shank.OuterWall)", "Right", "thread-profile-self-intersection")]
    [InlineData(4, 8, 1.25, 5, 9, 1, "face(Shank.Top)", "Right", "thread-support-not-cylinder")]
    public void InvalidThreadParametersHaveControlledDiagnostics(double radius, double major, double pitch,
        double length, double height, double start, string support, string hand, string code)
    {
        var result = FirmamentBuildAndExport.CompileSource(CylinderThread(radius, major, pitch, length, height, start, support, hand));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedHandIsExplicit()
    {
        var result = FirmamentBuildAndExport.CompileSource(CylinderThread(hand: "Left"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Message.Contains("thread-hand-unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void ThreadSchemaAndLanguageCompletionExposeFields()
    {
        var schema = Assert.IsType<FirmamentConstructSchema>(FirmamentSemanticSchemas.Get("Thread"));
        Assert.Equal(["Hand", "Length", "MajorDiameter", "Pitch", "StartOffset", "Surface"], schema.Fields.Select(f => f.Name));
        Assert.Contains(schema.Outputs, item => item.Name == "RootDiameter");
        Assert.Contains(schema.Outputs, item => item.Name == "TurnCount");
        var bare = "Model M { Units: mm\n Thr";
        var entry = FirmamentLanguageService.Complete(bare, "thread.firmament", "1", bare.Length);
        Assert.Equal("Thread", Assert.Single(entry.Entries!).Name);
        var partial = "Model M { Units: mm\n Thread T {\n ";
        var fields = FirmamentLanguageService.Complete(partial, "thread.firmament", "1", partial.Length);
        Assert.Equal("Thread", fields.Context);
        Assert.Contains(fields.Fields, field => field.Name == "Surface");
        Assert.Contains(fields.Fields, field => field.Name == "Pitch");
    }

    [Fact]
    public void ExistingHexBoltAndOneThreadProduceOneManifoldStepBody()
    {
        var source = File.ReadAllText(ThreadFixture("hexbolt-threaded.firmament"));
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        var report = Assert.IsType<FirmamentThreadReport>(result.Value.Thread);
        Assert.Equal(38, report.Turns);
        Assert.Equal(8, report.MajorDiameterMm);
        Assert.Equal(6.466413347465057, report.RootDiameterMm, 8);
        Assert.Equal(47.5, report.LengthMm);
        Assert.Equal(0.75, report.StartOffsetMm, 9);
        Assert.Equal(0.95, report.AxialStartMm, 9);
        Assert.Equal(176, report.Faces);
        Assert.Equal(355, report.Edges);
        Assert.Equal(184, report.Vertices);
        var standardPart = Assert.IsType<FirmamentStandardPartReport>(result.Value.StandardPart);
        Assert.Equal("HexBolt", standardPart.Family);
        Assert.Contains(standardPart.SemanticDescendants, item => item.StableId.EndsWith(".Head.TopFlat", StringComparison.Ordinal) && item.FaceId.HasValue);
        Assert.Contains(standardPart.SemanticDescendants, item => item.StableId.EndsWith(".TipFace", StringComparison.Ordinal) && item.FaceId.HasValue);
        Assert.DoesNotContain(standardPart.SemanticDescendants, item => item.StableId.Contains(".ThreadRegion", StringComparison.Ordinal));
        var body = Assert.IsType<BrepBody>(result.Value.RuntimeBody);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed, string.Join(" | ", mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent);
        var map = Assert.IsType<SemanticTopologyCorrespondence>(result.Value.RuntimeCorrespondence);
        Assert.Contains(map.Descendants, item => item.Role == SemanticTopologyRole.ThreadCrest && item.Face.HasValue);
        Assert.Contains(map.Descendants, item => item.Role == SemanticTopologyRole.ThreadLeadingFlank && item.Face.HasValue);
        Assert.Contains(map.Descendants, item => item.Role == SemanticTopologyRole.ThreadTrailingFlank && item.Face.HasValue);
        Assert.True(new GeometrySourceMap(map).TryGetSourceSpan(report.FeatureId, out var span));
        Assert.Contains("Thread MainThread", source.Substring(span.Start, span.Length));
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        var importedMass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(importedMass.IsEnclosed, string.Join(" | ", importedMass.Diagnostics));
        Assert.True(importedMass.IsOrientationConsistent);
        var repeated = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(report.StepSha256, repeated.Value.Thread!.StepSha256);
    }

    [Fact]
    public void WriteThreadedHexBoltDisplayArtifactWhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("AETHERIS_THREAD_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var source = File.ReadAllText(ThreadFixture("hexbolt-threaded.firmament"));
        var timer = Stopwatch.StartNew();
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(" | ", built.Diagnostics.Select(d => d.Message)));
        var compileAndBuildMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        var mesh = BrepDisplayTessellator.TessellateBounded(built.Value.RuntimeBody!,
            executionTimeout: TimeSpan.FromSeconds(60));
        Assert.True(mesh.IsSuccess, string.Join(" | ", mesh.Diagnostics.Select(d => d.Message)));
        var tessellationMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(built.Value.Thread!.Faces, mesh.Value.FacePatches.Count);
        File.WriteAllText(Path.Combine(output, "threaded-hexbolt.obj"), Obj(mesh.Value));
        File.WriteAllText(Path.Combine(output, "threaded-hexbolt.step"), built.Value.StepText);
        File.WriteAllText(Path.Combine(output, "threaded-hexbolt.json"), JsonSerializer.Serialize(new
        {
            built.Value.Thread,
            compileAndBuildMs,
            tessellationMs,
            triangles = mesh.Value.FacePatches.Sum(patch => patch.TriangleIndices.Count / 3)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void WriteThreadCylinderDisplayArtifactWhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("AETHERIS_THREAD_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var source = CylinderThread(length: 30, height: 34);
        var timer = Stopwatch.StartNew();
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(" | ", built.Diagnostics.Select(d => d.Message)));
        var compileAndBuildMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        var mesh = BrepDisplayTessellator.TessellateBounded(built.Value.RuntimeBody!,
            executionTimeout: TimeSpan.FromSeconds(60));
        Assert.True(mesh.IsSuccess, string.Join(" | ", mesh.Diagnostics.Select(d => d.Message)));
        var tessellationMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(built.Value.Thread!.Faces, mesh.Value.FacePatches.Count);
        File.WriteAllText(Path.Combine(output, "thread-cylinder-24.obj"), Obj(mesh.Value));
        File.WriteAllText(Path.Combine(output, "thread-cylinder-24.step"), built.Value.StepText);
        File.WriteAllText(Path.Combine(output, "thread-cylinder-24.json"), JsonSerializer.Serialize(new
        {
            built.Value.Thread,
            compileAndBuildMs,
            tessellationMs,
            triangles = mesh.Value.FacePatches.Sum(patch => patch.TriangleIndices.Count / 3)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Obj(Aetheris.Kernel.Core.Brep.Tessellation.DisplayTessellationResult mesh)
    {
        var obj = new StringBuilder();
        var vertexOffset = 1;
        foreach (var patch in mesh.FacePatches)
        {
            obj.AppendLine($"g Face_{patch.FaceId.Value}");
            foreach (var point in patch.Positions)
                obj.AppendLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
            for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
                obj.AppendLine($"f {vertexOffset + patch.TriangleIndices[i]} {vertexOffset + patch.TriangleIndices[i + 1]} {vertexOffset + patch.TriangleIndices[i + 2]}");
            vertexOffset += patch.Positions.Count;
        }
        return obj.ToString();
    }

    private static string ThreadFixture(string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
            root = root.Parent;
        if (root is null) throw new FileNotFoundException("Aetheris repository root was not found for Thread fixture.");
        return Path.Combine(root.FullName, "fixtures", "Thread", file);
    }
}
