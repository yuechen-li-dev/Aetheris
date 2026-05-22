using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum SurfaceTrimContourChainStatus
{
    ClosedLoop,
    OpenChain,
    BoundaryTouching,
    Ambiguous,
    Degenerate,
    Empty,
}

internal sealed record SurfaceTrimContourChainPoint2D(
    double U,
    double V,
    int SourceSegmentIndex,
    string SourceEndpoint,
    Point3D? Point3D = null);

internal sealed record SurfaceTrimContourChain2D(
    int ChainId,
    SurfaceTrimContourChainStatus Status,
    IReadOnlyList<SurfaceTrimContourChainPoint2D> Points,
    IReadOnlyList<int> SourceSegmentIndices,
    bool Closed,
    bool BoundaryTouching,
    IReadOnlyList<string> Diagnostics,
    string OrderingKey);

internal sealed record SurfaceTrimContourStitchResult(
    bool Success,
    IReadOnlyList<SurfaceTrimContourChain2D> Chains,
    int ClosedLoopCount,
    int OpenChainCount,
    int BoundaryTouchingCount,
    int AmbiguousCount,
    int DegenerateCount,
    IReadOnlyList<string> Diagnostics,
    bool AnalyticSnapImplemented = false,
    bool BRepTopologyImplemented = false,
    bool ExactExportAvailable = false);

