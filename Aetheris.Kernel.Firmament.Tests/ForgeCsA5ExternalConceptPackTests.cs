using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ForgeCsA5ExternalConceptPackTests
{
    [Fact]
    public void ForgeCsA5_WithoutExternalPack_UnknownExternalConceptRemainsInvalid()
    {
        var parse = ParseFixture("Language/invalid/concept-external-boss-hole.invalid.firmfixture");

        Assert.NotNull(parse.Document);
        Assert.Contains(FirmamentV2Parser.ConceptUnknownConcept, parse.Diagnostics);
    }

    [Fact]
    public void ForgeCsA5_WithExternalPack_ParserAndRuntimeAcceptExternalConcept()
    {
        var configuration = CreateExternalRuntimeConfiguration(typeof(Aetheris.TestForgePack.TestForgePack).Assembly.Location);
        var path = FixturePath("Language/invalid/concept-external-boss-hole.invalid.firmfixture");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path), Path.GetDirectoryName(path), configuration.Catalog);

        Assert.True(parse.IsSuccess);
        Assert.DoesNotContain(FirmamentV2Parser.ConceptUnknownConcept, parse.Diagnostics);

        var runtime = FirmamentV2RuntimeConceptValidation.Validate(parse.Document, configuration);
        Assert.Equal("Aetheris.Standard", runtime.ForgeRuntime.BuiltInPack);
        var externalPack = Assert.Single(runtime.ForgeRuntime.ExternalPacks);
        Assert.Equal("Aetheris.TestForgePack", externalPack.Id);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "testforge.boss-hole.seen");

        var report = FirmamentV2ValidationReportBuilder.Build(parse, path, runtime, configuration.Catalog);
        Assert.Equal("valid", report.Status);
        Assert.Equal("Aetheris.Standard", report.ForgeRuntime.BuiltInPack);
        Assert.Single(report.ForgeRuntime.ExternalPacks, pack => pack.Id == "Aetheris.TestForgePack");
        var concept = Assert.Single(report.Concepts);
        Assert.Equal("Aetheris.TestForgePack", concept.RuntimeValidation!.Provider);
        Assert.Contains(concept.RuntimeValidation.Diagnostics, diagnostic => diagnostic.Code == "testforge.boss-hole.seen");
    }

    [Fact]
    public void ForgeCsA5_DuplicateConceptRegistration_IsDeterministic()
    {
        var loader = new ForgeConceptPackAssemblyLoader();
        var duplicateAssembly = Path.Combine(AppContext.BaseDirectory, "Aetheris.TestForgePack.Duplicate.dll");
        var packs = loader.LoadFromAssemblyPath(duplicateAssembly)
            .Select(pack => (pack, AssemblyPath: duplicateAssembly))
            .ToArray();

        var exception = Assert.Throws<InvalidOperationException>(() => FirmamentV2ForgeRuntimeConfiguration.Create(packs));

        Assert.Contains("duplicate concept 'hole<Countersink>'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForgeCsA5_DefaultReport_RemainsBuiltInOnly()
    {
        var report = BuildFixture("Language/valid/concept-applications-forge.valid.firmfixture");

        Assert.Equal("Aetheris.Standard", report.ForgeRuntime.BuiltInPack);
        Assert.Empty(report.ForgeRuntime.ExternalPacks);
    }

    private static FirmamentV2ValidationReport BuildFixture(string relative)
    {
        var path = FixturePath(relative);
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(path), Path.GetDirectoryName(path));
        Assert.NotNull(parse.Document);
        return FirmamentV2ValidationReportBuilder.Build(parse, path);
    }

    private static FirmamentV2ParseResult ParseFixture(string relative)
    {
        var path = FixturePath(relative);
        return FirmamentV2Parser.Parse(File.ReadAllText(path), Path.GetDirectoryName(path));
    }

    private static FirmamentV2ForgeRuntimeConfiguration CreateExternalRuntimeConfiguration(string assemblyPath)
    {
        var loader = new ForgeConceptPackAssemblyLoader();
        var packs = loader.LoadFromAssemblyPath(assemblyPath)
            .Select(pack => (pack, AssemblyPath: assemblyPath))
            .ToArray();
        return FirmamentV2ForgeRuntimeConfiguration.Create(packs);
    }

    private static string FixturePath(string relative) => Path.Combine(FindRepoRoot(), "fixtures", relative);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aetheris.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }
}
