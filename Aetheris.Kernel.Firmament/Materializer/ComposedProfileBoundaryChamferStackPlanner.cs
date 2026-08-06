namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Transforms normalized source sections into one final variable-outer interval before emission.</summary>
public static class ComposedProfileBoundaryChamferStackPlanner
{
    private const double Tol = 1e-7;
    public static PrismaticSectionStackConstruction? TryApply(PrismaticSectionStackConstruction stack, ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double distance, out IReadOnlyList<string> diagnostics)
    {
        var d = new List<string>(); var top = stack.Feature.CriticalLevels.Max(); var lower = top - distance;
        var topSlab = stack.Slabs.SingleOrDefault(x => x.From < lower + Tol && x.To >= top - Tol);
        if (topSlab is null) { diagnostics = ["ProfileBoundaryChamferComposeStationUnavailable"]; return null; }
        if (stack.Feature.CounterboreHoles?.Any(h => h.To - h.CounterboreDepth > lower + Tol && h.To - h.CounterboreDepth < top - Tol) == true) { diagnostics = ["ProfileBoundaryChamferCounterboreStationOverlapUnsupported"]; return null; }
        var outer = topSlab.Region.Outer;
        // The arrangement-owned outer loop is the exact section consumed by this
        // stack.  Offset that source-equivalent loop, rather than assuming its
        // ordering matches the parser's profile declaration.
        var arrangedTarget = target with { ProfileId = outer.Name, LoopId = outer.Loops.Single(x => x.IsOuter).Name, SegmentIds = outer.Loops.Single(x => x.IsOuter).Segments.Select(x => x.Name).ToArray(), ChainKind = ProfileBoundaryChamferChainKind.ClosedLoop };
        if (!ProfileBoundaryChamferPlanner.TryCreateInsetOuterProfile(outer, arrangedTarget, distance, out var inset, out var insetDiagnostic)) { diagnostics = [insetDiagnostic ?? "ProfileBoundaryChamferInsetCollapse"]; return null; }
        if (outer.Loops[0].Segments.Count != inset!.Loops[0].Segments.Count) { diagnostics = ["VariableOuterSectionIntervalOuterVertexCountMismatch"]; return null; }
        var vertices = outer.Loops[0].Segments.Select((segment, i) => new VariableOuterVertexCorrespondence(segment.Provenance.StableId, inset.Loops[0].Segments[i].Provenance.StableId, i, i)).ToArray();
        var segments = outer.Loops[0].Segments.Select((segment, i) => new VariableOuterSegmentCorrespondence(segment.Provenance.StableId, inset.Loops[0].Segments[i].Provenance.StableId, i, i)).ToArray();
        var inners = topSlab.Region.Holes.OrderBy(x => x.Name, StringComparer.Ordinal).Select(hole => new UnchangedInnerLoopCorrespondence($"inner:{hole.Name}", FeatureFor(hole.Name), hole, hole)).ToArray();
        var interval = new VariableOuterSectionInterval($"variable-outer:{target.StableId}", lower, top, outer, inset, vertices, segments, inners, target.StableId, ["ProfileBoundaryChamfer", "PrismaticSectionStackTopologyPlan"]);
        d.AddRange(VariableOuterSectionIntervalValidator.Validate(interval).Diagnostics);
        if (d.Count != 0) { diagnostics = d; return null; }
        var slabs = stack.Slabs.Where(x => !ReferenceEquals(x, topSlab)).Append(new PrismaticSectionSlab(topSlab.From, lower, topSlab.Region, topSlab.ActiveOperations, topSlab.Arrangement)).OrderBy(x => x.From).ToArray();
        var capRegion = new PrismaticSectionRegion(inset, topSlab.Region.Holes, topSlab.Region.Provenance.Concat([target.StableId]).ToArray());
        var transitions = stack.Transitions.Where(x => Math.Abs(x.Level - top) > Tol).Append(new PrismaticSectionTransition(top, [capRegion], [])).OrderBy(x => x.Level).ToArray();
        diagnostics = [];
        return stack with { Slabs = slabs, Transitions = transitions, VariableOuterIntervals = [interval] };

        string FeatureFor(string profileName) => stack.Feature.Operations.FirstOrDefault(x => x.ProfileReference == profileName)?.SemanticFeatureId ?? $"profile:{profileName}";
    }
}
