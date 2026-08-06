using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2CanonicalGrammarTests
{
    [Fact]
    public void CanonicalBarePrimitives_ParseIntoTheNormalizedV2Document()
    {
        foreach (var fixture in new[] { "bare-box.firmament", "bare-cylinder.firmament", "bare-rounded-box.firmament", "bare-frustum.firmament" })
        {
            var parsed = FirmamentV2Parser.Parse(File.ReadAllText(Fixture(fixture)));
            Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
            Assert.Equal(FirmamentV2ParseDisposition.RecognizedValid, parsed.Disposition);
            Assert.Contains("firmament-v2-unified-canonical-parsed", parsed.Diagnostics);
            Assert.Empty(parsed.Document!.ModifyBlocks!);
        }
    }

    [Fact]
    public void CanonicalModify_AdmitsHoleAndEdgeFinishInTheSameBlock()
    {
        var parsed = FirmamentV2Parser.Parse(File.ReadAllText(Fixture("box-hole-chamfer.firmament")));

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var modify = Assert.Single(parsed.Document!.ModifyBlocks!);
        var hole = Assert.Single(modify.SemanticHoles);
        var edge = Assert.Single(modify.EdgeFinishes!);
        Assert.Equal("+Z", hole.EntryFace.Axis);
        Assert.Equal((0d, 0d), (hole.Center.U, hole.Center.V));
        Assert.Equal(FirmamentV2SemanticHoleEndKind.ThroughAll, hole.EndCondition.Kind);
        Assert.Equal("Chamfer", edge.Kind);
    }

    [Fact]
    public void CanonicalCombinedPart_ExportsThroughTheCombinedSemanticRoute()
    {
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-canonical-{Guid.NewGuid():N}.step");
        try
        {
            var build = FirmamentBuildAndExport.Run(Fixture("box-hole-chamfer.firmament"), output);
            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Message)));
            Assert.Equal("CombinedHoleEdgeFinish", build.Value.Export.Combined!.Route);
            Assert.True(File.Exists(output));
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void CanonicalPoint2_RejectsTheLegacyBracketLiteralWithASpecificDiagnostic()
    {
        var source = File.ReadAllText(Fixture("box-through-hole.firmament")).Replace("Point2(0mm, 0mm)", "[0mm, 0mm]", StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(FirmamentV2Parser.CanonicalPoint2Invalid, parsed.Diagnostics);
    }

    private static string Fixture(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Canonical/valid", name));
}
