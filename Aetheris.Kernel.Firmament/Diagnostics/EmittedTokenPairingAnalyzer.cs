using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Diagnostics;

internal enum TokenPairingStatus
{
    SafePair,
    MissingMate,
    AmbiguousMultiplicity,
    NoToken,
    Deferred,
    Unsupported
}

internal sealed record EmittedTokenGroup(
    string TokenOrderingKey,
    IReadOnlyList<EmittedTopologyIdentityEntry> Entries,
    TokenPairingStatus Status,
    string OrderingKey,
    IReadOnlyList<string> Diagnostics);

internal sealed record EmittedTokenPairCandidate(
    string TokenOrderingKey,
    EmittedTopologyIdentityEntry EntryA,
    EmittedTopologyIdentityEntry EntryB,
    TokenPairingStatus Status,
    IReadOnlyList<string> Diagnostics);

internal sealed record EmittedTokenPairingAnalysisResult(
    IReadOnlyList<EmittedTokenPairCandidate> SafePairs,
    IReadOnlyList<EmittedTokenGroup> MissingMateGroups,
    IReadOnlyList<EmittedTokenGroup> AmbiguousGroups,
    IReadOnlyList<EmittedTopologyIdentityEntry> NullTokenEntries,
    IReadOnlyList<string> Diagnostics,
    ShellClosureReadiness Readiness);

internal static class EmittedTokenPairingAnalyzer
{
    internal static EmittedTokenPairingAnalysisResult Analyze(IEnumerable<EmittedTopologyIdentityMap> identityMaps)
    {
        var allEntries = identityMaps.Where(m => m is not null).SelectMany(m => m.Entries ?? []).OrderBy(BuildEntryOrderingKey, StringComparer.Ordinal).ToArray();
        var diagnostics = new List<string> { "token-pairing-analysis-started: emitted identity entries grouped by InternalTrimIdentityToken ordering key." };

        var nullTokenEntries = allEntries.Where(e => e.TrimIdentityToken is null).ToArray();
        if (nullTokenEntries.Length > 0)
        {
            diagnostics.Add($"token-pairing-unmapped-entries: {nullTokenEntries.Length} emitted entries have null token and are excluded from pairing.");
        }

        var safePairs = new List<EmittedTokenPairCandidate>();
        var missing = new List<EmittedTokenGroup>();
        var ambiguous = new List<EmittedTokenGroup>();

        var tokenGroups = allEntries
            .Where(e => e.TrimIdentityToken is not null)
            .GroupBy(e => e.TrimIdentityToken!.Value.OrderingKey, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in tokenGroups)
        {
            var entries = group.OrderBy(BuildEntryOrderingKey, StringComparer.Ordinal).ToArray();
            var orderingKey = $"token:{group.Key}";

            if (entries.Length == 1)
            {
                var d = $"token-missing-mate: token={group.Key} appears once; missing opposite emitted boundary evidence.";
                diagnostics.Add(d);
                missing.Add(new EmittedTokenGroup(group.Key, entries, TokenPairingStatus.MissingMate, orderingKey, [d]));
                continue;
            }

            if (entries.Length > 2)
            {
                var d = $"token-ambiguous-multiplicity: token={group.Key} appears {entries.Length} times; arbitrary pairing is forbidden.";
                diagnostics.Add(d);
                ambiguous.Add(new EmittedTokenGroup(group.Key, entries, TokenPairingStatus.AmbiguousMultiplicity, orderingKey, [d]));
                continue;
            }

            var compatibility = AreRolesCompatible(entries[0].Role, entries[1].Role);
            if (!compatibility.compatible)
            {
                var d = $"token-incompatible-roles: token={group.Key} has role pair ({entries[0].Role},{entries[1].Role}) not admissible for safe boundary pairing evidence.";
                diagnostics.Add(d);
                missing.Add(new EmittedTokenGroup(group.Key, entries, TokenPairingStatus.Deferred, orderingKey, [d]));
                continue;
            }

            var safeDiagnostic = $"token-safe-pair: token={group.Key} forms deterministic compatible pair ({entries[0].Role},{entries[1].Role}).";
            diagnostics.Add(safeDiagnostic);
            safePairs.Add(new EmittedTokenPairCandidate(group.Key, entries[0], entries[1], TokenPairingStatus.SafePair, [safeDiagnostic]));
        }

        var readiness = ambiguous.Count == 0 && missing.All(m => m.Status != TokenPairingStatus.Deferred) && missing.Count == 0
            ? ShellClosureReadiness.ReadyForAssemblyEvidence
            : ShellClosureReadiness.Deferred;
        diagnostics.Add("shell-assembly-still-unimplemented: analysis only; no coedge mutation, edge merge, or shell stitching executed.");

        return new EmittedTokenPairingAnalysisResult(
            safePairs.OrderBy(p => p.TokenOrderingKey, StringComparer.Ordinal).ToArray(),
            missing.OrderBy(m => m.OrderingKey, StringComparer.Ordinal).ToArray(),
            ambiguous.OrderBy(a => a.OrderingKey, StringComparer.Ordinal).ToArray(),
            nullTokenEntries,
            diagnostics.Distinct().OrderBy(d => d, StringComparer.Ordinal).ToArray(),
            readiness);
    }

    private static (bool compatible, TokenPairingStatus status) AreRolesCompatible(EmittedTopologyRole a, EmittedTopologyRole b)
    {
        var pair = new[] { a, b }.OrderBy(r => r.ToString(), StringComparer.Ordinal).ToArray();
        var isPlanarCyl = pair[0] == EmittedTopologyRole.CylindricalBottomBoundary && pair[1] == EmittedTopologyRole.InnerCircularTrim
                       || pair[0] == EmittedTopologyRole.CylindricalTopBoundary && pair[1] == EmittedTopologyRole.InnerCircularTrim;
        return isPlanarCyl ? (true, TokenPairingStatus.SafePair) : (false, TokenPairingStatus.Deferred);
    }

    private static string BuildEntryOrderingKey(EmittedTopologyIdentityEntry entry)
        => $"{entry.LocalTopologyKey}|{entry.Kind}|{entry.Role}|{entry.OrientationPolicy}";
}
