using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Exact, non-tessellated host evidence for a construction-plane shaft through
/// the normalized section stack.  A partition is a planning boundary; a span is
/// material only after adjacent partitions have been proven continuous.
/// </summary>
public enum SectionStackFootprintClassification { InsideMaterial, Tangent, CrossesOuterBoundary, IntersectsInnerVoid, OutsideMaterial, Ambiguous }

public sealed record SectionStackHoleFootprintCheck(
    double LocalAxisStart, double LocalAxisEnd, double CenterX, double CenterY,
    double Radius, SectionStackFootprintClassification Classification, double NearestBoundaryDistance,
    IReadOnlyList<string> BoundaryProvenance);

public sealed record SectionStackHolePlannerPartition(
    double LocalAxisStart, double LocalAxisEnd, double SectionFrom, double SectionTo,
    IReadOnlyList<string> ActiveOperations, IReadOnlyList<string> ProfileRegions,
    bool CompleteCircularFootprintInMaterial, string EntryEvent, string ExitEvent,
    SectionStackHoleFootprintCheck Footprint, IReadOnlyList<string> Provenance)
{
    public double Length => LocalAxisEnd - LocalAxisStart;
}

public sealed record SectionStackHolePhysicalSpan(double Start, double End, IReadOnlyList<int> PartitionIndices, bool IsContinuous)
{
    public double Length => End - Start;
}

public sealed record SectionStackHoleTraversalEvidence(
    HoleHostTraversalEvidence HostTraversal,
    IReadOnlyList<SectionStackHolePlannerPartition> OrderedPartitions,
    IReadOnlyList<SectionStackHolePhysicalSpan> PhysicalSpans,
    IReadOnlyList<string> TransitionEvents,
    IReadOnlyList<SectionStackHoleFootprintCheck> FootprintChecks,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Provenance);

/// <summary>
/// Traverses the already-normalized stack.  The initial admitted mapping is an
/// axial signed-permutation plane (+/- world Z): this is the only mapping whose
/// circular footprint is a disk in the stack's native XY section.  Transverse
/// planes deliberately reject here rather than reusing the Box interval or
/// silently treating a YZ/XZ footprint as an XY disk.
/// </summary>
internal static class PrismaticSectionStackHoleTraversal
{
    private const double Tol = 1e-8;

