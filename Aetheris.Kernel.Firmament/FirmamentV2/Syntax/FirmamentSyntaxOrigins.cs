namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Matches retained syntax by lexical declaration/mapping ownership, never by
/// concatenated offsets. Generated nodes with no authored counterpart remain unmapped.</summary>
internal sealed class FirmamentSyntaxOrigins(DocumentSyntax original, DocumentSyntax input)
{
    private readonly IReadOnlyDictionary<string, FirmamentV2SourceSpan> authored = Index(original);
    private readonly IReadOnlyDictionary<string, FirmamentV2SourceSpan> incoming = Index(input);
    private Dictionary<FirmamentV2SourceSpan, FirmamentV2SourceSpan>? locations;
    internal FirmamentV2SourceSpan? Find(FirmamentV2SourceSpan span)
    {
        locations ??= incoming.Where(p => authored.ContainsKey(p.Key)).GroupBy(p => p.Value)
            .Where(g => g.Select(p => authored[p.Key]).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => authored[g.First().Key]);
        return locations.GetValueOrDefault(span);
    }
    private static IReadOnlyDictionary<string, FirmamentV2SourceSpan> Index(DocumentSyntax syntax)
    {
        var result = new Dictionary<string, FirmamentV2SourceSpan>(StringComparer.Ordinal);
        void Scope(IReadOnlyList<StatementSyntax> statements, string path)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var statement in statements)
            {
                var label = statement switch
                {
                    DeclarationSyntax declaration => declaration.Type.Name.Text + " " + declaration.Name?.Text,
                    MappingSyntax mapping => "mapping " + mapping.Key.Text,
                    FieldSyntax f => "field " + f.Name.Text,
                    ExpressionStatementSyntax { Value: CallSyntax { Target: ReferenceSyntax r } } => "call " + r.Name.Text,
                    _ => statement.GetType().Name
                };
                var ordinal = counts.GetValueOrDefault(label); counts[label] = ordinal + 1;
                var identity = path + "/" + label + "#" + ordinal;
                result[identity] = statement.Span;
                if (statement is DeclarationSyntax d)
                {
                    if (d.Body is not null) Scope(d.Body.Statements, identity);
                    if (d.Initializer is CallSyntax call) result[identity + "/invocation"] = call.Span;
                }
                if (statement is ExpressionStatementSyntax { Value: CallSyntax c }) result[identity + "/invocation"] = c.Span;
                if (statement is MappingSyntax m) Scope([m.Value], identity);
            }
        }
        Scope(syntax.Statements, "document"); return result;
    }
}
