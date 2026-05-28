using System.Security.Cryptography;
using System.Text.Json;
using Xunit.Abstractions;

namespace Aetheris.CLI.Tests;

public sealed class AirChamferCorpusStabilityTests
{
    private const string RunArtifactCorpusTestsEnvironmentVariable = "AETHERIS_RUN_ARTIFACT_CORPUS_TESTS";
    private readonly ITestOutputHelper output;

    public AirChamferCorpusStabilityTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    [Trait("Category", "ArtifactCorpus")]
    public void AirChamferCorpusStability_Repeated_Cli_Runs_Produce_Stable_Json_Markers_Topology_And_Step_Hashes()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunArtifactCorpusTestsEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            output.WriteLine(
                $"Skipping gated EDGE-X12 AirChamfer artifact corpus stability check. Set {RunArtifactCorpusTestsEnvironmentVariable}=1 and filter by AirChamferCorpusStability or Category=ArtifactCorpus to run it explicitly.");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"edge-x12-airchamfer-corpus-stability-{Guid.NewGuid():N}");
        var firstDir = Path.Combine(root, "first");
        var secondDir = Path.Combine(root, "second");

        try
        {
            var first = GenerateAndSummarizeCorpus(firstDir);
            var second = GenerateAndSummarizeCorpus(secondDir);

            Assert.Equal(first.StableJsonSummaryJson, second.StableJsonSummaryJson);
            Assert.Equal(first.StepSha256ByArtifactFileName, second.StepSha256ByArtifactFileName);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static AirChamferCorpusStabilityRun GenerateAndSummarizeCorpus(string outputDir)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "airchamfer-corpus", "--out-dir", outputDir, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());

        var summaryPath = Path.Combine(outputDir, "edge-x11-airchamfer-corpus.json");
        Assert.True(File.Exists(summaryPath));

        using var stdoutJson = JsonDocument.Parse(stdout.ToString());
        using var fileJson = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Assert.Equal(stdoutJson.RootElement.GetProperty("corpusVersion").GetString(), fileJson.RootElement.GetProperty("corpusVersion").GetString());
        Assert.Equal(stdoutJson.RootElement.GetProperty("milestone").GetString(), fileJson.RootElement.GetProperty("milestone").GetString());
        Assert.Equal(stdoutJson.RootElement.GetProperty("cases").GetArrayLength(), fileJson.RootElement.GetProperty("cases").GetArrayLength());

        var stableSummary = AirChamferCorpusStabilitySummary.FromJson(stdoutJson.RootElement);
        Assert.Contains("edge-x11-legacy-authority-preserved", stableSummary.Diagnostics);
        Assert.Contains("edge-x11-no-production-route-replacement", stableSummary.Diagnostics);
        Assert.Contains("edge-x11-no-3d-boolean-used", stableSummary.Diagnostics);
        Assert.True(stableSummary.LegacyAuthorityPreserved);
        Assert.False(stableSummary.ProductionOutputChanged);
        Assert.True(stableSummary.NoProductionRouteReplacement);
        Assert.True(stableSummary.No3DBooleanUsed);

        var shaByArtifact = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var corpusCase in stableSummary.Cases.Where(c => string.Equals(c.Status, "succeeded", StringComparison.Ordinal)))
        {
            Assert.False(string.IsNullOrWhiteSpace(corpusCase.ArtifactFileName));
            var stepPath = Path.Combine(outputDir, corpusCase.ArtifactFileName!);
            Assert.True(File.Exists(stepPath), $"Expected STEP artifact '{stepPath}' to exist.");
            Assert.True(new FileInfo(stepPath).Length > 0, $"Expected STEP artifact '{stepPath}' to be non-empty.");

            var stepText = File.ReadAllText(stepPath);
            AssertStepMarkers(stepText);
            shaByArtifact.Add(corpusCase.ArtifactFileName!, Sha256(stepPath));
        }

        Assert.Equal(new[] { "edge-x11-airchamfer-cube-canonical.step", "edge-x11-airchamfer-cube-nonorthogonal.step" }, shaByArtifact.Keys.ToArray());

        return new AirChamferCorpusStabilityRun(JsonSerializer.Serialize(stableSummary), shaByArtifact);
    }

    private static void AssertStepMarkers(string stepText)
    {
        Assert.False(string.IsNullOrWhiteSpace(stepText));
        Assert.Contains("ISO-10303-21", stepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", stepText, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", stepText, StringComparison.Ordinal);
        Assert.Contains("PLANE", stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("CYLINDRICAL_SURFACE", stepText, StringComparison.Ordinal);
        Assert.DoesNotContain("BREP_WITH_VOIDS", stepText, StringComparison.Ordinal);
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record AirChamferCorpusStabilityRun(
        string StableJsonSummaryJson,
        SortedDictionary<string, string> StepSha256ByArtifactFileName);

    private sealed record AirChamferCorpusStabilitySummary(
        string CorpusVersion,
        string Milestone,
        string CandidatePath,
        string Route,
        bool LegacyAuthorityPreserved,
        bool ProductionOutputChanged,
        bool NoProductionRouteReplacement,
        bool No3DBooleanUsed,
        string[] Diagnostics,
        string[] Errors,
        AirChamferCorpusStabilityCaseSummary[] Cases)
    {
        public static AirChamferCorpusStabilitySummary FromJson(JsonElement root)
            => new(
                root.GetProperty("corpusVersion").GetString()!,
                root.GetProperty("milestone").GetString()!,
                root.GetProperty("candidatePath").GetString()!,
                root.GetProperty("route").GetString()!,
                root.GetProperty("legacyAuthorityPreserved").GetBoolean(),
                root.GetProperty("productionOutputChanged").GetBoolean(),
                root.GetProperty("noProductionRouteReplacement").GetBoolean(),
                root.GetProperty("no3DBooleanUsed").GetBoolean(),
                ReadStringArray(root.GetProperty("diagnostics")),
                ReadStringArray(root.GetProperty("errors")),
                root.GetProperty("cases")
                    .EnumerateArray()
                    .Select(AirChamferCorpusStabilityCaseSummary.FromJson)
                    .OrderBy(c => c.CaseName, StringComparer.Ordinal)
                    .ToArray());
    }

    private sealed record AirChamferCorpusStabilityCaseSummary(
        string CaseName,
        string Status,
        string? ArtifactFileName,
        string CandidatePath,
        string Route,
        bool? RequiredPresentSatisfied,
        bool? ForbiddenAbsentSatisfied,
        string? MarkerBooleansJson,
        string? TopologySummaryJson,
        bool LegacyAuthorityPreserved,
        bool ProductionOutputChanged,
        bool NoProductionRouteReplacement,
        bool No3DBooleanUsed,
        string[] Diagnostics,
        string[] Errors)
    {
        public static AirChamferCorpusStabilityCaseSummary FromJson(JsonElement element)
        {
            var stepMarkerSummary = element.GetProperty("stepMarkerSummary");
            string? markerBooleansJson = null;
            bool? requiredPresentSatisfied = null;
            bool? forbiddenAbsentSatisfied = null;
            if (stepMarkerSummary.ValueKind != JsonValueKind.Null)
            {
                requiredPresentSatisfied = stepMarkerSummary.GetProperty("requiredPresentSatisfied").GetBoolean();
                forbiddenAbsentSatisfied = stepMarkerSummary.GetProperty("forbiddenAbsentSatisfied").GetBoolean();
                markerBooleansJson = stepMarkerSummary.GetProperty("markers").GetRawText();
            }

            var topologySummary = element.GetProperty("topologySummary");
            return new AirChamferCorpusStabilityCaseSummary(
                element.GetProperty("caseName").GetString()!,
                element.GetProperty("status").GetString()!,
                ReadNullableString(element.GetProperty("artifactFileName")),
                element.GetProperty("candidatePath").GetString()!,
                element.GetProperty("route").GetString()!,
                requiredPresentSatisfied,
                forbiddenAbsentSatisfied,
                markerBooleansJson,
                topologySummary.ValueKind == JsonValueKind.Null ? null : topologySummary.GetRawText(),
                element.GetProperty("legacyAuthorityPreserved").GetBoolean(),
                element.GetProperty("productionOutputChanged").GetBoolean(),
                element.GetProperty("noProductionRouteReplacement").GetBoolean(),
                element.GetProperty("no3DBooleanUsed").GetBoolean(),
                ReadStringArray(element.GetProperty("diagnostics")),
                ReadStringArray(element.GetProperty("errors")));
        }
    }

    private static string[] ReadStringArray(JsonElement element)
        => element.EnumerateArray()
            .Select(x => x.GetString()!)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static string? ReadNullableString(JsonElement element)
        => element.ValueKind == JsonValueKind.Null ? null : element.GetString();
}
