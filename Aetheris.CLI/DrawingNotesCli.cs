using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aetheris.Drawing;

namespace Aetheris.CLI;

internal static class DrawingNotesCli
{
    internal const string Usage = """
Usage:
  aetheris drawing inspect <drawing.pdf> [--json]
  aetheris drawing render <drawing.pdf> --page <N> --out <page.png> [--dpi <N>] [--json]
  aetheris drawing crop <drawing.pdf> --page <N> --bounds <x,y,width,height> --name <name> --out <crop.png> [--dpi <N>] [--json]
  aetheris drawing notes create <drawing.pdf> --out <directory> [--name <name>] [--title <title>] [--visible-date <date>] [--sheets <id,...>] [--json]
  aetheris drawing notes add-region <drawing-notes.json> --id <id> --name <name> --page <N> --bounds <x,y,width,height> --type <type> [--parent <id>] [--confidence <level>] [--json]
  aetheris drawing notes add-note <drawing-notes.json> --id <id> --label <label> --category <category> --page <N> --bounds <x,y,width,height> [--region <id>] [--note <text>] [--original-text <text>] [--aliases <a,b>] [--tags <a,b>] [--class <class>] [--confidence <level>] [--pass <id>] [--json]
  aetheris drawing notes add-dimension <drawing-notes.json> --id <id> --label <label> --value <text> --page <N> --bounds <x,y,width,height> [--region <id>] [--type <type>] [--axis <axis>] [--class <class>] [--confidence <level>] [--interpretation <text>] [--json]
  aetheris drawing notes add-relation <drawing-notes.json> --id <id> --from <id> --to <id> --kind <kind> [--note <text>] [--confidence <level>] [--json]
  aetheris drawing notes add-pass <drawing-notes.json> --id <id> --name <name> [--focus <text>] [--json]
  aetheris drawing notes export <drawing-notes.json> --format <markdown|json> [--out <path>] [--page <N>] [--region <id>] [--class <class>] [--unresolved] [--json]
  aetheris drawing notes validate <drawing-notes.json> [--source <drawing.pdf>] [--json]
  aetheris drawing notes ui-assets --out <directory> [--json]
  aetheris drawing notes serve <drawing-notes.json> --source <drawing.pdf> [--port <N>]

Bounds are PDF page-space points (1/72 inch), top-left origin. The PDF remains authority.
Region types: View, Detail, Section, NoteBlock, DimensionCluster, Keepout, DatumArea, Other.
Categories: Datum, Dimension, Feature, View, Section, Keepout, Note, Relationship, Uncertain, Question.
Relations: Targets, ReferencedFrom, LocatedIn, DetailOf, SectionOf, KeepoutFor, CoordinateIn, RelatedTo.
Confidence: High, Medium, Low, Unresolved. Classification: ProductGeometry, AccessoryKeepout, SensorKeepout, MaterialRestriction, CosmeticReference, FunctionalReference, Unknown.
""";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelp(args[0])) { stdout.WriteLine(Usage); return 0; }
        return args[0] switch
        {
            "inspect" => Inspect(args[1..], stdout, stderr),
            "render" => Render(args[1..], stdout, stderr),
            "crop" => Crop(args[1..], stdout, stderr),
            "notes" => Notes(args[1..], stdout, stderr),
            _ => Fail(stderr, $"Unknown drawing command '{args[0]}'.")
        };
    }

    private static int Inspect(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage);
        var pdf = Path.GetFullPath(args[0]); var json = args.Skip(1).Any(x => x == "--json");
        var document = DrawingPdf.Inspect(pdf);
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = true, source = pdf, document }, CliRunner.JsonOptions));
        else
        {
            stdout.WriteLine($"{document.FileName}: {document.PageCount} page(s), SHA-256 {document.Sha256}");
            foreach (var page in document.Pages) stdout.WriteLine($"  Page {page.Number}: {page.WidthPoints:0.##} x {page.HeightPoints:0.##} pt");
        }
        return 0;
    }

    private static int Render(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage);
        var options = Options(args[1..]);
        if (!Int(options, "--page", out var page) || !Required(options, "--out", out var output)) return Fail(stderr, Usage);
        var dpi = OptionalInt(options, "--dpi", 200);
        DrawingPdf.RenderPage(args[0], page, output, dpi);
        var report = new { success = true, source = Path.GetFullPath(args[0]), page, output = Path.GetFullPath(output), dpi, renderer = DrawingPdf.RendererVersion };
        Write(stdout, options, report, $"Rendered page {page} to {Path.GetFullPath(output)}.");
        return 0;
    }

    private static int Crop(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage);
        var options = Options(args[1..]);
        if (!Int(options, "--page", out var page) || !Bounds(options, out var bounds) || !Required(options, "--name", out var name) || !Required(options, "--out", out var output)) return Fail(stderr, Usage);
        var dpi = OptionalInt(options, "--dpi", 300);
        var metadata = DrawingPdf.Crop(args[0], page, bounds, name, output, dpi);
        Write(stdout, options, new { success = true, output = Path.GetFullPath(output), metadata }, $"Exported source-linked crop '{name}' to {Path.GetFullPath(output)}.");
        return 0;
    }

    private static int Notes(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || IsHelp(args[0])) { stdout.WriteLine(Usage); return 0; }
        return args[0] switch
        {
            "create" => Create(args[1..], stdout, stderr),
            "add-region" => AddRegion(args[1..], stdout, stderr),
            "add-note" => AddNote(args[1..], stdout, stderr, false),
            "add-dimension" => AddNote(args[1..], stdout, stderr, true),
            "add-relation" => AddRelation(args[1..], stdout, stderr),
            "add-pass" => AddPass(args[1..], stdout, stderr),
            "export" => Export(args[1..], stdout, stderr),
            "validate" => Validate(args[1..], stdout, stderr),
            "ui-assets" => UiAssets(args[1..], stdout, stderr),
            "serve" => Serve(args[1..], stdout, stderr),
            _ => Fail(stderr, $"Unknown drawing notes command '{args[0]}'.")
        };
    }

    private static int Create(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage);
        var options = Options(args[1..]);
        if (!Required(options, "--out", out var outputDirectory)) return Fail(stderr, Usage);
        var project = DrawingPdf.CreateProject(args[0], Value(options, "--name"), Value(options, "--title"), Value(options, "--visible-date"), Value(options, "--visible-revision"), Value(options, "--sheets")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "drawing-notes.json");
        DrawingNotesPersistence.Save(path, project);
        File.WriteAllText(Path.Combine(outputDirectory, "drawing-notes.md"), DrawingNotesMarkdown.Export(project));
        DrawingNotesUi.WriteAssets(Path.Combine(outputDirectory, "ui"));
        Write(stdout, options, new { success = true, project = Path.GetFullPath(path), project.Document.PageCount, project.Document.Sha256 }, $"Created Drawing Notes project: {Path.GetFullPath(path)}");
        return 0;
    }

    private static int AddRegion(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage); var options = Options(args[1..]);
        if (!Required(options, "--id", out var id) || !Required(options, "--name", out var name) || !Int(options, "--page", out var page) || !Bounds(options, out var bounds) || !Enum(options, "--type", out DrawingRegionType type)) return Fail(stderr, Usage);
        var project = DrawingNotesPersistence.Load(args[0]);
        var region = new DrawingRegion(id, name, page, bounds, type, Value(options, "--parent"), Value(options, "--note"), EnumOr(options, "--confidence", DrawingConfidence.High));
        new DrawingNotebook(project).AddRegion(region); DrawingNotesPersistence.Save(args[0], project);
        Write(stdout, options, new { success = true, region }, $"Added region '{region.Id}'."); return 0;
    }

    private static int AddNote(string[] args, TextWriter stdout, TextWriter stderr, bool dimension)
    {
        if (args.Length == 0) return Fail(stderr, Usage); var options = Options(args[1..]);
        if (!Required(options, "--id", out var id) || !Required(options, "--label", out var label) || !Int(options, "--page", out var page) || !Bounds(options, out var bounds)) return Fail(stderr, Usage);
        DrawingAnnotationCategory category;
        DrawingDimensionInterpretation? interpretation = null;
        if (dimension)
        {
            category = DrawingAnnotationCategory.Dimension;
            if (!Required(options, "--value", out var value)) return Fail(stderr, Usage);
            interpretation = DrawingDimensionParser.Parse(value, EnumOr(options, "--type", DrawingDimensionType.Unknown), NullableEnum<DrawingAxis>(options, "--axis"), Value(options, "--interpretation"));
            interpretation = interpretation with { DatumId = Value(options, "--datum"), SourceFeatureId = Value(options, "--from"), TargetFeatureId = Value(options, "--to") };
        }
        else if (!Enum(options, "--category", out category)) return Fail(stderr, Usage);
        var project = DrawingNotesPersistence.Load(args[0]);
        var annotation = new DrawingAnnotation(id, label, category, page, bounds, Value(options, "--region"), Value(options, "--note"), Value(options, "--original-text"), EnumOr(options, "--confidence", category == DrawingAnnotationCategory.Question ? DrawingConfidence.Unresolved : DrawingConfidence.High), EnumOr(options, "--class", DrawingSemanticClass.Unknown), Split(options, "--tags"), Split(options, "--aliases"), interpretation, Value(options, "--pass"));
        new DrawingNotebook(project).AddAnnotation(annotation); DrawingNotesPersistence.Save(args[0], project);
        Write(stdout, options, new { success = true, annotation }, $"Added {category} note '{annotation.Id}'."); return 0;
    }

    private static int AddRelation(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage); var options = Options(args[1..]);
        if (!Required(options, "--id", out var id) || !Required(options, "--from", out var from) || !Required(options, "--to", out var to) || !Enum(options, "--kind", out DrawingRelationKind kind)) return Fail(stderr, Usage);
        var project = DrawingNotesPersistence.Load(args[0]); var relation = new DrawingRelation(id, from, to, kind, Value(options, "--note"), EnumOr(options, "--confidence", DrawingConfidence.High));
        new DrawingNotebook(project).AddRelation(relation); DrawingNotesPersistence.Save(args[0], project);
        Write(stdout, options, new { success = true, relation }, $"Added relation '{relation.Id}'."); return 0;
    }

    private static int AddPass(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage); var options = Options(args[1..]);
        if (!Required(options, "--id", out var id) || !Required(options, "--name", out var name)) return Fail(stderr, Usage);
        var project = DrawingNotesPersistence.Load(args[0]); var pass = new DrawingPass(id, name, Value(options, "--focus"));
        new DrawingNotebook(project).AddPass(pass); DrawingNotesPersistence.Save(args[0], project);
        Write(stdout, options, new { success = true, pass }, $"Added pass '{pass.Id}'."); return 0;
    }

    private static int Export(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage); var options = Options(args[1..]);
        if (!Required(options, "--format", out var format) || format is not ("markdown" or "json")) return Fail(stderr, Usage);
        var project = DrawingNotesPersistence.Load(args[0]);
        var filter = new DrawingExportFilter(NullableInt(options, "--page"), Value(options, "--region"), NullableEnum<DrawingSemanticClass>(options, "--class"), options.ContainsKey("--unresolved"));
        var content = format == "markdown" ? DrawingNotesMarkdown.Export(project, filter) : DrawingNotesPersistence.Serialize(FilterProject(project, filter));
        var output = Value(options, "--out") ?? Path.ChangeExtension(args[0], format == "markdown" ? ".md" : ".export.json");
        File.WriteAllText(output, content); Write(stdout, options, new { success = true, output = Path.GetFullPath(output), format, bytes = new FileInfo(output).Length }, $"Exported {format}: {Path.GetFullPath(output)}"); return 0;
    }

    private static DrawingNotesProject FilterProject(DrawingNotesProject project, DrawingExportFilter filter)
    {
        if (filter == new DrawingExportFilter()) return project;
        var regions = project.Regions.Where(r => (filter.Page is null || r.Page == filter.Page) && (filter.RegionId is null || r.Id == filter.RegionId || r.ParentRegionId == filter.RegionId)).ToList();
        var regionIds = regions.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var annotations = project.Annotations.Where(a => (filter.Page is null || a.Page == filter.Page) && (filter.RegionId is null || a.RegionId is not null && regionIds.Contains(a.RegionId)) && (filter.SemanticClass is null || a.SemanticClass == filter.SemanticClass) && (!filter.UnresolvedOnly || a.Category == DrawingAnnotationCategory.Question || a.Confidence == DrawingConfidence.Unresolved)).ToList();
        var nodeIds = regions.Select(x => x.Id).Concat(annotations.Select(x => x.Id)).ToHashSet(StringComparer.Ordinal);
        return new DrawingNotesProject { Name = project.Name, Document = project.Document, Regions = regions, Annotations = annotations, Relations = project.Relations.Where(r => nodeIds.Contains(r.FromId) && nodeIds.Contains(r.ToId)).ToList(), Passes = project.Passes.ToList() };
    }

    private static int Validate(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage); var options = Options(args[1..]); var project = DrawingNotesPersistence.Load(args[0]); var notebook = new DrawingNotebook(project);
        bool? sourceMatches = null; if (Value(options, "--source") is { } source) sourceMatches = notebook.SourceMatches(source);
        var issues = notebook.InspectIssues().ToList(); if (sourceMatches == false) issues.Add(new("drawing-source-hash-mismatch", "The supplied PDF does not match the project source SHA-256.", []));
        var success = issues.All(issue => issue.Code == "drawing-likely-duplicate");
        Write(stdout, options, new { success, sourceMatches, regionCount = project.Regions.Count, annotationCount = project.Annotations.Count, relationCount = project.Relations.Count, unresolvedCount = project.Annotations.Count(a => a.Category == DrawingAnnotationCategory.Question || a.Confidence == DrawingConfidence.Unresolved), issues }, success ? "Drawing Notes project is valid." : "Drawing Notes project requires review.");
        return success ? 0 : 1;
    }

    private static int UiAssets(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var options = Options(args); if (!Required(options, "--out", out var output)) return Fail(stderr, Usage); DrawingNotesUi.WriteAssets(output);
        Write(stdout, options, new { success = true, output = Path.GetFullPath(Path.Combine(output, "index.html")) }, $"Wrote Drawing Notes UI assets to {Path.GetFullPath(output)}."); return 0;
    }

    private static int Serve(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return Fail(stderr, Usage); var options = Options(args[1..]); if (!Required(options, "--source", out var source)) return Fail(stderr, Usage); var port = OptionalInt(options, "--port", 4178);
        using var cancellation = new CancellationTokenSource(); ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); }; Console.CancelKeyPress += handler;
        try
        {
            var task = DrawingNotesUi.ServeAsync(Path.GetFullPath(args[0]), Path.GetFullPath(source), port, stdout, cancellation.Token);
            if (!Console.IsOutputRedirected) Process.Start(new ProcessStartInfo($"http://127.0.0.1:{port}/") { UseShellExecute = true });
            task.GetAwaiter().GetResult(); return 0;
        }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static Dictionary<string, string?> Options(string[] args)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new DrawingNotesException("drawing-option-invalid", $"Unexpected value '{args[i]}'.");
            if (args[i] is "--json" or "--unresolved") result[args[i]] = null;
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) result[args[i]] = args[++i];
            else throw new DrawingNotesException("drawing-option-value-missing", $"Option '{args[i]}' requires a value.");
        }
        return result;
    }

    private static string? Value(IReadOnlyDictionary<string, string?> options, string key) => options.TryGetValue(key, out var value) ? value : null;
    private static bool Required(IReadOnlyDictionary<string, string?> options, string key, out string value) { value = Value(options, key) ?? ""; return value.Length > 0; }
    private static bool Int(IReadOnlyDictionary<string, string?> options, string key, out int value) => int.TryParse(Value(options, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    private static int OptionalInt(IReadOnlyDictionary<string, string?> options, string key, int fallback) => Value(options, key) is { } text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static int? NullableInt(IReadOnlyDictionary<string, string?> options, string key) => Value(options, key) is { } text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static bool Bounds(IReadOnlyDictionary<string, string?> options, out DrawingBounds bounds)
    {
        bounds = new(0, 0, 0, 0); var parts = Value(options, "--bounds")?.Split(','); if (parts?.Length != 4) return false;
        var values = new double[4]; for (var i = 0; i < 4; i++) if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
        bounds = new(values[0], values[1], values[2], values[3]); return true;
    }
    private static bool Enum<T>(IReadOnlyDictionary<string, string?> options, string key, out T value) where T : struct, Enum => System.Enum.TryParse(Value(options, key), true, out value);
    private static T EnumOr<T>(IReadOnlyDictionary<string, string?> options, string key, T fallback) where T : struct, Enum => Enum<T>(options, key, out var value) ? value : fallback;
    private static T? NullableEnum<T>(IReadOnlyDictionary<string, string?> options, string key) where T : struct, Enum => Enum<T>(options, key, out var value) ? value : null;
    private static string[]? Split(IReadOnlyDictionary<string, string?> options, string key) => Value(options, key)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static bool IsHelp(string value) => value is "--help" or "-h" or "help";
    private static int Fail(TextWriter stderr, string message) { stderr.WriteLine(message); return 1; }
    private static void Write(TextWriter stdout, IReadOnlyDictionary<string, string?> options, object report, string human) => stdout.WriteLine(options.ContainsKey("--json") ? JsonSerializer.Serialize(report, CliRunner.JsonOptions) : human);
}
