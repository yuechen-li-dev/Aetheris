using System.Text;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242TessellationRobustnessTests
{
    [Fact]
    public void Step242_Tessellate_PlanarFace_AllowsMultipleLoops_UsesOuterOnly()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-face-with-hole-outer-and-inner.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("requires exactly one loop", StringComparison.Ordinal));

        var facePatch = Assert.Single(tessellation.Value.FacePatches);
        Assert.NotEmpty(facePatch.TriangleIndices);

        var warning = tessellation.Diagnostics.SingleOrDefault(d => string.Equals(d.Source, "Viewer.Tessellation.PlanarHolesIgnored", StringComparison.Ordinal));
        if (warning is not null)
        {
            Assert.Contains("ignored", warning.Message, StringComparison.Ordinal);
        }
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
