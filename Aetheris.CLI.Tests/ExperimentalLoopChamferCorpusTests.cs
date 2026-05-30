using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ExperimentalLoopChamferCorpusTests
{
    [Fact]
    public void Experimental_Loop_Chamfer_Corpus_Help_Is_Discoverable_And_Explicitly_Lab_Only()
    {
        var topStdout = new StringWriter();
        var topStderr = new StringWriter();
        var topExit = Aetheris.CLI.CliRunner.Run(["experimental", "--help"], topStdout, topStderr);

        Assert.Equal(0, topExit);
        Assert.Contains("loop-chamfer-corpus", topStdout.ToString(), StringComparison.Ordinal);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "loop-chamfer-corpus", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Usage: aetheris experimental loop-chamfer-corpus --out-dir <dir> [--json]", text, StringComparison.Ordinal);
        Assert.Contains("EDGE-LOOP-X2", text, StringComparison.Ordinal);
        Assert.Contains("Experimental/lab-only", text, StringComparison.Ordinal);
        Assert.Contains("no production route replacement", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("production chamfer/fillet behavior change", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Experimental_Loop_Chamfer_Corpus_Writes_Steps_Json_Diagnostics_And_Rejections()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"edge-loop-x2-corpus-{Guid.NewGuid():N}");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "loop-chamfer-corpus", "--out-dir", outputDir, "--json"], stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

            var expectedArtifacts = new[]
            {
                "edge-loop-x2-canonical-top-face-loop-chamfer.step",
                "edge-loop-x2-larger-top-face-loop-chamfer.step",
                "edge-loop-x2-non-square-top-face-loop-chamfer.step",
            };

            foreach (var artifact in expectedArtifacts)
            {
                var path = Path.Combine(outputDir, artifact);
                Assert.True(File.Exists(path), artifact);
                Assert.True(new FileInfo(path).Length > 0, artifact);
                AssertStepMarkers(File.ReadAllText(path));
            }

            var summaryPath = Path.Combine(outputDir, "edge-loop-x2-corpus.json");
            Assert.True(File.Exists(summaryPath));
            using var stdoutDoc = JsonDocument.Parse(stdout.ToString());
            using var fileDoc = JsonDocument.Parse(File.ReadAllText(summaryPath));
            Assert.Equal(stdoutDoc.RootElement.GetProperty("milestone").GetString(), fileDoc.RootElement.GetProperty("milestone").GetString());

            var root = stdoutDoc.RootElement;
            Assert.Equal("EDGE-LOOP-X2", root.GetProperty("milestone").GetString());
            Assert.Contains("aetheris experimental loop-chamfer-corpus", root.GetProperty("corpusRoute").GetString(), StringComparison.Ordinal);
            Assert.Equal("prismatic-section-transition", root.GetProperty("constructionRoute").GetString());
            Assert.Equal("preserve-section-splits", root.GetProperty("splitPolicy").GetString());
            AssertGuarantees(root.GetProperty("guarantees"));

            var cases = root.GetProperty("cases").EnumerateArray().ToDictionary(x => x.GetProperty("caseName").GetString()!);
            Assert.Equal("succeeded", cases["canonical-top-face-loop-chamfer"].GetProperty("status").GetString());
            Assert.Equal("succeeded", cases["larger-top-face-loop-chamfer"].GetProperty("status").GetString());
            Assert.Equal("succeeded", cases["non-square-top-face-loop-chamfer"].GetProperty("status").GetString());

            AssertLoopTopology(cases["canonical-top-face-loop-chamfer"], "[-5,-4,0]..[5,4,6]");
            AssertLoopTopology(cases["larger-top-face-loop-chamfer"], "[-5,-4,0]..[5,4,6]");
            AssertLoopTopology(cases["non-square-top-face-loop-chamfer"], "[-6,-2.5,0]..[6,2.5,7]");

            var rejectedOrDeferred = new[]
            {
                "invalid-zero-chamfer-distance",
                "invalid-negative-chamfer-distance",
                "too-large-chamfer-distance",
                "invalid-width",
                "invalid-depth",
                "invalid-height",
                "non-finite-dimensions",
                "non-uniform-rule-rejected",
                "arbitrary-graph-rejected",
                "open-chain-deferred",
                "non-closed-loop-rejected",
                "non-outer-loop-deferred",
                "non-planar-owning-face-deferred",
                "inset-self-intersection-risk",
            };

            foreach (var name in rejectedOrDeferred)
            {
                Assert.True(cases.ContainsKey(name), name);
                Assert.Contains(cases[name].GetProperty("status").GetString(), new[] { "rejected", "deferred" });
                Assert.Equal(JsonValueKind.Null, cases[name].GetProperty("artifactPath").ValueKind);
                Assert.Equal(JsonValueKind.Null, cases[name].GetProperty("artifactFileName").ValueKind);
                Assert.Equal(JsonValueKind.Null, cases[name].GetProperty("stepMarkerSummary").ValueKind);
                var caseDiagnostics = cases[name].GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
                Assert.Contains(caseDiagnostics, d => d!.StartsWith($"edge-loop-x2-case-{cases[name].GetProperty("status").GetString()}:{name}:", StringComparison.Ordinal));
            }

            Assert.Equal("rejected", cases["non-uniform-rule-rejected"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["arbitrary-graph-rejected"].GetProperty("status").GetString());
            Assert.Equal("deferred", cases["open-chain-deferred"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["non-closed-loop-rejected"].GetProperty("status").GetString());
            Assert.Equal("deferred", cases["non-outer-loop-deferred"].GetProperty("status").GetString());
            Assert.Equal("deferred", cases["non-planar-owning-face-deferred"].GetProperty("status").GetString());

            foreach (var name in new[] { "canonical-top-face-loop-chamfer", "larger-top-face-loop-chamfer", "non-square-top-face-loop-chamfer" })
            {
                var c = cases[name];
                Assert.Equal("Class B / face-boundary loop", c.GetProperty("selectionClass").GetString());
                Assert.Equal("prismatic-section-transition", c.GetProperty("constructionRoute").GetString());
                Assert.Equal("preserve-section-splits", c.GetProperty("splitPolicy").GetString());
                AssertGuarantees(c.GetProperty("guarantees"));
                var loop = c.GetProperty("loopSelectionSummary");
                Assert.Equal("top cap", loop.GetProperty("owningFace").GetString());
                Assert.Equal("outer", loop.GetProperty("loopKind").GetString());
                Assert.True(loop.GetProperty("closed").GetBoolean());
                Assert.Equal(4, loop.GetProperty("edgeCount").GetInt32());
                Assert.True(loop.GetProperty("ordered").GetBoolean());
                var rule = c.GetProperty("ruleSummary");
                Assert.Equal("uniform symmetric chamfer", rule.GetProperty("rule").GetString());
                Assert.True(rule.GetProperty("distance").GetDouble() > 0);
                var markers = c.GetProperty("stepMarkerSummary");
                Assert.True(markers.GetProperty("requiredMarkersPresent").GetBoolean());
                Assert.True(markers.GetProperty("forbiddenMarkersAbsent").GetBoolean());
                var diagnostics = c.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
                Assert.Contains($"edge-loop-x2-not-four-independent-single-edge-chamfers:{name}", diagnostics);
                Assert.Contains($"edge-loop-x2-split-preserving-topology-validated:{name}", diagnostics);
                Assert.Contains($"edge-loop-x2-step-smoke-succeeded:{name}", diagnostics);
            }

            var rootDiagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Contains("edge-loop-x2-no-production-route-replacement", rootDiagnostics);
            Assert.Contains("edge-loop-x2-no-air-edge-sweep-used", rootDiagnostics);
            Assert.Contains("edge-loop-x2-no-brep-bounded-chamfer-used", rootDiagnostics);
            Assert.Contains("edge-loop-x2-no-topology-graft-used", rootDiagnostics);
            Assert.Contains("edge-loop-x2-no-3d-boolean-used", rootDiagnostics);
            Assert.Contains("edge-loop-x2-no-coplanar-merge-used", rootDiagnostics);
            Assert.Contains("edge-loop-x2-json-summary-written", rootDiagnostics);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static void AssertLoopTopology(JsonElement corpusCase, string bounds)
    {
        var topology = corpusCase.GetProperty("topologySummary");
        Assert.True(topology.GetProperty("bodyProduced").GetBoolean());
        Assert.Equal(3, topology.GetProperty("sectionCount").GetInt32());
        Assert.Equal(12, topology.GetProperty("vertexCount").GetInt32());
        Assert.Equal(20, topology.GetProperty("edgeCount").GetInt32());
        Assert.Equal(10, topology.GetProperty("faceCount").GetInt32());
        Assert.Equal(10, topology.GetProperty("planarFaceCount").GetInt32());
        Assert.Equal(0, topology.GetProperty("cylindricalFaceCount").GetInt32());
        Assert.Equal(2, topology.GetProperty("capFaceCount").GetInt32());
        Assert.Equal(4, topology.GetProperty("lowerPrismSideFaceCount").GetInt32());
        Assert.Equal(4, topology.GetProperty("transitionFaceCount").GetInt32());
        Assert.Equal(4, topology.GetProperty("chamferTransitionFaceCount").GetInt32());
        Assert.Equal(10, topology.GetProperty("loopCount").GetInt32());
        Assert.Equal(40, topology.GetProperty("coedgeCount").GetInt32());
        Assert.Equal(bounds, topology.GetProperty("bounds").GetString());
    }

    private static void AssertGuarantees(JsonElement guarantees)
    {
        Assert.True(guarantees.GetProperty("noProductionRouteReplacement").GetBoolean());
        Assert.True(guarantees.GetProperty("noAirEdgeSweep").GetBoolean());
        Assert.True(guarantees.GetProperty("noBrepBoundedChamfer").GetBoolean());
        Assert.True(guarantees.GetProperty("noTopologyGraft").GetBoolean());
        Assert.True(guarantees.GetProperty("no3DBoolean").GetBoolean());
        Assert.True(guarantees.GetProperty("noCoplanarMerge").GetBoolean());
        Assert.True(guarantees.GetProperty("notFourIndependentSingleEdgeChamfers").GetBoolean());
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
}
