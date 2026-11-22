using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BrepWithVoidsImportTests
{
    [Fact]
    public void Import_BrepWithVoids_MissingVoidShell_FailsClearly()
    {
        const string missingVoid = "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF'));\nENDSEC;\nDATA;\n#1=BREP_WITH_VOIDS('v',#2,());\n#2=CLOSED_SHELL($,());\nENDSEC;\nEND-ISO-10303-21;";
        var import = Step242Importer.ImportBody(missingVoid);
        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal("Importer.TopologyRoot.BrepWithVoids", diagnostic.Source);
        Assert.Contains("at least one void shell", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Import_BrepWithVoids_MinimalOuterAndVoidShells_PreservesShellRepresentation(bool explicitDerivedFaces)
    {
        const string fixturePath = "testdata/step242/golden/firmament-v1/boolean_box_sphere_cavity_basic.step";
        var stepText = Step242Corpus.Text(fixturePath);
        Assert.Contains("BREP_WITH_VOIDS", stepText, StringComparison.Ordinal);
        if (explicitDerivedFaces)
        {
            Assert.Contains("ORIENTED_CLOSED_SHELL($,", stepText, StringComparison.Ordinal);
            stepText = stepText.Replace("ORIENTED_CLOSED_SHELL($,", "ORIENTED_CLOSED_SHELL('',*,", StringComparison.Ordinal);
        }

        var import = explicitDerivedFaces ? Step242Importer.ImportBody(stepText) : Step242Corpus.Import(fixturePath);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.NotNull(import.Value.ShellRepresentation);
        Assert.Single(import.Value.Topology.Bodies);
        Assert.Equal(2, import.Value.Topology.Shells.Count());
        Assert.Single(import.Value.ShellRepresentation!.InnerShellIds);
    }
}
