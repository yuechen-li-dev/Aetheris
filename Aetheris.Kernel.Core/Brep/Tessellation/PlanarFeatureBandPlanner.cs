using System.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public sealed record PlanarTransitionPort(int VertexA, int VertexB, double U, double V, double DirectionU, double DirectionV);
public sealed record PlanarFeatureBandPlan(
    int LoopId, PlanarFeatureLoopKind FeatureKind, int InnerBoundarySamples, int OuterGuideSamples,
    double BandWidth, int Rows, int CellCount, IReadOnlyList<PlanarTransitionPort> TransitionPorts,
    double TopologyLocality, string CollisionResolution);
public sealed record PlanarBridgePlan(
    int FeatureLoopId, int SourcePortIndex, int TargetOuterEdgeIndex, double DirectionU, double DirectionV,
    double Width, int Rows, int CellCount, double Cost, string TargetRule);
public sealed record PlanarFeatureClusterPlan(IReadOnlyList<int> MemberLoopIds, string Resolution);
public sealed record PlanarFeaturePlanningTimings(
    double ClassificationMilliseconds, double BandConstructionMilliseconds, double CollisionResolutionMilliseconds,
    double BridgeSelectionMilliseconds, double RemainderDecompositionMilliseconds, double TotalMilliseconds);
public sealed record PlanarFeatureDecompositionPlan(
    IReadOnlyList<PlanarFeatureBandPlan> Bands, IReadOnlyList<PlanarBridgePlan> Bridges,
    IReadOnlyList<PlanarFeatureClusterPlan> Clusters, int FeatureBandCellCount, int BridgeCellCount,
    int CoarseRemainderCellCount, int ResidualTransitionCellCount, double MaximumTopologyLocality,
    bool UsedM6Fallback, string? FallbackReason, PlanarFeaturePlanningTimings Timings);

internal static class PlanarFeatureBandPlanner
{
    private const double Epsilon = 1e-8d;
    private const int MaximumGuideSamples = 192;
    private const int MaximumBridgeRows = 8;