    public static SectionStackHoleTraversalEvidence Traverse(
        AirHoleFeature feature, PrismaticSectionStackConstruction stack, AirConstructionPlaneHolePlacement placement)
    {
        var diagnostics = new List<string>();
        var axis = placement.AxisZ.ToVector();
        var mouth = placement.WorldMouthCenter;
        if (!IsSignedPermutation(placement))
            diagnostics.Add("SectionStackHoleOrientationUnsupported: Construction Plane must be a signed-permutation frame.");
        if (Math.Abs(axis.Z) < 1d - Tol)
            diagnostics.Add("SectionStackHoleTransverseTraversalNotYetAdmitted: a transverse shaft footprint is YZ/XZ, not the stack native XY disk; no Box-interval fallback is permitted.");

        if (diagnostics.Count > 0)
            return Empty(feature, stack, placement, diagnostics);

        // t is always construction-plane local +Z distance.  For +/-Z axes the
        // section centre is invariant in XY and the radius test is exact against
        // every admitted line/arc boundary of the active normalized region.
        var raw = new List<(PrismaticSectionSlab Slab, double Start, double End)>();
        foreach (var slab in stack.Slabs)
        {
            var a = (slab.From - mouth.Z) / axis.Z;
            var b = (slab.To - mouth.Z) / axis.Z;
            var start = Math.Max(0d, Math.Min(a, b)); var end = Math.Max(a, b);
            if (end - start > Tol) raw.Add((slab, start, end));
        }
        raw.Sort((x, y) => x.Start.CompareTo(y.Start));
        if (raw.Count == 0)
        {
            diagnostics.Add("MouthMissesHost: construction-plane mouth does not meet any section-stack material slab.");
            return Empty(feature, stack, placement, diagnostics);
        }

        // Do not turn a clipped first interval into a mouth event: it would hide
        // an author declaring a mouth inside existing material.
        var firstRawStart = raw[0].Start;
        var firstUnclipped = axis.Z > 0d
            ? (raw[0].Slab.From - mouth.Z) / axis.Z
            : (raw[0].Slab.To - mouth.Z) / axis.Z;
        if (firstUnclipped < -Tol) diagnostics.Add("MouthInsideHostUnexpectedly: construction-plane mouth is inside material, not on its entering boundary.");
        if (firstRawStart > Tol) diagnostics.Add("DirectionDoesNotEnterMaterial: local +Z reaches air before host material.");

        var partitions = new List<SectionStackHolePlannerPartition>();
        foreach (var item in raw)
        {
            var check = CheckDisk(item.Slab.Region, (mouth.X, mouth.Y), feature.Shaft.Radius, item.Start, item.End);
            var footprintSupported = check.Classification == SectionStackFootprintClassification.InsideMaterial;
            if (!footprintSupported) diagnostics.Add($"HoleFootprintLeavesHost: slab=[{item.Slab.From:R},{item.Slab.To:R}]; classification={check.Classification}.");
            var index = partitions.Count;
            partitions.Add(new(item.Start, item.End, item.Slab.From, item.Slab.To, item.Slab.ActiveOperations,
                item.Slab.Region.Provenance, footprintSupported,
                index == 0 ? "MouthCandidate" : "PlannerTransition",
                "PlannerTransitionOrHostBoundary", check,
                item.Slab.Region.Provenance.Concat(["PrismaticSectionStackConstruction", "ProfileArrangement2D"]).Distinct().ToArray()));
        }

        var transitions = new List<string>();
        for (var i = 1; i < partitions.Count; i++)
        {
            var previous = partitions[i - 1]; var current = partitions[i];
            var continuous = Math.Abs(previous.LocalAxisEnd - current.LocalAxisStart) <= Tol &&
                previous.CompleteCircularFootprintInMaterial && current.CompleteCircularFootprintInMaterial;
            transitions.Add($"localAxis={current.LocalAxisStart:R}; kind={(continuous ? "InternalPlannerPartition" : "HostBoundaryOrVoid")}; below=[{previous.SectionFrom:R},{previous.SectionTo:R}]; above=[{current.SectionFrom:R},{current.SectionTo:R}]");
        }

        var spans = Collapse(partitions);
        var supported = partitions.Where(x => x.CompleteCircularFootprintInMaterial).ToArray();
        HoleHostTraversalClassification classification = supported.Length switch
        {
            0 => partitions.Any(x => x.Footprint.Classification == SectionStackFootprintClassification.Tangent) ? HoleHostTraversalClassification.TangentialContact : HoleHostTraversalClassification.NoMaterial,
            _ when spans.Count == 1 && partitions.Count == 1 => HoleHostTraversalClassification.OneContiguousInterval,
            _ when spans.Count == 1 => HoleHostTraversalClassification.MultipleContiguousPartitionsOfOneMaterialSpan,
            _ => HoleHostTraversalClassification.DisconnectedMaterialIntervals
        };
        var intervals = partitions.Select((p, i) => new HoleHostMaterialIntervalEvidence(p.LocalAxisStart, p.LocalAxisEnd,
            $"section-stack:slab:{p.SectionFrom:R}..{p.SectionTo:R}:partition:{i}", string.Join(";", p.Provenance),
            p.CompleteCircularFootprintInMaterial, p.EntryEvent, p.ExitEvent)).ToArray();
        var host = new HoleHostTraversalEvidence(feature.FeatureId, feature.TargetBodyId ?? stack.Feature.Name, placement.ConstructionPlaneId,
            [mouth.X, mouth.Y, mouth.Z], [axis.X, axis.Y, axis.Z], feature.Shaft.Radius, classification, intervals, diagnostics.Distinct().ToArray());
        return new(host, partitions, spans, transitions, partitions.Select(x => x.Footprint).ToArray(), diagnostics.Distinct().ToArray(),
            ["PrismaticSectionStackConstruction", "ProfileArrangement2D", "ExactLineArcDiskContainment", "NoTessellation"]);
    }

    private static SectionStackHoleTraversalEvidence Empty(AirHoleFeature feature, PrismaticSectionStackConstruction stack, AirConstructionPlaneHolePlacement placement, IReadOnlyList<string> diagnostics)
    {
        var mouth = placement.WorldMouthCenter; var axis = placement.AxisZ.ToVector();
        var host = new HoleHostTraversalEvidence(feature.FeatureId, feature.TargetBodyId ?? stack.Feature.Name, placement.ConstructionPlaneId,
            [mouth.X, mouth.Y, mouth.Z], [axis.X, axis.Y, axis.Z], feature.Shaft.Radius, HoleHostTraversalClassification.NoMaterial, [], diagnostics);
        return new(host, [], [], [], [], diagnostics, ["PrismaticSectionStackConstruction", "NoBoxIntervalFallback"]);
    }

