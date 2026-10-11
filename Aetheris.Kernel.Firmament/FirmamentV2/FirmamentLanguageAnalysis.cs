using System.Text;
using Aetheris.Kernel.Firmament.Scene;
using Aetheris.Kernel.Firmament.Assembly;

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
    internal sealed record Lexeme(int Start, int Length, string Text, string Shape);
    private static readonly HashSet<string> Keywords = ["Model", "Units", "Modify", "WireForm", "Construction", "Use", "schema", "Let", "Static", "With", "with", "Expose", "Include", "Subassembly", "Assembly", "Part", "Template", "Function", "Record", "Pattern", "Over", "Using", "Output"];
    private static readonly HashSet<string> DeclarationKinds = ["Model", "Struct", "Subassembly", "Assembly", "Template", "Profile", "Static", "Record", "Function", "Pattern", "Section", "SectionChain", "FrameTransform", "Points"];
    private static readonly HashSet<string> Types = ["Length", "Angle", "Float", "Int", "Bool", "Box3", "Plane", "Point2", "Point3"];
    private static readonly HashSet<string> Choices = FirmamentSemanticSchemas.All
        .SelectMany(construct => construct.Fields).SelectMany(field => field.Choices).ToHashSet(StringComparer.Ordinal);

    public static FirmamentLanguageAnalysis Analyze(string source, string document, string revision)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexemes = Lex(FirmamentSourceSpelling.Normalize(source));
        var tokens = lexemes.Select((lexeme, index) => new FirmamentLanguageToken(
            lexeme.Start, lexeme.Length, Classify(lexemes, index))).ToArray();
        if (Garment.GarmentAuthoring.HasRoot(source))
        {
            var garment = Garment.GarmentCompiler.Compile(source, document);
            return new(document, revision, tokens, garment.Diagnostics.Select(diagnostic =>
                new FirmamentLanguageDiagnostic("error", diagnostic.Code, diagnostic.Message, 0, 0)).ToArray());
        }
        if (SceneAuthoring.HasRoot(source))
        {
            var scene = SceneAuthoring.Parse(source, document);
            return new(document, revision, tokens, scene.Diagnostics.Select(d => new FirmamentLanguageDiagnostic(
                "error", d.Code, d.Message, 0, 0)).ToArray());
        }
        // Imported authoring modules need project context. Do not feed them into
        // the unrelated concrete-part parser and manufacture syntax errors.
        if (lexemes.Any(t => t.Text == "Include" && t.Shape == "identifier"))
            return new(document, revision, tokens, [new("information", "firmament-language-project-context-required", "Imported declarations require project-aware analysis.", 0, 0)]);
        if (HasAssemblyRoot(source))
        {
            var assembly = new AssemblyM0Parser().Parse(source, document);
            return new(document, revision, tokens, assembly.Diagnostics.Select(d => new FirmamentLanguageDiagnostic(d.Severity == AssemblyDiagnosticSeverity.Error ? "error" : "information", d.Code, d.Message, 0, 0)).ToArray());
        }
        if (SectionChainAuthoringParser.IsSectionChainSource(source))
        {
            var section = SectionChainAuthoringParser.Compile(source, materialize: false);
            return new(document, revision, tokens, section.Diagnostics.Select(d => LocateDiagnostic(source, lexemes, d)).ToArray());
        }
        if (Materializer.WireRouteAuthoring.IsSource(source) && !lexemes.Any(t => t.Shape == "identifier" && t.Text == "Template"))
        {
            var route = Materializer.WireFormAuthoring.Parse(source);
            return new(document, revision, tokens, route.Diagnostics.Select(d => new FirmamentLanguageDiagnostic("error", d.Source, d.Message, 0, 0)).ToArray());
        }
        if (lexemes.Any(t => t.Shape == "identifier" && t.Text is "Template" or "Subassembly" or "Appearance" or "Material"))
            return new(document, revision, tokens, [new("information", "firmament-language-project-context-required", "Declaration modules are validated in their consuming project.", 0, 0)]);
        var parse = FirmamentV2Parser.Parse(source);
        var failures = parse.Diagnostics.Where(item => FirmamentV2Parser.IsFatalDiagnosticCode(item) ||
            item == "firmament-v2-parse-failed").Distinct(StringComparer.Ordinal).ToArray();
        if (failures.Length > 1) failures = failures.Where(item => item != "firmament-v2-parse-failed").ToArray();
        var diagnostics = failures.Select(item => LocateDiagnostic(source, lexemes, item)).ToArray();
        return new(document, revision, tokens, diagnostics);
    }

    public static FirmamentLanguageHover? Hover(string source, string document, string revision, int offset)
    {
        var lexemes = Lex(FirmamentSourceSpelling.Normalize(source));
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
        var tokens = Lex(FirmamentSourceSpelling.Normalize(source));
        var selected = tokens.FirstOrDefault(item => item.Shape == "identifier" && item.Start <= offset && offset <= item.Start + item.Length);
        if (selected is null) return null;
        var declarations = Declarations(tokens, selected.Text, ReferenceKind(tokens, selected)).ToArray();
        return declarations.Length == 1 ? new(document, declarations[0].Start, declarations[0].Length) : null;
    }

    /// <summary>Lexical source navigation only; never grants access to a private placement port.</summary>
    public static FirmamentLanguageLocation? Definition(FirmamentProjectSnapshot project, string document, int offset)
    {
        if (!project.TryResolve(document, out var source)) return null;
        var local = Definition(source, document, offset);
        if (local is not null) return local;
        var tokens = Lex(FirmamentSourceSpelling.Normalize(source));
        var selected = tokens.FirstOrDefault(t => t.Shape == "identifier" && t.Start <= offset && offset < t.Start + t.Length);
        if (selected is null) return null;
        var matches = project.Documents.SelectMany(d => Declarations(Lex(d.Value), selected.Text, ReferenceKind(tokens, selected))
            .Select(t => new FirmamentLanguageLocation(d.Key, t.Start, t.Length))).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static FirmamentLanguageAnalysis Analyze(FirmamentProjectSnapshot project, string document, string revision)
    {
        if (!project.TryResolve(document, out var source)) throw new ArgumentException("Document is absent from the snapshot.", nameof(document));
        if (SceneAuthoring.HasRoot(source) || Garment.GarmentAuthoring.HasRoot(source)) return Analyze(source,document,revision);
        var tokens = Lex(FirmamentSourceSpelling.Normalize(source));
        var parsed = new AssemblyM0Parser().ParseProject(project);
        return new(document, revision, tokens.Select((t, i) => new FirmamentLanguageToken(t.Start, t.Length, Classify(tokens, i))).ToArray(),
            parsed.Diagnostics.Select(d => new FirmamentLanguageDiagnostic(d.Severity == AssemblyDiagnosticSeverity.Error ? "error" : "information", d.Code, d.Message, 0, 0)).ToArray());
    }

    public static bool HasAssemblyRoot(string source)
    {
        var tokens = Lex(source).Where(t => t.Shape != "comment").ToArray();
        var depth = 0;
        for (var i = 0; i + 1 < tokens.Length; i++)
        {
            if (tokens[i].Text == "{") depth++;
            else if (tokens[i].Text == "}") depth--;
            if (depth == 0 && tokens[i].Text == "<" && tokens[i + 1].Text == "Assembly") return true;
            if (depth == 0 && tokens[i].Text == "Assembly" && tokens[i + 1].Shape == "identifier" && (i == 0 || tokens[i - 1].Text != "/")) return true;
        }
        return false;
    }

    private static string? ReferenceKind(IReadOnlyList<Lexeme> tokens, Lexeme selected)
    {
        var at = tokens.ToList().FindIndex(t => t.Start == selected.Start);
        return at >= 2 && tokens[at - 1].Text == ":" && tokens[at - 2].Text is "Material" or "Appearance" or "Profile"
            ? tokens[at - 2].Text : null;
    }

    private static IEnumerable<Lexeme> Declarations(IReadOnlyList<Lexeme> tokens, string name, string? kind = null)
    {
        for (var i = 1; i + 1 < tokens.Count; i++)
        {
            if (tokens[i].Shape != "identifier" || tokens[i].Text != name) continue;
            var previous = i - 1;
            if (tokens[previous].Text == ">")
            {
                while (previous >= 0 && tokens[previous].Text != "<") previous--;
                previous--;
            }
            if (previous < 0 || !(DeclarationKinds.Contains(tokens[previous].Text) || FirmamentSemanticSchemas.All.Any(s => s.Name == tokens[previous].Text))) continue;
            if (kind is not null && tokens[previous].Text != kind) continue;
            if (tokens[i + 1].Text is "{" or "=" or ":" or "(" or "<") yield return tokens[i];
        }
    }

    public static FirmamentLanguageFormat Format(string source, string document, string revision)
    {
        if (Garment.GarmentAuthoring.HasRoot(source))
        {
            var before = Garment.GarmentCompiler.Compile(source, document);
            if (!before.IsSuccess)
            {
                throw new InvalidOperationException("Format Document requires valid Garment source. Fix syntax errors first.");
            }
            var garmentFormat = FormatConventions(source, document, revision);
            var after = Garment.GarmentCompiler.Compile(garmentFormat.Text, document);
            if (!after.IsSuccess || before.Garment!.Cloth.ContentKey != after.Garment!.Cloth.ContentKey)
            {
                throw new InvalidOperationException("Garment formatting changed compilation.");
            }
            return garmentFormat;
        }
        if (SceneAuthoring.HasRoot(source))
        {
            if (!SceneAuthoring.Parse(source,document).IsSuccess)
                throw new InvalidOperationException("Format Document requires valid Scene source. Fix syntax errors first.");
            // Preserve generic specialization text, authored ports and pattern keys.
            var scene = FormatConventions(source,document,revision);
            if (!SceneAuthoring.Parse(scene.Text,document).IsSuccess)
                throw new InvalidOperationException("Scene formatting changed parser admission.");
            return scene;
        }
        if (!FirmamentV2Parser.Parse(source).IsSuccess)
            throw new InvalidOperationException("Format Document requires valid Firmament source. Fix syntax errors first.");
        var preferred = FirmamentSourceSpelling.Prefer(source);
        var lexemes = Lex(preferred);
        var output = new StringBuilder(source.Length + 64);
        var indent = 0;
        var lineStart = true;
        Lexeme? previous = null;
        foreach (var token in lexemes)
        {
            var gap = previous is null ? string.Empty : preferred[(previous.Start + previous.Length)..token.Start];
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
        if (!Lex(FirmamentSourceSpelling.Normalize(source)).Select(t => (t.Text, t.Shape))
            .SequenceEqual(Lex(FirmamentSourceSpelling.Normalize(formatted)).Select(t => (t.Text, t.Shape))))
            throw new InvalidOperationException("Canonical layout was refused because it changed source tokens.");
        if (!FirmamentV2Parser.Parse(formatted).IsSuccess)
            throw new InvalidOperationException("Canonical layout was refused because it changed parser admission.");
        return new(document, revision, formatted, formatted != source);

        void BreakLine()
        {
            if (!lineStart) { output.Append('\n'); lineStart = true; }
        }
    }

    /// <summary>
    /// Conservative project/module pass: only admitted vocabulary spellings change.
    /// Layout, literals, comments, and authored identities remain byte-for-byte intact.
    /// This is not standalone or project validation; use the consuming compiler for that.
    /// </summary>
    public static FirmamentLanguageFormat FormatConventions(string source, string document, string revision)
    {
        var text = FirmamentSourceSpelling.Prefer(source);
        if (FirmamentSourceSpelling.Normalize(text) != FirmamentSourceSpelling.Normalize(source))
            throw new InvalidOperationException("Convention formatting changed canonical source.");
        return new(document, revision, text, text != source);
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

    internal static List<Lexeme> Lex(string source)
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
            else if (at + 1 < source.Length && source[at] == '/' && source[at + 1] == '*')
            {
                var end = source.IndexOf("*/", at + 2, StringComparison.Ordinal);
                at = end < 0 ? source.Length : end + 2;
                shape = "comment";
            }
            else if (source[at] == '"')
            {
                at++;
                while (at < source.Length)
                {
                    if (source[at] == '\\' && at + 1 < source.Length) { at += 2; continue; }
                    if (source[at++] != '"') continue;
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
