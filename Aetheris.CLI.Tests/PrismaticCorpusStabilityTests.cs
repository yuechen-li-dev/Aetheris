using System.Security.Cryptography;
using System.Text.Json;
using Xunit.Abstractions;

namespace Aetheris.CLI.Tests;

public sealed class PrismaticCorpusStabilityTests
{
    private const string RunArtifactCorpusTestsEnvironmentVariable = "AETHERIS_RUN_ARTIFACT_CORPUS_TESTS";
    private readonly ITestOutputHelper output;

    public PrismaticCorpusStabilityTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    [Trait("Category", "ArtifactCorpus")]
    public void PrismaticCorpusStability_Repeated_Cli_Runs_Produce_Stable_Json_Step_Hashes_Sections_And_Map_Blocker()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunArtifactCorpusTestsEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            output.WriteLine(
                $"Skipping gated EDGE-PRISMATIC-X6 prismatic artifact corpus stability and analyzer confirmation check. Set {RunArtifactCorpusTestsEnvironmentVariable}=1 and filter by PrismaticCorpusStability or Category=ArtifactCorpus to run it explicitly.");
            return;
        }

        output.WriteLine("edge-prismatic-x6-corpus-stability-started");

        var root = Path.Combine(Path.GetTempPath(), $"edge-prismatic-x6-corpus-stability-{Guid.NewGuid():N}");
        var firstDir = Path.Combine(root, "first");
        var secondDir = Path.Combine(root, "second");

        try
        {
            var first = GenerateAnalyzeAndSummarizeCorpus(firstDir, "first");
            var second = GenerateAnalyzeAndSummarizeCorpus(secondDir, "second");

            Assert.Equal(first.StableJsonSummaryJson, second.StableJsonSummaryJson);
            output.WriteLine("edge-prismatic-x6-json-stability-succeeded");

            Assert.Equal(first.StepSha256ByArtifactFileName, second.StepSha256ByArtifactFileName);
            output.WriteLine("edge-prismatic-x6-step-hash-stability-succeeded");

            Assert.Equal(first.StepNormalizedSummaryByArtifactFileName, second.StepNormalizedSummaryByArtifactFileName);
            output.WriteLine("edge-prismatic-x6-step-normalized-stability-succeeded");

            Assert.Equal(first.SectionAnalysisByCaseName, second.SectionAnalysisByCaseName);
            foreach (var caseName in first.SectionAnalysisByCaseName.Keys)
            {
                output.WriteLine($"edge-prismatic-x6-analyze-section-stability-succeeded:{caseName}");
            }

            Assert.Equal(first.MapAnalysisByCaseName, second.MapAnalysisByCaseName);
            foreach (var caseName in first.MapAnalysisByCaseName.Keys)
            {
                output.WriteLine($"edge-prismatic-x6-analyze-map-stability-succeeded:{caseName}");
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private PrismaticCorpusStabilityRun GenerateAnalyzeAndSummarizeCorpus(string outputDir, string runName)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-corpus", "--out-dir", outputDir, "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        output.WriteLine($"edge-prismatic-x6-corpus-run-completed:{runName}");

        var summaryPath = Path.Combine(outputDir, "edge-prismatic-x5-corpus.json");
        Assert.True(File.Exists(summaryPath));

        using var stdoutJson = JsonDocument.Parse(stdout.ToString());
        using var fileJson = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Assert.Equal(stdoutJson.RootElement.GetProperty("milestone").GetString(), fileJson.RootElement.GetProperty("milestone").GetString());
        Assert.Equal(stdoutJson.RootElement.GetProperty("cases").GetArrayLength(), fileJson.RootElement.GetProperty("cases").GetArrayLength());

        var stableSummary = PrismaticCorpusStabilitySummary.FromJson(stdoutJson.RootElement);
        Assert.Equal("EDGE-PRISMATIC-X5", stableSummary.Milestone);
        Assert.Equal("experimental", stableSummary.Route);
        Assert.Equal("prismatic-section-transition", stableSummary.TransitionRoute);
        Assert.Equal("PrismaticSectionTransitionEmitter", stableSummary.EmitterComponentName);
        Assert.Equal("preserve-section-splits", stableSummary.SplitPolicy);
        AssertGuarantees(stableSummary.Guarantees);
        Assert.Contains("edge-prismatic-x5-no-production-route-replacement", stableSummary.Diagnostics);
        Assert.Contains("edge-prismatic-x5-no-air-edge-sweep-used", stableSummary.Diagnostics);
        Assert.Contains("edge-prismatic-x5-no-brep-bounded-chamfer-used", stableSummary.Diagnostics);
        Assert.Contains("edge-prismatic-x5-no-topology-graft-used", stableSummary.Diagnostics);
        Assert.Contains("edge-prismatic-x5-no-3d-boolean-used", stableSummary.Diagnostics);
        Assert.Contains("edge-prismatic-x5-no-coplanar-merge-used", stableSummary.Diagnostics);
        output.WriteLine("edge-prismatic-x6-no-production-route-replacement");
        output.WriteLine("edge-prismatic-x6-no-air-edge-sweep-used");
        output.WriteLine("edge-prismatic-x6-no-brep-bounded-chamfer-used");
        output.WriteLine("edge-prismatic-x6-no-topology-graft-used");
        output.WriteLine("edge-prismatic-x6-no-3d-boolean-used");
        output.WriteLine("edge-prismatic-x6-no-coplanar-merge-used");

        var expectedCases = new[]
        {
            "arcs-deferred",
            "hexagon-scaled",
            "holes-deferred",
            "invalid-self-intersecting-profile",
            "mismatched-vertex-count",
            "missing-correspondence",
            "multiple-loops-deferred",
            "non-identity-correspondence",
            "non-increasing-sections",
            "pentagon-asymmetric",
            "pentagon-scaled",
            "rectangle-inset",
            "top-edge-chamfer",
        };
        Assert.Equal(expectedCases, stableSummary.Cases.Select(c => c.CaseName).ToArray());

        var shaByArtifact = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var normalizedByArtifact = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var corpusCase in stableSummary.Cases.Where(c => string.Equals(c.Status, "succeeded", StringComparison.Ordinal)))
        {
            Assert.False(string.IsNullOrWhiteSpace(corpusCase.ArtifactFileName));
            var stepPath = Path.Combine(outputDir, corpusCase.ArtifactFileName!);
            Assert.True(File.Exists(stepPath), $"Expected STEP artifact '{stepPath}' to exist.");
            Assert.True(new FileInfo(stepPath).Length > 0, $"Expected STEP artifact '{stepPath}' to be non-empty.");

            var stepText = File.ReadAllText(stepPath);
            AssertStepMarkers(stepText);
            shaByArtifact.Add(corpusCase.ArtifactFileName!, Sha256(stepPath));
            normalizedByArtifact.Add(corpusCase.ArtifactFileName!, JsonSerializer.Serialize(new StepNormalizedSummary(corpusCase.Status, corpusCase.ArtifactFileName, corpusCase.TopologySummaryJson, corpusCase.StepMarkerSummaryJson)));
        }

        Assert.Equal(
            new[]
            {
                "edge-prismatic-x5-hexagon-scaled.step",
                "edge-prismatic-x5-pentagon-asymmetric.step",
                "edge-prismatic-x5-pentagon-scaled.step",
                "edge-prismatic-x5-rectangle-inset.step",
                "edge-prismatic-x5-top-edge-chamfer.step",
            },
            shaByArtifact.Keys.ToArray());

        var sectionByCase = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["rectangle-inset"] = AnalyzeSection(outputDir, "rectangle-inset", "edge-prismatic-x5-rectangle-inset.step", "--xy", "0.5"),
            ["top-edge-chamfer"] = AnalyzeSection(outputDir, "top-edge-chamfer", "edge-prismatic-x5-top-edge-chamfer.step", "--xy", "5.5"),
            ["hexagon-scaled"] = AnalyzeSection(outputDir, "hexagon-scaled", "edge-prismatic-x5-hexagon-scaled.step", "--xy", "0.5"),
        };

        var mapByCase = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["top-edge-chamfer"] = AnalyzeMap(outputDir, "top-edge-chamfer", "edge-prismatic-x5-top-edge-chamfer.step"),
            ["hexagon-scaled"] = AnalyzeMap(outputDir, "hexagon-scaled", "edge-prismatic-x5-hexagon-scaled.step"),
        };

        return new PrismaticCorpusStabilityRun(
            JsonSerializer.Serialize(stableSummary),
            shaByArtifact,
            normalizedByArtifact,
            sectionByCase,
            mapByCase);
    }

    private string AnalyzeSection(string outputDir, string caseName, string artifactFileName, string plane, string offset)
    {
        var stepPath = Path.Combine(outputDir, artifactFileName);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "section", stepPath, plane, "--offset", offset, "--json"], stdout, stderr);
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());

        using var doc = JsonDocument.Parse(stdout.ToString());
        var summary = doc.RootElement.GetProperty("summary");
        Assert.True(summary.GetProperty("loopCount").GetInt32() > 0);
        Assert.True(summary.GetProperty("closedLoopCount").GetInt32() > 0);
        Assert.True(summary.GetProperty("lineSegmentCount").GetInt32() > 0);
        Assert.Equal(0, summary.GetProperty("unsupportedSegmentCount").GetInt32());
        output.WriteLine($"edge-prismatic-x6-analyze-section-succeeded:{caseName}");

        return JsonSerializer.Serialize(SectionProjection.FromJson(doc.RootElement));
    }

    private string AnalyzeMap(string outputDir, string caseName, string artifactFileName)
    {
        var stepPath = Path.Combine(outputDir, artifactFileName);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["analyze", "map", stepPath, "--top", "--rows", "16", "--cols", "16", "--json"], stdout, stderr);
        Assert.Equal(1, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());

        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("analysis-failure", root.GetProperty("errorKind").GetString());
        Assert.Contains("Orthographic map v1 currently supports bodies accepted by BrepSpatialQueries.Raycast", root.GetProperty("error").GetString(), StringComparison.Ordinal);
        output.WriteLine($"edge-prismatic-x6-analyze-map-blocker-confirmed:{caseName}");

        return JsonSerializer.Serialize(MapProjection.FromJson(root));
    }

    private static void AssertGuarantees(PrismaticGuarantees guarantees)
    {
        Assert.True(guarantees.NoProductionRouteReplacement);
        Assert.True(guarantees.NoAirEdgeSweep);
        Assert.True(guarantees.NoBrepBoundedChamfer);
        Assert.True(guarantees.NoTopologyGraft);
        Assert.True(guarantees.No3DBoolean);
        Assert.True(guarantees.NoCoplanarMerge);
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

    private sealed record PrismaticCorpusStabilityRun(
        string StableJsonSummaryJson,
        SortedDictionary<string, string> StepSha256ByArtifactFileName,
        SortedDictionary<string, string> StepNormalizedSummaryByArtifactFileName,
        SortedDictionary<string, string> SectionAnalysisByCaseName,
        SortedDictionary<string, string> MapAnalysisByCaseName);

    private sealed record PrismaticCorpusStabilitySummary(
        string? CorpusVersion,
        string Milestone,
        string Route,
        string TransitionRoute,
        string EmitterComponentName,
        string SplitPolicy,
        PrismaticCorpusStabilityCaseSummary[] Cases,
        string[] Diagnostics,
        string[] Errors,
        PrismaticGuarantees Guarantees)
    {
        public static PrismaticCorpusStabilitySummary FromJson(JsonElement root)
            => new(
                ReadOptionalString(root, "corpusVersion"),
                root.GetProperty("milestone").GetString()!,
                root.GetProperty("route").GetString()!,
                root.GetProperty("transitionRoute").GetString()!,
                root.GetProperty("emitterComponentName").GetString()!,
                root.GetProperty("splitPolicy").GetString()!,
                root.GetProperty("cases")
                    .EnumerateArray()
                    .Select(PrismaticCorpusStabilityCaseSummary.FromJson)
                    .OrderBy(c => c.CaseName, StringComparer.Ordinal)
                    .ToArray(),
                ReadStringArray(root.GetProperty("diagnostics")),
                ReadStringArray(root.GetProperty("errors")),
                PrismaticGuarantees.FromJson(root.GetProperty("guarantees")));
    }

    private sealed record PrismaticCorpusStabilityCaseSummary(
        string CaseName,
        string Status,
        string? ArtifactFileName,
        string Route,
        string TransitionRoute,
        string EmitterComponentName,
        string SplitPolicy,
        string? TopologySummaryJson,
        string? StepMarkerSummaryJson,
        string[] Diagnostics,
        string[] Errors,
        PrismaticGuarantees Guarantees)
    {
        public static PrismaticCorpusStabilityCaseSummary FromJson(JsonElement element)
            => new(
                element.GetProperty("caseName").GetString()!,
                element.GetProperty("status").GetString()!,
                ReadNullableString(element.GetProperty("artifactFileName")),
                element.GetProperty("route").GetString()!,
                element.GetProperty("transitionRoute").GetString()!,
                element.GetProperty("emitterComponentName").GetString()!,
                element.GetProperty("splitPolicy").GetString()!,
                ReadNullableRawText(element.GetProperty("topologySummary")),
                ReadNullableRawText(element.GetProperty("stepMarkerSummary")),
                ReadStringArray(element.GetProperty("diagnostics")),
                ReadStringArray(element.GetProperty("errors")),
                PrismaticGuarantees.FromJson(element.GetProperty("guarantees")));
    }

    private sealed record PrismaticGuarantees(
        bool NoProductionRouteReplacement,
        bool NoAirEdgeSweep,
        bool NoBrepBoundedChamfer,
        bool NoTopologyGraft,
        bool No3DBoolean,
        bool NoCoplanarMerge)
    {
        public static PrismaticGuarantees FromJson(JsonElement element)
            => new(
                element.GetProperty("noProductionRouteReplacement").GetBoolean(),
                element.GetProperty("noAirEdgeSweep").GetBoolean(),
                element.GetProperty("noBrepBoundedChamfer").GetBoolean(),
                element.GetProperty("noTopologyGraft").GetBoolean(),
                element.GetProperty("no3DBoolean").GetBoolean(),
                element.GetProperty("noCoplanarMerge").GetBoolean());
    }

    private sealed record StepNormalizedSummary(string Status, string? ArtifactFileName, string? TopologySummaryJson, string? StepMarkerSummaryJson);

    private sealed record SectionProjection(
        string PlaneFamily,
        double Offset,
        string OffsetAxis,
        string SectionAxisU,
        string SectionAxisV,
        string BoundingBoxJson,
        int LoopCount,
        int ClosedLoopCount,
        int LineSegmentCount,
        int ArcSegmentCount,
        int UnsupportedSegmentCount,
        string? SectionBoundingBox2DJson)
    {
        public static SectionProjection FromJson(JsonElement root)
        {
            var metadata = root.GetProperty("metadata");
            var summary = root.GetProperty("summary");
            var sectionBounds = summary.GetProperty("sectionBoundingBox2D");
            return new SectionProjection(
                metadata.GetProperty("planeFamily").GetString()!,
                metadata.GetProperty("offset").GetDouble(),
                metadata.GetProperty("offsetAxis").GetString()!,
                metadata.GetProperty("sectionAxisU").GetString()!,
                metadata.GetProperty("sectionAxisV").GetString()!,
                metadata.GetProperty("boundingBox").GetRawText(),
                summary.GetProperty("loopCount").GetInt32(),
                summary.GetProperty("closedLoopCount").GetInt32(),
                summary.GetProperty("lineSegmentCount").GetInt32(),
                summary.GetProperty("arcSegmentCount").GetInt32(),
                summary.GetProperty("unsupportedSegmentCount").GetInt32(),
                sectionBounds.ValueKind == JsonValueKind.Null ? null : sectionBounds.GetRawText());
        }
    }

    private sealed record MapProjection(bool Success, string ErrorKind, string Error)
    {
        public static MapProjection FromJson(JsonElement root)
            => new(
                root.GetProperty("success").GetBoolean(),
                root.GetProperty("errorKind").GetString()!,
                root.GetProperty("error").GetString()!);
    }

    private static string[] ReadStringArray(JsonElement element)
        => element.EnumerateArray()
            .Select(x => x.GetString()!)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static string? ReadNullableString(JsonElement element)
        => element.ValueKind == JsonValueKind.Null ? null : element.GetString();

    private static string? ReadOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;

    private static string? ReadNullableRawText(JsonElement element)
        => element.ValueKind == JsonValueKind.Null ? null : element.GetRawText();
}
