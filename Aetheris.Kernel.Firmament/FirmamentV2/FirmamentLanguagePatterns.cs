using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Modules;
using Aetheris.Kernel.Firmament.Scene;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentPatternRow(string Key, int Ordinal, string Value, double[]? Coordinates,
    string? StableId, IReadOnlyList<string> GeneratedIds, FirmamentOutlineSpan? Span)
{
    /// <summary>Exact authored keyed Set value; null for generated or derived entries.</summary>
    public string? Authored { get; init; }
}
public sealed record FirmamentPatternLayout(string Node, string Name, string ElementType, string Kind,
    IReadOnlyList<FirmamentPatternRow> Rows);
public sealed record FirmamentPatternApplication(string Node, string Name, string? Source, string Body,
    IReadOnlyList<string> Controls, IReadOnlyList<string> Consumers, string? ReadOnlyReason);
public sealed record FirmamentPatternProjection(string Version, string Document, string Revision,
    IReadOnlyList<FirmamentPatternLayout> Layouts, IReadOnlyList<FirmamentPatternApplication> Patterns,
    IReadOnlyList<FirmamentOutlineDiagnostic> Diagnostics, string ValidationLevel);

/// <summary>Authoring projection of the production finite-layout binder. Coordinates are local
/// values, never guessed world placements. Full project Preview remains the geometry admission gate.</summary>
public static class FirmamentPatternService
{
    public static FirmamentPatternProjection Project(string source, string document, string revision, FirmamentProjectSnapshot? project = null)
    {
        var parsed = FirmamentSyntaxParser.Parse(source, document);
        var declarations = FirmamentLanguageOutlineService.Declarations(parsed);
        var outline = FirmamentLanguageOutlineService.Outline(source, document, revision);
        var errors = parsed.Diagnostics.Select(Diagnostic).ToList();
        var codes = new List<string>();
        var input = source;
        FirmamentModuleGraph.Projection? projection = null;
        if (project is not null && FirmamentModuleGraph.HasImports(source))
        {
            var graph = FirmamentModuleGraph.Load(source, document, project);
            errors.AddRange(graph.Diagnostics.Select(d => new FirmamentOutlineDiagnostic("error", d.Code, d.Message,
                d.SourceSpan?.Source == document ? new(d.SourceSpan.Start, d.SourceSpan.Length) : null)));
            if (graph.Diagnostics.Count == 0) { projection = graph.ProjectSource(graph.Root); input = projection.Text; }
        }
        FirmamentV2StaticAuthoringDocument? bound = null;
        if (errors.Count == 0)
        {
            // Use the existing expansion owner: this is not an editor evaluator or validator.
            var assembly = FirmamentLanguageAnalysisService.HasAssemblyRoot(input);
            var scene = SceneAuthoring.HasRoot(input);
            if (assembly || scene)
                bound = CanonicalStaticAuthoring.ExpandAssemblyPatterns(input, codes, spatial: scene, report: d => errors.Add(MappedDiagnostic(d)))?.Document;
            else
            {
                var result = FirmamentV2Parser.Parse(input);
                bound = result.Document?.StaticAuthoring;
                errors.AddRange(result.SyntaxDiagnostics.Select(MappedDiagnostic));
                if (!result.IsSuccess) codes.AddRange(result.Diagnostics);
            }
            foreach (var code in codes.Where(c => !errors.Any(e => c.StartsWith(e.Code, StringComparison.Ordinal))))
            {
                var diagnostic = FirmamentInterop.FirmamentV2DiagnosticAdapter.FromParserDiagnosticCode(code);
                errors.Add(new(diagnostic.Severity.ToString().ToLowerInvariant(), code.Split(':')[0], diagnostic.Message, null));
            }
        }
        var layouts = new List<FirmamentPatternLayout>();
        foreach (var point in bound?.ConceptPoints ?? [])
        {
            var node = declarations.FirstOrDefault(d => d.Syntax.Type.Name.Text == "Points"
                && d.Syntax.Name?.Text == point.Name.Split('.').Last()
                && declarations.Any(p => p.Id == d.ParentId && p.Syntax.Name?.Text == point.Name.Split('.')[0]));
            if (node.Syntax is null) continue;
            layouts.Add(new(node.Id, point.Name, point.ElementType, point.RecipeKind ?? "Points",
                point.Points.Select(p => new FirmamentPatternRow(p.Key, p.Ordinal,
                    $"{p.ElementType}({string.Join(", ", p.Coordinates.Select(v => ConceptPointAuthoring.Literal(v, "mm")))})",
                    p.Coordinates.ToArray(), p.StableId, Generated(point.Name, p.Key), MappedSpan(p.SourceSpan))).ToArray()));
        }
        foreach (var set in bound?.Sets ?? [])
        {
            if (set.Name.StartsWith("__ConceptPoints_", StringComparison.Ordinal)) continue;
            var node = declarations.FirstOrDefault(d => d.Syntax.Type.Name.Text == "Static" && d.Syntax.Name?.Text == set.Name);
            if (node.Syntax is null) continue;
            layouts.Add(new(node.Id, set.Name, set.ElementType, "Keyed Set", set.Entries.Select(e =>
                new FirmamentPatternRow(e.Name, e.SourceOrder, e.Value, null, null, Generated(set.Name, e.Name),
                    node.Syntax.Body?.Statements.OfType<MappingSyntax>().FirstOrDefault(m => m.Key.Text == e.Name) is { } entry ? Span(entry.Value.Span) : null)
                { Authored = node.Syntax.Body?.Statements.OfType<MappingSyntax>().FirstOrDefault(m => m.Key.Text == e.Name) is { } mapping ? Slice(mapping.Value.Span) : null }).ToArray()));
        }
        var patterns = declarations.Where(d => d.Syntax.Type.Name.Text == "Pattern").Select(d =>
        {
            var over = d.Syntax.Clauses.FirstOrDefault(c => c.Kind == ClauseKind.Over);
            var reachable = new HashSet<string>(StringComparer.Ordinal) { d.Id };
            if (FirmamentSyntaxValues.Reference(over?.Value) is { } layoutName
                && layouts.FirstOrDefault(l => l.Name == layoutName) is { } layout) reachable.Add(layout.Node);
            bool changed;
            do
            {
                changed = false;
                foreach (var edge in outline.Edges.Where(e => reachable.Contains(e.From) && e.ToDocument is null)) changed |= reachable.Add(edge.To);
                foreach (var child in outline.Nodes.Where(n => n.ParentId is not null && reachable.Contains(n.ParentId))) changed |= reachable.Add(child.Id);
            } while (changed);
            var controls = outline.Nodes.Where(n => reachable.Contains(n.Id)
                && (n.Fields.Count > 0 || n.Value is not null)).Select(n => n.Id).ToArray();
            var consumers = outline.Edges.Where(e => reachable.Contains(e.To) && !reachable.Contains(e.From)).Select(e => e.From).Distinct().ToArray();
            return new FirmamentPatternApplication(d.Id, d.Syntax.Name?.Text ?? "Pattern", over is null ? null : Slice(over.Value.Span),
                d.Syntax.Body is { } body ? source.Substring(body.Span.Start + 1, Math.Max(0, body.Span.Length - 2)).Trim() : "",
                controls, consumers, over is null ? "This legacy Pattern has a dedicated domain owner. Its source and geometry remain supported; this editor changes finite 'over' applications." : null);
        }).ToArray();
        return new("firmament-patterns/1", document, revision, layouts, patterns, errors,
            errors.Any(d => d.Severity is "error" or "fatal") ? "Unresolved" : "LayoutResolvedNotBuilt");

        FirmamentOutlineSpan? MappedSpan(FirmamentV2SourceSpan? span)
        {
            if (projection is null) return Span(span);
            return span is { } s && projection.Original(s) is { } original && original.Source == document
                ? new(original.Start, original.Length) : null;
        }
        FirmamentOutlineDiagnostic MappedDiagnostic(FirmamentV2ValidationDiagnostic d) => new(d.Severity, d.Code, d.Message, MappedSpan(d.SourceSpan));
        string Slice(FirmamentV2SourceSpan span) => source.Substring(span.Start, span.Length);
        IReadOnlyList<string> Generated(string name, string key) => (bound?.Patterns ?? [])
            .Where(p => p.Source == name).SelectMany(p => p.Associations ?? []).Where(a => a.SourceEntry == key).Select(a => a.GeneratedId).ToArray();
    }

