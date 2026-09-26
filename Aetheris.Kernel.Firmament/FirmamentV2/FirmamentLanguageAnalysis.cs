using System.Text;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentLanguageToken(int Start, int Length, string Kind);
public sealed record FirmamentLanguageDiagnostic(string Severity, string Code, string Message, int Start, int Length);
public sealed record FirmamentLanguageAnalysis(string Document, string Revision,
    IReadOnlyList<FirmamentLanguageToken> Tokens, IReadOnlyList<FirmamentLanguageDiagnostic> Diagnostics);
public sealed record FirmamentLanguageLocation(string Document, int Start, int Length);
public sealed record FirmamentLanguageHover(string Document, string Revision, int Start, int Length, string Title, string Description);
public sealed record FirmamentLanguageFormat(string Document, string Revision, string Text, bool Changed);

/// <summary>
/// Source-side projection of the existing Firmament parser and generated semantic schema.
/// It never constructs a BRep. Lexical spans support editor colors and source navigation;
/// parser diagnostics and construct/field descriptions remain compiler owned.
/// </summary>
public static class FirmamentLanguageAnalysisService
{
    private sealed record Lexeme(int Start, int Length, string Text, string Shape);
    private static readonly HashSet<string> Keywords = ["Model", "Units", "Modify", "WireForm", "Construction", "Use", "schema", "Let", "Static", "With", "Expose"];
    private static readonly HashSet<string> Types = ["Length", "Angle", "Float", "Int", "Bool", "Box3", "Plane", "Point2", "Point3"];
    private static readonly HashSet<string> Choices = FirmamentSemanticSchemas.All
        .SelectMany(construct => construct.Fields).SelectMany(field => field.Choices).ToHashSet(StringComparer.Ordinal);

