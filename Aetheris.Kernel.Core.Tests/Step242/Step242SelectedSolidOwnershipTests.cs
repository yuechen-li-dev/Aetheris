using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242SelectedSolidOwnershipTests
{
    [Theory, Trait("Category", "SlowCorpus")]
    [InlineData("CTC/nist_ctc_02_asme1_ap242-e2.stp", 635)]
    [InlineData("CTC/nist_ctc_04_asme1_ap242-e1.stp", 483)]
    [InlineData("FTC/nist_ftc_07_asme1_ap242-e2.stp", 304)]
    [InlineData("FTC/nist_ftc_09_asme1_ap242-e1.stp", 158)]
    [InlineData("FTC/nist_ftc_10_asme1_ap242-e2.stp", 264)]
    [InlineData("STC/nist_stc_07_asme1_ap242-e3.stp", 304)]
    [InlineData("STC/nist_stc_08_asme1_ap242-e3.stp", 270)]
    public void MaterialFaces_EqualTheSelectedSourceClosedShell(string path, int expectedCount)
    {
        var relative = "testdata/step242/nist/" + path;
        var parsed = Step242SubsetParser.Parse(Step242Corpus.Text(relative)).Value;
        var root = Assert.Single(parsed.Entities, e => e.Name == "MANIFOLD_SOLID_BREP");
        var shellId = Step242SubsetDecoder.ReadEntityRefOrInlineConstructor(root, 1, "solid shell").Value.ReferenceId!.Value;
        var shell = parsed.TryGetEntity(shellId, "CLOSED_SHELL").Value;
        var faces = Step242SubsetDecoder.ReadReferenceList(shell, 1, "material faces").Value.Select(r => r.TargetId).Order().ToArray();
        Assert.Equal(expectedCount, faces.Length);
        var body = Step242Corpus.Body(relative);
        Assert.Equal(faces, body.Bindings.FaceBindings.Select(b => b.SourceStepEntityId!.Value).Order());
    }

    [Theory, Trait("Category", "SlowCorpus")]
    [InlineData(false)]
    [InlineData(true)]
    public void UnownedLegacyFace_RemainsCompatible_ButOwnedAuxiliaryFaceIsExcluded(bool owned)
    {
        const string path = "testdata/step242/nist/FTC/nist_ftc_09_asme1_ap242-e1.stp";
        var step = Step242Corpus.Text(path);
        // Policy-specific source variant deliberately removes the auxiliary shell
        // owner, retaining its planar face and all of its independent trim geometry.
        if (!owned)
        {
            const string owner = "#6072=OPEN_SHELL('',(#6064));";
            Assert.Contains(owner, step);
            step = step.Replace(owner, "#6072=OPEN_SHELL('',());");
        }
        var imported = owned ? Step242Corpus.Import(path) : Step242Importer.ImportBody(step);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(owned ? 158 : 159, imported.Value.Topology.Faces.Count());
        Assert.Equal(!owned, imported.Value.Bindings.FaceBindings.Any(b => b.SourceStepEntityId == 6064));
    }
}