    public static bool TryPlan(
        PlanarDomain domain,
        PlaneSurface plane,
        List<SurfaceMeshVertex> vertices,
        ref int nextVertexId,
        bool sameSense,
        out IReadOnlyList<SurfaceMeshCell> cells,
        out PlanarFeatureDecompositionPlan plan,
        out string? failure)
    {
        var totalWatch = Stopwatch.StartNew();
        cells = [];
        failure = null;
        var classificationMs = totalWatch.Elapsed.TotalMilliseconds;
        var uv = domain.BoundaryVertices.ToDictionary(pair => pair.Key, pair => pair.Value);
        var allCells = new List<SurfaceMeshCell>();
        var bandPlans = new List<PlanarFeatureBandPlan>();
        var bridgePlans = new List<PlanarBridgePlan>();
        var clusters = new List<PlanarFeatureClusterPlan>();
        var bands = new List<BandGeometry>();

        var bandWatch = Stopwatch.StartNew();
        var faceBounds = Bounds(domain.OuterLoop.LocalCoordinates);
        var faceScale = double.Min(faceBounds.MaxU - faceBounds.MinU, faceBounds.MaxV - faceBounds.MinV);
        if (faceScale <= Epsilon)
            return Fail("planar domain has no usable local scale", classificationMs, totalWatch, out plan, out failure);

        var loops = new[] { domain.OuterLoop }.Concat(domain.InnerLoops).ToArray();
        for (var loopIndex = 0; loopIndex < loops.Length; loopIndex++)
        {
            var loop = loops[loopIndex];
            var isOuter = loop.LoopId == domain.OuterLoop.LoopId;
            var feature = isOuter ? null : domain.FeatureLoops.Single(item => item.Loop.LoopId == loop.LoopId);
            var clearance = MinimumClearance(loop, loops.Where(other => other.LoopId != loop.LoopId));
            var size = isOuter ? faceScale : double.Max(feature!.CharacteristicSize, faceScale * 0.005d);
            var nominalWidth = isOuter ? faceScale * 0.025d : double.Min(size * 0.45d, faceScale * 0.035d);
            var proposedWidth = double.Min(nominalWidth, clearance * (isOuter ? 0.15d : 0.20d));
            if (proposedWidth <= faceScale * 1e-5d)
                return Fail($"loop {loop.LoopId.Value} has insufficient separation for a valid feature band", classificationMs, totalWatch, out plan, out failure);

            var targetCount = isOuter
                ? int.Clamp(loop.BoundarySpans?.Count ?? 24, 16, MaximumGuideSamples)
                : TargetGuideCount(feature!, loop.VertexIds.Count);
            BandGeometry? band = null;
            var collisionResolution = proposedWidth < nominalWidth - Epsilon ? "ClearanceClamped" : "Independent";
            var lastBandFailure = "unknown guide failure";
            for (var attempt = 0; attempt < 6 && band is null; attempt++)
            {
                var width = proposedWidth * double.Pow(0.65d, attempt);
                if (TryCreateBand(loop, feature?.Kind, isOuter, width, targetCount, domain, plane, vertices, uv, ref nextVertexId, sameSense, out var candidate, out lastBandFailure))
                {
                    if (bands.Any(existing => LoopsIntersect(candidate.GuideCoordinates, existing.GuideCoordinates)))
                    {
                        collisionResolution = "ShrunkForNeighbor";
                        continue;
                    }
                    band = candidate;
                    if (attempt > 0) collisionResolution = "ShrunkForDomainOrNeighbor";
                }
            }
            if (band is null)
                return Fail($"loop {loop.LoopId.Value} could not construct a simple non-overlapping guide: {lastBandFailure}", classificationMs, totalWatch, out plan, out failure);
            bands.Add(band);
            allCells.AddRange(band.Cells);
            bandPlans.Add(new(loop.LoopId.Value, feature?.Kind ?? PlanarFeatureLoopKind.UnknownSimpleInnerLoop,
                loop.VertexIds.Count, band.GuideIds.Count, band.Width, 2, band.Cells.Count,
                CandidatePorts(band, domain.DominantDirections), band.Width, collisionResolution));
        }
        bandWatch.Stop();

        var outerBand = bands.Single(band => band.IsOuter);
        var innerBands = bands.Where(band => !band.IsOuter).OrderBy(band => band.SourceLoop.LoopId.Value).ToArray();
        var bridgeWatch = Stopwatch.StartNew();
        var bridges = new List<BridgeGeometry>();
        var occupiedTargets = new HashSet<int>();
        foreach (var band in innerBands.OrderByDescending(band => DistanceToLoop(band.GuideCoordinates, outerBand.GuideCoordinates)))
        {
            if (!TrySelectBridge(band, outerBand, innerBands, bridges, occupiedTargets, domain, plane, vertices, uv, ref nextVertexId, sameSense, out var bridge))
                return Fail($"loop {band.SourceLoop.LoopId.Value} has no non-crossing structured bridge candidate", classificationMs, totalWatch, out plan, out failure);
            bridges.Add(bridge);
            occupiedTargets.Add(bridge.TargetEdgeIndex);
        }
        bridgeWatch.Stop();

        var remainderWatch = Stopwatch.StartNew();
        var acceptedBridges = new List<BridgeGeometry>();
        foreach (var candidate in bridges.OrderBy(bridge => bridge.Cost).ThenBy(bridge => bridge.Source.SourceLoop.LoopId.Value))
        {
            var trial = acceptedBridges.Append(candidate).ToArray();
            if (CanTriangulateRemainder(outerBand, innerBands, trial, plane, vertices)) acceptedBridges.Add(candidate);
        }
        foreach (var bridge in acceptedBridges)
        {
            allCells.AddRange(bridge.Cells);
            bridgePlans.Add(new(bridge.Source.SourceLoop.LoopId.Value, bridge.SourceEdgeIndex, bridge.TargetEdgeIndex,
                bridge.Direction.U, bridge.Direction.V, bridge.Width, bridge.Rows.Count - 1, bridge.Cells.Count, bridge.Cost,
                "nearest valid outer-guide edge with dominant-direction, crossing, and skew penalties"));
        }
        var remainderIds = BuildNotchedRemainder(outerBand, acceptedBridges);
        if (remainderIds.Count < 3 || remainderIds.Distinct().Count() != remainderIds.Count)
            return Fail("structured bridges did not produce one simple coarse remainder boundary", classificationMs, totalWatch, out plan, out failure);
        var remainderLocal = remainderIds.Select(id => uv[id]).ToArray();
        if (!IsSimplePolygon(remainderLocal))
            return Fail("structured bridge remainder is self-crossing", classificationMs, totalWatch, out plan, out failure);
        var remainderLoop = new SurfaceMeshTrimLoop(domain.OuterLoop.LoopId, remainderIds, remainderLocal, false, SignedArea(remainderLocal));
        var bridgedLoopIds = acceptedBridges.Select(bridge => bridge.Source.SourceLoop.LoopId).ToHashSet();
        var remainderHoles = innerBands.Where(band => !bridgedLoopIds.Contains(band.SourceLoop.LoopId))
            .Select(band => new SurfaceMeshTrimLoop(band.SourceLoop.LoopId, band.GuideIds, band.GuideCoordinates, true, SignedArea(band.GuideCoordinates))).ToArray();
        var remainderDomain = PlanarDomainPlanner.Create(new[] { remainderLoop }.Concat(remainderHoles).ToArray());
        var vertexById = vertices.ToDictionary(vertex => vertex.Id);
        if (!PlanarDomainPlanner.TryDecompose(remainderDomain, plane, vertexById, sameSense, out var rawRemainder, out _))
        {
            PlanarPolygonTriangulator.TryTriangulateWithHoles(
                remainderIds.Select(id => vertexById[id].Position).ToArray(),
                remainderHoles.Select(loop => (IReadOnlyList<Point3D>)loop.VertexIds.Select(id => vertexById[id].Position).ToArray()).ToArray(),
                plane.Normal.ToVector(), out _, out _, out var triangulationFailure);
            return Fail($"coarse notched remainder decomposition failed: {triangulationFailure}", classificationMs, totalWatch, out plan, out failure);
        }
        var conformingRemainder = SurfaceMeshIrTessellator.PreserveBoundaryVerticesInParameterSpace(
            rawRemainder, new[] { remainderLoop }.Concat(remainderHoles).ToArray());
        var mergedRemainder = PlanarDomainPlanner.MergeConformingCells(conformingRemainder, remainderDomain.BoundaryVertices, sameSense)
            .Select(cell => cell with
            {
                Provenance = cell.Kind == SurfaceMeshCellKind.Triangle ? SurfaceMeshCellProvenance.ResidualTransition : SurfaceMeshCellProvenance.CoarseRemainder,
                ExceptionalReason = cell.Kind == SurfaceMeshCellKind.Triangle ? "bounded residual of the simple notched remainder" : null
            }).ToArray();
        allCells.AddRange(mergedRemainder);
        remainderWatch.Stop();
        totalWatch.Stop();

        var residual = allCells.Count(cell => cell.Provenance == SurfaceMeshCellProvenance.ResidualTransition);
        plan = new(bandPlans, bridgePlans, clusters,
            allCells.Count(cell => cell.Provenance == SurfaceMeshCellProvenance.FeatureBand),
            allCells.Count(cell => cell.Provenance == SurfaceMeshCellProvenance.Bridge),
            allCells.Count(cell => cell.Provenance == SurfaceMeshCellProvenance.CoarseRemainder), residual,
            bandPlans.Select(item => item.TopologyLocality).DefaultIfEmpty().Max(), false, null,
            new(classificationMs, bandWatch.Elapsed.TotalMilliseconds, 0d, bridgeWatch.Elapsed.TotalMilliseconds,
                remainderWatch.Elapsed.TotalMilliseconds, totalWatch.Elapsed.TotalMilliseconds));
        cells = allCells;
        return true;
    }

