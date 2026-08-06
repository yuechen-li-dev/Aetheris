namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Source-level admission for a composed-host boundary chamfer.  This is deliberately
/// evaluated from the normalized Profile and cavity declarations, before a topology plan
/// exists; no materialized edge or face participates in the decision.
/// </summary>
public sealed record ProfileBoundaryChamferCorridor(
    string EdgeFinishId, string ProfileId, string LoopId, double Distance,
    double From, double To, IReadOnlyList<string> SourceSegmentIds);
public sealed record ProfileBoundaryChamferCavityInteraction(
    string FeatureId, string Kind, double CenterX, double CenterY, double Radius,
    double From, double To, string Classification, string Diagnostic);
public sealed record ComposedProfileBoundaryChamferAdmission(
    bool Admitted, ProfileBoundaryChamferCorridor? Corridor,
    IReadOnlyList<ProfileBoundaryChamferCavityInteraction> Interactions,
    IReadOnlyList<string> Diagnostics);

public static class ComposedProfileBoundaryChamferAdmissionChecker
{
    private const double Tol = 1e-7;

    public static ComposedProfileBoundaryChamferAdmission Check(
        PrismaticSectionStackConstruction stack, ResolvedProfile2D profile,
        ProfileBoundaryChamferTarget target, double distance)
    {
        if (target.ChainKind != ProfileBoundaryChamferChainKind.ClosedLoop)
            return new(false, null, [], ["ProfileBoundaryChamferComposeOpenChainUnsupported"]);
        if (target.Side != ProfileBoundaryChamferSide.Top)
            return new(false, null, [], ["ProfileBoundaryChamferComposeBottomUnsupported"]);
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null || !loop.IsOuter || loop.Segments.Any(x => x.Geometry is not LineArcLineSegment2D))
            return new(false, null, [], ["ProfileBoundaryChamferSegmentKindUnsupported"]);
        var top = stack.Feature.CriticalLevels.Max();
        var corridor = new ProfileBoundaryChamferCorridor(target.StableId, profile.Name, loop.Name, distance, top - distance, top, target.SegmentIds);
        var interactions = new List<ProfileBoundaryChamferCavityInteraction>();
        foreach (var hole in stack.Feature.ShaftHoles ?? [])
            interactions.Add(Classify(loop, corridor, hole.StableId, "Shaft", hole.CenterX, hole.CenterY, hole.Diameter / 2d, hole.From, hole.To));
        foreach (var hole in stack.Feature.CounterboreHoles ?? [])
        {
            // The entry recess overlaps the Top corridor in the admitted +Z family.
            // It must be tested with its larger radius, not merely its shaft radius.
            var boreFrom = Math.Max(hole.From, hole.To - hole.CounterboreDepth);
            interactions.Add(Classify(loop, corridor, hole.StableId, "Counterbore", hole.CenterX, hole.CenterY, hole.CounterboreDiameter / 2d, boreFrom, hole.To));
            if (boreFrom > hole.From + Tol)
                interactions.Add(Classify(loop, corridor, hole.StableId, "CounterboreShaft", hole.CenterX, hole.CenterY, hole.Diameter / 2d, hole.From, boreFrom));
        }
        var rejected = interactions.Where(x => x.Classification != "Disjoint").ToArray();
        return new(rejected.Length == 0, corridor, interactions,
            rejected.Select(x => x.Diagnostic).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static ProfileBoundaryChamferCavityInteraction Classify(
        ResolvedProfileLoop2D loop, ProfileBoundaryChamferCorridor corridor,
        string featureId, string kind, double x, double y, double radius, double from, double to)
    {
        var overlaps = from < corridor.To - Tol && to > corridor.From + Tol;
        if (!overlaps) return new(featureId, kind, x, y, radius, from, to, "Disjoint", string.Empty);
        var boundaryDistance = loop.Segments.Cast<ResolvedProfileSegment2D>()
            .Select(segment => (LineArcLineSegment2D)segment.Geometry)
            .Select(line => PointSegmentDistance((x, y), line.Start, line.End)).Min();
        // This is a conservative analytic ring test: the Minkowski sum of the
        // profile-boundary corridor and the circular cavity footprint.  A touching
        // result is intentionally rejected; a later exact cell test may only relax it.
        if (boundaryDistance <= radius + corridor.Distance + Tol)
        {
            var code = kind == "Shaft" ? "ProfileBoundaryChamferIntersectsShaft" : "ProfileBoundaryChamferIntersectsCounterbore";
            var classification = Math.Abs(boundaryDistance - radius - corridor.Distance) <= Tol ? "TouchingWithinTolerance" : "Intersecting";
            return new(featureId, kind, x, y, radius, from, to, classification,
                $"{code}:edgeFinish={corridor.EdgeFinishId}:cavity={featureId}:profile={corridor.ProfileId}.{corridor.LoopId}:distance={corridor.Distance:R}:center=({x:R},{y:R}):radius={radius:R}:interval=({Math.Max(from, corridor.From):R},{Math.Min(to, corridor.To):R})");
        }
        return new(featureId, kind, x, y, radius, from, to, "Disjoint", string.Empty);
    }

    private static double PointSegmentDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var lengthSquared = dx * dx + dy * dy;
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0d, 1d);
        var rx = p.X - (a.X + t * dx); var ry = p.Y - (a.Y + t * dy); return Math.Sqrt(rx * rx + ry * ry);
    }
}
