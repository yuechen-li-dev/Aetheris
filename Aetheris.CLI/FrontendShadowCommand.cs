using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.CLI;

/// <summary>Developer-only collection. Production validation remains in CliRunner;
/// this command never grants permission to build and never writes geometry.</summary>
internal static class FrontendShadowCommand
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 1024,
        Converters = { new JsonStringEnumConverter() }
    };
    private sealed record Measurement(double ParseMilliseconds, long ParseAllocatedBytes);
    private sealed record Benchmark(int Iterations, int SourceCharacters, double MedianMilliseconds, double P95Milliseconds, long MedianAllocatedBytes);
    private sealed record Entry(string Source, string SourceGroup, FirmamentShadowReport Shadow,
        Measurement Measurement, Benchmark? Benchmark, JsonElement? ProductionValidation, int? ProductionExitCode, DocumentSyntax? Ast);

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            stdout.WriteLine("Usage: aetheris frontend-shadow <file> [--json] [--ast] [--validate] [--benchmark]");
            stdout.WriteLine("       aetheris frontend-shadow --corpus <directory> [--output-dir <directory>]");
            stdout.WriteLine("Developer syntax evidence. Structural success alone does not grant engineering admission; normal mechanical validation uses the unified AST binder.");
            return args.Length == 0 ? 1 : 0;
        }
        var corpus = args[0] == "--corpus";
        var input = corpus && args.Length > 1 ? args[1] : args[0];
        var json = false; var ast = false; var validate = false; var benchmark = false;
        var output = Path.Combine("artifacts", "local", "frontend-x1", "corpus");
        for (var i = corpus ? 2 : 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json": json = true; break;
                case "--ast": ast = true; break;
                case "--validate": validate = true; break;
                case "--benchmark": benchmark = true; break;
                case "--output-dir" when i + 1 < args.Length: output = args[++i]; break;
                default: stderr.WriteLine($"Unknown or incomplete shadow option '{args[i]}'."); return 1;
            }
        }
        if (corpus)
        {
            if (!Directory.Exists(input)) { stderr.WriteLine($"Corpus directory not found: {input}"); return 1; }
            if (ast || validate || benchmark) { stderr.WriteLine("Use --ast/--validate/--benchmark on individual files; corpus collection compares the generic production projection only."); return 1; }
            var files = Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)
                .Where(p => Path.GetExtension(p) is ".firmament" or ".firmfixture" or ".firmasm").Order(StringComparer.Ordinal).ToArray();
            var entries = new List<Entry>();
            foreach (var path in files) entries.Add(Analyze(path, false, false, false, Path.GetFullPath(input)));
            var parsed = entries.Count(e => e.Shadow.ShadowStatus == "Parsed");
            var summary = new { schemaVersion = 1, qualified = false, mode = "ShadowOnly", sourceCount = files.Length,
                parsed, rejected = entries.Count - parsed, semanticParityQualified = false,
                comparedFacts = entries.Sum(e => e.Shadow.Comparison.ComparedFacts),
                differences = entries.Sum(e => e.Shadow.Comparison.Differences.Count), entries };
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(summary, Json));
            var lines = new List<string> { "# Firmament X1 shadow collection", "", $"Sources: {files.Length}; structurally parsed: {parsed}; rejected: {entries.Count - parsed}.",
                "", "Qualification: false. Generic production projection and bounded structural comparison only; no semantic or geometry parity claim.", "",
                "| Source | Shadow | Production route | Facts compared | Differences |", "| --- | --- | --- | --- | --- |" };
            lines.AddRange(entries.Select(e => $"| {e.Source} | {e.Shadow.ShadowStatus} | {e.Shadow.ProductionProjection.ParserRoute ?? "Unselected"} | {e.Shadow.Comparison.ComparedFacts} | {e.Shadow.Comparison.Differences.Count} |"));
            File.WriteAllLines(Path.Combine(output, "report.md"), lines);
            stdout.WriteLine($"Collected {files.Length} sources: {parsed} parsed, {files.Length - parsed} rejected. Semantic parity unqualified. Reports: {Path.GetFullPath(output)}");
            return 0; // collection completed, not a qualification pass
        }
        if (!File.Exists(input)) { stderr.WriteLine($"Shadow input not found: {input}"); return 1; }
        var entry = Analyze(input, ast, validate, benchmark, null);
        if (json || ast) stdout.WriteLine(JsonSerializer.Serialize(entry, Json));
        else
        {
            stdout.WriteLine($"Shadow: {entry.Shadow.ShadowStatus} ({entry.Shadow.ValidationLevel}); language: {entry.Shadow.LanguageVersion}; production projection route: {entry.Shadow.ProductionProjection.ParserRoute ?? "Unselected"}");
            stdout.WriteLine($"Declarations: {entry.Shadow.Summary.DeclarationCount}; compared facts: {entry.Shadow.Comparison.ComparedFacts}; differences: {entry.Shadow.Comparison.Differences.Count}; semantic parity: unqualified.");
            foreach (var d in entry.Shadow.Diagnostics) stderr.WriteLine($"{input}({d.Line},{d.Column}): {d.Severity} {d.Code}: {d.Message}");
            foreach (var d in entry.Shadow.Comparison.Differences) stdout.WriteLine($"{d.Category} {d.Code}: {d.Message}");
            if (validate) stdout.WriteLine($"Production validate exit code: {entry.ProductionExitCode}");
        }
        return entry.Shadow.ShadowStatus == "Rejected" || entry.ProductionExitCode == 1 ? 1 : 0;
    }

    private static Entry Analyze(string path, bool ast, bool validate, bool benchmark, string? corpusRoot)
    {
        var source = File.ReadAllText(path);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var syntax = FirmamentSyntaxParser.Parse(source, path);
        stopwatch.Stop();
        var measurement = new Measurement(stopwatch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated);
        Benchmark? timings = null;
        if (benchmark)
        {
            _ = FirmamentSyntaxParser.Parse(source, path); // warm-up, no retained parse cache
            var samples = new List<Measurement>();
            for (var i = 0; i < 20; i++)
            {
                allocated = GC.GetAllocatedBytesForCurrentThread(); stopwatch.Restart();
                _ = FirmamentSyntaxParser.Parse(source, path); stopwatch.Stop();
                samples.Add(new(stopwatch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated));
            }
            timings = new(20, source.Length, samples.Select(s => s.ParseMilliseconds).Order().ElementAt(10),
                samples.Select(s => s.ParseMilliseconds).Order().ElementAt(18), samples.Select(s => s.ParseAllocatedBytes).Order().ElementAt(10));
        }
        var production = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(Path.GetFullPath(path)));
        var report = FirmamentShadowAnalysis.Compare(syntax, production, source);
        JsonElement? validation = null; int? exit = null;
        if (validate)
        {
            var captured = new StringWriter(); var errors = new StringWriter();
            exit = CliRunner.Run(["validate", path, "--json"], captured, errors);
            using var document = JsonDocument.Parse(captured.ToString()); validation = document.RootElement.Clone();
        }
        var relative = corpusRoot is null ? path : Path.GetRelativePath(corpusRoot, Path.GetFullPath(path)).Replace('\\', '/');
        var group = path.Replace('\\', '/').Split('/').FirstOrDefault(p => p is "Canonical" or "Regression" or "Compatibility" or "Speculative") ?? "Other";
        return new(relative, group, report, measurement, timings, validation, exit, ast ? syntax.Document : null);
    }
}