    private static bool CanTriangulateRemainder(
        BandGeometry outer, IReadOnlyList<BandGeometry> inner, IReadOnlyList<BridgeGeometry> bridges,
        PlaneSurface plane, IReadOnlyList<SurfaceMeshVertex> vertices)
    {
        var ids = BuildNotchedRemainder(outer, bridges);
        if (ids.Count < 3 || ids.Distinct().Count() != ids.Count) return false;
        var byId = vertices.ToDictionary(vertex => vertex.Id);
        var bridged = bridges.Select(bridge => bridge.Source.SourceLoop.LoopId).ToHashSet();
        var holes = inner.Where(band => !bridged.Contains(band.SourceLoop.LoopId))
            .Select(band => (IReadOnlyList<Point3D>)band.GuideIds.Select(id => byId[id].Position).ToArray()).ToArray();
        return PlanarPolygonTriangulator.TryTriangulateWithHoles(ids.Select(id => byId[id].Position).ToArray(), holes,
            plane.Normal.ToVector(), out _, out _, out _);
    }

    private static bool Fail(string reason, double classificationMs, Stopwatch totalWatch, out PlanarFeatureDecompositionPlan plan, out string? failure)
    {
        totalWatch.Stop(); failure = reason;
        plan = new([], [], [], 0, 0, 0, 0, 0d, true, reason,
            new(classificationMs, 0d, 0d, 0d, 0d, totalWatch.Elapsed.TotalMilliseconds));
        return false;
    }