    public static FirmamentLanguageAnalysis Analyze(string source, string document, string revision)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexemes = Lex(source);
        var tokens = lexemes.Select((lexeme, index) => new FirmamentLanguageToken(
            lexeme.Start, lexeme.Length, Classify(lexemes, index))).ToArray();
        var parse = FirmamentV2Parser.Parse(source);
        var failures = parse.Diagnostics.Where(item => FirmamentV2Parser.IsFatalDiagnosticCode(item) ||
            item == "firmament-v2-parse-failed").Distinct(StringComparer.Ordinal).ToArray();
        if (failures.Length > 1) failures = failures.Where(item => item != "firmament-v2-parse-failed").ToArray();
        var diagnostics = failures.Select(item => LocateDiagnostic(source, lexemes, item)).ToArray();
        return new(document, revision, tokens, diagnostics);
    }

    public static FirmamentLanguageHover? Hover(string source, string document, string revision, int offset)
    {
        var lexemes = Lex(source);
        var index = lexemes.FindIndex(item => item.Start <= offset && offset <= item.Start + item.Length && item.Shape == "identifier");
        if (index < 0) return null;
        var token = lexemes[index];
        var construct = FirmamentSemanticSchemas.All.FirstOrDefault(item => item.Name == token.Text);
        if (construct is not null)
            return new(document, revision, token.Start, token.Length, construct.Name, construct.Description ?? construct.Context ?? "Firmament construct");
        var field = FirmamentSemanticSchemas.All.SelectMany(item => item.Fields).FirstOrDefault(item => item.Name == token.Text);
        if (field is not null && Classify(lexemes, index) == "field")
            return new(document, revision, token.Start, token.Length, field.Name,
                $"{field.Kind}{(field.Unit == FirmamentUnitKind.None ? "" : $" · {field.Unit}")}. {field.Description ?? ""}".Trim());
        return null;
    }

    public static FirmamentLanguageLocation? Definition(string source, string document, int offset)
    {
        var tokens = Lex(source);
        var selected = tokens.FirstOrDefault(item => item.Shape == "identifier" && item.Start <= offset && offset <= item.Start + item.Length);
        if (selected is null) return null;
        var declarations = tokens.Select((token, index) => (token, index))
            .Where(pair => pair.index > 0 && pair.token.Text == selected.Text && pair.token.Shape == "identifier" &&
                FirmamentSemanticSchemas.All.Any(schema => schema.Name == tokens[pair.index - 1].Text) &&
                pair.index + 1 < tokens.Count && tokens[pair.index + 1].Text == "{")
            .Select(pair => pair.token).ToArray();
        return declarations.Length == 1 ? new(document, declarations[0].Start, declarations[0].Length) : null;
    }

    public static FirmamentLanguageFormat Format(string source, string document, string revision)
    {
        if (!FirmamentV2Parser.Parse(source).IsSuccess)
            throw new InvalidOperationException("Format Document requires valid Firmament source. Fix syntax errors first.");
        var lexemes = Lex(source);
        var output = new StringBuilder(source.Length + 64);
        var indent = 0;
        var lineStart = true;
        Lexeme? previous = null;
        foreach (var token in lexemes)
        {
            var gap = previous is null ? string.Empty : source[(previous.Start + previous.Length)..token.Start];
            if (token.Text == "}")
            {
                indent = Math.Max(0, indent - 1);
                BreakLine();
            }
            else if (gap.Contains('\n') && token.Text != ";" && previous?.Text != "{") BreakLine();

            if (lineStart) { output.Append(' ', indent * 2); lineStart = false; }
            else if (NeedsSpace(previous?.Text, token.Text)) output.Append(' ');
            output.Append(token.Text);
            if (token.Shape == "comment" || token.Text is "{" or ";" or "}")
            {
                if (token.Text == "{") indent++;
                BreakLine();
            }
            previous = token;
        }
        var formatted = output.ToString().TrimEnd() + "\n";
        if (!FirmamentV2Parser.Parse(formatted).IsSuccess)
            throw new InvalidOperationException("Canonical layout was refused because it changed parser admission.");
        return new(document, revision, formatted, formatted != source);

        void BreakLine()
        {
            if (!lineStart) { output.Append('\n'); lineStart = true; }
        }
    }

    private static bool NeedsSpace(string? previous, string current)
    {
        if (previous is null || previous is "(" or "[" or "<" or "." or "+" or "-") return false;
        if (current is ")" or "]" or ">" or "." or ":" or "," or ";" or "(" or "[") return false;
        return true;
    }

    private static string Classify(IReadOnlyList<Lexeme> tokens, int index)
    {
        var token = tokens[index];
        if (token.Shape != "identifier") return token.Shape;
        if (Keywords.Contains(token.Text)) return "keyword";
        if (FirmamentSemanticSchemas.All.Any(item => item.Name == token.Text)) return "construct";
        if (Types.Contains(token.Text)) return "type";
        if (index + 1 < tokens.Count && tokens[index + 1].Text == ":" &&
            FirmamentSemanticSchemas.All.Any(item => item.Fields.Any(field => field.Name == token.Text))) return "field";
        if (token.Text is "mm" or "cm" or "m" or "deg" or "rad") return "unit";
        if (Choices.Contains(token.Text)) return "value";
        if (token.Text == "face" && index + 1 < tokens.Count && tokens[index + 1].Text == "(") return "selector";
        return "identifier";
    }

    private static FirmamentLanguageDiagnostic LocateDiagnostic(string source, IReadOnlyList<Lexeme> tokens, string diagnostic)
    {
        var separator = diagnostic.LastIndexOf(':');
        var code = separator > 0 ? diagnostic[..separator] : diagnostic;
        var subject = separator > 0 ? diagnostic[(separator + 1)..] : string.Empty;
        var match = tokens.FirstOrDefault(token => token.Text == subject);
        var start = match?.Start ?? Math.Max(0, source.Length - 1);
        var length = match?.Length ?? (source.Length == 0 ? 0 : 1);
        return new("error", code, diagnostic.Replace('-', ' '), start, length);
    }

    private static List<Lexeme> Lex(string source)
    {
        var result = new List<Lexeme>();
        for (var at = 0; at < source.Length;)
        {
            if (char.IsWhiteSpace(source[at])) { at++; continue; }
            var start = at;
            string shape;
            if (at + 1 < source.Length && source[at] == '/' && source[at + 1] == '/')
            {
                at += 2;
                while (at < source.Length && source[at] != '\n') at++;
                shape = "comment";
            }
            else if (source[at] == '"')
            {
                at++;
                while (at < source.Length)
                {
                    if (source[at++] != '"') continue;
                    if (at >= 2 && source[at - 2] == '\\') continue;
                    break;
                }
                shape = "string";
            }
            else if (char.IsLetter(source[at]) || source[at] == '_')
            {
                at++;
                while (at < source.Length && (char.IsLetterOrDigit(source[at]) || source[at] == '_')) at++;
                shape = "identifier";
            }
            else if (char.IsDigit(source[at]) || source[at] == '.' && at + 1 < source.Length && char.IsDigit(source[at + 1]))
            {
                at++;
                while (at < source.Length && (char.IsDigit(source[at]) || source[at] == '.')) at++;
                shape = "number";
            }
            else { at++; shape = "punctuation"; }
            result.Add(new(start, at - start, source[start..at], shape));
        }
        return result;
    }
}
