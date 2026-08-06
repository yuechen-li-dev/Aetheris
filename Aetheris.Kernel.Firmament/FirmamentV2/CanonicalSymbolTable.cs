using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>One canonical namespace for declarations that can participate in
/// author-visible cross-family references. It is deliberately independent of
/// materializer implementation types.</summary>
public enum FirmamentV2CanonicalSymbolKind
{
    Record,
    StaticArray,
    Template,
    Pattern,
    Require,
    Profile,
    Compose,
    Hole,
    Slot,
    Selection
}

public sealed record FirmamentV2CanonicalSymbol(
    string Name,
    FirmamentV2CanonicalSymbolKind Kind,
    FirmamentV2SourceSpan SourceSpan)
{
    public string CanonicalId => $"{Kind}:{Name}";
}

/// <summary>A checked source reference, retained independently of the adapter
/// that supplied its eventual material geometry.</summary>
public sealed record FirmamentV2CanonicalSymbolBinding(
    string OwnerCanonicalId,
    string Relation,
    string TargetCanonicalId,
    FirmamentV2SourceSpan SourceSpan);

public sealed record FirmamentV2CanonicalSymbolTable(
    IReadOnlyList<FirmamentV2CanonicalSymbol> Symbols,
    IReadOnlyList<FirmamentV2CanonicalSymbolBinding> Bindings)
{
    public FirmamentV2CanonicalSymbol? Resolve(string name) =>
        Symbols.SingleOrDefault(symbol => string.Equals(symbol.Name, name, StringComparison.Ordinal));
}

internal static class FirmamentV2CanonicalSymbolBinder
{
    internal const string Duplicate = "firmament-v2-symbol-duplicate";
    internal const string KindMismatch = "firmament-v2-symbol-kind-mismatch";

    internal sealed record Result(FirmamentV2CanonicalSymbolTable Table, IReadOnlyList<string> Diagnostics);

    public static Result Bind(FirmamentV2Document document, string source)
    {
        var symbols = new List<FirmamentV2CanonicalSymbol>();
        var byName = new Dictionary<string, FirmamentV2CanonicalSymbol>(StringComparer.Ordinal);
        var diagnostics = new List<string>();

        void Add(string name, FirmamentV2CanonicalSymbolKind kind, FirmamentV2SourceSpan span)
        {
            if (byName.TryGetValue(name, out var previous))
            {
                diagnostics.Add($"{Duplicate}:{name}:{previous.Kind}:{kind}");
                return;
            }
            var symbol = new FirmamentV2CanonicalSymbol(name, kind, span);
            byName.Add(name, symbol);
            symbols.Add(symbol);
        }

        if (document.StaticAuthoring is { } staticAuthoring)
        {
            foreach (var record in staticAuthoring.RecordTypes) Add(record.Name, FirmamentV2CanonicalSymbolKind.Record, record.SourceSpan);
            foreach (var array in staticAuthoring.Arrays) Add(array.Name, FirmamentV2CanonicalSymbolKind.StaticArray, array.SourceSpan);
            foreach (var template in staticAuthoring.Templates) Add(template.Name, FirmamentV2CanonicalSymbolKind.Template, template.SourceSpan);
            foreach (var pattern in staticAuthoring.Patterns) Add(pattern.Name, FirmamentV2CanonicalSymbolKind.Pattern, pattern.SourceSpan);
            foreach (var require in staticAuthoring.Requires) Add(require.Name, FirmamentV2CanonicalSymbolKind.Require, require.SourceSpan);
        }

        foreach (var profile in document.Profiles ?? []) Add(profile.Name, FirmamentV2CanonicalSymbolKind.Profile, profile.SourceSpan);
        foreach (var compose in document.Composes ?? []) Add(compose.Name, FirmamentV2CanonicalSymbolKind.Compose, compose.SourceSpan);
        foreach (Match feature in Regex.Matches(source, @"\b(?<kind>Hole|Slot)\s*<[^>]+>\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            Add(feature.Groups["name"].Value,
                string.Equals(feature.Groups["kind"].Value, "Hole", StringComparison.Ordinal)
                    ? FirmamentV2CanonicalSymbolKind.Hole
                    : FirmamentV2CanonicalSymbolKind.Slot,
                new(feature.Index, feature.Length));
        }
        foreach (var selection in document.Selections ?? []) Add(selection.Name, FirmamentV2CanonicalSymbolKind.Selection, selection.SourceSpan);

        var bindings = new List<FirmamentV2CanonicalSymbolBinding>();
        foreach (var selection in document.Selections ?? [])
        {
            var reference = ParseSelectionReference(selection.Source);
            if (reference is null) continue; // Syntax and unknown-source diagnostics belong to the selection parser.
            if (!byName.TryGetValue(reference.Value.Name, out var target)) continue;
            if (target.Kind != reference.Value.Kind)
            {
                diagnostics.Add($"{KindMismatch}:{selection.Name}:{reference.Value.Kind}:{reference.Value.Name}:{target.Kind}");
                continue;
            }
            bindings.Add(new($"{FirmamentV2CanonicalSymbolKind.Selection}:{selection.Name}", "Source", target.CanonicalId, selection.SourceSpan));
        }

        return new(new(symbols, bindings), diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private static (string Name, FirmamentV2CanonicalSymbolKind Kind)? ParseSelectionReference(string source)
    {
        var profile = Regex.Match(source, @"^(?<name>[A-Za-z_]\w*)\.Profile(?:Segments|Loop)\s*\(", RegexOptions.CultureInvariant);
        if (profile.Success) return (profile.Groups["name"].Value, FirmamentV2CanonicalSymbolKind.Profile);
        var slot = Regex.Match(source, @"^Slot\s*\(\s*(?<name>[A-Za-z_]\w*)\s*\)$", RegexOptions.CultureInvariant);
        if (slot.Success) return (slot.Groups["name"].Value, FirmamentV2CanonicalSymbolKind.Slot);
        var hole = Regex.Match(source, @"^Hole\s*\(\s*(?<name>[A-Za-z_]\w*)\s*\)$", RegexOptions.CultureInvariant);
        return hole.Success ? (hole.Groups["name"].Value, FirmamentV2CanonicalSymbolKind.Hole) : null;
    }
}
