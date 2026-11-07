namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>A section-stack body declared hollow: its wall, and which end caps the cavity runs out through.</summary>
public sealed record PrismaticHollowIntent(double WallThickness, bool OpenTop, bool OpenBottom, string SourceSpan);

/// <summary>
/// Derives the cavity of a hollow section stack from the exterior's own construction, as more section-stack
/// removals. The cavity is the exterior eroded by a cylinder of radius t and half-height t: in each band its
/// section is the exact inset (<see cref="ProfileInset2D"/>) of the intersection of the exterior sections within t
/// above and below. Walls are exactly t thick, caps exactly t, and where slabs step the corner stays sharp (the wall
/// there reaches √2·t at a right angle). Bosses and pockets declared in the exterior are hollowed with it; holes and
/// slots are wall features and are cut afterwards.
/// </summary>
public static class PrismaticHollowCavity
{
    private const double Tol = 1e-7;

    public static PrismaticProfileCompositionParseResult? Derive(PrismaticProfileCompositionParseResult parsed, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string>();
        diagnostics = d;
        var feature = parsed.Feature!;
        var hollow = feature.Hollow!;
        var t = hollow.WallThickness;
        var exteriorOperations = feature.Operations.Where(IsExterior).ToArray();
        var exterior = new PrismaticProfileCompositionFeature(feature.Name, feature.Frame, feature.Axis, feature.Placement, exteriorOperations,
            exteriorOperations.SelectMany(o => new[] { o.From, o.To }).Distinct().Order().ToArray(), feature.Provenance);
        var stack = PrismaticSectionStackCompiler.Normalize(new(exterior, parsed.Profiles, []), out var exteriorDiagnostics);
        if (stack is null || stack.Slabs.Count == 0)
        {
            d.AddRange(exteriorDiagnostics);
            d.Add("hollow-exterior-invalid");
            return null;
        }
        var slabs = stack.Slabs;
        var bottom = slabs.Min(s => s.From);
        var top = slabs.Max(s => s.To);
        var low = hollow.OpenBottom ? bottom : bottom + t;
        var high = hollow.OpenTop ? top : top - t;
        if (high - low <= Tol)
        {
            d.Add($"hollow-wall-offset-collapse:{feature.Name}: the body is {top - bottom:R} tall, too short for {(hollow.OpenTop || hollow.OpenBottom ? 1 : 2)} wall(s) of {t:R}");
            return null;
        }
        var breaks = slabs.SelectMany(s => new[] { s.From - t, s.From + t, s.To - t, s.To + t })
            .Append(low).Append(high).Where(z => z >= low - Tol && z <= high + Tol)
            .Select(z => double.Clamp(z, low, high)).Order().ToList();
        var levels = new List<double>();
        foreach (var z in breaks) if (levels.Count == 0 || z - levels[^1] > Tol) levels.Add(z);

        var profiles = parsed.Profiles.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var cavities = new List<PrismaticProfileOperation>();
        int[]? previousSet = null;
        List<PrismaticProfileOperation> previousOps = [];
        for (var band = 0; band + 1 < levels.Count; band++)
        {
            var (a, b) = (levels[band], levels[band + 1]);
            var windowLow = hollow.OpenBottom ? double.Max(a - t, bottom) : a - t;
            var windowHigh = hollow.OpenTop ? double.Min(b + t, top) : b + t;
            var set = Enumerable.Range(0, slabs.Count).Where(i => slabs[i].From < windowHigh - Tol && slabs[i].To > windowLow + Tol).ToArray();
            // A window the slabs do not cover reaches outside the body: no cavity in this band.
            var covered = windowLow;
            foreach (var i in set.OrderBy(i => slabs[i].From))
                if (slabs[i].From <= covered + Tol) covered = double.Max(covered, slabs[i].To);
            if (set.Length == 0 || covered < windowHigh - Tol) { previousSet = null; previousOps = []; continue; }
            // When one slab's section lies inside all the others, the intersection is that section itself: use it
            // directly, so neighbouring bands with the same governing section merge into one removal.
            var areas = set.Select(i => slabs[i].MaterialRegions.Sum(PrismaticSectionStackCompiler.Area)).ToArray();
            IReadOnlyList<PrismaticSectionRegion> regions;
            int[] key;
            var smallest = set[Array.IndexOf(areas, areas.Min())];
            if (set.Select(i => slabs[i].MaterialRegions).Distinct().Count() == 1)
            {
                regions = slabs[smallest].MaterialRegions;
                key = [smallest];
            }
            else
            {
                var intersection = ProfileArrangementBuilder.Intersection(feature.Frame, set.Select(i => slabs[i].MaterialRegions).ToArray(),
                    $"hollow:{feature.Name}:band={a:R}..{b:R}");
                d.AddRange(intersection.Arrangement.Diagnostics.Where(x => x.Contains("rejected", StringComparison.Ordinal)));
                var area = intersection.MaterialRegions.Sum(PrismaticSectionStackCompiler.Area);
                if (double.Abs(area - areas.Min()) <= 1e-6 * double.Max(1d, area))
                {
                    regions = slabs[smallest].MaterialRegions;
                    key = [smallest];
                }
                else
                {
                    regions = intersection.MaterialRegions;
                    key = set;
                }
            }
            if (previousSet is not null && previousSet.SequenceEqual(key))
            {
                for (var k = 0; k < previousOps.Count; k++)
                {
                    var index = cavities.IndexOf(previousOps[k]);
                    cavities[index] = previousOps[k] = previousOps[k] with { To = b };
                }
                continue;
            }
            previousSet = key;
            previousOps = [];
            for (var r = 0; r < regions.Count; r++)
            {
                var name = $"{feature.Name}Cavity{cavities.Count}";
                var inset = ProfileInset2D.Inset(regions[r], t, name, $"{feature.Name}:band={a:R}..{b:R}:region{r}");
                if (inset.Profile is null) { d.AddRange(inset.Diagnostics); return null; }
                profiles[name] = inset.Profile;
                var operation = new PrismaticProfileOperation(name, PrismaticProfileIntent.Remove, name, a, b, "HollowCavity",
                    hollow.SourceSpan, $"hollow:{feature.Name}", "Hollow");
                cavities.Add(operation);
                previousOps.Add(operation);
            }
        }
        if (cavities.Count == 0)
        {
            d.Add($"hollow-wall-offset-collapse:{feature.Name}: no section is wider than twice the wall {t:R}");
            return null;
        }
        var operations = feature.Operations.Concat(cavities).ToArray();
        var critical = feature.CriticalLevels.Concat(cavities.SelectMany(o => new[] { o.From, o.To })).Order().ToList();
        var merged = new List<double>();
        foreach (var z in critical) if (merged.Count == 0 || z - merged[^1] > Tol) merged.Add(z);
        return new(feature with { Operations = operations, CriticalLevels = merged, Hollow = null }, profiles, parsed.Diagnostics, parsed.Expansion);
    }

    private static bool IsExterior(PrismaticProfileOperation operation) =>
        operation.SemanticFeatureKind is null or "Boss" or "Pocket";
}
