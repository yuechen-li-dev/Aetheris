using System.Diagnostics;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242Ftc07ViewMaterializationRegressionTests
{
    private static readonly TimeSpan Ftc07TessellationBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Ftc07WallClockGuard = TimeSpan.FromSeconds(30);
    private const string RelativePath = "testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp";

    [Fact]
    public void Step242Ftc07ViewMaterialization_CompletesOrReportsBoundedDiagnostic()
    {
        var body = ImportFixture();

        var stopwatch = Stopwatch.StartNew();
        var tessellation = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: Ftc07TessellationBudget);
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < Ftc07WallClockGuard,
            $"FTC-07 display tessellation exceeded the bounded wall-clock guard ({stopwatch.Elapsed}); " +
            $"tessellation budget was {Ftc07TessellationBudget}; diagnostics: {FormatDiagnostics(tessellation.Diagnostics)}");

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

        var tessellation = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: Ftc07TessellationBudget);
        if (tessellation.IsSuccess)
        {
            return;
        }

        Assert.All(tessellation.Diagnostics, diagnostic =>
        {
            Assert.Contains("face", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("phase", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("surface", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(tessellation.Diagnostics, diagnostic =>
            string.Equals(diagnostic.Source, "Viewer.Tessellation.Timeout", StringComparison.Ordinal)
            || diagnostic.Source?.StartsWith("Viewer.PlanarTriangulation.", StringComparison.Ordinal) == true);
    }


    [Fact]
    public void DisplayPrepare_Ftc07_ReturnsPartialDisplayInsteadOfWholeBodyFailure()
    {
        var body = ImportFixture();

        var partial = BrepDisplayTessellator.TessellateBoundedPartial(body, executionTimeout: Ftc07TessellationBudget);

        Assert.NotEmpty(partial.FacePatches);
        if ((partial.FaceDiagnostics ?? []).Count == 0)
        {
            return;
        }

        Assert.Contains(partial.FaceDiagnostics ?? [], diagnostic =>
            diagnostic.Code == "Viewer.Tessellation.Timeout"
            || diagnostic.Code.StartsWith("Viewer.PlanarTriangulation.", StringComparison.Ordinal));
        Assert.Contains(partial.FaceDiagnostics ?? [], diagnostic =>
            diagnostic.Phase is "PlanarTriangulationWithHoles" or "PlanarLoopClassification" or "FaceTessellation" or "TrimLoopSampling" or "FaceDispatch");
    }

    private static string FormatDiagnostics(IReadOnlyList<global::Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic> diagnostics)
        => diagnostics.Count == 0
            ? "<none>"
            : string.Join(" | ", diagnostics.Select(diagnostic => $"{diagnostic.Source ?? diagnostic.Code.ToString()}: {diagnostic.Message}"));

    private static global::Aetheris.Kernel.Core.Brep.BrepBody ImportFixture()
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var import = Step242Importer.ImportBody(File.ReadAllText(absolutePath));
        Assert.True(import.IsSuccess);
        return import.Value;
    }
}
