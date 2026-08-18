using System.Text.RegularExpressions;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentCanonicalDialectTests
{
    private static readonly Regex NonCanonicalVocabulary = new(
        @"(?m)^\s*(?:model|units|solid|modify|analysis|fixed|force|pmi)\b|\b(?:size|radius|height|body|material|region|components|vector|results|lattice|target|value|tolerance|datumrefs)\s*:",
        RegexOptions.CultureInvariant);

    private static readonly Regex FirmamentFence = new(
        @"```firmament\s*\r?\n(?<source>[\s\S]*?)\r?\n```",
        RegexOptions.CultureInvariant);

    private static readonly Regex OwnedSnakeCase = new(
        @"\b(?:body_resource|datum_refs|minimum_floor_thickness|counterbore_diameter|counterbore_depth|countersink_diameter|countersink_angle|inside_radius|k_factor|surface_family)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public void CanonicalFixtures_UseOnlyCanonicalOwnedVocabulary()
    {
        var root = FindRoot();
        var files = Directory.GetFiles(Path.Combine(root, "fixtures", "Canonical"), "*.firmament", SearchOption.AllDirectories);

        Assert.NotEmpty(files);
        AssertNoViolations(files.Select(path => (path, File.ReadAllText(path))));
    }

    [Fact]
    public void PublicFirmamentExamplesAndSnippets_UseOnlyCanonicalOwnedVocabulary()
    {
        var root = FindRoot();
        var examples = Directory.GetFiles(Path.Combine(root, "docs", "public"), "*.md", SearchOption.AllDirectories)
            .SelectMany(path => FirmamentFence.Matches(File.ReadAllText(path)).Select((match, index) =>
                ($"{path}#firmament-{index + 1}", match.Groups["source"].Value)));
        var snippets = Path.Combine(root, "tools", "vscode-firmament", "snippets", "firmament.json");

        AssertNoViolations(examples.Append((snippets, File.ReadAllText(snippets))));
    }

    private static void AssertNoViolations(IEnumerable<(string Path, string Source)> sources)
    {
        var failures = sources.SelectMany(item =>
                NonCanonicalVocabulary.Matches(item.Source).Select(match => $"{item.Path}: noncanonical '{match.Value.Trim()}'")
                    .Concat(OwnedSnakeCase.Matches(item.Source).Select(match => $"{item.Path}: Firmament-owned snake_case '{match.Value}'")))
            .ToArray();
        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
