using System.Text;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
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

    [Fact]
    public void Step242_Tessellate_PlanarFace_NonConvexSingleLoop_SucceedsAndProducesTriangles()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-nonconvex-single-loop.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.True(tessellation.IsSuccess);

        var facePatch = Assert.Single(tessellation.Value.FacePatches);
        Assert.True(facePatch.TriangleIndices.Count > 0);
        Assert.DoesNotContain(tessellation.Diagnostics, d => d.Message.Contains("requires a convex polygon", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242_Tessellate_PlanarFace_DegenerateLoop_FailsDeterministically()
    {
        var text = LoadFixture("testdata/step242/tessellation-robustness/planar-degenerate-loop.step");

        var import = Step242Importer.ImportBody(text);

        Assert.True(import.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);

        Assert.False(tessellation.IsSuccess);

        var diagnostic = Assert.Single(tessellation.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal("Viewer.Tessellation.PlanarPolygonDegenerate", diagnostic.Source);
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
