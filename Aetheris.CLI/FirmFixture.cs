namespace Aetheris.CLI;

internal sealed record FirmFixture(
    string Path,
    string CaseName,
    string Expectation,
    string? ExpectedStage,
    string? ExpectedRoute,
    string? ExpectedReason,
    IReadOnlyDictionary<string, string> Metadata,
    string SourceBody,
    bool ParserBacked,
    IReadOnlyList<string> Diagnostics);

internal static class FirmFixtureLoader
{
    public static readonly string[] ValidExtensions = [".valid.firmfixture", ".invalid.firmfixture"];

    public static FirmFixture Load(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!File.Exists(path)) throw new FirmFixtureException("air-x7-fixture-file-not-found", $"Firmament fixture file was not found: {path}");
        var expectation = ClassifyExpectation(path);
        var lines = File.ReadAllLines(path);
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (!line.StartsWith("//", StringComparison.Ordinal)) break;
            var body = line[2..].Trim();
            var colon = body.IndexOf(':');
            if (colon <= 0) continue;
            var key = body[..colon].Trim();
            var value = body[(colon + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0) throw new FirmFixtureException("air-x7-firmfixture-metadata-invalid", $"Invalid fixture metadata line: {raw}");
            metadata[key] = value;
        }

        if (!metadata.TryGetValue("case", out var caseName) || string.IsNullOrWhiteSpace(caseName))
            throw new FirmFixtureException("air-x7-firmfixture-metadata-invalid", "Firmament fixture requires leading metadata `// case: <name>`.");
        if (metadata.TryGetValue("expected", out var expected) && !string.Equals(expected, expectation, StringComparison.Ordinal))
            throw new FirmFixtureException("air-x7-firmfixture-metadata-invalid", $"Fixture extension implies '{expectation}' but metadata expected '{expected}'.");

        var sourceBody = ExtractSourceBody(lines);
        var parserBacked = metadata.TryGetValue("parser-backed", out var parserBackedValue) && string.Equals(parserBackedValue, "true", StringComparison.OrdinalIgnoreCase);
        if (!parserBacked && string.Equals(metadata.GetValueOrDefault("syntax-version"), "FirmamentV2", StringComparison.Ordinal) && string.Equals(metadata.GetValueOrDefault("implementation"), "parser-backed", StringComparison.Ordinal)) parserBacked = true;
        var diagnostics = new[]
        {
            "air-x7-firmfixture-loaded",
            "air-x7-firmfixture-extension-classified",
            "air-x7-firmfixture-metadata-parsed",
            expectation == "valid" ? "air-x7-firmfixture-expectation-valid" : "air-x7-firmfixture-expectation-invalid",
            parserBacked ? "air-x8-parser-backed-fixture-loaded" : "air-x7-metadata-driven-fixture-loaded",
            parserBacked ? "air-x8-firmfixture-source-body-extracted" : "air-x7-firmfixture-source-body-not-required"
        }.Order(StringComparer.Ordinal).ToArray();
        return new(normalized, caseName, expectation, metadata.GetValueOrDefault("expected-stage"), metadata.GetValueOrDefault("expected-route"), metadata.GetValueOrDefault("expected-reason"), metadata, sourceBody, parserBacked, diagnostics);
    }

    private static string ExtractSourceBody(string[] lines)
    {
        var bodyStart = 0;
        for (; bodyStart < lines.Length; bodyStart++)
        {
            var line = lines[bodyStart].Trim();
            if (line.Length == 0) continue;
            if (!line.StartsWith("//", StringComparison.Ordinal)) break;
        }

        return string.Join(Environment.NewLine, lines.Skip(bodyStart)).Trim();
    }

    public static string ClassifyExpectation(string path)
    {
        if (path.EndsWith(".valid.firmfixture", StringComparison.Ordinal)) return "valid";
        if (path.EndsWith(".invalid.firmfixture", StringComparison.Ordinal)) return "invalid";
        throw new FirmFixtureException("air-x7-invalid-firmfixture-extension", "Trace fixtures must use `.valid.firmfixture` or `.invalid.firmfixture`.");
    }
}

internal sealed class FirmFixtureException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
