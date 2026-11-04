using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

/// <summary>Small authoring conveniences found while reconstructing NIST CTC-01: trigonometric Statics,
/// Close after a path that already returns to its start, variant shapes as one-line Profiles, and diagnostics
/// that explain path and shape errors instead of repeating their codes.</summary>
public sealed class AuthoringConvenienceCliTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void TrigonometricStaticsResolveToTheDrawingDimension()
    {
        var body = BuildAndImport("fixtures/Regression/AuthoringConvenience/trig-statics.firmament");
        var xs = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p.X : double.NaN).ToArray();
        // 2·(10/cos30°)·sin60° + 10√2·√2 + 10·tan30°·tan60° = 20 + 20 + 10.
        Assert.Equal(50d, xs.Max() - xs.Min(), 9);
    }

    [Fact]
    public void CloseAfterAnArcIntoTheStartAddsNoEdge()
    {
        var body = BuildAndImport("fixtures/Regression/AuthoringConvenience/close-after-arc.firmament");
        // Four sides, four corner cylinders, top and bottom: no sliver face from a zero-length closing line.
        Assert.Equal(10, body.Topology.Faces.Count());
    }

    [Fact]
    public void VariantShapeBuildsAsAOneLineProfile()
    {
        var body = BuildAndImport("fixtures/Regression/AuthoringConvenience/regular-polygon-profile.firmament");
        Assert.Equal(8, body.Topology.Faces.Count());
    }

    [Theory]
    [InlineData("sqrt-negative.firmament", "firmament-v2-expression-unsupported", "outside its domain")]
    [InlineData("unknown-intrinsic.firmament", "firmament-v2-expression-unsupported", "calls 'Atan', which is not a compile-time intrinsic (Pow, Sqrt, Sin, Cos, Tan)")]
    [InlineData("zero-length-step.firmament", "concept-path-zero-length:Outline:Back", "Step 'Back' of Path 'Outline' has zero length")]
    [InlineData("shape-negative-size.firmament", "firmament-boundary2-invalid-dimensions:Plate__Boundary", "Shape 'Plate' has missing or non-positive dimensions")]
    public void RejectionsExplainThemselves(string file, string code, string message)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["validate", Path.Combine(RepoRoot, "fixtures/Invalid/AuthoringConvenience", file), "--json"], stdout, stderr);
        Assert.NotEqual(0, exit);
        using var document = JsonDocument.Parse(stdout.ToString());
        var report = document.RootElement.TryGetProperty("firmamentV2Validation", out var nested) ? nested : document.RootElement;
        var diagnostic = report.GetProperty("diagnostics").EnumerateArray().Single(d => d.GetProperty("code").GetString() == code);
        var text = diagnostic.GetProperty("message").GetString()!;
        Assert.Contains(message, text, StringComparison.Ordinal);
        Assert.NotEqual(code, text);
    }

    private static Aetheris.Kernel.Core.Brep.BrepBody BuildAndImport(string fixture)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-authoring-convenience", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var step = Path.Combine(directory, Path.GetFileNameWithoutExtension(fixture) + ".step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.True(CliRunner.Run(["build", Path.Combine(RepoRoot, fixture), "--out", step], stdout, stderr) == 0, $"{fixture}: {stderr}{stdout}");
        var import = Step242Importer.ImportBody(File.ReadAllText(step));
        Assert.True(import.IsSuccess, string.Join("; ", import.Diagnostics.Select(d => d.Message)));
        return import.Value;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
