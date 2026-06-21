using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2InlineStepTests
{
    private const string FixturePath = "fixtures/FirmamentV2/InlineStep/valid/inline-step-v2-canonical-box-reexport-step-verified.valid.firmfixture";
    private const string InputStepPath = "fixtures/FirmamentV2/InlineStep/testdata/canonical-box-10x8x6.step";

    [Fact]
    public void InlineStep_CanonicalBox_ReexportsAndRoundTripsThroughAp242()
    {
        var repo = FindRepoRoot();
        var outputPath = Path.Combine(Path.GetTempPath(), $"aetheris-inline-step-x1-{Guid.NewGuid():N}.step");
        try
        {
            var result = FirmamentBuildAndExport.Run(Path.Combine(repo, FixturePath), outputPath);

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.True(File.Exists(outputPath));
            Assert.Equal("inline-step", result.Value.Export.ExportedBodyCategory);
            Assert.Equal("aetheris-canonical-ap242", result.Value.Export.ExportedFeatureKind);

            var output = File.ReadAllText(outputPath);
            Assert.Contains("ADVANCED_FACE", output, StringComparison.Ordinal);
            Assert.Contains("VERTEX_POINT", output, StringComparison.Ordinal);
            Assert.True(Count(output, "ADVANCED_FACE") > 0);
            Assert.True(Count(output, "VERTEX_POINT") > 0);
            Assert.Contains("MANIFOLD_SOLID_BREP", output, StringComparison.Ordinal);
            Assert.DoesNotContain("trace-only", output, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(File.ReadAllText(Path.Combine(repo, InputStepPath)), output);

            var import = Step242Importer.ImportBody(output);
            Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
            Assert.Equal(6, import.Value.Topology.Faces.Count());
            Assert.Equal(8, import.Value.Topology.Vertices.Count());
            Assert.Equal(12, import.Value.Topology.Edges.Count());
            Assert.Equal(480d, ComputeBoxVolume(import.Value), precision: 8);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void InlineStep_NonCanonicalStep_IsRejectedWithDeterministicDiagnostic()
    {
        var source = """
model BadInlineStep {
    units mm

    solid importedPart: InlineStep {
        path: "../../../../testdata/step242/generated/v0-required/gen_box_v0.step"
    }
}
""";

        var parse = FirmamentV2Parser.Parse(source, Path.Combine(FindRepoRoot(), "fixtures/FirmamentV2/InlineStep/valid"));

        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.InlineStepRequiresCanonical, parse.Diagnostics);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static int Count(string text, string needle) => text.Split(needle, StringSplitOptions.None).Length - 1;

    private static double ComputeBoxVolume(Aetheris.Kernel.Core.Brep.BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException("Missing vertex point.")).ToArray();
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var minZ = points.Min(p => p.Z); var maxZ = points.Max(p => p.Z);
        return (maxX - minX) * (maxY - minY) * (maxZ - minZ);
    }
}
