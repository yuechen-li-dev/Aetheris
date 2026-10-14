using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>
/// BUILD-PERF-M2: the declarations one assembly definition actually depends on.
///
/// An assembly carries one declaration catalog (every included document, concatenated). Each part
/// definition used to be compiled against the whole catalog and cached under its whole text, so
/// editing any included document recompiled every part. This splits the catalog into top-level
/// declarations and keeps the transitive closure of the ones the definition's template references.
/// The materializer compiles that closure, and the definition cache keys on it, so an edit only
/// invalidates the definitions whose closure it touches.
///
/// The split is deliberately conservative; a mistake can only make a closure larger, or make it
/// fail to compile, in which case the materializer falls back to the whole catalog:
/// <list type="bullet">
/// <item>A declaration starts only at a column-0 identifier, outside any bracket, directly after a
/// token that ends a statement ('}' or ';'). Anything else continues the previous declaration, so
/// multi-line expressions are never cut; a declaration merged this way keeps the names of every
/// statement in it.</item>
/// <item>A declaration's names are the identifiers in its header (before its first '{', '=', '('
/// or ':' outside '&lt;…&gt;'), except keywords: the first word of any declaration and a few
/// connective words. A name several declarations share pulls in all of them; a stray name only
/// enlarges closures.</item>
/// <item>Any identifier in a declaration's text references every declaration with that name.</item>
/// <item>A declaration without a name of its own (a modifier, an annotation block) joins every
/// closure whose names it mentions.</item>
/// </list>
/// The closure keeps the declarations in their original order and text, joined by newlines.
/// </summary>
internal static class AssemblyDeclarationScope
{
    private sealed record Declaration(int Start, int End, IReadOnlySet<string> Names, IReadOnlySet<string> References);

    // Words that join a declaration header without naming anything.
    private static readonly string[] Connectives = ["Struct", "Assembly", "Sites", "From", "using", "with", "Set", "Path", "Model", "Interface", "Part", "Template"];
    private static readonly object Gate = new();
    private static string? lastText;
    private static IReadOnlyList<Declaration>? lastDeclarations;

