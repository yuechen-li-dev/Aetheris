using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Value substitution inside the existing expansion owners. Shared AST declaration
/// spans protect local binders; lexer spans protect labels, members, strings and comments.</summary>
internal static class FirmamentValueSubstitution
{
    internal static string Apply(string source, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0) return source;
        const string prefix = "Struct Binding {\n";
        var syntax = FirmamentSyntaxParser.Parse(prefix + source + "\n}");
        var declarations = FirmamentSyntaxTraversal.Declarations(syntax.Document).ToArray();
        var names = declarations.Where(d => d.Name is not null).Select(d => d.Name!.Span.Start - prefix.Length).ToHashSet();
        var scopes = declarations.Where(d => d.Body is not null).Select(d => (Span: d.Body!.Span,
            Names: d.Parameters.Select(p => p.Name?.Text).OfType<string>()
                .Concat(d.Modifiers.Where(m => m.Name.Text == "Template").SelectMany(m => m.Arguments).Select(a => a.Name?.Text).OfType<string>())
                .Concat(d.Type.Name.Text == "Feature" ? d.Body.Statements.OfType<DeclarationSyntax>()
                    .Where(l => l.Type.Name.Text == "let").Select(l => l.Name?.Text).OfType<string>() : [])
                .ToHashSet(StringComparer.Ordinal))).Where(s => s.Names.Count > 0).ToArray();
        var tokens = FirmamentLanguageAnalysisService.Lex(source).Where(t => t.Shape != "comment").ToArray();
        var changes = new List<(int Start, int Length, string Value)>();
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Shape == "string" || names.Contains(tokens[i].Start) || i > 0 && tokens[i - 1].Text == "."
                || i + 1 < tokens.Length && tokens[i + 1].Text == ":") continue;
            if (scopes.Any(s => tokens[i].Start + prefix.Length >= s.Span.Start
                && tokens[i].Start + prefix.Length < s.Span.Start + s.Span.Length && s.Names.Contains(tokens[i].Text))) continue;
            var key = tokens[i].Text;
            var last = i;
            var matched = values.TryGetValue(key, out var replacement);
            var matchedLast = i;
            while (last + 2 < tokens.Length && tokens[last + 1].Text == ".")
            {
                key += "." + tokens[last + 2].Text;
                last += 2;
                if (values.TryGetValue(key, out var member)) { matched = true; matchedLast = last; replacement = member; }
            }
            if (!matched) continue;
            changes.Add((tokens[i].Start, tokens[matchedLast].Start + tokens[matchedLast].Length - tokens[i].Start, replacement!));
            i = matchedLast;
        }
        foreach (var change in changes.OrderByDescending(c => c.Start))
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Value);
        return source;
    }
}
