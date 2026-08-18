using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep.Verification;

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
    public void CanonicalFrustum_ExportsThroughTheProductionConeRoute()
    {
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-canonical-frustum-{Guid.NewGuid():N}.step");
        try
        {
            var build = FirmamentBuildAndExport.Run(Fixture("bare-frustum.firmament"), output);

            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Message)));
            Assert.True(File.Exists(output));
            Assert.Contains("CONICAL_SURFACE", File.ReadAllText(output), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
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
    public void CanonicalCombinedPart_PreservesSupportedPmiInAp242AndInspectionEvidence()
    {
        var firstOutput = Path.Combine(Path.GetTempPath(), $"aetheris-canonical-pmi-edge-{Guid.NewGuid():N}.step");
        var secondOutput = Path.Combine(Path.GetTempPath(), $"aetheris-canonical-pmi-edge-{Guid.NewGuid():N}.step");
        try
        {
            var fixture = Fixture("box-holes-pmi-chamfer.firmament");
            var parsed = FirmamentV2Parser.Parse(File.ReadAllText(fixture));
            Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
            Assert.Equal(3, parsed.Document!.Pmi!.Count);

            var first = FirmamentBuildAndExport.Run(fixture, firstOutput);
            var second = FirmamentBuildAndExport.Run(fixture, secondOutput);

            Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(d => d.Message)));
            Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(d => d.Message)));
            Assert.Equal("CombinedHoleEdgeFinish", first.Value.Export.Combined!.Route);
            Assert.Single(first.Value.Export.DatumInspection!);
            Assert.Equal(2, first.Value.Export.DimensionInspection!.Count);
            Assert.Equal(File.ReadAllText(firstOutput), File.ReadAllText(secondOutput));

            var pmi = Step242SemanticPmiInspector.Inspect(File.ReadAllText(firstOutput));
            Assert.True(pmi.Success, string.Join(Environment.NewLine, pmi.Diagnostics));
            Assert.Equal(1, pmi.DatumCount);
            Assert.Equal(2, pmi.DimensionCount);
        }
        finally
        {
            if (File.Exists(firstOutput)) File.Delete(firstOutput);
            if (File.Exists(secondOutput)) File.Delete(secondOutput);
        }
    }

    [Fact]
    public void CanonicalCounterbore_ReimportsAsAnEnclosedManifold()
    {
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-canonical-counterbore-{Guid.NewGuid():N}.step");
        try
        {
            var build = FirmamentBuildAndExport.Run(Fixture("counterbore-hole.firmament"), output);
            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Message)));

            var imported = Step242Importer.ImportBody(File.ReadAllText(output));
            Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));

            var mass = BrepMassProperties.Evaluate(imported.Value);
            Assert.True(mass.IsEnclosed, string.Join(Environment.NewLine, mass.Diagnostics));
            Assert.True(mass.IsOrientationConsistent, string.Join(Environment.NewLine, mass.Diagnostics));
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

    private static string Fixture(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Canonical/valid", name));
}
