using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ReleaseAdmissionRegressionTests
{
    [Theory]
    [InlineData("Learn/06-post-on-plate.firmament", "to: height }", "to: height; zzUnknownField: 1mm }")]
    [InlineData("Learn/06-post-on-plate.firmament", "Semantic Foot {", "Semantic Foot { zzUnknownField: 1mm;")]
    [InlineData("Learn/06-post-on-plate.firmament", "anchor: PostOnPlate.Plate;", "anchor: PostOnPlate.Plate; zzUnknownField: 1mm;")]
    [InlineData("SectionChain/explicit-sketch-ruled.firmament", "position: [2mm, -2mm]", "position: [2mm, -2deg]")]
    [InlineData("SectionChain/explicit-sketch-ruled.firmament", "Point2 B { position: [2mm, -2mm] }", "Point2 B { position: [2mm, -2mm]; zzUnknownField: 1mm }")]
    [InlineData("WireForm/straight-wire.firmament", "Straight Body {", "Straight Body { zzUnknownField: 1mm;")]
    [InlineData("WireForm/straight-wire.firmament", "Straight Body {", "Straight Ignored; Straight Body {")]
    [InlineData("WireForm/straight-wire.firmament", "Straight Body {", "Straight Body on XZ {")]
    [InlineData("WireForm/straight-wire.firmament", "WireForm Wire {", "WireForm Other { Diameter: 2mm Origin: [0,0,0] Tangent: [1,0,0] Up: [0,1,0] Straight Body { Length: 20mm } } WireForm Wire {")]
    [InlineData("WireForm/straight-wire.firmament", "StartFrame {", "StartFrame { zzUnknownField: 1mm;")]
    [InlineData("WireForm/straight-wire.firmament", "WireForm Wire {", "WireForm Wire { zzUnknownField: 1mm;")]
    [InlineData("WireForm/straight-wire.firmament", "Model StraightWire {", "Model StraightWire { zzUnknownField: 1mm;")]
    [InlineData("WireForm/straight-wire.firmament", "Tangent: [1, 0, 0]", "Tangent: [1mm, 0, 0]")]
    [InlineData("Assembly/annular-pair.firmament", "<Part First = Sleeve<R: 20mm>>", "<Part First = Sleeve<R: 20mm>> zzUnknownField: 1mm;")]
    [InlineData("Learn/06-post-on-plate.firmament", "expose {", "expose { zzUnknownField: 1mm;")]
    [InlineData("SectionChain/explicit-sketch-ruled.firmament", "Loop Outer {", "Loop Outer { zzUnknownField: 1mm;")]
    [InlineData("Span/line-pipeline.firmament", "Span<Line> MountingEdge {", "Span<Line> MountingEdge { zzUnknownField: 1mm;")]
    [InlineData("Features/EdgeFinish/single-edge-fillet.firmament", "Modify Body {", "Modify Body { zzUnknownField: 1mm;")]
    [InlineData("Offset/prism-pad.firmament", "AddOffset<Prism> LocalPad {", "AddOffset<Prism> LocalPad { zzUnknownField: 1mm;")]
    [InlineData("Offset/prism-pad.firmament", "Height: 3mm", "Height: 3mm; Axis: +Z; Along: -Z")]
    [InlineData("Offset/prism-pad.firmament", "Height: 3mm", "Height: 3mm; Axis: WrongDirection")]
    [InlineData("Assembly/template-block-pair.firmament", "Struct AssemblyBlock: AssemblyBlockConcept {", "Struct AssemblyBlock: AssemblyBlockConcept { zzUnknownField: 1mm;")]
    [InlineData("Assembly/template-block-pair.firmament", "EdgeFinish ProofBreak {", "EdgeFinish ProofBreak { zzUnknownField: 1mm;")]
    [InlineData("Feature/feature-pattern.firmament", "return Hole<Shaft> {", "return Hole<Shaft> { zzUnknownField: 1mm;")]
    [InlineData("Feature/pickup-functional.firmament", "Profile Section {", "Profile Section { zzUnknownField: 1mm;")]
    [InlineData("Feature/pickup-functional.firmament", "Axis2 Longitudinal {", "Axis2 Longitudinal { zzUnknownField: 1mm;")]
    [InlineData("Templates/local-closed-boundaries.firmament", "Struct Plate {", "Struct Plate { zzUnknownField: 1mm;")]
    [InlineData("Templates/local-closed-boundaries.firmament", "Rect2 Outside {", "Rect2 Outside { zzUnknownField: 1mm;")]
    [InlineData("Templates/local-closed-boundaries.firmament", "Profile Section {", "Profile Section { zzUnknownField: 1mm;")]
    [InlineData("Templates/local-closed-boundaries.firmament", "Loop Outer {", "Loop Outer { zzUnknownField: 1mm;")]
    [InlineData("Templates/local-closed-boundaries.firmament", "Extrude Body {", "Extrude Body { zzUnknownField: 1mm;")]
    public void UnconsumedIntentAndWrongDimensionsRejectBothCommands(string fixture, string original, string replacement)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical", fixture));
        // The other definition is deliberately unused by the original fixture.
        // Qualify that its fields reject when that exact recipe is selected.
        if (fixture == "Templates/local-closed-boundaries.firmament")
            source = source.Replace("Struct Selected = Disc<R: 20mm>", "Struct Selected = Plate<Width: 40mm>", StringComparison.Ordinal);
        Assert.Contains(original, source);
        var directory = Directory.CreateTempSubdirectory("aetheris-release-admission-").FullName;
        try
        {
            var path = Path.Combine(directory, "case.firmament");
            File.WriteAllText(path, source);
            Assert.Equal(0, Run("validate", path).Exit);
            Assert.Equal(0, Run("build", path, Path.Combine(directory, "control.step")).Exit);
            File.WriteAllText(path, source.Replace(original, replacement, StringComparison.Ordinal));
            var output = Path.Combine(directory, "invalid.step");
            foreach (var verb in new[] { "validate", "build" })
            {
                var result = Run(verb, path, output);
                Assert.NotEqual(0, result.Exit);
                Assert.False(File.Exists(output));
                Assert.DoesNotContain("Unhandled exception", result.Output);
                using var json = JsonDocument.Parse(result.Output);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NativeV2RootCaseAliasPreservesGeometryAndOwner()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/EquationCurves/circle.firmament"));
        var directory = Directory.CreateTempSubdirectory("aetheris-root-alias-").FullName;
        try
        {
            var path = Path.Combine(directory, "circle.firmament");
            var steps = new List<string>();
            foreach (var text in new[] { source, source.Replace("Model RoundPlate", "model RoundPlate") })
            {
                File.WriteAllText(path, text);
                Assert.Equal(0, Run("validate", path).Exit);
                var output = Path.Combine(directory, $"{steps.Count}.step");
                var build = Run("build", path, output);
                Assert.Equal(0, build.Exit);
                using var json = JsonDocument.Parse(build.Output);
                Assert.Equal("RoundPlate", json.RootElement.GetProperty("model").GetString());
                steps.Add(File.ReadAllText(output));
            }
            Assert.Equal(steps[0], steps[1]);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("build")]
    [InlineData("measure")]
    [InlineData("verify")]
    [InlineData("format")]
    [InlineData("lock")]
    [InlineData("mesh")]
    public void MissingInputRetainsStructuredActionableDiagnostic(string verb)
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".firmament");
        var result = Run(verb, missing);
        Assert.Equal(1, result.Exit);
        using var json = JsonDocument.Parse(result.Output);
        var diagnostic = json.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal("source-file-not-found", diagnostic.GetProperty("code").GetString());
        Assert.Contains(missing, diagnostic.GetProperty("message").GetString());
        Assert.Equal(missing, diagnostic.GetProperty("sourceFile").GetString());
        Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("sourceSpan").ValueKind);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("measure")]
    [InlineData("verify")]
    [InlineData("format")]
    [InlineData("lock")]
    [InlineData("mesh")]
    public void MissingInputTextIsShortAndActionable(string verb)
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(1, CliRunner.Run([verb, missing], stdout, stderr));
        Assert.Empty(stdout.ToString());
        Assert.Contains("source-file-not-found", stderr.ToString());
        Assert.Contains(missing, stderr.ToString());
        Assert.DoesNotContain("Exception", stderr.ToString());
        Assert.InRange(stderr.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length, 1, 2);
    }

    [Theory]
    [InlineData("Regression/FeatureAccounting/extrude-hole.firmament", "to: 8mm", "to: 8deg", "to")]
    [InlineData("Canonical/SectionChain/explicit-sketch-ruled.firmament", "position: [2mm, -2mm]", "position: [2mm, -2deg]", "position")]
    public void WrongUnitsAreReportedAtTheField(string fixture, string before, string after, string field)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures", fixture)).Replace(before, after);
        var directory = Directory.CreateTempSubdirectory("aetheris-wrong-unit-").FullName;
        try
        {
            var path = Path.Combine(directory, "wrong-unit.firmament"); File.WriteAllText(path, source);
            foreach (var verb in new[] { "validate", "build" })
            {
                var result = Run(verb, path);
                Assert.Equal(1, result.Exit);
                using var json = JsonDocument.Parse(result.Output);
                var payload = json.RootElement.TryGetProperty("firmamentV2Validation", out var validation) ? validation : json.RootElement;
                var diagnostic = Assert.Single(payload.GetProperty("diagnostics").EnumerateArray(),
                    d => d.GetProperty("code").GetString()!.StartsWith("firmament-v2-field-unit-mismatch"));
                Assert.Contains("requires mm; received deg", diagnostic.GetProperty("message").GetString());
                var span = diagnostic.GetProperty("sourceSpan");
                var text = source.Substring(span.GetProperty("start").GetInt32(), span.GetProperty("length").GetInt32());
                Assert.Contains(field + ":", text, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static (int Exit, string Output) Run(string verb, string path, string? output = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var args = new List<string> { verb, path, "--json" };
        if (verb == "build" && output is not null) args.AddRange(["--out", output]);
        return (CliRunner.Run(args.ToArray(), stdout, stderr), stdout.ToString());
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