    private static IReadOnlyList<SectionStackHolePhysicalSpan> Collapse(IReadOnlyList<SectionStackHolePlannerPartition> partitions)
    {
        var result = new List<SectionStackHolePhysicalSpan>();
        var current = new List<int>();
        for (var i = 0; i < partitions.Count; i++)
        {
            var partition = partitions[i];
            var contiguous = current.Count > 0 && Math.Abs(partitions[current[^1]].LocalAxisEnd - partition.LocalAxisStart) <= Tol;
            if (!partition.CompleteCircularFootprintInMaterial || (!contiguous && current.Count > 0))
            {
                if (current.Count > 0) result.Add(new(partitions[current[0]].LocalAxisStart, partitions[current[^1]].LocalAxisEnd, current.ToArray(), true));
                current.Clear();
            }
            if (partition.CompleteCircularFootprintInMaterial) current.Add(i);
        }
        if (current.Count > 0) result.Add(new(partitions[current[0]].LocalAxisStart, partitions[current[^1]].LocalAxisEnd, current.ToArray(), true));
        return result;
    }

    private static SectionStackHoleFootprintCheck CheckDisk(PrismaticSectionRegion region, (double X, double Y) center, double radius, double start, double end)
    {
        var outerLocation = ProfileArrangementBuilder.PointInProfile(region.Outer, center);
        var outer = BoundaryDistance(region.Outer, center);
        var holes = region.Holes.Select(h => (Location: ProfileArrangementBuilder.PointInProfile(h, center), Distance: BoundaryDistance(h, center), Profile: h)).ToArray();
        var provenance = region.Provenance;
        if (outerLocation != ArrangementPointLocation.Inside)
            return new(start, end, center.X, center.Y, radius, SectionStackFootprintClassification.OutsideMaterial, outer, provenance);
        if (holes.Any(x => x.Location is ArrangementPointLocation.Inside or ArrangementPointLocation.OnBoundary))
            return new(start, end, center.X, center.Y, radius, SectionStackFootprintClassification.IntersectsInnerVoid, holes.Min(x => x.Distance), provenance);
        var nearest = Math.Min(outer, holes.Length == 0 ? double.PositiveInfinity : holes.Min(x => x.Distance));
        var kind = Math.Abs(nearest - radius) <= Tol ? SectionStackFootprintClassification.Tangent
            : nearest < radius ? (outer < radius ? SectionStackFootprintClassification.CrossesOuterBoundary : SectionStackFootprintClassification.IntersectsInnerVoid)
            : SectionStackFootprintClassification.InsideMaterial;
        return new(start, end, center.X, center.Y, radius, kind, nearest, provenance);
    }

    private static double BoundaryDistance(ResolvedProfile2D profile, (double X, double Y) point) => profile.Loops.Single().Segments.Min(segment => segment.Geometry switch
    {
        LineArcLineSegment2D line => DistanceToLineSegment(point, line.Start, line.End),
        LineArcCircularArc2D arc => DistanceToArc(point, arc),
        _ => double.NaN
    });

    private static double DistanceToLineSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var length2 = dx * dx + dy * dy;
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length2, 0d, 1d);
        return Math.Sqrt(Math.Pow(p.X - (a.X + t * dx), 2) + Math.Pow(p.Y - (a.Y + t * dy), 2));
    }

    private static double DistanceToArc((double X, double Y) p, LineArcCircularArc2D arc)
    {
        var dx = p.X - arc.Center.X; var dy = p.Y - arc.Center.Y; var distance = Math.Sqrt(dx * dx + dy * dy);
        var angle = Math.Atan2(dy, dx);
        if (OnArc(arc, angle)) return Math.Abs(distance - arc.Radius);
        var a = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
        var bAngle = arc.StartAngleRadians + arc.SweepAngleRadians;
        var b = (arc.Center.X + arc.Radius * Math.Cos(bAngle), arc.Center.Y + arc.Radius * Math.Sin(bAngle));
        return Math.Min(Math.Sqrt(Math.Pow(p.X - a.Item1, 2) + Math.Pow(p.Y - a.Item2, 2)), Math.Sqrt(Math.Pow(p.X - b.Item1, 2) + Math.Pow(p.Y - b.Item2, 2)));
    }

    private static bool OnArc(LineArcCircularArc2D arc, double angle)
    {
        var delta = angle - arc.StartAngleRadians;
        if (arc.SweepAngleRadians >= 0d) while (delta < 0d) delta += 2d * Math.PI;
        else while (delta > 0d) delta -= 2d * Math.PI;
        return delta / arc.SweepAngleRadians >= -Tol && delta / arc.SweepAngleRadians <= 1d + Tol;
    }

    private static bool IsSignedPermutation(AirConstructionPlaneHolePlacement placement)
    {
        var axes = new[] { placement.AxisX.ToVector(), placement.AxisY.ToVector(), placement.AxisZ.ToVector() };
        return axes.All(axis => new[] { Math.Abs(axis.X), Math.Abs(axis.Y), Math.Abs(axis.Z) }.Count(value => Math.Abs(value - 1d) <= Tol) == 1
            && new[] { Math.Abs(axis.X), Math.Abs(axis.Y), Math.Abs(axis.Z) }.Count(value => value <= Tol) == 2);
    }
}
