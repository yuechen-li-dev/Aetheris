using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Collaboration;

/// <summary>Parses bounded, ordinary Firmament Review declarations into backend-independent ReviewIR.</summary>
public static partial class FirmamentReviewCompiler
{
    public const string AuthorRequired = "review-author-required";
    public const string DateRequired = "review-date-required";
    public const string DateInvalid = "review-date-invalid";
    public const string TargetUnknown = "review-target-unknown";
    public const string UnitMismatch = "review-proposal-unit-mismatch";
    public const string IdentityDuplicate = "review-identity-duplicate";

    public static ReviewCompilationResult Compile(
        string source,
        string sourcePath,
        IReadOnlyDictionary<string, (string? CurrentValue, IReadOnlyList<string> Capabilities)>? knownTargets = null)
    {
        var diagnostics = new List<string>();
        var ranges = new List<(int, int)>();
        var threads = new List<ReviewThreadIr>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in ReviewStart().Matches(source))
        {
            var open = source.IndexOf('{', match.Index + match.Length - 1);
            var close = FindClosingBrace(source, open);
            if (close < 0) { diagnostics.Add($"review-unclosed: {match.Groups[1].Value}"); continue; }
            ranges.Add((match.Index, close - match.Index + 1));
            var id = match.Groups[1].Value;
            if (!identities.Add(id)) diagnostics.Add($"{IdentityDuplicate}: {id}");
            var body = source[(open + 1)..close];
            var target = Field(body, "Target");
            var statusText = Field(body, "Status") ?? "Open";
            if (target is null) { diagnostics.Add($"review-target-required: {id}"); continue; }
            if (!Enum.TryParse<ReviewStatus>(statusText, true, out var status)) diagnostics.Add($"review-status-invalid: {id}: {statusText}");
            var targetInfo = knownTargets?.GetValueOrDefault(target);
            if (knownTargets is not null && !knownTargets.ContainsKey(target)) diagnostics.Add($"{TargetUnknown}: {id}: {target}");
            var entries = new List<ReviewEntryIr>();
            var entryOrder = 0;
            foreach (Match entryMatch in EntryStart().Matches(body))
            {
                var entryOpen = body.IndexOf('{', entryMatch.Index + entryMatch.Length - 1);
                var entryClose = FindClosingBrace(body, entryOpen);
                if (entryClose < 0) { diagnostics.Add($"review-entry-unclosed: {id}"); continue; }
                var kind = Enum.Parse<ReviewEntryKind>(entryMatch.Groups[1].Value, true);
                var entryId = entryMatch.Groups[2].Success ? entryMatch.Groups[2].Value : $"{id}.{kind}.{entryOrder + 1}";
                if (!identities.Add(entryId)) diagnostics.Add($"{IdentityDuplicate}: {entryId}");
                var entryBody = body[(entryOpen + 1)..entryClose];
                var authorName = StringField(entryBody, "Author");
                if (string.IsNullOrWhiteSpace(authorName)) diagnostics.Add($"{AuthorRequired}: {entryId}");
                var dateText = Field(entryBody, "Date");
                if (dateText is null) diagnostics.Add($"{DateRequired}: {entryId}");
                else if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) diagnostics.Add($"{DateInvalid}: {entryId}: {dateText}");
                _ = DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
                var text = StringField(entryBody, "Text") ?? StringField(entryBody, "Reason") ?? string.Empty;
                StructuredProposalIr? proposal = null;
                if (kind == ReviewEntryKind.Proposal)
                {
                    var property = Field(entryBody, "Property");
                    var proposed = Field(entryBody, "Proposed") ?? Field(entryBody, "Tolerance");
                    var current = Field(entryBody, "Current") ?? targetInfo?.CurrentValue;
                    var units = Field(entryBody, "Units") ?? UnitOf(proposed);
                    var currentUnit = UnitOf(current); var proposedUnit = UnitOf(proposed);
                    if (currentUnit is not null && proposedUnit is not null && !string.Equals(currentUnit, proposedUnit, StringComparison.OrdinalIgnoreCase))
                        diagnostics.Add($"{UnitMismatch}: {entryId}: {currentUnit} vs {proposedUnit}");
                    if (proposed is not null) proposal = new(property ?? "value", current, proposed, units, StringField(entryBody, "Reason"));
                }
                entries.Add(new(entryId, kind, new(authorName ?? "<missing>", StringField(entryBody, "Organization"), StringField(entryBody, "Email")), date, text, proposal, entryOrder++));
            }
            threads.Add(new(id, new(target, sourcePath, targetInfo?.CurrentValue, targetInfo?.Capabilities ?? [], null), status, entries, $"{sourcePath}:Review {id}"));
        }
        return new(diagnostics.Count == 0, new(threads), diagnostics, ranges);
    }

    public static string EraseDeclarations(string source, IEnumerable<(int Start, int Length)> ranges)
    {
        var chars = source.ToCharArray();
        foreach (var (start, length) in ranges)
            for (var index = start; index < start + length; index++) if (chars[index] != '\n' && chars[index] != '\r') chars[index] = ' ';
        return new(chars);
    }

    private static int FindClosingBrace(string text, int open)
    {
        var depth = 0; var quoted = false; var escaped = false;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
            if (c == '"') { quoted = true; continue; }
            if (c == '{') depth++; else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static string? Field(string body, string name) => Regex.Match(body, $@"(?m)(?:^|;)\s*{Regex.Escape(name)}\s*:\s*([^;\r\n]+)", RegexOptions.CultureInvariant).Groups[1].Value.Trim() is { Length: > 0 } value ? value : null;
    private static string? StringField(string body, string name) { var value = Field(body, name); return value is not null && value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? Regex.Unescape(value[1..^1]) : value; }
    private static string? UnitOf(string? value) => value is null ? null : Regex.Match(value, @"(?i)(mm|cm|m|in|deg)\b").Groups[1].Value is { Length: > 0 } unit ? unit.ToLowerInvariant() : null;

    [GeneratedRegex(@"(?m)^\s*Review\s+([A-Za-z_][A-Za-z0-9_.-]*)\s*\{")]
    private static partial Regex ReviewStart();
    [GeneratedRegex(@"(?m)^\s*(Comment|Issue|Proposal|Resolution)(?:\s+([A-Za-z_][A-Za-z0-9_.-]*))?\s*\{")]
    private static partial Regex EntryStart();
}