    /// <summary>The closure of <paramref name="name"/>'s declaration, or null when the catalog has no
    /// declaration that owns that name alone (the caller then uses the whole catalog).</summary>
    public static string? Closure(string declarations, string name)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        IReadOnlyList<Declaration> split;
        lock (Gate)
        {
            if (!ReferenceEquals(lastText, declarations) && !string.Equals(lastText, declarations, StringComparison.Ordinal))
            {
                lastDeclarations = Split(declarations);
                lastText = declarations;
            }
            split = lastDeclarations!;
        }
        var owners = split.Where(d => d.Names.Contains(name)).ToArray();
        if (owners.Length == 0) return null;
        var included = new bool[split.Count];
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<int>();
        void Include(int index)
        {
            if (included[index]) return;
            included[index] = true;
            names.UnionWith(split[index].Names);
            pending.Enqueue(index);
        }
        foreach (var owner in owners) Include(IndexOf(split, owner));
        while (true)
        {
            while (pending.Count > 0)
            {
                var references = split[pending.Dequeue()].References;
                for (var i = 0; i < split.Count; i++)
                    if (!included[i] && split[i].Names.Overlaps(references)) Include(i);
            }
            // Unnamed declarations act on what they mention; they follow every closure they touch.
            var joined = false;
            for (var i = 0; i < split.Count; i++)
                if (!included[i] && split[i].Names.Count == 0 && split[i].References.Overlaps(names)) { Include(i); joined = true; }
            if (!joined) break;
        }
        if (included.All(value => value)) return declarations;
        return string.Join("\n", split.Where((_, i) => included[i]).Select(d => declarations[d.Start..d.End]));
    }

    private static int IndexOf(IReadOnlyList<Declaration> split, Declaration declaration)
    {
        for (var i = 0; i < split.Count; i++) if (ReferenceEquals(split[i], declaration)) return i;
        throw new InvalidOperationException("declaration-scope-index");
    }

    private static IReadOnlyList<Declaration> Split(string text)
    {
        var tokens = FirmamentLanguageAnalysisService.Lex(text).Where(token => token.Shape != "comment").ToArray();
        // Candidates: column-0 identifiers outside brackets (statement beginnings). Starts: the
        // candidates that follow a finished statement; only those split declarations.
        var starts = new List<int>();
        var candidates = new List<int>();
        var depth = 0;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            var candidate = depth == 0 && token.Shape == "identifier" && (token.Start == 0 || text[token.Start - 1] == '\n');
            if (candidate) candidates.Add(i);
            if (i == 0) starts.Add(0);
            else if (candidate && tokens[i - 1].Text is "}" or ";") starts.Add(i);
            depth += token.Text switch { "{" or "(" or "[" => 1, "}" or ")" or "]" => -1, _ => 0 };
            if (depth < 0) depth = 0;
        }
        var headers = new List<HashSet<string>>();
        var bodies = new List<(int Start, int End, HashSet<string> References)>();
        for (var s = 0; s < starts.Count; s++)
        {
            var first = starts[s];
            var last = s + 1 < starts.Count ? starts[s + 1] : tokens.Length;
            // A merged declaration (statements without a terminator in between) has one header
            // per statement it contains.
            var header = new HashSet<string>(StringComparer.Ordinal);
            foreach (var headerStart in candidates.Where(c => c >= first && c < last).Prepend(first).Distinct())
            {
                var angle = 0;
                for (var i = headerStart; i < last; i++)
                {
                    var t = tokens[i].Text;
                    if (t is "{" or "=" or "(" or ":" && angle == 0) break;
                    if (t == "<") angle++;
                    else if (t == ">") angle = Math.Max(0, angle - 1);
                    else if (angle == 0 && tokens[i].Shape == "identifier") header.Add(t);
                }
            }
            var references = new HashSet<string>(StringComparer.Ordinal);
            for (var i = first; i < last; i++) if (tokens[i].Shape == "identifier") references.Add(tokens[i].Text);
            headers.Add(header);
            // Text runs from the declaration's first token to the start of the next declaration's,
            // so trailing comments and whitespace stay with the declaration they follow.
            var start = s == 0 ? 0 : tokens[first].Start;
            var end = s + 1 < starts.Count ? tokens[starts[s + 1]].Start : text.Length;
            bodies.Add((start, end, references));
        }
        var keywords = new HashSet<string>(Connectives, StringComparer.Ordinal);
        foreach (var first in candidates.Concat(starts)) if (first < tokens.Length && tokens[first].Shape == "identifier") keywords.Add(tokens[first].Text);
        return bodies.Select((body, index) =>
        {
            var names = headers[index].Where(n => !keywords.Contains(n)).ToHashSet(StringComparer.Ordinal);
            var references = body.References.Where(r => !names.Contains(r)).ToHashSet(StringComparer.Ordinal);
            return new Declaration(body.Start, body.End, names, references);
        }).ToArray();
    }
}

/// <summary>
/// BUILD-PERF-M2: facts about one declaration catalog that every definition of a build needs
/// (InlineStep presence, Gear-blanked text, content hash). They were recomputed per definition,
/// which in the interpreted browser runtime cost hundreds of milliseconds per rebuild.
/// </summary>
internal sealed class DeclarationTextFacts
{
    private static readonly object Gate = new();
    private static DeclarationTextFacts? last;

    private readonly Lazy<bool> hasInlineStep;
    private readonly Lazy<string> ordinary;
    private readonly Lazy<string> hash;

    private DeclarationTextFacts(string text)
    {
        Text = text;
        hasInlineStep = new(() => System.Text.RegularExpressions.Regex.IsMatch(text, @"\bInlineStep\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant));
        ordinary = new(() => AssemblyDefinitionMaterializer.OrdinaryDeclarations(text));
        hash = new(() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))));
    }

    public string Text { get; }
    public bool HasInlineStep => hasInlineStep.Value;
    /// <summary>The catalog with Gear roots blanked, as ordinary definitions compile it.</summary>
    public string Ordinary => ordinary.Value;
    /// <summary>SHA-256 (hex) of the UTF-8 text.</summary>
    public string Hash => hash.Value;

    public static DeclarationTextFacts Of(string text)
    {
        lock (Gate)
        {
            if (last is not null && (ReferenceEquals(last.Text, text) || string.Equals(last.Text, text, StringComparison.Ordinal))) return last;
            return last = new DeclarationTextFacts(text);
        }
    }
}
