using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AirTraceCommandTests
{
    [Fact]
    public void TraceHelp_IsDiscoverable()
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["--help"], stdout, stderr));
        Assert.Contains("trace", stdout.ToString(), StringComparison.Ordinal);

        stdout = new StringWriter(); stderr = new StringWriter();
        Assert.Equal(0, Aetheris.CLI.CliRunner.Run(["trace", "--help"], stdout, stderr));
        var text = stdout.ToString();
        Assert.Contains("default output is human-readable text", text, StringComparison.Ordinal);
        Assert.Contains("--json", text, StringComparison.Ordinal);
        Assert.Contains("analyze", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TracePrismatic_DefaultsToText()
    {
        var (exitCode, output, error) = Run("trace", "--case", "prismatic-section-transition");
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("Aetheris trace", output); Assert.Contains("Case: prismatic-section-transition", output);
        Assert.Contains("Trace kind: lowering", output); Assert.Contains("Route decision", output);
        Assert.Contains("BRepPlan", output); Assert.Contains("CIR mirror", output); Assert.Contains("STEP smoke", output);
        Assert.Contains("no production route replacement", output);
    }

    [Fact]
    public void TraceTopFaceLoopChamfer_DefaultsToText()
    {
        var (exitCode, output, _) = Run("trace", "--case", "top-face-loop-chamfer");
        Assert.Equal(0, exitCode); Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("TopFaceLoopChamfer", output); Assert.Contains("FaceBoundaryLoop", output);
        Assert.Contains("UniformChamfer", output); Assert.Contains("SwitchMatch", output);
        Assert.Contains("Chamfer faces: 4", output); Assert.Contains("not four independent single-edge chamfers", output);
        Assert.Contains("no AirEdgeSweep", output); Assert.Contains("no BrepBoundedChamfer", output);
    }

    [Fact]
    public void TracePrismatic_JsonOutput_IsDeterministicAndParses()
    {
        var (exitCode, output, _) = Run("trace", "--case", "prismatic-section-transition", "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("trace", root.GetProperty("command").GetString());
        Assert.Equal("lowering", root.GetProperty("traceKind").GetString());
        Assert.Equal("built-in-case", root.GetProperty("inputKind").GetString());
        Assert.Equal("prismatic-section-transition", root.GetProperty("caseName").GetString());
        Assert.Equal("Direct", root.GetProperty("routeDecision").GetProperty("mode").GetString());
        var plan = root.GetProperty("brepPlan");
        Assert.Equal(12, plan.GetProperty("vertices").GetInt32()); Assert.Equal(20, plan.GetProperty("edges").GetInt32());
        Assert.Equal(10, plan.GetProperty("faces").GetInt32()); Assert.Equal(40, plan.GetProperty("coedges").GetInt32());
        Assert.True(root.TryGetProperty("cirMirror", out _));
        Assert.Contains("topology-parity", root.GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void TraceTopFaceLoopChamfer_JsonOutput_IsDeterministicAndParses()
    {
        var (exitCode, output, _) = Run("trace", "--case", "top-face-loop-chamfer", "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("top-face-loop-chamfer", root.GetProperty("caseName").GetString());
        Assert.Equal("FaceBoundaryLoop", root.GetProperty("air").GetProperty("selectionClass").GetString());
        Assert.Equal("UniformChamfer", root.GetProperty("air").GetProperty("rule").GetString());
        Assert.Equal("SwitchMatch", root.GetProperty("routeDecision").GetProperty("mode").GetString());
        Assert.Equal(4, root.GetProperty("brepPlan").GetProperty("chamferFaces").GetInt32());
        var losses = root.GetProperty("cirMirror").GetProperty("knownLosses").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("chamfer-face-identity", losses); Assert.Contains("brep-plan-role-parity", losses);
        Assert.Contains("not four independent single-edge chamfers", root.GetProperty("guarantees").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void TraceOutDir_WritesTextOrJsonReport()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-trace-tests", Guid.NewGuid().ToString("N"));
        var (textExit, textOut, _) = Run("trace", "--case", "top-face-loop-chamfer", "--out-dir", dir);
        Assert.Equal(0, textExit); Assert.Contains("Trace report written:", textOut);
        var textPath = Path.Combine(dir, "air-x6-top-face-loop-chamfer-trace.txt"); Assert.True(File.Exists(textPath));
        Assert.Contains("Aetheris trace", File.ReadAllText(textPath));
        var (jsonExit, jsonOut, _) = Run("trace", "--case", "top-face-loop-chamfer", "--out-dir", dir, "--json");
        Assert.Equal(0, jsonExit); JsonDocument.Parse(jsonOut).Dispose();
        Assert.True(File.Exists(Path.Combine(dir, "air-x6-top-face-loop-chamfer-trace.json")));
    }

    [Fact]
    public void TraceInvalidUsage_IsHelpful()
    {
        var missing = Run("trace"); Assert.NotEqual(0, missing.ExitCode); Assert.Contains("--case", missing.Stderr); Assert.Contains("Supported cases", missing.Stderr);
        var unknown = Run("trace", "--case", "nope"); Assert.NotEqual(0, unknown.ExitCode); Assert.Contains("Supported cases", unknown.Stderr);
        var step = Run("trace", "some.step"); Assert.NotEqual(0, step.ExitCode); Assert.Contains("trace does not analyze STEP files", step.Stderr); Assert.Contains("aetheris analyze", step.Stderr);
    }

    [Fact]
    public void TraceOutput_IsDeterministic()
    {
        Assert.Equal(Run("trace", "--case", "prismatic-section-transition", "--json").Stdout, Run("trace", "--case", "prismatic-section-transition", "--json").Stdout);
        Assert.Equal(Run("trace", "--case", "top-face-loop-chamfer", "--json").Stdout, Run("trace", "--case", "top-face-loop-chamfer", "--json").Stdout);
    }



    [Fact]
    public void TraceParserBackedBoxFixture_ReachesConstructiveAir_DefaultText()
    {
        var (exitCode, output, error) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("Fixture", output); Assert.Contains("Frontend", output);
        Assert.Contains("Parser-backed: true", output); Assert.Contains("Parse succeeded: true", output);
        Assert.Contains("Expected stage: constructive-air", output); Assert.Contains("Actual stage: constructive-air", output);
        Assert.Contains("Feature AIR", output); Assert.Contains("Source op: box", output); Assert.Contains("Node: CreateBox", output);
        Assert.Contains("Constructive AIR", output); Assert.Contains("Node: AirProfileExtrude", output); Assert.Contains("Canonical form: rectangle-profile-extrude", output);
        Assert.Contains("Profile: Rectangle(width=10, depth=8)", output); Assert.Contains("Extrusion: height=6", output);
        Assert.Contains("Expectation satisfied: true", output);
    }

    [Fact]
    public void TraceParserBackedBoxFixture_ReachesConstructiveAir_Json()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("firmfixture", root.GetProperty("inputKind").GetString());
        Assert.Equal("valid", root.GetProperty("fixture").GetProperty("expectation").GetString());
        Assert.True(root.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
        Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean());
        Assert.True(root.GetProperty("frontend").GetProperty("parserBacked").GetBoolean());
        Assert.True(root.GetProperty("frontend").GetProperty("parseSucceeded").GetBoolean());
        Assert.Equal("constructive-air", root.GetProperty("frontend").GetProperty("frontendStageReached").GetString());
        Assert.Equal("constructive-air", root.GetProperty("actualStageReached").GetString());
        Assert.Equal("box", root.GetProperty("featureAir").GetProperty("sourceOpKind").GetString());
        Assert.Equal("CreateBox", root.GetProperty("featureAir").GetProperty("nodeKind").GetString());
        Assert.Equal("AirProfileExtrude", root.GetProperty("constructiveAir").GetProperty("nodeKind").GetString());
        Assert.Equal("rectangle-profile-extrude", root.GetProperty("constructiveAir").GetProperty("canonicalForm").GetString());
        Assert.Contains("air-x10-firmament-parse-succeeded", output);
    }


    [Fact]
    public void TraceParserBackedBoxFixture_DimensionsMappedToProfileExtrude()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output);
        var dimensions = doc.RootElement.GetProperty("featureAir").GetProperty("dimensions");
        Assert.Equal(10, dimensions.GetProperty("width").GetDouble());
        Assert.Equal(8, dimensions.GetProperty("depth").GetDouble());
        Assert.Equal(6, dimensions.GetProperty("height").GetDouble());
        var constructive = doc.RootElement.GetProperty("constructiveAir");
        Assert.Equal(10, constructive.GetProperty("width").GetDouble());
        Assert.Equal(8, constructive.GetProperty("depth").GetDouble());
        Assert.Equal(6, constructive.GetProperty("height").GetDouble());
        Assert.Equal("Rectangle", constructive.GetProperty("profileKind").GetString());
        Assert.Contains("air-x10-box-dimensions-extracted", output);
    }

    [Fact]
    public void TraceParserBackedBoxFixture_DoesNotPretendBRepPlanOrCIR()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", PrimitiveFixture("valid/box.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("constructive-air", root.GetProperty("actualStageReached").GetString());
        Assert.Equal("none", root.GetProperty("brepPlan").GetProperty("planKind").GetString());
        Assert.Equal("not-requested", root.GetProperty("cirMirror").GetProperty("status").GetString());
        Assert.Equal("AirProfileExtrude", root.GetProperty("constructiveAir").GetProperty("nodeKind").GetString());
        Assert.Contains("air-x10-brepplan-deferred", output);
        Assert.Contains("air-x10-cir-mirror-deferred", output);
    }

    [Fact]
    public void MetadataDrivenChamferFixtures_StillWork()
    {
        Assert.Equal(0, Run("trace", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/arbitrary-graph-chamfer.invalid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/non-uniform-loop-chamfer.invalid.firmfixture"), "--json").ExitCode);
        Assert.Equal(0, Run("trace", "--fixture", Fixture("invalid/loop-fillet-deferred.invalid.firmfixture"), "--json").ExitCode);
    }

    [Fact]
    public void ParserBackedBoxExpectationFailure_ReturnsNonZeroWithReport()
    {
        var source = File.ReadAllText(PrimitiveFixture("valid/box.valid.firmfixture")).Replace("// expected-stage: constructive-air", "// expected-stage: cir-mirror", StringComparison.Ordinal);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".valid.firmfixture");
        File.WriteAllText(path, source);
        var (exitCode, output, error) = Run("trace", "--fixture", path, "--json");
        Assert.NotEqual(0, exitCode); Assert.Contains("expectation", error, StringComparison.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.False(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean());
        Assert.True(root.GetProperty("frontend").GetProperty("parseSucceeded").GetBoolean());
        Assert.Equal("constructive-air", root.GetProperty("actualStageReached").GetString());
        Assert.Contains("air-x10-firmament-parse-succeeded", output);
    }

    [Fact]
    public void ParserBackedBoxJsonDeterministic()
    {
        var valid = PrimitiveFixture("valid/box.valid.firmfixture");
        Assert.Equal(Run("trace", "--fixture", valid, "--json").Stdout, Run("trace", "--fixture", valid, "--json").Stdout);
    }

    [Fact]
    public void TraceFixtureValidTopFaceLoopChamfer_DefaultsToText()
    {
        var (exitCode, output, error) = Run("trace", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.False(output.TrimStart().StartsWith("{", StringComparison.Ordinal));
        Assert.Contains("Fixture", output); Assert.Contains("Expectation: valid", output);
        Assert.Contains("Actual stage: cir-mirror", output); Assert.Contains("Expectation satisfied: true", output);
        Assert.Contains("TopFaceLoopChamfer", output); Assert.Contains("FaceBoundaryLoop", output);
        Assert.Contains("UniformChamfer", output); Assert.Contains("Chamfer faces: 4", output);
    }

    [Fact]
    public void TraceFixtureValidTopFaceLoopChamfer_JsonParses()
    {
        var (exitCode, output, _) = Run("trace", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"), "--json");
        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(output); var root = doc.RootElement;
        Assert.Equal("firmfixture", root.GetProperty("inputKind").GetString());
        Assert.Equal("valid", root.GetProperty("fixtureExpectation").GetString());
        Assert.True(root.GetProperty("expectationSatisfied").GetBoolean());
        Assert.Equal("top-face-loop-chamfer", root.GetProperty("fixtureCaseName").GetString());
        Assert.True(root.TryGetProperty("air", out _)); Assert.True(root.TryGetProperty("routeDecision", out _));
        Assert.True(root.TryGetProperty("brepPlan", out _)); Assert.True(root.TryGetProperty("cirMirror", out _));
    }

    [Theory]
    [InlineData("invalid/arbitrary-graph-chamfer.invalid.firmfixture", "arbitrary-graph-unsupported")]
    [InlineData("invalid/non-uniform-loop-chamfer.invalid.firmfixture", "non-uniform-rule-unsupported")]
    [InlineData("invalid/loop-fillet-deferred.invalid.firmfixture", "loop-fillet-deferred")]
    public void TraceFixtureInvalid_StopsBeforeGeometry(string path, string reason)
    {
        var (exitCode, output, error) = Run("trace", "--fixture", Fixture(path));
        Assert.Equal(0, exitCode); Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.Contains("Expectation: invalid", output); Assert.Contains("Expectation satisfied: true", output);
        Assert.Contains(reason, output); Assert.Contains("Faces: 0", output);
        Assert.Contains("STEP smoke: unavailable", output); Assert.Contains("Status: not-requested", output);
    }

    [Fact]
    public void TraceFixtureUsageErrors_AreHelpful()
    {
        var both = Run("trace", "--case", "top-face-loop-chamfer", "--fixture", Fixture("valid/top-face-loop-chamfer.valid.firmfixture"));
        Assert.NotEqual(0, both.ExitCode); Assert.Contains("mutually exclusive", both.Stderr);
        var wrongPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt"); File.WriteAllText(wrongPath, "// case: nope");
        var wrong = Run("trace", "--fixture", wrongPath); Assert.NotEqual(0, wrong.ExitCode); Assert.Contains(".valid.firmfixture", wrong.Stderr);
        var missing = Run("trace", "--fixture", "fixtures/Firmament/Chamfer/missing.valid.firmfixture"); Assert.NotEqual(0, missing.ExitCode); Assert.Contains("not found", missing.Stderr);
        var unknownPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".valid.firmfixture"); File.WriteAllText(unknownPath, "// case: nope\n// expected: valid\n");
        var unknown = Run("trace", "--fixture", unknownPath); Assert.NotEqual(0, unknown.ExitCode); Assert.Contains("Supported fixture cases", unknown.Stderr);
    }

    [Fact]
    public void TraceFixtureOutput_IsDeterministic()
    {
        var valid = Fixture("valid/top-face-loop-chamfer.valid.firmfixture");
        var invalid = Fixture("invalid/arbitrary-graph-chamfer.invalid.firmfixture");
        Assert.Equal(Run("trace", "--fixture", valid, "--json").Stdout, Run("trace", "--fixture", valid, "--json").Stdout);
        Assert.Equal(Run("trace", "--fixture", invalid, "--json").Stdout, Run("trace", "--fixture", invalid, "--json").Stdout);
    }

    private static string Fixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Firmament/Chamfer", relative));
    private static string PrimitiveFixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Firmament/Primitive", relative));

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}
