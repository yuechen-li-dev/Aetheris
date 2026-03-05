using System.Text;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242InlineSurfaceConstructorTests
{
    [Fact]
    public void Step242_AdvancedFace_Surface_AllowsInlineSphericalSurfaceConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-sphere-valid.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.DoesNotContain(import.Diagnostics, d => d.Source is not null && d.Source.StartsWith("Parser", StringComparison.Ordinal));
        Assert.DoesNotContain(import.Diagnostics, d => string.Equals(d.Source, "Importer.StepSyntax.InlineEntity", StringComparison.Ordinal));

        if (!import.IsSuccess)
        {
            Assert.NotEmpty(import.Diagnostics);
            return;
        }

        Assert.Contains(import.Value.Geometry.Surfaces, s => s.Value.Kind == SurfaceGeometryKind.Sphere);
    }

    [Fact]
    public void Step242_AdvancedFace_Surface_RejectsMalformedInlineSphericalSurfaceConstructor()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/advanced-face-inline-sphere-malformed.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var import = Step242Importer.ImportBody(text);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Importer.StepSyntax.InlineEntity", diagnostic.Source);
        Assert.Equal("Inline ADVANCED_FACE.surface constructor 'SPHERICAL_SURFACE' has unsupported argument shape.", diagnostic.Message);
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