    private static bool TryCreateBand(
        SurfaceMeshTrimLoop loop, PlanarFeatureLoopKind? kind, bool isOuter, double width, int targetCount,
        PlanarDomain domain, PlaneSurface plane, List<SurfaceMeshVertex> vertices,
        Dictionary<int, (double U, double V)> uv, ref int nextVertexId, bool sameSense,
        out BandGeometry band, out string failure)
    {
        band = default!; failure = "";
        var source = loop.LocalCoordinates;
        // Left/right is a property of the loop winding in this face's plane,
        // not of global XYZ or face sense.  Outer guides move into their loop;
        // inner guides move away from the void and into face material.
        var winding = SignedArea(source) >= 0d ? 1d : -1d;
        var signedWidth = width * (isOuter ? winding : -winding);
        var fullGuide = OffsetLoop(source, signedWidth);
        // A sampled straight chain can contain coincident offset positions at a
        // B-rep junction.  The guide is the topological boundary that must be
        // simple; the dense correspondence row is validated cell-by-cell below.
        var selected = RefineGuideIndices(SelectGuideIndices(loop, targetCount), fullGuide, loop, isOuter, domain);
        var guideCoordinates = selected.Select(index => fullGuide[index]).ToArray();
        if (!IsSimplePolygon(guideCoordinates)) { failure = "simplified guide self-intersects"; return false; }
        if (!GuideIsInMaterial(guideCoordinates, loop, isOuter, domain)) { failure = "simplified guide leaves face material"; return false; }

        var guideIds = new int[selected.Count];
        for (var index = 0; index < selected.Count; index++) guideIds[index] = AddVertex(fullGuide[selected[index]], plane, vertices, uv, ref nextVertexId);
        var cells = new List<SurfaceMeshCell>(source.Count);
        for (var selectedIndex = 0; selectedIndex < selected.Count; selectedIndex++)
        {
            var start = selected[selectedIndex];
            var end = selected[(selectedIndex + 1) % selected.Count];
            var path = new List<int> { loop.VertexIds[start] };
            for (var index = (start + 1) % source.Count; index != (end + 1) % source.Count; index = (index + 1) % source.Count) path.Add(loop.VertexIds[index]);
            path.Add(guideIds[(selectedIndex + 1) % selected.Count]); path.Add(guideIds[selectedIndex]);
            cells.AddRange(PartitionLocalPolygon(path, plane, uv, sameSense));
        }
        band = new(loop, kind, isOuter, width, [], guideIds, guideCoordinates, cells);
        return true;
    }

    private static IReadOnlyList<SurfaceMeshCell> PartitionLocalPolygon(
        IReadOnlyList<int> ids, PlaneSurface plane, IReadOnlyDictionary<int, (double U, double V)> uv, bool sameSense)
    {
        // A sector bounded by one coarse guide edge and one authoritative trim
        // chain is already the ideal planar cell.  Preserve it as a local safe
        // polygon whenever Aetheris can prove its own deterministic lowering;
        // sampled curvature does not justify exposing the lowering diagonals in
        // SurfaceMeshIR/OBJ.
        if (ids.Count >= 3 && ids.Distinct().Count() == ids.Count
            && PlanarPolygonTriangulator.TryTriangulate(ids.Select(id => plane.Evaluate(uv[id].U, uv[id].V)).ToArray(),
                plane.Normal.ToVector(), out _, out _))
            return [CreateCell(ids, uv, sameSense, SurfaceMeshCellProvenance.FeatureBand)];

        // The polygon is one open dense guide path closed by one coarse guide
        // edge. Split it into two bounded local fans plus a central transition.
        // This retains every dense boundary segment and cannot leave a sector
        // uncovered when generic ear clipping rejects near-collinear offsets.
        var path = ids.Take(ids.Count - 2).ToArray();
        var guideNext = ids[^2]; var guide = ids[^1];
        if (path.Length < 2 || guide == guideNext) return [];
        var pivot = (path.Length - 1) / 2;
        var triangles = new List<SurfaceMeshCell>();
        for (var index = 0; index + 1 < path.Length; index++)
            triangles.Add(CreateCell([path[index], path[index + 1], index < pivot ? guide : guideNext], uv, sameSense,
                SurfaceMeshCellProvenance.ResidualTransition, "unequal feature-band sample transition"));
        triangles.Add(CreateCell([guide, path[pivot], guideNext], uv, sameSense,
            SurfaceMeshCellProvenance.ResidualTransition, "unequal feature-band sample transition"));
        return PlanarDomainPlanner.MergeConformingCells(triangles, uv, sameSense).Select(cell => cell with
        {
            Provenance = cell.Kind == SurfaceMeshCellKind.Triangle ? SurfaceMeshCellProvenance.ResidualTransition : SurfaceMeshCellProvenance.FeatureBand,
            ExceptionalReason = cell.Kind == SurfaceMeshCellKind.Triangle ? "unequal feature-band sample transition" : null
        }).ToArray();
    }

