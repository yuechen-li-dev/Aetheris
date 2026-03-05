using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BenchmarkFixturesTests
{
    public static IEnumerable<object[]> FixturePaths()
    {
        yield return new object[] { "benchmark_bahamut", "testdata/step242/benchmarks/BAHAMUT.step" };
        yield return new object[] { "benchmark_ifrit", "testdata/step242/benchmarks/IFRIT.step" };
        yield return new object[] { "benchmark_shiva", "testdata/step242/benchmarks/SHIVA.step" };
    }

    [Theory]
    [MemberData(nameof(FixturePaths))]
    public void BenchmarkFixtures_RunFullPipeline_AndRoundTripStable(string fixtureId, string relativePath)
    {
        var entry = new Step242CorpusManifestEntry(fixtureId, relativePath, "passRequired", "benchmark", null, true, null, null);
        var report1 = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal("success", report1.Status);
        Assert.NotNull(report1.CanonicalSha256);
        Assert.NotNull(report1.ExportedCanonicalText);

        var import2 = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(report1.ExportedCanonicalText!);
        Assert.True(import2.IsSuccess);

        var validate2 = BrepBindingValidator.Validate(import2.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validate2.IsSuccess);

        var tess2 = BrepDisplayTessellator.Tessellate(import2.Value);
        Assert.True(tess2.IsSuccess);
        Assert.True(tess2.Value.FacePatches.Count > 0);
        Assert.True(tess2.Value.Indices.Count > 0);

        var report2 = Step242CorpusManifestRunner.RunOne(entry);
        Assert.Equal(report1.CanonicalSha256, report2.CanonicalSha256);
        Assert.Equal(report1.ExportedCanonicalText, report2.ExportedCanonicalText);
    }
}
