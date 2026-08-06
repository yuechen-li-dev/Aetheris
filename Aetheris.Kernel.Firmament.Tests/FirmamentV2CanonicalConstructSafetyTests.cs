using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2CanonicalConstructSafetyTests
{
    [Theory]
    [InlineData("PmiX")]
    [InlineData("EdgFinish")]
    [InlineData("Replce")]
    [InlineData("Recgonize")]
    [InlineData("Templat")]
    [InlineData("Constructon")]
    public void CanonicalTopLevelUnknownBlock_IsNeverSilentlyDiscarded(string keyword)
    {
        var parse = FirmamentV2Parser.Parse("Model UnknownConstruct {\n"
            + "    Units: mm\n"
            + "    Box Base { Size: [10mm, 10mm, 10mm] }\n"
            + "    " + keyword + " Example { }\n}");

        Assert.False(parse.IsSuccess);
        Assert.Equal(FirmamentV2ParseDisposition.RecognizedInvalid, parse.Disposition);
        Assert.Contains(FirmamentV2Parser.CanonicalDeclarationUnknown + ":" + keyword, parse.Diagnostics);
    }

    [Fact]
    public void CompatibilityOnlyCanonicalSpelling_ReportsItsExplicitPortStatus()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model CompatibilityOnly {
                Units: mm
                Box Base { Size: [10mm, 10mm, 10mm] }
                Solid OldShape { }
            }
            """);

        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.CanonicalConstructNotYetSupported + ":Solid", parse.Diagnostics);
    }

    [Fact]
    public void CanonicalPmi_NormalizesAndBindsThroughTheExistingPmiRecords()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model PmiPart {
                Units: mm
                Box Base { Size: [40mm, 30mm, 10mm] }
                Modify Base { Hole<Shaft> Mount { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll } }
                Pmi {
                    Datum A { Target: face(+Z) }
                    HoleDiameter MountCallout { Target: Mount Value: 8mm Tolerance: PlusMinus(0.05mm, 0.02mm) DatumRefs: [A] }
                }
            }
            """);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var records = parse.Document!.PmiBlock!.Records;
        Assert.Equal(2, records.Count);
        var callout = Assert.Single(parse.Document.BoundPmi!.Dimensions);
        Assert.Equal("Mount", callout.Targets.Single());
        Assert.Equal("A", callout.DatumRefs.Single());
        Assert.Equal(0.05d, callout.DimensionTolerance!.Plus);
        Assert.Equal(0.02d, callout.DimensionTolerance.Minus);
    }

    [Fact]
    public void CanonicalPmiFixture_BuildsExportsAndReimportsAp242Pmi()
    {
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2/Canonical/valid/box-hole-pmi.firmament"));
        var output = Path.Combine(Path.GetTempPath(), "aetheris-canonical-pmi-" + Guid.NewGuid().ToString("N") + ".step");
        try
        {
            var build = FirmamentBuildAndExport.Run(fixture, output);
            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var step = File.ReadAllText(output);
            Assert.Contains("firmament-datum:A", step, StringComparison.Ordinal);
            Assert.Contains("diameter_tolerance:Base.Mount", step, StringComparison.Ordinal);
            var reimport = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(step);
            Assert.True(reimport.IsSuccess, string.Join(Environment.NewLine, reimport.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Theory]
    [InlineData("Tolerence")]
    [InlineData("DatumRef")]
    [InlineData("Targt")]
    [InlineData("Valu")]
    public void CanonicalPmi_UnknownFieldFailsSpecifically(string field)
    {
        var parse = FirmamentV2Parser.Parse("Model BadPmi {\n"
            + "    Units: mm\n"
            + "    Box Base { Size: [10mm, 10mm, 10mm] }\n"
            + "    Pmi { Datum A { Target: face(+Z) " + field + ": 1mm } }\n}");

        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.PmiUnknownField, parse.Diagnostics);
    }
}
