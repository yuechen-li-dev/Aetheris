using System.Diagnostics;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242Ftc07ViewMaterializationRegressionTests
{
    private const string RelativePath = "testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp";

    [Fact]
    public void Step242Ftc07ViewMaterialization_CompletesOrReportsBoundedDiagnostic()
    {
        var body = ImportFixture();

        var stopwatch = Stopwatch.StartNew();
        var tessellation = BrepDisplayTessellator.TessellateBounded(body);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"FTC-07 display tessellation exceeded the bounded wall clock budget ({stopwatch.Elapsed}).");

        if (tessellation.IsSuccess)
        {
            Assert.NotEmpty(tessellation.Value.FacePatches);
            return;
        }

        Assert.Contains(tessellation.Diagnostics, diagnostic => string.Equals(diagnostic.Source, "Viewer.Tessellation.Timeout", StringComparison.Ordinal));
    }

    [Fact]
    public void Step242Ftc07ViewMaterialization_ReportsPhaseAndFaceOnFailure()
    {
        var body = ImportFixture();

        var tessellation = BrepDisplayTessellator.TessellateBounded(body);
        if (tessellation.IsSuccess)
        {
            return;
        }

        var diagnostic = Assert.Single(tessellation.Diagnostics, candidate => string.Equals(candidate.Source, "Viewer.Tessellation.Timeout", StringComparison.Ordinal));
        Assert.Contains("face", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("phase", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("surface", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            diagnostic.Message.Contains("PlanarTriangulationWithHoles", StringComparison.Ordinal)
            || diagnostic.Message.Contains("FaceTessellation", StringComparison.Ordinal));
    }

    private static global::Aetheris.Kernel.Core.Brep.BrepBody ImportFixture()
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var import = Step242Importer.ImportBody(File.ReadAllText(absolutePath));
        Assert.True(import.IsSuccess);
        return import.Value;
    }
}