internal static class SurfaceTrimContourStitcher
{
    internal static SurfaceTrimContourStitchResult Stitch(SurfaceTrimContourExtractionResult extraction, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        var tol = tolerance ?? ToleranceContext.Default;
        var diagnostics = new List<string>
        {
            "contour-stitching-started",
            $"contour-stitching-input-segment-count:{extraction.Segments.Count}",
        };

        if (extraction.Segments.Count == 0)
        {
            diagnostics.Add("contour-stitching-empty-input");
            diagnostics.Add("contour-analytic-snap-not-implemented");
            diagnostics.Add("contour-brep-topology-not-implemented");
            diagnostics.Add("contour-exact-export-not-available");
            return new SurfaceTrimContourStitchResult(true, [], 0, 0, 0, 0, 0, diagnostics);
        }

        var endpoints = new List<(int segmentIndex, string endpoint, SurfaceTrimContourPoint2D point, SurfaceTrimContourPoint3D? point3D)>();
        for (var i = 0; i < extraction.Segments.Count; i++)
        {
            endpoints.Add((i, "A", extraction.Segments[i].A, extraction.Segments[i].A3D));
            endpoints.Add((i, "B", extraction.Segments[i].B, extraction.Segments[i].B3D));
        }

        var orderedEndpoints = endpoints
            .OrderBy(e => e.point.V)
            .ThenBy(e => e.point.U)
            .ThenBy(e => e.segmentIndex)
            .ThenBy(e => e.endpoint, StringComparer.Ordinal)
            .ToList();

        var clusterCenters = new List<(double u, double v)>();
        var endpointToCluster = new Dictionary<(int segmentIndex, string endpoint), int>();

        foreach (var endpoint in orderedEndpoints)
        {
            var assigned = -1;
            for (var i = 0; i < clusterCenters.Count; i++)
            {
                var c = clusterCenters[i];
                var du = endpoint.point.U - c.u;
                var dv = endpoint.point.V - c.v;
                if ((du * du) + (dv * dv) <= (tol.Linear * tol.Linear))
                {
                    assigned = i;
                    break;
                }
            }

            if (assigned < 0)
            {
                assigned = clusterCenters.Count;
                clusterCenters.Add((endpoint.point.U, endpoint.point.V));
            }

            endpointToCluster[(endpoint.segmentIndex, endpoint.endpoint)] = assigned;
        }

        var clusterToEdges = new Dictionary<int, List<int>>();
        for (var i = 0; i < extraction.Segments.Count; i++)
        {
            var a = endpointToCluster[(i, "A")];
            var b = endpointToCluster[(i, "B")];
            if (!clusterToEdges.ContainsKey(a)) clusterToEdges[a] = [];
            if (!clusterToEdges.ContainsKey(b)) clusterToEdges[b] = [];
            clusterToEdges[a].Add(i);
            clusterToEdges[b].Add(i);
        }

        var ambiguousClusters = clusterToEdges.Where(kv => kv.Value.Count > 2).Select(kv => kv.Key).OrderBy(i => i).ToArray();
        diagnostics.Add($"contour-stitching-endpoint-cluster-count:{clusterCenters.Count}");
        diagnostics.Add($"contour-stitching-ambiguous-cluster-count:{ambiguousClusters.Length}");

        var visited = new bool[extraction.Segments.Count];
        var chains = new List<SurfaceTrimContourChain2D>();
        var chainId = 0;

        foreach (var segIndex in Enumerable.Range(0, extraction.Segments.Count))
        {
            if (visited[segIndex])
            {
                continue;
            }

            var component = CollectComponent(segIndex, endpointToCluster, clusterToEdges, extraction.Segments.Count, visited);
            var componentEdges = component.OrderBy(i => i).ToList();
            var componentClusters = componentEdges
                .SelectMany(i => new[] { endpointToCluster[(i, "A")], endpointToCluster[(i, "B")] })
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            var hasAmbiguous = componentClusters.Any(c => clusterToEdges[c].Count > 2);
            var oddDegree = componentClusters.Where(c => clusterToEdges[c].Count % 2 != 0).ToList();
            var endpointsOpen = componentClusters.Where(c => clusterToEdges[c].Count == 1).ToList();

            var diagnosticsLocal = new List<string>();
            if (hasAmbiguous) diagnosticsLocal.Add("ambiguous-branch-node-degree-gt2");

            var orderedPoints = BuildOrderedPoints(componentEdges, endpointToCluster, clusterToEdges, extraction.Segments);
            var totalLen = ChainLength(orderedPoints);
            var start = orderedPoints.FirstOrDefault();
            var end = orderedPoints.LastOrDefault();
            var closedByDistance = orderedPoints.Count > 2 && start is not null && end is not null &&
                                   DistanceSquared(start.U, start.V, end.U, end.V) <= (tol.Linear * tol.Linear);

            var boundaryTouching = endpointsOpen.Any(c => IsBoundary(clusterCenters[c], tol));
            var status = SurfaceTrimContourChainStatus.OpenChain;
            if (orderedPoints.Count == 0)
            {
                status = SurfaceTrimContourChainStatus.Empty;
            }
            else if (totalLen <= tol.Linear || componentEdges.Count == 0 || orderedPoints.Select(p => (p.U, p.V)).Distinct().Count() < 2)
            {
                status = SurfaceTrimContourChainStatus.Degenerate;
            }
            else if (hasAmbiguous || oddDegree.Count > 0 && oddDegree.Count != 2)
            {
                status = SurfaceTrimContourChainStatus.Ambiguous;
            }
            else if (endpointsOpen.Count == 0 || closedByDistance)
            {
                status = SurfaceTrimContourChainStatus.ClosedLoop;
            }
            else if (boundaryTouching)
            {
                status = SurfaceTrimContourChainStatus.BoundaryTouching;
            }

            chains.Add(new SurfaceTrimContourChain2D(
                chainId++,
                status,
                orderedPoints,
                componentEdges,
                status == SurfaceTrimContourChainStatus.ClosedLoop,
                boundaryTouching,
                diagnosticsLocal,
                $"{orderedPoints.FirstOrDefault()?.V:R}:{orderedPoints.FirstOrDefault()?.U:R}:{componentEdges.FirstOrDefault()}"));
        }

        chains = chains.OrderBy(c => c.OrderingKey, StringComparer.Ordinal).ThenBy(c => c.ChainId).ToList();
        for (var i = 0; i < chains.Count; i++) chains[i] = chains[i] with { ChainId = i };

        var closed = chains.Count(c => c.Status == SurfaceTrimContourChainStatus.ClosedLoop);
        var open = chains.Count(c => c.Status == SurfaceTrimContourChainStatus.OpenChain);
        var boundary = chains.Count(c => c.Status == SurfaceTrimContourChainStatus.BoundaryTouching);
        var ambiguous = chains.Count(c => c.Status == SurfaceTrimContourChainStatus.Ambiguous);
        var degenerate = chains.Count(c => c.Status == SurfaceTrimContourChainStatus.Degenerate);

        diagnostics.Add($"contour-stitching-closed-loop-count:{closed}");
        diagnostics.Add($"contour-stitching-open-chain-count:{open}");
        diagnostics.Add($"contour-stitching-boundary-touching-count:{boundary}");
        diagnostics.Add($"contour-stitching-ambiguous-count:{ambiguous}");
        diagnostics.Add($"contour-stitching-degenerate-count:{degenerate}");
        diagnostics.Add("contour-analytic-snap-not-implemented");
        diagnostics.Add("contour-brep-topology-not-implemented");
        diagnostics.Add("contour-exact-export-not-available");

        return new SurfaceTrimContourStitchResult(true, chains, closed, open, boundary, ambiguous, degenerate, diagnostics);
    }

