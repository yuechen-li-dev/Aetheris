using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ExperimentalPrismaticCorpusTests
{
    [Fact]
    public void Experimental_Prismatic_Corpus_Help_Is_Discoverable_And_Explicitly_Lab_Only()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-corpus", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Usage: aetheris experimental prismatic-corpus --out-dir <dir> [--json]", text, StringComparison.Ordinal);
        Assert.Contains("EDGE-PRISMATIC-X5", text, StringComparison.Ordinal);
        Assert.Contains("Experimental/lab-only", text, StringComparison.Ordinal);
        Assert.Contains("no production route replacement", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no coplanar merge", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Experimental_Prismatic_Corpus_Writes_Split_Preserving_Steps_Json_And_Diagnostics()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"edge-prismatic-x5-corpus-{Guid.NewGuid():N}");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-corpus", "--out-dir", outputDir, "--json"], stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));

            var expectedArtifacts = new[]
            {
                "edge-prismatic-x5-rectangle-inset.step",
                "edge-prismatic-x5-top-edge-chamfer.step",
                "edge-prismatic-x5-pentagon-scaled.step",
                "edge-prismatic-x5-hexagon-scaled.step",
                "edge-prismatic-x5-pentagon-asymmetric.step",
            };

            foreach (var artifact in expectedArtifacts)
            {
                var path = Path.Combine(outputDir, artifact);
                Assert.True(File.Exists(path), artifact);
                Assert.True(new FileInfo(path).Length > 0, artifact);
                AssertStepMarkers(File.ReadAllText(path));
            }

            Assert.False(File.Exists(Path.Combine(outputDir, "edge-prismatic-x5-mismatched-vertex-count.step")));
            Assert.False(File.Exists(Path.Combine(outputDir, "edge-prismatic-x5-holes-deferred.step")));
            var summaryPath = Path.Combine(outputDir, "edge-prismatic-x5-corpus.json");
            Assert.True(File.Exists(summaryPath));

            using var stdoutDoc = JsonDocument.Parse(stdout.ToString());
            using var fileDoc = JsonDocument.Parse(File.ReadAllText(summaryPath));
            Assert.Equal(stdoutDoc.RootElement.GetProperty("milestone").GetString(), fileDoc.RootElement.GetProperty("milestone").GetString());

            var root = stdoutDoc.RootElement;
            Assert.Equal("EDGE-PRISMATIC-X5", root.GetProperty("milestone").GetString());
            Assert.Equal("experimental", root.GetProperty("route").GetString());
            Assert.Equal("prismatic-section-transition", root.GetProperty("transitionRoute").GetString());
            Assert.Equal("PrismaticSectionTransitionEmitter", root.GetProperty("emitterComponentName").GetString());
            Assert.Equal("preserve-section-splits", root.GetProperty("splitPolicy").GetString());

            AssertGuarantees(root.GetProperty("guarantees"));

            var cases = root.GetProperty("cases").EnumerateArray().ToDictionary(x => x.GetProperty("caseName").GetString()!);
            Assert.Equal("succeeded", cases["rectangle-inset"].GetProperty("status").GetString());
            Assert.Equal("succeeded", cases["top-edge-chamfer"].GetProperty("status").GetString());
            Assert.Equal("succeeded", cases["pentagon-scaled"].GetProperty("status").GetString());
            Assert.Equal("succeeded", cases["hexagon-scaled"].GetProperty("status").GetString());
            Assert.Equal("succeeded", cases["pentagon-asymmetric"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["mismatched-vertex-count"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["non-increasing-sections"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["invalid-self-intersecting-profile"].GetProperty("status").GetString());
            Assert.Equal("deferred", cases["holes-deferred"].GetProperty("status").GetString());
            Assert.Equal("deferred", cases["arcs-deferred"].GetProperty("status").GetString());
            Assert.Equal("deferred", cases["multiple-loops-deferred"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["missing-correspondence"].GetProperty("status").GetString());
            Assert.Equal("rejected", cases["non-identity-correspondence"].GetProperty("status").GetString());

            AssertTopology(cases["rectangle-inset"], sectionCount: 2, vertices: 8, edges: 12, faces: 6, transitionFaces: 4, capFaces: 2, loops: 6, coedges: 24);
            AssertTopology(cases["pentagon-scaled"], sectionCount: 2, vertices: 10, edges: 15, faces: 7, transitionFaces: 5, capFaces: 2, loops: 7, coedges: 30);
            AssertTopology(cases["hexagon-scaled"], sectionCount: 2, vertices: 12, edges: 18, faces: 8, transitionFaces: 6, capFaces: 2, loops: 8, coedges: 36);
            AssertTopology(cases["pentagon-asymmetric"], sectionCount: 2, vertices: 10, edges: 15, faces: 7, transitionFaces: 5, capFaces: 2, loops: 7, coedges: 30);
            AssertTopEdgeChamferTopology(cases["top-edge-chamfer"]);

            foreach (var name in expectedArtifacts.Select(x => x.Replace("edge-prismatic-x5-", string.Empty).Replace(".step", string.Empty)))
            {
                var caseName = name == "rectangle-inset" ? "rectangle-inset" : name;
                var markers = cases[caseName].GetProperty("stepMarkerSummary");
                Assert.True(markers.GetProperty("requiredMarkersPresent").GetBoolean());
                Assert.True(markers.GetProperty("forbiddenMarkersAbsent").GetBoolean());
            }

            foreach (var invalidCase in new[] { "mismatched-vertex-count", "non-increasing-sections", "invalid-self-intersecting-profile", "holes-deferred", "arcs-deferred", "multiple-loops-deferred", "missing-correspondence", "non-identity-correspondence" })
            {
                Assert.Equal(JsonValueKind.Null, cases[invalidCase].GetProperty("artifactPath").ValueKind);
                Assert.Equal(JsonValueKind.Null, cases[invalidCase].GetProperty("stepMarkerSummary").ValueKind);
            }

            var diagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Contains("edge-prismatic-x5-no-production-route-replacement", diagnostics);
            Assert.Contains("edge-prismatic-x5-no-air-edge-sweep-used", diagnostics);
            Assert.Contains("edge-prismatic-x5-no-brep-bounded-chamfer-used", diagnostics);
            Assert.Contains("edge-prismatic-x5-no-topology-graft-used", diagnostics);
            Assert.Contains("edge-prismatic-x5-no-3d-boolean-used", diagnostics);
            Assert.Contains("edge-prismatic-x5-no-coplanar-merge-used", diagnostics);
            Assert.Contains("edge-prismatic-x5-step-smoke-succeeded:rectangle-inset", diagnostics);
            Assert.Contains("edge-prismatic-x5-split-preserving-topology-validated:top-edge-chamfer", diagnostics);
            Assert.Contains("edge-prismatic-x5-case-rejected:mismatched-vertex-count:mismatched-vertex-count", diagnostics);
            Assert.Contains("edge-prismatic-x5-case-deferred:holes-deferred:holes", diagnostics);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static void AssertTopology(JsonElement corpusCase, int sectionCount, int vertices, int edges, int faces, int transitionFaces, int capFaces, int loops, int coedges)
    {
        var topology = corpusCase.GetProperty("topologySummary");
        Assert.True(topology.GetProperty("bodyProduced").GetBoolean());
        Assert.Equal(sectionCount, topology.GetProperty("sectionCount").GetInt32());
        Assert.Equal(vertices, topology.GetProperty("vertexCount").GetInt32());
        Assert.Equal(edges, topology.GetProperty("edgeCount").GetInt32());
        Assert.Equal(faces, topology.GetProperty("faceCount").GetInt32());
        Assert.Equal(faces, topology.GetProperty("planarFaceCount").GetInt32());
        Assert.Equal(0, topology.GetProperty("cylindricalFaceCount").GetInt32());
        Assert.Equal(transitionFaces, topology.GetProperty("transitionFaceCount").GetInt32());
        Assert.Equal(capFaces, topology.GetProperty("capFaceCount").GetInt32());
        Assert.Equal(loops, topology.GetProperty("loopCount").GetInt32());
        Assert.Equal(coedges, topology.GetProperty("coedgeCount").GetInt32());
    }

    private static void AssertTopEdgeChamferTopology(JsonElement corpusCase)
    {
        AssertTopology(corpusCase, sectionCount: 3, vertices: 12, edges: 20, faces: 10, transitionFaces: 4, capFaces: 2, loops: 10, coedges: 40);
        var topology = corpusCase.GetProperty("topologySummary");
        Assert.Equal(4, topology.GetProperty("lowerPrismSideFaceCount").GetInt32());
        Assert.Equal(1, topology.GetProperty("chamferTransitionFaceCount").GetInt32());
    }

    private static void AssertGuarantees(JsonElement guarantees)
    {
        Assert.True(guarantees.GetProperty("noProductionRouteReplacement").GetBoolean());
        Assert.True(guarantees.GetProperty("noAirEdgeSweep").GetBoolean());
        Assert.True(guarantees.GetProperty("noBrepBoundedChamfer").GetBoolean());
        Assert.True(guarantees.GetProperty("noTopologyGraft").GetBoolean());
        Assert.True(guarantees.GetProperty("no3DBoolean").GetBoolean());
        Assert.True(guarantees.GetProperty("noCoplanarMerge").GetBoolean());
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
