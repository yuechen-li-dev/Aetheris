using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Deterministic structural evidence, independent of production route and
/// shifted source offsets. Original token spellings and scope order remain significant.</summary>
public static class FirmamentSyntaxEvidence
{
    private static readonly JsonSerializerOptions StructuralJson = new() { MaxDepth = 1024 };
    public static string Normalize(StatementSyntax node)
    {
        var json = JsonSerializer.SerializeToNode(node, StructuralJson)!;
        RemoveSpans(json);
        return json.ToJsonString(StructuralJson);
    }

    public static string Fingerprint(DocumentSyntax document) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", document.Statements.Select(Normalize)))));

    public static IEnumerable<DeclarationSyntax> Declarations(DocumentSyntax document) => FirmamentSyntaxTraversal.Declarations(document);
    public static IEnumerable<DeclarationSyntax> Declarations(StatementSyntax statement) => FirmamentSyntaxTraversal.Declarations(statement);
    public static IEnumerable<StatementSyntax> Statements(DocumentSyntax document) => FirmamentSyntaxTraversal.Statements(document);

    private static void RemoveSpans(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove("Span");
            foreach (var property in obj) RemoveSpans(property.Value);
        }
        else if (node is JsonArray array) foreach (var item in array) RemoveSpans(item);
    }

    // Only literal comparison, not an expression evaluator or admission policy.
    internal static double[]? LiteralMillimeters(ExpressionSyntax expression)
    {
        var sharedUnit = expression is QuantitySyntax q ? q.Unit.Text : null;
        var value = expression is QuantitySyntax quantity ? quantity.Value : expression;
        if (value is not SequenceSyntax { Kind: SequenceKind.List } sequence) return null;
        var values = new List<double>();
        foreach (var arg in sequence.Items)
        {
            var literal = arg.Value is QuantitySyntax item ? item.Value : arg.Value;
            var unit = arg.Value is QuantitySyntax itemQuantity ? itemQuantity.Unit.Text : sharedUnit;
            if (literal is not LiteralSyntax { Kind: LiteralKind.Number } number || unit != "mm"
                || !double.TryParse(number.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var scalar)) return null;
            values.Add(scalar);
        }
        return values.ToArray();
    }
}

public enum ShadowDifferenceCategory { A_NewParserRegression, B_ExistingParserDefect, C_LanguageAmbiguity, D_CompatibilityAlias, E_UnsupportedSource, Unclassified }
public sealed record ShadowDifference(string Code, ShadowDifferenceCategory Category, string Message,
    FirmamentV2SourceSpan? SourceSpan, string? Context);
public sealed record ShadowComparison(int ComparedFacts, int UncomparedDeclarations, int UncomparedFields, bool SemanticParityQualified,
    IReadOnlyList<ShadowDifference> Differences, IReadOnlyList<string> Limitations);
public sealed record SyntaxSummary(int TokenCount, int DeclarationCount, int FieldCount,
    IReadOnlyDictionary<string, int> ConstructCounts, string Fingerprint);
