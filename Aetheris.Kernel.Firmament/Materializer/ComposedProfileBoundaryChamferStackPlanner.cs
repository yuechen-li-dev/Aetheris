namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Transforms normalized source sections into one final variable-outer interval before emission.</summary>
public static class ComposedProfileBoundaryChamferStackPlanner
{
    private const double Tol = 1e-7;
    internal static double? SupportLevel(PrismaticSectionStackConstruction stack, ResolvedProfile2D profile) =>
        stack.Feature.Operations.Where(o => o.Intent != PrismaticProfileIntent.Remove && o.ProfileReference == profile.Name)
            .Select(o => (double?)o.To).Max();

    public static PrismaticSectionStackConstruction? TryApply(PrismaticSectionStackConstruction stack, ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double distance, out IReadOnlyList<string> diagnostics)
    {
        if (SupportLevel(stack, profile) is not { } top) { diagnostics = ["ProfileBoundaryChamferComposeProfileSupportMissing"]; return null; }
        var d = new List<string>(); var lower = top - distance;
        if (stack.Feature.CountersinkHoles?.Any(h => h.ConeFrom > lower + Tol && h.ConeFrom < top - Tol) == true)
        { diagnostics = ["ProfileBoundaryChamferCountersinkStationOverlapUnsupported:chamfer-corridor-crosses-shaft-to-cone-transition"]; return null; }
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
        // A boss elsewhere may continue above the named Profile's top. Keep its
        // material and remove its footprint from this support's exposed cap.
        var above = stack.Slabs.SingleOrDefault(s => Math.Abs(s.From - top) < Tol)?.MaterialRegions ?? [];
        var protruding = ProfileArrangementBuilder.Difference(stack.Feature.Frame, above, [capRegion], "chamfer-continuing-material");
        if (protruding.Arrangement.Diagnostics.Count > 0 || protruding.MaterialRegions.Count > 0)
        { diagnostics = ["ProfileBoundaryChamferIntersectsContinuingMaterial:the-named-profile-corridor-cannot-cut-a-continuing-boss"]; return null; }
        var cap = ProfileArrangementBuilder.Difference(stack.Feature.Frame, [capRegion], above, "chamfer-exposed-cap");
        if (cap.Arrangement.Diagnostics.Count > 0) { diagnostics = cap.Arrangement.Diagnostics; return null; }
        var transitions = stack.Transitions.Where(x => Math.Abs(x.Level - top) > Tol).Append(new PrismaticSectionTransition(top, cap.MaterialRegions, [])).OrderBy(x => x.Level).ToArray();
        var middle = outer with { Loops = [outer.Loops[0] with { Segments = outer.Loops[0].Segments.Zip(inset.Loops[0].Segments).Select(pair =>
        {
            var a = (LineArcLineSegment2D)pair.First.Geometry; var b = (LineArcLineSegment2D)pair.Second.Geometry;
            return pair.First with { Geometry = new LineArcLineSegment2D(((a.Start.X + b.Start.X) / 2, (a.Start.Y + b.Start.Y) / 2), ((a.End.X + b.End.X) / 2, (a.End.Y + b.End.Y) / 2)) };
        }).ToArray() }] };
        // Linear vertex transport gives a quadratic section area. Simpson's
        // integral is exact here; disjoint cavity volumes are retained unchanged.
        var lowerArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(outer));
        var removedVolume = distance * (lowerArea - (lowerArea + 4 * Math.Abs(PrismaticSectionStackCompiler.ProfileArea(middle)) + Math.Abs(PrismaticSectionStackCompiler.ProfileArea(inset))) / 6);
        diagnostics = [];
        return stack with { Slabs = slabs, Transitions = transitions, VariableOuterIntervals = [interval], AnalyticVolume = stack.AnalyticVolume - removedVolume };

        string FeatureFor(string profileName) => stack.Feature.Operations.FirstOrDefault(x => x.ProfileReference == profileName)?.SemanticFeatureId ?? $"profile:{profileName}";
    }
}
