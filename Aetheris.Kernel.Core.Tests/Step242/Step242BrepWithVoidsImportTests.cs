using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BrepWithVoidsImportTests
{
    [Fact]
    public void Import_BrepWithVoids_MissingVoidShell_FailsClearly()
    {
        const string missingVoid = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=BREP_WITH_VOIDS('v',#2,());\n#2=CLOSED_SHELL($,());\nENDSEC;\nEND-ISO-10303-21;";
        var import = Step242Importer.ImportBody(missingVoid);
        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal("Importer.TopologyRoot.BrepWithVoids", diagnostic.Source);
        Assert.Contains("at least one void shell", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_BrepWithVoids_MinimalOuterAndVoidShells_PreservesShellRepresentation()
    {
        var fixturePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "firmament", "exports", "boolean_box_sphere_cavity_basic.step");
        var stepText = File.ReadAllText(fixturePath);
        Assert.Contains("BREP_WITH_VOIDS", stepText, StringComparison.Ordinal);

        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.NotNull(import.Value.ShellRepresentation);
        Assert.Single(import.Value.Topology.Bodies);
        Assert.Equal(2, import.Value.Topology.Shells.Count());
        Assert.Single(import.Value.ShellRepresentation!.InnerShellIds);
    }
}