public sealed record ProductionSyntaxProjection(bool IsSuccess, string Disposition, string? ParserRoute,
    string? ModelName, int TemplateInstanceCount, int FeatureInvocationCount, int StaticPatternGeneratedCount,
    IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
public sealed record FirmamentShadowReport(string LanguageVersion, string ShadowStatus, string ValidationLevel,
    SyntaxSummary Summary, ProductionSyntaxProjection ProductionProjection, ShadowComparison Comparison,
    IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);

/// <summary>Comparison is deliberately partial. Legacy AIR has already erased most
/// fields and source scopes. A missing receipt is a gap, never a parity success.</summary>
public static class FirmamentShadowAnalysis
{
    public static FirmamentShadowReport Compare(SyntaxParseResult syntax, FirmamentV2ParseResult production, string source)
    {
        var declarations = FirmamentSyntaxEvidence.Declarations(syntax.Document).ToArray();
        var differences = new List<ShadowDifference>();
        var compared = new HashSet<DeclarationSyntax>();
        var facts = 0;
        var comparedFields = 0;
        var doc = production.Document;
        var model = syntax.Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text == "Model");
        if (model?.Name is not null && doc is not null)
        {
            facts++; compared.Add(model);
            if (model.Name.Text != doc.ModelName) differences.Add(new("owner-name-mismatch", ShadowDifferenceCategory.Unclassified,
                $"Original Model '{model.Name.Text}' differs from production owner '{doc.ModelName}'.", model.Name.Span, "Model"));
        }
        foreach (var solid in doc?.Solids ?? [])
        {
            var named = declarations.Where(d => d.Name?.Text == solid.Name).ToArray();
            var typed = named.Where(d => d.Type.Name.Text == solid.RecordType).ToArray();
            var candidates = typed.Length > 0 ? typed : named;
            if (candidates.Length != 1) continue; // expansion/scoping requires X2 provenance
            var candidate = candidates[0]; compared.Add(candidate); facts++;
            if (candidate.Type.Name.Text != solid.RecordType)
                differences.Add(new("primitive-type-mismatch", ShadowDifferenceCategory.Unclassified,
                    $"Original declaration type '{candidate.Type.Name.Text}' differs from production record '{solid.RecordType}'. Expansion may require provenance.", candidate.Type.Span, solid.Name));
            if (solid.Box is null) continue;
            var sizes = candidate.Body?.Statements.OfType<FieldSyntax>().Where(f => f.Name.Text is "Size" or "size").ToArray() ?? [];
            if (sizes.Length != 1) continue;
            var size = sizes[0];
            var literals = FirmamentSyntaxEvidence.LiteralMillimeters(size.Value);
            if (literals is null) continue;
            facts++;
            comparedFields++;
            if (!literals.SequenceEqual(solid.Box.Size)) differences.Add(new("box-size-mismatch", ShadowDifferenceCategory.Unclassified,
                "Original literal Box size differs from the production primitive record.", size.Span, solid.Name));
        }
        foreach (var compose in doc?.Composes ?? [])
        {
            var candidates = declarations.Where(d => d.Type.Name.Text == "Compose" && d.Name?.Text == compose.Name).ToArray();
            if (candidates.Length != 1) continue;
            var declaration = candidates[0]; compared.Add(declaration);
            foreach (var child in declaration.Body?.Statements.OfType<DeclarationSyntax>() ?? [])
            {
                if (child.Name is null) continue;
                facts++;
                if (!compose.Operations.Contains(child.Name.Text, StringComparer.Ordinal))
                    differences.Add(new("compose-operation-without-receipt", ShadowDifferenceCategory.Unclassified,
                        $"Source {child.Type.Name.Text} '{child.Name.Text}' has no operation in production Compose '{compose.Name}'. Expansion may require provenance; this is not semantic parity.", child.Span, compose.Name));
            }
        }
        if (!syntax.IsSuccess && production.IsSuccess)
            differences.Add(new("production-accepted-shadow-rejected", ShadowDifferenceCategory.Unclassified,
                "Production projection accepts source rejected by the shadow grammar. Classify each syntax diagnostic before migration.", null, null));
        var existingReport = FirmamentV2ValidationReportBuilder.Build(production, syntax.Document.SourceFile, sourceText: source);
        var tokens = syntax.Document.Tokens;
        var leading = tokens.Where(t => t.Kind is not (SyntaxTokenKind.Whitespace or SyntaxTokenKind.Comment or SyntaxTokenKind.End)).Take(2).ToArray();
        var legacy = production.ParserRoute == "LegacySerialization";
        var fieldCount = FirmamentSyntaxEvidence.Statements(syntax.Document).OfType<FieldSyntax>().Count();
        var summary = new SyntaxSummary(tokens.Count, declarations.Length, fieldCount,
            declarations.GroupBy(d => d.Type.Name.Text).OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal), FirmamentSyntaxEvidence.Fingerprint(syntax.Document));
        return new(legacy ? "LegacySerialization" : leading.Length == 0 ? "Empty" : production.Disposition != FirmamentV2ParseDisposition.NotRecognized ? "V2" : "V2Candidate",
            syntax.IsSuccess ? "Parsed" : "Rejected", syntax.ValidationLevel, summary,
            new(production.IsSuccess, production.Disposition.ToString(), production.ParserRoute, doc?.ModelName,
                doc?.TemplateInstantiations?.Count ?? 0, doc?.FeatureInvocations?.Count ?? 0,
                doc?.StaticAuthoring?.Patterns.Sum(p => p.GeneratedCount) ?? 0, existingReport.Diagnostics),
            new(facts, declarations.Length - compared.Count, fieldCount - comparedFields, false, differences.ToArray(),
            ["No semantic admission, expansion, dimensional evaluation or geometry is performed by the shadow parser.",
             "Comparison covers original Model identity, unambiguous primitive type/name, literal-mm Box size and Compose operation receipts only.",
             "Legacy AIR does not retain complete original nesting, field consumption or expansion provenance; other declarations and all unmatched fields are unqualified."]), syntax.Diagnostics);
    }
}