    private static FirmamentOutlineSpan? Span(FirmamentV2SourceSpan? span) => span is { } s ? new(s.Start, s.Length) : null;
    private static FirmamentOutlineDiagnostic Diagnostic(FirmamentV2ValidationDiagnostic d) => new(d.Severity, d.Code, d.Message, Span(d.SourceSpan));
}

public static partial class FirmamentLanguageEditService
{
    private static (string, string?) SetSetEntry(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        var key = RequireIdentifier(op.Field, "Set entry key");
        if (declaration.Type.Name.Text != "Static"
            || declaration.Annotation is not SpecializationSyntax { Target: ReferenceSyntax { Name.Text: "Set" } })
            throw new FirmamentLanguageEditException("edit-set-required", "Choose an authored Static Set.");
        var entries = declaration.Body?.Statements.OfType<MappingSyntax>().Where(m => m.Key.Text == key).ToArray() ?? [];
        if (entries.Length != 1)
            throw new FirmamentLanguageEditException("edit-set-entry-readonly", "Choose one uniquely authored Set entry. Derived values remain owned by their source.");
        var value = RequireValue(op.Value, key);
        return (Replace(text, entries[0].Value.Span.Start, entries[0].Value.Span.Length, value), id);
    }

    private static (string, string?) SetPattern(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        if (declaration.Type.Name.Text != "Pattern" || declaration.Body is null
            || !declaration.Clauses.Any(c => c.Kind == ClauseKind.Over))
            throw new FirmamentLanguageEditException("edit-pattern-required", "Choose a finite Pattern over a collection.");
        if (op.Kind == "setPatternSource")
        {
            var clause = declaration.Clauses.Single(c => c.Kind == ClauseKind.Over);
            var value = RequireValue(op.Value, "source");
            return (Replace(text, clause.Value.Span.Start, clause.Value.Span.Length, value), id);
        }
        if (string.IsNullOrWhiteSpace(op.Value))
            throw new FirmamentLanguageEditException("edit-pattern-body-required", "A Pattern needs its keyed mapping and recipe.");
        var body = declaration.Body.Span;
        return (Replace(text, body.Start + 1, body.Length - 2, "\n" + op.Value.Trim() + "\n"), id);
    }
}
