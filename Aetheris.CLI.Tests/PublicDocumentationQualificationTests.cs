using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aetheris.CLI.Tests;

public sealed class PublicDocumentationQualificationTests
{
    private static readonly string RepoRoot = FindRoot();

    [Fact]
    public void PublicMarkdownRelativeLinksResolveInsideRepository()
    {
        var markdown = Directory.GetFiles(Path.Combine(RepoRoot, "docs", "public"), "*.md", SearchOption.AllDirectories)
            .Append(Path.Combine(RepoRoot, "README.md"));
        foreach (var file in markdown)
        {
            var links = Regex.Matches(File.ReadAllText(file), @"\]\((?<target>[^)]+)\)")
                .Select(match => match.Groups["target"].Value)
                .Where(target => !target.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !target.StartsWith('#'));
            foreach (var target in links)
            {
                var path = target.Split('#', 2)[0].Replace('/', Path.DirectorySeparatorChar);
                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, path));
                Assert.True(File.Exists(resolved) || Directory.Exists(resolved), $"Broken public-doc link '{target}' in {Path.GetRelativePath(RepoRoot, file)}.");
                Assert.StartsWith(RepoRoot, resolved, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData("fixtures/FirmamentV2/Canonical/valid/box-hole-pmi.firmament", 1, 1)]
    [InlineData("fixtures/FirmamentV2/Canonical/valid/record-array-pattern-holes.firmament", 0, 0)]
    [InlineData("fixtures/FirmamentV2/Canonical/valid/box-holes-pmi-chamfer.firmament", 1, 2)]
    [InlineData("fixtures/FirmamentV2/PublicDogfood/ai-model.firmament", 0, 0)]
    public void PublicModelAndTemplateExamplesBuildThroughRealCli(string relativeSource, int datums, int diameters)
    {
        using var temp = TempDirectory.Create();
        var output = new StringWriter(); var error = new StringWriter();
        var step = Path.Combine(temp.Path, "part.step");
        var exit = CliRunner.Run(["build", Path.Combine(RepoRoot, relativeSource), "--output", step, "--json"], output, error);
        Assert.Equal(0, exit); Assert.Empty(error.ToString()); Assert.True(File.Exists(step));
        using var report = JsonDocument.Parse(output.ToString());
        Assert.True(report.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(datums, report.RootElement.GetProperty("pmiExportEvidence").GetProperty("datum").GetArrayLength());
        Assert.Equal(diameters, report.RootElement.GetProperty("pmiExportEvidence").GetProperty("diameter").GetArrayLength());
    }

    [Theory]
    [InlineData("fixtures/FirmamentV2/SheetMetal/preview3-l-bracket-hole.firmament")]
    [InlineData("fixtures/FirmamentV2/PublicDogfood/ai-sheet-metal.firmament")]
    public void PublicSheetMetalExampleBuildsAndFlattensWithOneFeature(string relativeSource)
    {
        using var temp = TempDirectory.Create();
        var source = Path.Combine(RepoRoot, relativeSource);
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["build", source, "--output", Path.Combine(temp.Path, "formed.step"), "--json"], output, error));
        using (var report = JsonDocument.Parse(output.ToString()))
            Assert.Equal(1, report.RootElement.GetProperty("part").GetProperty("features").GetInt32());
        output.GetStringBuilder().Clear();
        Assert.Equal(0, CliRunner.Run(["sheetmetal", "flatten", source, "--step", Path.Combine(temp.Path, "flat.step"), "--svg", Path.Combine(temp.Path, "flat.svg"), "--json"], output, error));
        Assert.True(File.Exists(Path.Combine(temp.Path, "flat.step"))); Assert.True(File.Exists(Path.Combine(temp.Path, "flat.svg")));
    }

    [Theory]
    [InlineData("fixtures/FirmamentV2/Materials/catalog-material-coupon.firmament", "FirmamentNative")]
    [InlineData("fixtures/FirmamentV2/FEA/cantilever.firmament", "FirmamentNative")]
    [InlineData("fixtures/FirmamentV2/FEA/inline-step-through-hole.firmament", "InlineStep")]
    [InlineData("fixtures/FirmamentV2/PublicDogfood/ai-fea-a36-cantilever.firmament", "FirmamentNative")]
    public void PublicMaterialAndFeaExamplesSolveThroughRealCli(string relativeSource, string sourceKind)
    {
        using var temp = TempDirectory.Create();
        var output = new StringWriter(); var error = new StringWriter();
        var exit = CliRunner.Run(["fea", Path.Combine(RepoRoot, relativeSource), "--out-dir", temp.Path, "--json"], output, error);
        Assert.Equal(0, exit); Assert.Empty(error.ToString());
        using var report = JsonDocument.Parse(output.ToString());
        Assert.Equal(sourceKind, report.RootElement.GetProperty("analysis").GetProperty("sourceKind").GetString());
        Assert.True(report.RootElement.GetProperty("solver").GetProperty("converged").GetBoolean());
    }

    private static string FindRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) { Path = path; Directory.CreateDirectory(path); }
        public string Path { get; }
        public static TempDirectory Create() => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetheris-public-docs-" + Guid.NewGuid().ToString("N")));
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
