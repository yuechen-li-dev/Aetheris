namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// Per-type entity counts of an exported STEP file, and the congruence check that a pattern of N identical
/// instances adds exactly N times what one instance adds. The writer shares leaf geometry (points, directions,
/// vectors) between entities that happen to coincide, so those leaf types are not counted against the
/// prediction; every entity that refers to them is.
/// </summary>
public static class Step242EntityCensus
{
    private static readonly HashSet<string> SharedLeafTypes = new(StringComparer.Ordinal) { "CARTESIAN_POINT", "DIRECTION", "VECTOR" };

    /// <summary>Counts simple instances (<c>#n=TYPE(...)</c>) by type; complex instances (<c>#n=(...)</c>) are not counted.</summary>
    public static IReadOnlyDictionary<string, int> Count(string stepText)
    {
        ArgumentNullException.ThrowIfNull(stepText);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var span = stepText.AsSpan();
        var position = 0;
        while (position < span.Length)
        {
            var lineEnd = span[position..].IndexOf('\n');
            var line = lineEnd < 0 ? span[position..] : span.Slice(position, lineEnd);
            position = lineEnd < 0 ? span.Length : position + lineEnd + 1;
            line = line.TrimStart();
            if (line.Length < 4 || line[0] != '#') continue;
            var equals = line.IndexOf('=');
            if (equals < 0) continue;
            var rest = line[(equals + 1)..].TrimStart();
            var open = rest.IndexOf('(');
            if (open <= 0) continue;
            var type = rest[..open].TrimEnd().ToString();
            counts[type] = counts.GetValueOrDefault(type) + 1;
        }

        return counts;
    }

    /// <summary>
    /// Null when <paramref name="full"/> holds exactly <c>one + (n - 1) * (two - one)</c> entities of every type
    /// other than the shared leaf types; otherwise the first type that differs.
    /// </summary>
    public static string? LinearMismatch(IReadOnlyDictionary<string, int> one, IReadOnlyDictionary<string, int> two,
        IReadOnlyDictionary<string, int> full, int n)
    {
        foreach (var type in one.Keys.Concat(two.Keys).Concat(full.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (SharedLeafTypes.Contains(type)) continue;
            var expected = one.GetValueOrDefault(type) + (n - 1) * (two.GetValueOrDefault(type) - one.GetValueOrDefault(type));
            var actual = full.GetValueOrDefault(type);
            if (actual != expected) return $"{type}: expected {expected} for {n} congruent instances, found {actual}";
        }

        return null;
    }
}