    private static bool TrySelectBridge(
        BandGeometry source, BandGeometry outer, IReadOnlyList<BandGeometry> allInner, IReadOnlyList<BridgeGeometry> existing,
        IReadOnlySet<int> occupiedTargets, PlanarDomain domain, PlaneSurface plane, List<SurfaceMeshVertex> vertices,
        Dictionary<int, (double U, double V)> uv, ref int nextVertexId, bool sameSense, out BridgeGeometry bridge)
    {
        bridge = default!;
        var candidates = new List<BridgeCandidate>();
        for (var sourceIndex = 0; sourceIndex < source.GuideIds.Count; sourceIndex++)
        for (var targetIndex = 0; targetIndex < outer.GuideIds.Count; targetIndex++)
        {
            if (occupiedTargets.Contains(targetIndex)) continue;
            var s0 = source.GuideCoordinates[sourceIndex]; var s1 = source.GuideCoordinates[(sourceIndex + 1) % source.GuideIds.Count];
            var t0 = outer.GuideCoordinates[targetIndex]; var t1 = outer.GuideCoordinates[(targetIndex + 1) % outer.GuideIds.Count];
            var sourceMid = Midpoint(s0, s1); var targetMid = Midpoint(t0, t1);
            var direction = Normalize((targetMid.U - sourceMid.U, targetMid.V - sourceMid.V));
            var alignment = domain.DominantDirections.Select(axis => double.Abs(Dot(axis, direction))).DefaultIfEmpty(1d).Max();
            var distance = Distance(sourceMid, targetMid);
            var skew = double.Abs(double.Log(double.Max(Distance(s0, s1), Epsilon) / double.Max(Distance(t0, t1), Epsilon)));
            var cost = distance * (1d + (0.30d * (1d - alignment))) + (skew * source.Width);
            candidates.Add(new(sourceIndex, targetIndex, direction, cost));
        }
        foreach (var candidate in candidates.OrderBy(item => item.Cost).ThenBy(item => item.TargetIndex).ThenBy(item => item.SourceIndex).Take(128))
        {
            var s0 = source.GuideCoordinates[candidate.SourceIndex]; var s1 = source.GuideCoordinates[(candidate.SourceIndex + 1) % source.GuideIds.Count];
            var t0 = outer.GuideCoordinates[candidate.TargetIndex]; var t1 = outer.GuideCoordinates[(candidate.TargetIndex + 1) % outer.GuideIds.Count];
            // Remainder traversal descends from t0 to s1 and returns from s0 to t1.
            if (!BridgeIsClear(s1, s0, t0, t1, source, allInner, existing, domain)) continue;
            var length = Distance(Midpoint(s0, s1), Midpoint(t0, t1));
            var rowSpacing = double.Max(source.Width * 1.5d, double.Max(Distance(s0, s1), Distance(t0, t1)));
            var segments = int.Clamp((int)double.Ceiling(length / double.Max(rowSpacing, Epsilon)), 1, MaximumBridgeRows);
            var rows = new List<(int Left, int Right)>(segments + 1)
            {
                (source.GuideIds[(candidate.SourceIndex + 1) % source.GuideIds.Count], source.GuideIds[candidate.SourceIndex])
            };
            for (var row = 1; row < segments; row++)
            {
                var t = (double)row / segments;
                rows.Add((AddVertex(Lerp(s1, t0, t), plane, vertices, uv, ref nextVertexId), AddVertex(Lerp(s0, t1, t), plane, vertices, uv, ref nextVertexId)));
            }
            rows.Add((outer.GuideIds[candidate.TargetIndex], outer.GuideIds[(candidate.TargetIndex + 1) % outer.GuideIds.Count]));
            var bridgeCells = new List<SurfaceMeshCell>(segments);
            for (var row = 0; row < rows.Count - 1; row++)
                bridgeCells.Add(CreateCell([rows[row].Left, rows[row].Right, rows[row + 1].Right, rows[row + 1].Left], uv, sameSense, SurfaceMeshCellProvenance.Bridge));
            bridge = new(source, candidate.SourceIndex, candidate.TargetIndex, candidate.Direction, source.Width, candidate.Cost, rows, bridgeCells,
                [s1, s0, t1, t0]);
            return true;
        }
        return false;
    }

    private static IReadOnlyList<int> BuildNotchedRemainder(BandGeometry outer, IReadOnlyList<BridgeGeometry> bridges)
    {
        var byTarget = bridges.ToDictionary(bridge => bridge.TargetEdgeIndex);
        var output = new List<int> { outer.GuideIds[0] };
        for (var outerIndex = 0; outerIndex < outer.GuideIds.Count; outerIndex++)
        {
            var nextOuter = (outerIndex + 1) % outer.GuideIds.Count;
            if (!byTarget.TryGetValue(outerIndex, out var bridge))
            {
                output.Add(outer.GuideIds[nextOuter]);
                continue;
            }
            for (var row = bridge.Rows.Count - 2; row >= 0; row--) output.Add(bridge.Rows[row].Left);
            var guide = bridge.Source.GuideIds;
            var sourceS1 = (bridge.SourceEdgeIndex + 1) % guide.Count;
            var cursor = (sourceS1 + 1) % guide.Count;
            while (cursor != bridge.SourceEdgeIndex)
            {
                output.Add(guide[cursor]); cursor = (cursor + 1) % guide.Count;
            }
            output.Add(guide[bridge.SourceEdgeIndex]);
            for (var row = 1; row < bridge.Rows.Count; row++) output.Add(bridge.Rows[row].Right);
        }
        if (output.Count > 1 && output[^1] == output[0]) output.RemoveAt(output.Count - 1);
        return output;
    }

