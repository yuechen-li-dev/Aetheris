using System.Diagnostics;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Step242;
using Xunit.Abstractions;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BenchmarkPerfSmokeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("benchmark_bahamut", "testdata/step242/benchmarks/BAHAMUT.step")]
    [InlineData("benchmark_ifrit", "testdata/step242/benchmarks/IFRIT.step")]
    [InlineData("benchmark_shiva", "testdata/step242/benchmarks/SHIVA.step")]
    public void BenchmarkFixtures_PerfSmoke_EmitStageMetrics(string fixtureId, string relativePath)
    {
        var root = Step242CorpusManifestRunner.RepoRoot();
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(fullPath, Encoding.UTF8);

        var parseImport = Measure(() => Step242Importer.ImportBody(text));
        Assert.True(parseImport.Result.IsSuccess);

        var validate = Measure(() => BrepBindingValidator.Validate(parseImport.Result.Value, requireAllEdgeAndFaceBindings: true));
        Assert.True(validate.Result.IsSuccess);

        var tessellate = Measure(() => BrepDisplayTessellator.Tessellate(parseImport.Result.Value));
        Assert.True(tessellate.Result.IsSuccess);

        var export = Measure(() => Step242Exporter.ExportBody(parseImport.Result.Value));
        Assert.True(export.Result.IsSuccess);

        output.WriteLine($"{fixtureId}: import={parseImport.ElapsedMs:F3}ms/{parseImport.AllocatedBytes}B, validate={validate.ElapsedMs:F3}ms/{validate.AllocatedBytes}B, tess={tessellate.ElapsedMs:F3}ms/{tessellate.AllocatedBytes}B, export={export.ElapsedMs:F3}ms/{export.AllocatedBytes}B");

        Assert.InRange(parseImport.ElapsedMs, 0, 15_000);
        Assert.InRange(validate.ElapsedMs, 0, 15_000);
        Assert.InRange(tessellate.ElapsedMs, 0, 15_000);
        Assert.InRange(export.ElapsedMs, 0, 15_000);
    }

    private static Perf<T> Measure<T>(Func<T> action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        var result = action();
        sw.Stop();
        var after = GC.GetAllocatedBytesForCurrentThread();
        return new Perf<T>(result, sw.Elapsed.TotalMilliseconds, Math.Max(0, after - before));
    }

    private sealed record Perf<T>(T Result, double ElapsedMs, long AllocatedBytes);
}
