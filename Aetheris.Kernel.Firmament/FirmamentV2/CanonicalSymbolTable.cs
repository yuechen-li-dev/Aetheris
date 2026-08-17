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
    Boss,
    Pocket,
    Hole,
    Slot,
    Selection,
    Body
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
                if (kind == FirmamentV2CanonicalSymbolKind.Body
                    && previous.Kind is FirmamentV2CanonicalSymbolKind.Profile or FirmamentV2CanonicalSymbolKind.Compose)
                    return;
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
        foreach (var boss in document.Bosses ?? []) Add(boss.Name, FirmamentV2CanonicalSymbolKind.Boss, boss.SourceSpan);
        foreach (var pocket in document.Pockets ?? []) Add(pocket.Name, FirmamentV2CanonicalSymbolKind.Pocket, pocket.SourceSpan);
        var profileOrComposeNames = (document.Profiles ?? []).Select(profile => profile.Name).Concat((document.Composes ?? []).Select(compose => compose.Name)).ToHashSet(StringComparer.Ordinal);
        foreach (var solid in document.Solids.Where(solid => !profileOrComposeNames.Contains(solid.Name))) Add(solid.Name, FirmamentV2CanonicalSymbolKind.Body, new(0, 0));
        foreach (Match body in Regex.Matches(source, @"(?<!Concept\s)\bStruct\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant)) Add(body.Groups["name"].Value, FirmamentV2CanonicalSymbolKind.Body, new(body.Index, body.Length));
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
        foreach (var boss in document.Bosses ?? [])
        {
            if (byName.TryGetValue(boss.Host, out var host)) bindings.Add(new($"Boss:{boss.Name}", "Host", host.CanonicalId, boss.SourceSpan));
            if (byName.TryGetValue(boss.Profile, out var profile)) bindings.Add(new($"Boss:{boss.Name}", "Profile", profile.CanonicalId, boss.SourceSpan));
        }
        foreach (var pocket in document.Pockets ?? [])
        {
            if (byName.TryGetValue(pocket.Host, out var host)) bindings.Add(new($"Pocket:{pocket.Name}", "Host", host.CanonicalId, pocket.SourceSpan));
            if (byName.TryGetValue(pocket.Profile, out var profile)) bindings.Add(new($"Pocket:{pocket.Name}", "Profile", profile.CanonicalId, pocket.SourceSpan));
        }
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
        foreach (var assertion in document.VolumeAssertions ?? [])
        {
            if (!byName.TryGetValue(assertion.TargetBodyId, out var target)) diagnostics.Add($"firmament-v2-assert-volume-target-unknown:{assertion.TargetBodyId}");
            else if (target.Kind is not (FirmamentV2CanonicalSymbolKind.Body or FirmamentV2CanonicalSymbolKind.Compose)) diagnostics.Add($"firmament-v2-assert-volume-target-not-material-body:{assertion.TargetBodyId}:{target.Kind}");
            else bindings.Add(new($"AssertVolume:{assertion.Id}", "Target", target.CanonicalId, assertion.SourceSpan));
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