    private static bool BridgeIsClear(
        (double U, double V) sourceLeft, (double U, double V) sourceRight,
        (double U, double V) targetLeft, (double U, double V) targetRight,
        BandGeometry source, IReadOnlyList<BandGeometry> allInner, IReadOnlyList<BridgeGeometry> existing, PlanarDomain domain)
    {
        var polygon = new[] { sourceLeft, sourceRight, targetRight, targetLeft };
        if (!IsSimplePolygon(polygon)) return false;
        foreach (var sample in new[] { Midpoint(sourceLeft, targetLeft), Midpoint(sourceRight, targetRight), PolygonAverage(polygon) })
        {
            if (!PointInPolygon(sample, domain.OuterLoop.LocalCoordinates)) return false;
            if (domain.InnerLoops.Any(loop => loop.LoopId != source.SourceLoop.LoopId && PointInPolygon(sample, loop.LocalCoordinates))) return false;
        }
        foreach (var other in allInner.Where(other => other.SourceLoop.LoopId != source.SourceLoop.LoopId))
            if (LoopsIntersect(polygon, other.GuideCoordinates) || PointInPolygon(PolygonAverage(polygon), other.GuideCoordinates)) return false;
        if (existing.Any(item => PolygonsIntersect(polygon, item.Polygon))) return false;
        return true;
    }

    private static IReadOnlyList<PlanarTransitionPort> CandidatePorts(BandGeometry band, IReadOnlyList<(double U, double V)> directions)
    {
        var center = PolygonAverage(band.GuideCoordinates);
        var candidates = Enumerable.Range(0, band.GuideIds.Count).Select(index =>
        {
            var next = (index + 1) % band.GuideIds.Count;
            var midpoint = Midpoint(band.GuideCoordinates[index], band.GuideCoordinates[next]);
            var direction = Normalize((midpoint.U - center.U, midpoint.V - center.V));
            var alignment = directions.Select(axis => double.Abs(Dot(axis, direction))).DefaultIfEmpty(1d).Max();
            return (index, next, midpoint, direction, alignment);
        }).OrderByDescending(item => item.alignment).ThenBy(item => item.index).Take(4);
        return candidates.Select(item => new PlanarTransitionPort(band.GuideIds[item.index], band.GuideIds[item.next], item.midpoint.U, item.midpoint.V, item.direction.U, item.direction.V)).ToArray();
    }

    private static int TargetGuideCount(PlanarFeatureLoop feature, int sourceCount) => feature.Kind switch
    {
        PlanarFeatureLoopKind.CircularHole => int.Min(sourceCount, 12),
        PlanarFeatureLoopKind.Slot => int.Min(sourceCount, 12),
        PlanarFeatureLoopKind.RoundedSlot => int.Min(sourceCount, 16),
        PlanarFeatureLoopKind.GeneralConvexInnerLoop => int.Min(sourceCount, 12),
        _ => int.Min(sourceCount, 20)
    };

    private static IReadOnlyList<int> SelectGuideIndices(SurfaceMeshTrimLoop loop, int targetCount)
    {
        var count = loop.VertexIds.Count;
        if (targetCount >= count) return Enumerable.Range(0, count).ToArray();
        var selected = new SortedSet<int>();
        for (var index = 0; index < targetCount; index++) selected.Add((int)double.Floor((double)index * count / targetCount));
        if (loop.BoundarySpans is { Count: > 0 })
        {
            var indexById = loop.VertexIds.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
            foreach (var span in loop.BoundarySpans)
            {
                if (indexById.TryGetValue(span.StartVertexId, out var index)) selected.Add(index);
                if (indexById.TryGetValue(span.EndVertexId, out var end)) selected.Add(end);
            }
        }
        while (selected.Count > MaximumGuideSamples)
        {
            var removable = selected.Where(index => index != 0).Select((index, order) => (index, order)).FirstOrDefault(item => item.order % 2 == 1);
            if (removable == default) break;
            selected.Remove(removable.index);
        }
        return selected.ToArray();
    }

