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

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exitCode = Aetheris.CLI.CliRunner.Run(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}
