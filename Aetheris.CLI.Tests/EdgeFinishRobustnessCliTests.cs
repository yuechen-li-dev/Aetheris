using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class EdgeFinishRobustnessCliTests
{
    [Theory]
    [InlineData("d-profile-fillet", false)]
    [InlineData("d-profile-fillet", true)]
    [InlineData("d-profile-chamfer", false)]
    [InlineData("d-profile-chamfer", true)]
    [InlineData("polygon-rim-fillet", false)]
    [InlineData("polygon-rim-fillet", true)]
    public void ValidateAndBuildAdmitActualFinishedGeometry(string fixture, bool bottom)
    {
        WithSource(fixture, text => bottom ? text.Replace("on: Top", "on: Bottom") : text, (path, output) =>
        {
            var validate = new StringWriter(); var build = new StringWriter(); var error = new StringWriter();
            Assert.True(CliRunner.Run(["validate", path, "--json"], validate, error) == 0, validate + error.ToString());
            Assert.True(CliRunner.Run(["build", path, "--output", output, "--json"], build, error) == 0, build + error.ToString());
            using var json = JsonDocument.Parse(build.ToString());
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            var feature = Assert.Single(json.RootElement.GetProperty("engineeringFeatures").EnumerateArray());
            Assert.Equal("edgefinish:Rim", feature.GetProperty("featureId").GetString());
            var imported = Step242Importer.ImportBody(File.ReadAllText(output));
            Assert.True(imported.IsSuccess);
            Assert.Equal(fixture.StartsWith("d-", StringComparison.Ordinal) ? 6 : 10, imported.Value.Topology.Faces.Count());
            if (fixture.StartsWith("d-", StringComparison.Ordinal))
            {
                var planning = json.RootElement.GetProperty("edgeFinishPlanning");
                Assert.Equal("CircularSegmentMiter", planning.GetProperty("decision").GetProperty("strategy").GetString());
                Assert.InRange(planning.GetProperty("maximumCurveDeviationMm").GetDouble(), 0, 1e-6);
                Assert.Equal(2, planning.GetProperty("decision").GetProperty("judgment").GetProperty("candidates").GetArrayLength());
                Assert.False(planning.GetProperty("decision").TryGetProperty("circularSegment", out _)); // Internal tuple packet is not a JSON contract.
            }
        });
    }

    [Theory]
    [InlineData("d-profile-fillet", "radius: 1mm", "radius: 5mm", "ProfileEdgeFinishInsetCollapse")]
    [InlineData("d-profile-chamfer", "distance: 1mm", "distance: 6mm", "ProfileEdgeFinishInsetCollapse")]
    [InlineData("d-profile-fillet", "radius: 1mm", "radius: -1mm", "Radius")]
    [InlineData("d-profile-fillet", "radius: 1mm", "radius: 1mm; endClearance: 1mm", "EndClearanceRequiresOpenChain")]
    [InlineData("d-profile-fillet", "radius: 1mm", "radius: 1mm; raduis: 1mm", "raduis")]
    [InlineData("d-profile-fillet", "radius: 1mm", "radius: 1mm; endClearance: Bogus", "field-value-invalid")]
    [InlineData("d-profile-fillet", "radius: 1mm", "radius: 1mm; reflexJunction: 0", "ReflexJunctionStyleUnsupported")]
    [InlineData("d-profile-fillet", "radius: 1mm }", "radius: 1mm }\n EdgeFinish LowerRim { target: Section.Outer; on: Bottom; kind: Fillet; radius: 1mm }", "ProfileBoundaryEdgeFinishSequenceUnsupported")]
    [InlineData("d-profile-chamfer", "distance: 1mm }", "distance: 1mm }\n EdgeFinish LowerRim { target: Section.Outer; on: Bottom; kind: Chamfer; distance: 1mm }", "ProfileBoundaryEdgeFinishSequenceUnsupported")]
    [InlineData("d-profile-fillet", "radius: 1mm }", "radius: 1mm }\n EdgeFinish LowerRim { target: Section.Outer; on: Bottom; kind: Chamfer; distance: 1mm }", "ProfileBoundaryEdgeFinishSequenceUnsupported")]
    public void RejectedRequestsAgreeAndLeaveNoArtifact(string fixture, string oldText, string replacement, string code)
    {
        WithSource(fixture, text => text.Replace(oldText, replacement), (path, output) =>
        {
            foreach (var command in new[] { "validate", "build" })
            {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                var args = command == "build" ? new[] { command, path, "--output", output, "--json" } : [command, path, "--json"];
                Assert.True(CliRunner.Run(args, stdout, stderr) == 1, stdout + stderr.ToString());
                using var json = JsonDocument.Parse(stdout.ToString());
                var report = command == "validate" ? json.RootElement.GetProperty("firmamentV2Validation") : json.RootElement;
                Assert.Contains(report.GetProperty("diagnostics").EnumerateArray(), d =>
                    d.GetProperty("code").GetString()!.Contains(code, StringComparison.Ordinal) || d.GetProperty("message").GetString()!.Contains(code, StringComparison.Ordinal));
                if (code is "ProfileEdgeFinishInsetCollapse" or "ProfileBoundaryEdgeFinishSequenceUnsupported")
                    Assert.Contains(report.GetProperty("diagnostics").EnumerateArray(), d => d.GetProperty("line").GetInt32() > 1);
            }
            Assert.False(File.Exists(output));
        });
    }

    [Theory]
    [InlineData("d-profile-fillet", false)]
    [InlineData("d-profile-fillet", true)]
    [InlineData("d-profile-chamfer", false)]
    [InlineData("d-profile-chamfer", true)]
    [InlineData("polygon-rim-fillet", false)]
    [InlineData("polygon-rim-fillet", true)]
    public void FinishedStepMeshesWithCompleteWatertightOutwardTopology(string fixture, bool bottom)
    {
        WithSource(fixture, text => bottom ? text.Replace("on: Top", "on: Bottom") : text, (path, output) =>
        {
            var error = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["build", path, "--out", output, "--json"], new StringWriter(), error));
            var stdout = new StringWriter();
            Assert.True(CliRunner.Run(["mesh", output, "--format", "obj", "--output", output + ".obj", "--json"], stdout, error) == 0,
                stdout + error.ToString());
            using var json = JsonDocument.Parse(stdout.ToString()); var report = json.RootElement;
            Assert.True(report.GetProperty("watertight").GetBoolean());
            Assert.True(report.GetProperty("outwardOriented").GetBoolean());
            Assert.True(report.GetProperty("connected").GetBoolean());
            Assert.Equal(0, report.GetProperty("crackCount").GetInt32());
            Assert.Equal(0, report.GetProperty("nonManifoldEdgeCount").GetInt32());
            Assert.Equal(fixture.StartsWith("d-", StringComparison.Ordinal) ? 6 : 10, report.GetProperty("patchCount").GetInt32());
        });
    }

    [Fact]
    public void TypedEdgeFinishVariantRetainsTheSameGeometryAndIdentity()
    {
        WithSource("d-profile-fillet", text => text.Replace("EdgeFinish Rim", "EdgeFinish<Fillet> Rim").Replace("kind: Fillet; ", ""), (path, output) =>
        {
            var stdout = new StringWriter(); var stderr = new StringWriter();
            Assert.True(CliRunner.Run(["build", path, "--out", output, "--json"], stdout, stderr) == 0, stdout + stderr.ToString());
            Assert.Contains("edgefinish:Rim", stdout.ToString());
            Assert.Equal(6, Step242Importer.ImportBody(File.ReadAllText(output)).Value.Topology.Faces.Count());
        });
    }

    [Fact]
    public void UncalledFinishRecipeDoesNotBecomeAnotherOperation()
    {
        var fixture = File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/EdgeFinishRobustness/d-profile-fillet.firmament"));
        var expected = Aetheris.Kernel.Firmament.FirmamentBuildAndExport.CompileSource(fixture);
        var recipe = "Feature Unused() -> EdgeFinish { return EdgeFinish { target: Section.Outer; on: Bottom; kind: Fillet; radius: 1mm } }\n";
        var actual = Aetheris.Kernel.Firmament.FirmamentBuildAndExport.CompileSource(fixture.Replace("    Modify", recipe + "    Modify"));
        Assert.True(expected.IsSuccess);
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected.Value.StepText, actual.Value.StepText);
        Assert.Single(actual.Value.EngineeringFeatures!);
    }

    [Fact]
    public void NamedClosedSelectionUsesTheSameFinishedGeometry()
    {
        var fixture = File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/EdgeFinishRobustness/d-profile-fillet.firmament"));
        var expected = Aetheris.Kernel.Firmament.FirmamentBuildAndExport.CompileSource(fixture);
        var selection = "Selection Perimeter { source: Section.Outer.[Round, Flat]; require: ClosedLoop }\n";
        var named = fixture.Replace("    Modify", selection + "    Modify").Replace("target: Section.Outer", "target: Perimeter");
        var actual = Aetheris.Kernel.Firmament.FirmamentBuildAndExport.CompileSource(named);
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected.Value.StepText, actual.Value.StepText);
    }

    private static void WithSource(string fixture, Func<string, string> transform, Action<string, string> assertion)
    {
        var directory = Path.Combine(Root(), "artifacts/local/tests/edge-finish-robustness", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "source.firmament");
            File.WriteAllText(path, transform(File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/EdgeFinishRobustness/" + fixture + ".firmament"))));
            assertion(path, Path.Combine(directory, "body.step"));
        }
        finally { Directory.Delete(directory, true); }
    }
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