    private static IReadOnlyList<int> RefineGuideIndices(
        IReadOnlyList<int> seed, IReadOnlyList<(double U, double V)> fullGuide,
        SurfaceMeshTrimLoop source, bool isOuter, PlanarDomain domain)
    {
        var selected = new SortedSet<int>(seed);
        while (selected.Count < int.Min(MaximumGuideSamples, fullGuide.Count))
        {
            var ordered = selected.ToArray();
            int? insertion = null;
            for (var index = 0; index < ordered.Length; index++)
            {
                var start = ordered[index]; var end = ordered[(index + 1) % ordered.Length];
                var midpoint = Midpoint(fullGuide[start], fullGuide[end]);
                if (PointIsInMaterial(midpoint, source, isOuter, domain)) continue;
                var span = (end - start + fullGuide.Count) % fullGuide.Count;
                if (span <= 1) return selected.ToArray();
                insertion = (start + (span / 2)) % fullGuide.Count;
                break;
            }
            if (insertion is null || !selected.Add(insertion.Value)) break;
        }
        return selected.ToArray();
    }

    private static IReadOnlyList<(double U, double V)> OffsetLoop(IReadOnlyList<(double U, double V)> points, double width)
    {
        var result = new (double U, double V)[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var previous = points[(index - 1 + points.Count) % points.Count]; var current = points[index]; var next = points[(index + 1) % points.Count];
            var incoming = Normalize((current.U - previous.U, current.V - previous.V));
            var outgoing = Normalize((next.U - current.U, next.V - current.V));
            var leftIncoming = (-incoming.V, incoming.U); var leftOutgoing = (-outgoing.V, outgoing.U);
            var bisector = Normalize((leftIncoming.Item1 + leftOutgoing.Item1, leftIncoming.Item2 + leftOutgoing.Item2));
            if (Distance((0d, 0d), bisector) <= Epsilon) bisector = leftIncoming;
            var correction = double.Max(double.Abs(Dot(bisector, leftIncoming)), 0.35d);
            var displacement = double.Min(width / correction, width * 2.25d);
            result[index] = (current.U + (bisector.U * displacement), current.V + (bisector.V * displacement));
        }
        return result;
    }

    private static bool GuideIsInMaterial(IReadOnlyList<(double U, double V)> guide, SurfaceMeshTrimLoop source, bool isOuter, PlanarDomain domain)
    {
        foreach (var point in guide.Concat(Enumerable.Range(0, guide.Count).Select(index => Midpoint(guide[index], guide[(index + 1) % guide.Count]))))
        {
            if (!PointIsInMaterial(point, source, isOuter, domain)) return false;
        }
        return true;
    }
    private static bool PointIsInMaterial((double U, double V) point, SurfaceMeshTrimLoop source, bool isOuter, PlanarDomain domain)
        => PointInPolygon(point, domain.OuterLoop.LocalCoordinates)
           && (isOuter || !PointInPolygon(point, source.LocalCoordinates))
           && !domain.InnerLoops.Any(loop => loop.LoopId != source.LoopId && PointInPolygon(point, loop.LocalCoordinates));

    private static SurfaceMeshCell CreateCell(
        IReadOnlyList<int> ids, IReadOnlyDictionary<int, (double U, double V)> uv, bool sameSense,
        SurfaceMeshCellProvenance provenance, string? reason = null)
    {
        var oriented = SignedArea(ids.Select(id => uv[id]).ToArray()) >= 0d ? ids.ToArray() : ids.Reverse().ToArray();
        if (!sameSense) Array.Reverse(oriented);
        SurfaceMeshCell cell = oriented.Length switch { 3 => new TriangleCell(oriented), 4 => new QuadCell(oriented), _ => new BoundaryPolygonCell(oriented) };
        return cell with { Provenance = provenance, ExceptionalReason = reason };
    }

    private static int AddVertex((double U, double V) point, PlaneSurface plane, ICollection<SurfaceMeshVertex> vertices, Dictionary<int, (double U, double V)> uv, ref int nextVertexId)
    {
        var id = nextVertexId++;
        vertices.Add(new SurfaceMeshVertex(id, plane.Evaluate(point.U, point.V), point.U, point.V)); uv[id] = point;
        return id;
    }

