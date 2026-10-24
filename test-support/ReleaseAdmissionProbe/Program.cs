using System.Text.Json;
using Aetheris.CLI;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

if (args.Length is not (1 or 2)) throw new ArgumentException("Usage: ReleaseAdmissionProbe <evidence-directory> [previous-summary.json]");
var root = Directory.GetCurrentDirectory();
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
if (File.Exists(Path.Combine(output, "probes.jsonl"))) throw new ArgumentException("Use a fresh evidence directory.");
var replay = args.Length == 2 ? JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.EnumerateArray()
    .Where(r => r.GetProperty("accepted").GetBoolean() && r.GetProperty("category").GetString() == "closed-authoring")
    .Select(r => (Path.GetFullPath(r.GetProperty("source").GetString()!), r.GetProperty("offset").GetInt32())).ToHashSet() : null;
var results = new List<object>();
var files = Directory.GetFiles(Path.Combine(root, "fixtures/Canonical"), "*", SearchOption.AllDirectories)
    .Where(p => Path.GetExtension(p) is ".firmament" or ".firmasm" or ".firmscene")
    .Where(p => !Path.GetFileName(p).StartsWith(".release-probe-", StringComparison.Ordinal)).Select(Path.GetFullPath).Order().ToArray();
foreach (var file in files)
{
    if (replay is not null && !replay.Any(r => r.Item1 == file)) continue;
    var source = File.ReadAllText(file);
    if (Validate(file).Exit != 0) continue;
    var syntax = FirmamentSyntaxParser.Parse(source).Document;
    var declarations = FirmamentSyntaxTraversal.Declarations(syntax).Where(d => d.Body is { Delimited: true }).ToArray();
    var candidates = declarations.Select(d => (Body: d.Body!, Owner: d.Type.Name.Text, Name: d.Name?.Text,
            Data: d.Type.Name.Text is "Record" or "Static" or "Table" || d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept")))
        .Concat(FirmamentSyntaxTraversal.Expressions(syntax).OfType<ObjectSyntax>().Where(o => o.Body.Delimited)
            .Select(o => (Body: o.Body, Owner: FirmamentSyntaxValues.Reference(o.Type) ?? "object", Name: o.Name?.Text, Data: false)))
        .DistinctBy(c => c.Body.ContentSpan.Start);
    foreach (var candidate in candidates)
    {
        var position = candidate.Body.ContentSpan.Start;
        if (replay is not null && !replay.Contains((file, position))) continue;
        var mutant = source.Insert(position, " zzUnknownField: 3mm; ");
        // Real CLI include/resource resolution requires the original module directory.
        // Only evidence is retained; the temporary source is always removed.
        var path = Path.Combine(Path.GetDirectoryName(file)!, ".release-probe-" + Guid.NewGuid().ToString("N") + Path.GetExtension(file));
        try
        {
            File.WriteAllText(path, mutant);
            var result = Validate(path);
            var authoredData = candidate.Data;
            var template = declarations.Any(d => (d.Type.Name.Text == "Feature" || d.Modifiers.Any(m => m.Name.Text == "Template")) &&
                position >= d.Span.Start && position < d.Span.Start + d.Span.Length);
            var row = new { source = Path.GetRelativePath(root, file), owner = candidate.Owner,
                name = candidate.Name, offset = position, accepted = result.Exit == 0,
                category = authoredData ? "authored-data" : template ? "template-definition" : "closed-authoring",
                diagnostics = result.Exit == 0 ? null : result.Output };
            results.Add(row);
            File.AppendAllText(Path.Combine(output, "probes.jsonl"), JsonSerializer.Serialize(row) + "\n");
            if (result.Exit == 0 && !authoredData) Console.WriteLine($"ACCEPTED {row.source}:{position} {row.owner} {row.category}");
        }
        finally { File.Delete(path); }
    }
    Console.WriteLine($"Probed {Path.GetRelativePath(root, file)} ({results.Count} cases)");
}
if (replay is not null && results.Count == 0) throw new InvalidOperationException("Replay matched no specimens; qualification is incomplete.");
File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

static (int Exit, string Output) Validate(string path)
{
    var stdout = new StringWriter();
    var stderr = new StringWriter();
    var exit = CliRunner.Run(["validate", path, "--json"], stdout, stderr);
    return (exit, stdout.ToString() + stderr);
}