    private static List<int> CollectComponent(int seed, Dictionary<(int segmentIndex, string endpoint), int> endpointToCluster, Dictionary<int, List<int>> clusterToEdges, int segmentCount, bool[] visited)
    {
        var list = new List<int>();
        var q = new Queue<int>();
        q.Enqueue(seed);
        visited[seed] = true;
        while (q.Count > 0)
        {
            var s = q.Dequeue();
            list.Add(s);
            var a = endpointToCluster[(s, "A")];
            var b = endpointToCluster[(s, "B")];
            foreach (var n in clusterToEdges[a].Concat(clusterToEdges[b]).Distinct().OrderBy(i => i))
            {
                if (n >= 0 && n < segmentCount && !visited[n])
                {
                    visited[n] = true;
                    q.Enqueue(n);
                }
            }
        }

        return list;
    }

    private static List<SurfaceTrimContourChainPoint2D> BuildOrderedPoints(List<int> componentEdges, Dictionary<(int segmentIndex, string endpoint), int> endpointToCluster, Dictionary<int, List<int>> clusterToEdges, IReadOnlyList<SurfaceTrimContourSegment2D> segments)
    {
        var ordered = new List<SurfaceTrimContourChainPoint2D>();
        var unvisited = new HashSet<int>(componentEdges);
        if (componentEdges.Count == 0) return ordered;

        var startEdge = componentEdges[0];
        var ends = componentEdges.SelectMany(e => new[] { endpointToCluster[(e, "A")], endpointToCluster[(e, "B")] });
        var startNode = ends.GroupBy(n => n).Where(g => clusterToEdges[g.Key].Count == 1).Select(g => g.Key).OrderBy(i => i).FirstOrDefault();
        var currentNode = startNode;
        var currentEdge = startEdge;

        while (unvisited.Count > 0)
        {
            if (!unvisited.Contains(currentEdge))
            {
                currentEdge = unvisited.OrderBy(i => i).First();
            }

            var seg = segments[currentEdge];
            var aNode = endpointToCluster[(currentEdge, "A")];
            var bNode = endpointToCluster[(currentEdge, "B")];
            var forward = currentNode == aNode || (currentNode != bNode && aNode <= bNode);
            if (forward)
            {
                ordered.Add(new SurfaceTrimContourChainPoint2D(seg.A.U, seg.A.V, currentEdge, "A", seg.A3D?.Point));
                ordered.Add(new SurfaceTrimContourChainPoint2D(seg.B.U, seg.B.V, currentEdge, "B", seg.B3D?.Point));
                currentNode = bNode;
            }
            else
            {
                ordered.Add(new SurfaceTrimContourChainPoint2D(seg.B.U, seg.B.V, currentEdge, "B", seg.B3D?.Point));
                ordered.Add(new SurfaceTrimContourChainPoint2D(seg.A.U, seg.A.V, currentEdge, "A", seg.A3D?.Point));
                currentNode = aNode;
            }

            unvisited.Remove(currentEdge);
            var next = clusterToEdges[currentNode].Where(e => unvisited.Contains(e)).OrderBy(i => i).FirstOrDefault(-1);
            if (next < 0 && unvisited.Count > 0) next = unvisited.OrderBy(i => i).First();
            if (next < 0) break;
            currentEdge = next;
        }

        return ordered;
    }

    private static bool IsBoundary((double u, double v) p, ToleranceContext tol)
        => double.Abs(p.u - 0d) <= tol.Linear || double.Abs(p.u - 1d) <= tol.Linear || double.Abs(p.v - 0d) <= tol.Linear || double.Abs(p.v - 1d) <= tol.Linear;

    private static double ChainLength(IReadOnlyList<SurfaceTrimContourChainPoint2D> points)
    {
        var len = 0d;
        for (var i = 1; i < points.Count; i++)
        {
            len += double.Sqrt(DistanceSquared(points[i - 1].U, points[i - 1].V, points[i].U, points[i].V));
        }

        return len;
    }

    private static double DistanceSquared(double ua, double va, double ub, double vb)
    {
        var du = ua - ub;
        var dv = va - vb;
        return (du * du) + (dv * dv);
    }
}