    private static double MinimumClearance(SurfaceMeshTrimLoop loop, IEnumerable<SurfaceMeshTrimLoop> others)
        => others.SelectMany(other => loop.LocalCoordinates.SelectMany(a => other.LocalCoordinates.Select(b => Distance(a, b)))).DefaultIfEmpty(double.PositiveInfinity).Min();
    private static double DistanceToLoop(IReadOnlyList<(double U, double V)> a, IReadOnlyList<(double U, double V)> b)
        => a.SelectMany(x => b.Select(y => Distance(x, y))).DefaultIfEmpty().Min();
    private static (double MinU, double MinV, double MaxU, double MaxV) Bounds(IReadOnlyList<(double U, double V)> points) => (points.Min(point => point.U), points.Min(point => point.V), points.Max(point => point.U), points.Max(point => point.V));
    private static (double U, double V) Midpoint((double U, double V) a, (double U, double V) b) => ((a.U + b.U) * 0.5d, (a.V + b.V) * 0.5d);
    private static (double U, double V) PolygonAverage(IReadOnlyList<(double U, double V)> points) => (points.Average(point => point.U), points.Average(point => point.V));
    private static (double U, double V) Lerp((double U, double V) a, (double U, double V) b, double t) => (a.U + ((b.U - a.U) * t), a.V + ((b.V - a.V) * t));
    private static double Distance((double U, double V) a, (double U, double V) b) => double.Sqrt(((a.U - b.U) * (a.U - b.U)) + ((a.V - b.V) * (a.V - b.V)));
    private static double Dot((double U, double V) a, (double U, double V) b) => (a.U * b.U) + (a.V * b.V);
    private static (double U, double V) Normalize((double U, double V) value)
    {
        var length = Distance((0d, 0d), value); return length <= Epsilon ? (0d, 0d) : (value.U / length, value.V / length);
    }
    private static double SignedArea(IReadOnlyList<(double U, double V)> points) => Enumerable.Range(0, points.Count).Sum(index => (points[index].U * points[(index + 1) % points.Count].V) - (points[(index + 1) % points.Count].U * points[index].V)) * 0.5d;

    private static bool IsSimplePolygon(IReadOnlyList<(double U, double V)> points)
    {
        if (points.Count < 3 || double.Abs(SignedArea(points)) <= Epsilon) return false;
        for (var a = 0; a < points.Count; a++)
        for (var b = a + 1; b < points.Count; b++)
        {
            if (a == b || (a + 1) % points.Count == b || a == (b + 1) % points.Count) continue;
            if (SegmentsIntersect(points[a], points[(a + 1) % points.Count], points[b], points[(b + 1) % points.Count])) return false;
        }
        return true;
    }
    private static bool LoopsIntersect(IReadOnlyList<(double U, double V)> a, IReadOnlyList<(double U, double V)> b)
    {
        for (var i = 0; i < a.Count; i++) for (var j = 0; j < b.Count; j++)
            if (SegmentsIntersect(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
        return false;
    }
    private static bool PolygonsIntersect(IReadOnlyList<(double U, double V)> a, IReadOnlyList<(double U, double V)> b)
        => LoopsIntersect(a, b) || PointInPolygon(a[0], b) || PointInPolygon(b[0], a);
    private static bool SegmentsIntersect((double U, double V) a, (double U, double V) b, (double U, double V) c, (double U, double V) d)
    {
        static double Cross((double U, double V) p, (double U, double V) q, (double U, double V) r) => ((q.U - p.U) * (r.V - p.V)) - ((q.V - p.V) * (r.U - p.U));
        static bool OnSegment((double U, double V) p, (double U, double V) q, (double U, double V) r)
            => q.U >= double.Min(p.U, r.U) - Epsilon && q.U <= double.Max(p.U, r.U) + Epsilon
               && q.V >= double.Min(p.V, r.V) - Epsilon && q.V <= double.Max(p.V, r.V) + Epsilon;
        var abC = Cross(a, b, c); var abD = Cross(a, b, d); var cdA = Cross(c, d, a); var cdB = Cross(c, d, b);
        if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) && ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon))) return true;
        return (double.Abs(abC) <= Epsilon && OnSegment(a, c, b)) || (double.Abs(abD) <= Epsilon && OnSegment(a, d, b))
               || (double.Abs(cdA) <= Epsilon && OnSegment(c, a, d)) || (double.Abs(cdB) <= Epsilon && OnSegment(c, b, d));
    }
    private static bool PointInPolygon((double U, double V) point, IReadOnlyList<(double U, double V)> polygon)
    {
        var inside = false;
        for (var index = 0; index < polygon.Count; index++)
        {
            var a = polygon[index]; var b = polygon[(index + 1) % polygon.Count];
            if ((a.V > point.V) != (b.V > point.V) && point.U < (((b.U - a.U) * (point.V - a.V)) / (b.V - a.V)) + a.U) inside = !inside;
        }
        return inside;
    }

    private sealed record BandGeometry(
        SurfaceMeshTrimLoop SourceLoop, PlanarFeatureLoopKind? Kind, bool IsOuter, double Width,
        IReadOnlyList<int> MidIds, IReadOnlyList<int> GuideIds, IReadOnlyList<(double U, double V)> GuideCoordinates,
        IReadOnlyList<SurfaceMeshCell> Cells);
    private sealed record BridgeCandidate(int SourceIndex, int TargetIndex, (double U, double V) Direction, double Cost);
    private sealed record BridgeGeometry(
        BandGeometry Source, int SourceEdgeIndex, int TargetEdgeIndex, (double U, double V) Direction,
        double Width, double Cost, IReadOnlyList<(int Left, int Right)> Rows, IReadOnlyList<SurfaceMeshCell> Cells,
        IReadOnlyList<(double U, double V)> Polygon);
}
