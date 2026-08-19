using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep.Verification;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2CanonicalGrammarTests
{
    [Fact]
    public void CanonicalBarePrimitives_ParseIntoTheNormalizedV2Document()
    {
        foreach (var fixture in new[] { "box.firmament", "cylinder.firmament", "rounded-box.firmament", "frustum.firmament" })
        {
            var parsed = FirmamentV2Parser.Parse(File.ReadAllText(Fixture(fixture)));
            Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
            Assert.Equal(FirmamentV2ParseDisposition.RecognizedValid, parsed.Disposition);
            Assert.Contains("firmament-v2-unified-canonical-parsed", parsed.Diagnostics);
            Assert.Empty(parsed.Document!.ModifyBlocks!);
        }
    }

    [Theory]
    [InlineData("Sphere Body { Radius: 7.5mm }", "solid Body: Sphere { radius: 7.5mm }")]
    [InlineData("Cone Body { BottomRadius: 6mm TopRadius: 0mm Height: 18mm }", "solid Body: Cone { bottomRadius: 6mm topRadius: 0mm height: 18mm }")]
    [InlineData("Torus Body { MajorRadius: 12mm MinorRadius: 3mm }", "solid Body: Torus { majorRadius: 12mm minorRadius: 3mm }")]
    public void CanonicalAnalyticPrimitiveAndCompatibilitySolid_LowerToTheSameRecord(string canonicalDeclaration, string compatibilityDeclaration)
    {
        var canonical = FirmamentV2Parser.Parse($"Model Analytic {{ Units: mm {canonicalDeclaration} }}");
        var compatibility = FirmamentV2Parser.Parse($"model Analytic {{ units mm {compatibilityDeclaration} }}");

        Assert.True(canonical.IsSuccess, string.Join(Environment.NewLine, canonical.Diagnostics));
        Assert.True(compatibility.IsSuccess, string.Join(Environment.NewLine, compatibility.Diagnostics));
        var canonicalSolid = Assert.Single(canonical.Document!.Solids);
        var compatibilitySolid = Assert.Single(compatibility.Document!.Solids);
        Assert.Equal(canonicalSolid.Name, compatibilitySolid.Name);
        Assert.Equal(canonicalSolid.RecordType, compatibilitySolid.RecordType);
        Assert.Equal(canonicalSolid.Primitive, compatibilitySolid.Primitive);
        var canonicalLowering = FirmamentV2BuildLowering.LowerPrimitiveBridge(canonical.Document);
        var compatibilityLowering = FirmamentV2BuildLowering.LowerPrimitiveBridge(compatibility.Document);
        Assert.True(canonicalLowering.IsSuccess);
        Assert.True(compatibilityLowering.IsSuccess);
        Assert.Equal(Assert.Single(canonicalLowering.Value.Primitives), Assert.Single(compatibilityLowering.Value.Primitives));
    }

    [Fact]
    public void CanonicalPrimitiveFields_AcceptLowercaseCompatibilityWithoutChangingTheRoute()
    {
        var canonical = FirmamentV2Parser.Parse("Model X { Units: mm Box body { Size: [10mm, 20mm, 30mm] } }");
        var mixed = FirmamentV2Parser.Parse("Model X { units: mm Box body { size: [10mm, 20mm, 30mm] } }");

        Assert.True(canonical.IsSuccess, string.Join(Environment.NewLine, canonical.Diagnostics));
        Assert.True(mixed.IsSuccess, string.Join(Environment.NewLine, mixed.Diagnostics));
        Assert.Equal(Assert.Single(canonical.Document!.Solids).Box!.Size, Assert.Single(mixed.Document!.Solids).Box!.Size);
        Assert.Contains("firmament-v2-unified-canonical-parsed", mixed.Diagnostics);
    }

    [Fact]
    public void CanonicalFrustum_ExportsThroughTheProductionConeRoute()
    {
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-canonical-frustum-{Guid.NewGuid():N}.step");
        try
        {
            var build = FirmamentBuildAndExport.Run(Fixture("frustum.firmament"), output);

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
        var parsed = FirmamentV2Parser.Parse(File.ReadAllText(Fixture("boundary-chamfer.firmament")));

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
            var build = FirmamentBuildAndExport.Run(Fixture("boundary-chamfer.firmament"), output);
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
            var fixture = Fixture("multiple-hole-dimensions-with-chamfer.firmament");
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
            var build = FirmamentBuildAndExport.Run(Fixture("counterbore.firmament"), output);
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
        var source = File.ReadAllText(Fixture("through-hole.firmament")).Replace("Point2(0mm, 0mm)", "[0mm, 0mm]", StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);

        Assert.False(parsed.IsSuccess);
        Assert.Contains(FirmamentV2Parser.CanonicalPoint2Invalid, parsed.Diagnostics);
    }

    private static string Fixture(string name)
    {
        var domain = name switch
        {
            "box.firmament" or "cylinder.firmament" => "Basics",
            "rounded-box.firmament" or "frustum.firmament" => "Primitives",
            "boundary-chamfer.firmament" => "Features/EdgeFinish",
            "counterbore.firmament" or "through-hole.firmament" => "Features/Holes",
            "multiple-hole-dimensions-with-chamfer.firmament" => "PMI",
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown canonical fixture")
        };
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Canonical", domain, name));
    }
}
