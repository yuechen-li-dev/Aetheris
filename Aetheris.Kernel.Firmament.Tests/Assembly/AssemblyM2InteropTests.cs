using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyM2InteropTests
{
    [Fact]
    public void FirmasmProfile_LegacyOcctFixtureMigratesThroughOrdinaryParserWithExplicitAuthority()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/occt-as1/as1-assembly.firmasm");
        var result = new FirmamentAssemblyDocumentCompiler().CompileFile(path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Compilation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.Equal(FirmamentDocumentProfile.Assembly, result.Profile);
        Assert.True(result.MigratedLegacyJson);
        Assert.Contains("Assembly occt_as1_flattened", result.EffectiveFirmamentSource, StringComparison.Ordinal);
        Assert.Equal(18, result.Compilation.Geometry!.Artifact.Instances.Count);
        Assert.All(result.Compilation.Ir!.Instances.Where(instance => instance.Kind == AssemblyInstanceKind.Part), instance => Assert.Equal(PlacementAuthority.LegacyExplicit, instance.PlacementAuthority));
        Assert.Empty(result.Compilation.Ir.Mates);
    }

    [Fact]
    public void HistoricalOcctStep_EmitsCurrentComponentPackageThatRecompiles()
    {
        var source = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/step242/OCCT/as1.step");
        var output = TempDirectory();
        try
        {
            var package = Step242FirmasmPackageImporter.Import(source, output);
            Assert.True(package.IsSuccess, string.Join(Environment.NewLine, package.Diagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.Equal(5, package.Value.Components.Count);
            Assert.Equal(27, package.Value.ProductStructure.Occurrences.Count);
            Assert.DoesNotContain("\"manifest\"", File.ReadAllText(package.Value.FirmasmPath), StringComparison.Ordinal);

            var compiled = new FirmamentAssemblyDocumentCompiler().CompileFile(package.Value.FirmasmPath);
            Assert.True(compiled.IsSuccess, string.Join(Environment.NewLine, compiled.Compilation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
            Assert.Equal(18, compiled.Compilation.Geometry!.Artifact.Instances.Count);
            Assert.All(compiled.Compilation.Ir!.Instances.Where(instance => instance.Kind == AssemblyInstanceKind.Part), instance => Assert.Equal(PlacementAuthority.ImportedOccurrence, instance.PlacementAuthority));
            Assert.Empty(compiled.Compilation.Ir.Mates);
        }
        finally { Directory.Delete(output, true); }
    }

    [Fact]
    public void AssemblyIr_NativeAp242RoundTripPreservesOccurrencesDefinitionsAndTransforms()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmasm/examples/occt-as1/as1-assembly.firmasm");
        var compiled = new FirmamentAssemblyDocumentCompiler().CompileFile(path).Compilation;
        var exported = AssemblyIrAp242Exporter.Export(compiled);
        Assert.True(exported.IsSuccess, string.Join(Environment.NewLine, exported.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var imported = Step242AssemblyImporter.Import(exported.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(18, imported.Value.Occurrences.Count);
        Assert.Equal(5, imported.Value.Definitions.Count(definition => definition.Geometry is not null));
        Assert.Equal(compiled.Ir!.Instances.Single(instance => instance.Path.ToString().EndsWith("part_004_plate_inst_001", StringComparison.Ordinal)).ResolvedTransform!.Matrix[12],
            imported.Value.Occurrences.Single(occurrence => occurrence.Name == "part_004_plate_inst_001").LocalTransform[12], 8);
    }

    [Fact]
    public void FirmasmProfile_RequiresExactlyOneRootAssembly()
    {
        Assert.Contains(FirmamentAssemblyDocumentCompiler.ValidateProfile("Record X { }", FirmamentDocumentProfile.Assembly), diagnostic => diagnostic.Code == "assembly-profile-no-root");
        Assert.Contains(FirmamentAssemblyDocumentCompiler.ValidateProfile("Assembly A { }\nAssembly B { }", FirmamentDocumentProfile.Assembly), diagnostic => diagnostic.Code == "assembly-profile-multiple-roots");
        Assert.Empty(FirmamentAssemblyDocumentCompiler.ValidateProfile("Assembly A { }", FirmamentDocumentProfile.Assembly));
        Assert.Empty(FirmamentAssemblyDocumentCompiler.ValidateProfile("Assembly A { }\nAssembly B { }", FirmamentDocumentProfile.General));
    }

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aetheris-m2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
}
