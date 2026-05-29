using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ExperimentalPrismaticMapTests
{
    [Fact]
    public void Experimental_Prismatic_Map_Help_Is_Discoverable_And_Explicitly_Generated_Source_Only()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("prismatic-map", text, StringComparison.Ordinal);
        Assert.Contains("generated-source-only", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accepts no STEP input", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Experimental_Prismatic_Map_Route_Help_States_Scope_And_No_Step_Input()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", "--help"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        var text = stdout.ToString();
        Assert.Contains("Usage: aetheris experimental prismatic-map --case <case> --rows <N> --cols <N> --json", text, StringComparison.Ordinal);
        Assert.Contains("generated AIR/prismatic source route only", text, StringComparison.Ordinal);
        Assert.Contains("not normal 'aetheris analyze map'", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No STEP input", text, StringComparison.Ordinal);
        Assert.Contains("map occupancy only", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("rectangle-inset", 0.7d, 0.8d, 4d, 3.0d, 3.1d)]
    [InlineData("top-edge-chamfer", 3.3d, 3.4d, 4d, 3.9d, 4.0d)]
    public void Experimental_Prismatic_Map_Generated_Cases_Emit_Deterministic_Json_Summary(
        string caseName,
        double minLower,
        double minUpper,
        double expectedMax,
        double avgLower,
        double avgUpper)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", "--case", caseName, "--rows", "16", "--cols", "16", "--json"], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("EDGE-PRISMATIC-X9", root.GetProperty("milestone").GetString());
        Assert.Equal("experimental prismatic-map", root.GetProperty("commandRoute").GetString());
        Assert.Equal(caseName, root.GetProperty("caseName").GetString());
        Assert.Equal("generated-air-prismatic-source", root.GetProperty("generatedSourceKind").GetString());
        Assert.Equal("cir-convex-polyhedron", root.GetProperty("backendSelected").GetString());
        Assert.Equal("mirror-admitted-exact", root.GetProperty("mirrorStatus").GetString());
        Assert.Equal("map-occupancy", root.GetProperty("requestedUse").GetString());
        Assert.Equal("top", root.GetProperty("view").GetString());
        Assert.Equal(16, root.GetProperty("rows").GetInt32());
        Assert.Equal(16, root.GetProperty("cols").GetInt32());
        Assert.Equal(256, root.GetProperty("occupiedCount").GetInt32());
        Assert.Equal(0, root.GetProperty("emptyCount").GetInt32());
        Assert.InRange(root.GetProperty("thicknessMin").GetDouble(), minLower, minUpper);
        Assert.Equal(expectedMax, root.GetProperty("thicknessMax").GetDouble(), 6);
        Assert.InRange(root.GetProperty("thicknessAverage").GetDouble(), avgLower, avgUpper);

        var losses = root.GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("face identity lost", losses);
        Assert.Contains("loop identity lost", losses);
        Assert.Contains("split-face lineage lost", losses);
        Assert.Contains("feature role labels lost", losses);
        Assert.Contains("topology parity unavailable", losses);

        var diagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("edge-prismatic-x9-no-step-input", diagnostics);
        Assert.Contains("edge-prismatic-x9-no-imported-step-mirror-inference", diagnostics);
        Assert.Contains("edge-prismatic-x9-no-production-analyzer-behavior-changed", diagnostics);
        Assert.Contains("edge-prismatic-x9-no-default-cli-behavior-changed", diagnostics);
        Assert.Contains($"edge-prismatic-x9-cir-mirror-admitted-exact:{caseName}", diagnostics);
        Assert.Contains("edge-prismatic-x9-backend-selected:cir-convex-polyhedron", diagnostics);
        Assert.Contains($"edge-prismatic-x9-map-summary-created:{caseName}", diagnostics);

        var guarantees = root.GetProperty("guarantees");
        Assert.True(guarantees.GetProperty("noProductionAnalyzerBehaviorChanged").GetBoolean());
        Assert.True(guarantees.GetProperty("noDefaultCliBehaviorChanged").GetBoolean());
        Assert.True(guarantees.GetProperty("noStepInput").GetBoolean());
        Assert.True(guarantees.GetProperty("noImportedStepMirrorInference").GetBoolean());
        Assert.True(guarantees.GetProperty("noCirToBrepExtraction").GetBoolean());
        Assert.True(guarantees.GetProperty("noTopologyIdentityClaim").GetBoolean());
    }

    [Fact]
    public void Experimental_Prismatic_Map_Unknown_Case_Rejects_With_Stable_Diagnostic()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", "--case", "non-convex", "--rows", "16", "--cols", "16", "--json"], stdout, stderr);

        Assert.NotEqual(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
        Assert.Contains("edge-prismatic-x9-unknown-case:non-convex", stderr.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--case", "rectangle-inset", "--cols", "16", "--json")]
    [InlineData("--case", "rectangle-inset", "--rows", "16", "--json")]
    [InlineData("--case", "rectangle-inset", "--rows", "0", "--cols", "16", "--json")]
    [InlineData("--case", "rectangle-inset", "--rows", "16", "--cols", "-1", "--json")]
    public void Experimental_Prismatic_Map_Missing_Or_Invalid_Grid_Rejects(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", .. args], stdout, stderr);

        Assert.NotEqual(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
        Assert.Contains("edge-prismatic-x9-invalid-grid", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Experimental_Prismatic_Map_Missing_Case_Rejects()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", "--rows", "16", "--cols", "16", "--json"], stdout, stderr);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("edge-prismatic-x9-missing-case", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Experimental_Prismatic_Map_Json_Is_Required()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", "--case", "rectangle-inset", "--rows", "16", "--cols", "16"], stdout, stderr);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("edge-prismatic-x9-json-required", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Experimental_Prismatic_Map_Positional_Step_Input_Rejects_Without_Mirror_Inference()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", "part.step", "--case", "rectangle-inset", "--rows", "16", "--cols", "16", "--json"], stdout, stderr);

        Assert.NotEqual(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
        var error = stderr.ToString();
        Assert.Contains("edge-prismatic-x9-step-input-rejected", error, StringComparison.Ordinal);
        Assert.Contains("does not accept STEP input", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("face-identity")]
    [InlineData("topology-parity")]
    public void Experimental_Prismatic_Map_Lossy_Request_Rejects_With_Structured_Failure(string request)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Aetheris.CLI.CliRunner.Run(["experimental", "prismatic-map", "--case", "rectangle-inset", "--rows", "16", "--cols", "16", "--json", "--request", request], stdout, stderr);

        Assert.NotEqual(0, exitCode);
        Assert.Contains($"edge-prismatic-x9-lossy-request-rejected:{request}", stderr.ToString(), StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("unsupported", doc.RootElement.GetProperty("backendSelected").GetString());
    }
}
