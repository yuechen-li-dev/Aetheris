using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal enum PlanarPolygonTriangulationFailure
{
    Degenerate,
    NonSimple,
    TriangulationFailed
}

internal static class PlanarPolygonTriangulator
{
    private const double Epsilon = 1e-9d;

    public static bool TryTriangulate(
        IReadOnlyList<Point3D> polygonPoints,
        Vector3D planeNormal,
        out IReadOnlyList<int> indices,
        out PlanarPolygonTriangulationFailure? failure)
    {
        indices = Array.Empty<int>();
        failure = null;

        if (polygonPoints.Count < 3)
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        if (!TryCreateProjection(polygonPoints, planeNormal, out var projection, out failure))
        {
            return false;
        }

        var projected = ProjectPoints(polygonPoints, projection);
        if (!ValidatePolygon(projected))
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        if (HasSelfIntersection(projected))
        {
            failure = PlanarPolygonTriangulationFailure.NonSimple;
            return false;
        }

        var winding = SignedArea(projected);
        if (double.Abs(winding) <= Epsilon)
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        if (!TryEarClipSimplePolygon(projected, Enumerable.Range(0, polygonPoints.Count).ToList(), out var result))
        {
            failure = PlanarPolygonTriangulationFailure.TriangulationFailed;
            return false;
        }

        OrientTrianglesToExpectedNormal(polygonPoints, projection.Normal, result);

        indices = result;
        return true;
    }

    public static bool TryTriangulateWithHoles(
        IReadOnlyList<Point3D> outerPolygonPoints,
        IReadOnlyList<IReadOnlyList<Point3D>> holePolygonPoints,
        Vector3D planeNormal,
        out IReadOnlyList<Point3D> triangulationPoints,
        out IReadOnlyList<int> indices,
        out PlanarPolygonTriangulationFailure? failure)
    {
        triangulationPoints = Array.Empty<Point3D>();
        indices = Array.Empty<int>();
        failure = null;

        if (holePolygonPoints.Count == 0)
        {
            if (!TryTriangulate(outerPolygonPoints, planeNormal, out indices, out failure))
            {
                return false;
            }

            triangulationPoints = outerPolygonPoints.ToArray();
            return true;
        }

        if (outerPolygonPoints.Count < 3 || holePolygonPoints.Any(hole => hole.Count < 3))
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        if (!TryCreateProjection(outerPolygonPoints, planeNormal, out var projection, out failure))
        {
            return false;
        }

        var outerProjected = ProjectPoints(outerPolygonPoints, projection);
        if (!ValidatePolygon(outerProjected))
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        if (HasSelfIntersection(outerProjected))
        {
            failure = PlanarPolygonTriangulationFailure.NonSimple;
            return false;
        }

        var outerArea = SignedArea(outerProjected);
        if (double.Abs(outerArea) <= Epsilon)
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        var allPoints = new List<Point3D>();
        var allProjected = new List<Point2>();
        var outerNode = CreateLinkedRing(outerPolygonPoints, outerProjected, clockwise: false, allPoints, allProjected);
        if (outerNode is null)
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        var holeEntries = new List<HoleEntry>(holePolygonPoints.Count);
        for (var holeIndex = 0; holeIndex < holePolygonPoints.Count; holeIndex++)
        {
            var hole = holePolygonPoints[holeIndex];
            var holeProjected = ProjectPoints(hole, projection);
            if (!ValidatePolygon(holeProjected))
            {
                failure = PlanarPolygonTriangulationFailure.Degenerate;
                return false;
            }

            if (HasSelfIntersection(holeProjected))
            {
                failure = PlanarPolygonTriangulationFailure.NonSimple;
                return false;
            }

            var holeArea = SignedArea(holeProjected);
            if (double.Abs(holeArea) <= Epsilon)
            {
                failure = PlanarPolygonTriangulationFailure.Degenerate;
                return false;
            }

            var holeNode = CreateLinkedRing(hole, holeProjected, clockwise: true, allPoints, allProjected);
            if (holeNode is null)
            {
                failure = PlanarPolygonTriangulationFailure.Degenerate;
                return false;
            }

            holeEntries.Add(new HoleEntry(holeIndex, holeNode, SelectBridgeHoleVertex(holeNode)));
        }

        if (HoleIntersectsBoundary(outerNode, holeEntries.Select(entry => entry.RingStart).ToArray()))
        {
            failure = PlanarPolygonTriangulationFailure.NonSimple;
            return false;
        }

        foreach (var holeEntry in holeEntries
                     .OrderByDescending(entry => entry.Anchor.Point.X)
                     .ThenBy(entry => entry.Anchor.Point.Y)
                     .ThenBy(entry => entry.Anchor.Index))
        {
            var candidate = FindDeterministicBridgeVertex(outerNode, holeEntry.Anchor, holeEntries);
            if (candidate is null)
            {
                failure = PlanarPolygonTriangulationFailure.TriangulationFailed;
                return false;
            }

            outerNode = SplitPolygon(candidate, holeEntry.Anchor, allPoints, allProjected);
            outerNode = FilterPoints(outerNode);
            if (outerNode is null)
            {
                failure = PlanarPolygonTriangulationFailure.TriangulationFailed;
                return false;
            }
        }

        outerNode = FilterPoints(outerNode);
        if (outerNode is null)
        {
            failure = PlanarPolygonTriangulationFailure.TriangulationFailed;
            return false;
        }

        if (!TryEarClipLinkedPolygon(outerNode, allProjected, out var result))
        {
            failure = PlanarPolygonTriangulationFailure.TriangulationFailed;
            return false;
        }

        OrientTrianglesToExpectedNormal(allPoints, projection.Normal, result);

        triangulationPoints = allPoints;
        indices = result;
        return true;
    }

    private static bool TryCreateProjection(
        IReadOnlyList<Point3D> polygonPoints,
        Vector3D planeNormal,
        out PlaneProjection projection,
        out PlanarPolygonTriangulationFailure? failure)
    {
        projection = default;
        failure = null;

        if (!planeNormal.TryNormalize(out var normal))
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        var referenceAxis = double.Abs(normal.Z) < 0.9d
            ? new Vector3D(0d, 0d, 1d)
            : new Vector3D(1d, 0d, 0d);

        var uAxis = referenceAxis.Cross(normal);
        if (!uAxis.TryNormalize(out uAxis))
        {
            referenceAxis = new Vector3D(0d, 1d, 0d);
            uAxis = referenceAxis.Cross(normal);
            if (!uAxis.TryNormalize(out uAxis))
            {
                failure = PlanarPolygonTriangulationFailure.Degenerate;
                return false;
            }
        }

        var vAxis = normal.Cross(uAxis);
        if (!vAxis.TryNormalize(out vAxis))
        {
            failure = PlanarPolygonTriangulationFailure.Degenerate;
            return false;
        }

        projection = new PlaneProjection(polygonPoints[0], normal, uAxis, vAxis);
        return true;
    }

    private static List<Point2> ProjectPoints(IReadOnlyList<Point3D> polygonPoints, PlaneProjection projection)
    {
        return polygonPoints
            .Select(point =>
            {
                var offset = point - projection.Origin;
                return new Point2(offset.Dot(projection.UAxis), offset.Dot(projection.VAxis));
            })
            .ToList();
    }

    private static Node? CreateLinkedRing(
        IReadOnlyList<Point3D> points3D,
        IReadOnlyList<Point2> points2D,
        bool clockwise,
        List<Point3D> allPoints,
        List<Point2> allProjected)
    {
        var signedArea = SignedArea(points2D);
        if (double.Abs(signedArea) <= Epsilon)
        {
            return null;
        }

        var shouldReverse = clockwise ? signedArea > 0d : signedArea < 0d;
        Node? last = null;

        if (shouldReverse)
        {
            for (var i = points3D.Count - 1; i >= 0; i--)
            {
                last = InsertNode(allPoints.Count, points3D[i], points2D[i], ref last, allPoints, allProjected);
            }
        }
        else
        {
            for (var i = 0; i < points3D.Count; i++)
            {
                last = InsertNode(allPoints.Count, points3D[i], points2D[i], ref last, allPoints, allProjected);
            }
        }

        return FilterPoints(last?.Next);
    }

    private static Node InsertNode(
        int index,
        Point3D point3D,
        Point2 point2D,
        ref Node? last,
        List<Point3D> allPoints,
        List<Point2> allProjected)
    {
        allPoints.Add(point3D);
        allProjected.Add(point2D);

        var node = new Node(index, point2D);
        if (last is null)
        {
            node.Prev = node;
            node.Next = node;
        }
        else
        {
            node.Next = last.Next!;
            node.Prev = last;
            last.Next!.Prev = node;
            last.Next = node;
        }

        last = node;
        return node;
    }

    private static Node SelectBridgeHoleVertex(Node start)
    {
        var best = start;
        var node = start.Next!;
        while (node != start)
        {
            if (node.Point.X > best.Point.X + Epsilon
                || (double.Abs(node.Point.X - best.Point.X) <= Epsilon && node.Point.Y < best.Point.Y - Epsilon)
                || (double.Abs(node.Point.X - best.Point.X) <= Epsilon && double.Abs(node.Point.Y - best.Point.Y) <= Epsilon && node.Index < best.Index))
            {
                best = node;
            }

            node = node.Next!;
        }

        return best;
    }

    private static bool HoleIntersectsBoundary(Node outerRing, IReadOnlyList<Node> holeStarts)
    {
        for (var i = 0; i < holeStarts.Count; i++)
        {
            if (RingIntersectsRing(outerRing, holeStarts[i]))
            {
                return true;
            }

            for (var j = i + 1; j < holeStarts.Count; j++)
            {
                if (RingIntersectsRing(holeStarts[i], holeStarts[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RingIntersectsRing(Node first, Node second)
    {
        var a = first;
        do
        {
            var b = second;
            do
            {
                if (SegmentsIntersect(a.Point, a.Next!.Point, b.Point, b.Next!.Point)
                    && !SegmentsShareEndpoint(a.Point, a.Next.Point, b.Point, b.Next.Point))
                {
                    return true;
                }

                b = b.Next!;
            } while (b != second);

            a = a.Next!;
        } while (a != first);

        return false;
    }

    private static Node? FindDeterministicBridgeVertex(
        Node outerRing,
        Node holeAnchor,
        IReadOnlyList<HoleEntry> holes)
    {
        var bridgeMidpointHoles = holes
            .Where(entry => entry.Anchor != holeAnchor)
            .Select(entry => entry.RingStart)
            .ToArray();

        Node? best = null;
        var bestScore = (DistanceSquared: double.PositiveInfinity, X: double.PositiveInfinity, Y: double.PositiveInfinity, Index: int.MaxValue);

        var candidate = outerRing;
        do
        {
            if (IsBridgeVisible(candidate, holeAnchor, outerRing, holeAnchor, bridgeMidpointHoles))
            {
                var score = (
                    DistanceSquared(holeAnchor.Point, candidate.Point),
                    candidate.Point.X,
                    candidate.Point.Y,
                    candidate.Index);

                if (score.CompareTo(bestScore) < 0)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            candidate = candidate.Next!;
        } while (candidate != outerRing);

        return best;
    }

    private static bool IsBridgeVisible(
        Node outerCandidate,
        Node holeAnchor,
        Node outerRing,
        Node currentHoleRing,
        IReadOnlyList<Node> otherHoleRings)
    {
        if (PointsAlmostEqual(outerCandidate.Point, holeAnchor.Point))
        {
            return false;
        }

        if (!BridgeClearsRing(outerCandidate, holeAnchor, outerRing))
        {
            return false;
        }

        if (!BridgeClearsRing(outerCandidate, holeAnchor, currentHoleRing))
        {
            return false;
        }

        for (var i = 0; i < otherHoleRings.Count; i++)
        {
            if (!BridgeClearsRing(outerCandidate, holeAnchor, otherHoleRings[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool BridgeClearsRing(Node outerCandidate, Node holeAnchor, Node ringStart)
    {
        var edge = ringStart;
        do
        {
            var edgeStart = edge.Point;
            var edgeEnd = edge.Next!.Point;
            if (SegmentsIntersect(outerCandidate.Point, holeAnchor.Point, edgeStart, edgeEnd)
                && !SegmentsShareEndpoint(outerCandidate.Point, holeAnchor.Point, edgeStart, edgeEnd))
            {
                return false;
            }

            edge = edge.Next!;
        } while (edge != ringStart);

        return true;
    }

    private static Node SplitPolygon(Node outerCandidate, Node holeAnchor, List<Point3D> allPoints, List<Point2> allProjected)
    {
        var outerCandidateCopy = CreateDuplicateNode(outerCandidate, allPoints, allProjected);
        var holeAnchorCopy = CreateDuplicateNode(holeAnchor, allPoints, allProjected);
        var outerNext = outerCandidate.Next!;
        var holePrev = holeAnchor.Prev!;

        outerCandidate.Next = holeAnchor;
        holeAnchor.Prev = outerCandidate;

        outerCandidateCopy.Next = outerNext;
        outerNext.Prev = outerCandidateCopy;

        holeAnchorCopy.Next = outerCandidateCopy;
        outerCandidateCopy.Prev = holeAnchorCopy;

        holePrev.Next = holeAnchorCopy;
        holeAnchorCopy.Prev = holePrev;

        return outerCandidateCopy;
    }

    private static Node CreateDuplicateNode(Node source, List<Point3D> allPoints, List<Point2> allProjected)
    {
        var index = allPoints.Count;
        allPoints.Add(allPoints[source.Index]);
        allProjected.Add(allProjected[source.Index]);
        return new Node(index, source.Point);
    }

    private static Node? FilterPoints(Node? start)
    {
        if (start is null)
        {
            return null;
        }

        var node = start;
        var changed = false;
        do
        {
            changed = false;

            if (PointsAlmostEqual(node.Point, node.Next!.Point))
            {
                RemoveNode(node);
                node = node.Prev!;
                changed = true;
            }
            else if (double.Abs(Orientation(node.Prev!.Point, node.Point, node.Next.Point)) <= Epsilon)
            {
                RemoveNode(node);
                node = node.Prev!;
                changed = true;
            }
            else
            {
                node = node.Next!;
            }
        } while (changed || node != start);

        return node;
    }

    private static void RemoveNode(Node node)
    {
        node.Next!.Prev = node.Prev;
        node.Prev!.Next = node.Next;
    }

    private static bool TryEarClipSimplePolygon(IReadOnlyList<Point2> projected, List<int> working, out List<int> result)
    {
        result = new List<int>((working.Count - 2) * 3);
        var winding = SignedArea(working.Select(index => projected[index]).ToArray());
        if (double.Abs(winding) <= Epsilon)
        {
            return false;
        }

        var isCounterClockwise = winding > 0d;
        while (working.Count > 3)
        {
            var earFound = false;
            for (var i = 0; i < working.Count; i++)
            {
                var prevIndex = working[(i + working.Count - 1) % working.Count];
                var currentIndex = working[i];
                var nextIndex = working[(i + 1) % working.Count];

                if (!IsConvex(projected[prevIndex], projected[currentIndex], projected[nextIndex], isCounterClockwise))
                {
                    continue;
                }

                var containsVertex = false;
                for (var j = 0; j < working.Count; j++)
                {
                    var candidate = working[j];
                    if (candidate == prevIndex || candidate == currentIndex || candidate == nextIndex)
                    {
                        continue;
                    }

                    if (PointInTriangle(projected[candidate], projected[prevIndex], projected[currentIndex], projected[nextIndex]))
                    {
                        containsVertex = true;
                        break;
                    }
                }

                if (containsVertex)
                {
                    continue;
                }

                result.Add(prevIndex);
                result.Add(currentIndex);
                result.Add(nextIndex);
                working.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                return false;
            }
        }

        result.Add(working[0]);
        result.Add(working[1]);
        result.Add(working[2]);
        return true;
    }

    private static bool TryEarClipLinkedPolygon(Node start, IReadOnlyList<Point2> allProjected, out List<int> result)
    {
        result = new List<int>();
        var vertexCount = CountNodes(start);
        if (vertexCount < 3)
        {
            return false;
        }

        result.Capacity = (vertexCount - 2) * 3;
        var current = start;
        var remaining = vertexCount;
        var iterationsWithoutEar = 0;

        while (remaining > 3)
        {
            if (IsEar(current, start, allProjected))
            {
                result.Add(current.Prev!.Index);
                result.Add(current.Index);
                result.Add(current.Next!.Index);

                RemoveNode(current);
                current = current.Next!;
                start = current;
                remaining--;
                iterationsWithoutEar = 0;
                continue;
            }

            current = current.Next!;
            iterationsWithoutEar++;
            if (iterationsWithoutEar > remaining)
            {
                return false;
            }
        }

        result.Add(current.Prev!.Index);
        result.Add(current.Index);
        result.Add(current.Next!.Index);
        return true;
    }

    private static int CountNodes(Node start)
    {
        var count = 0;
        var node = start;
        do
        {
            count++;
            node = node.Next!;
        } while (node != start);

        return count;
    }

    private static bool IsEar(Node node, Node polygonStart, IReadOnlyList<Point2> allProjected)
    {
        var a = node.Prev!;
        var b = node;
        var c = node.Next!;
        if (Orientation(a.Point, b.Point, c.Point) <= Epsilon)
        {
            return false;
        }

        var candidate = c.Next!;
        while (candidate != a)
        {
            if (candidate != b
                && candidate != node.Prev
                && candidate != node.Next
                && PointInTriangleStrict(candidate.Point, a.Point, b.Point, c.Point))
            {
                return false;
            }

            candidate = candidate.Next!;
        }

        return true;
    }

    private static void OrientTrianglesToExpectedNormal(
        IReadOnlyList<Point3D> polygonPoints,
        Vector3D expectedNormal,
        List<int> result)
    {
        if (result.Count < 3)
        {
            return;
        }

        var firstNormal = (polygonPoints[result[1]] - polygonPoints[result[0]])
            .Cross(polygonPoints[result[2]] - polygonPoints[result[0]]);
        if (firstNormal.Dot(expectedNormal) < 0d)
        {
            for (var i = 0; i < result.Count; i += 3)
            {
                (result[i + 1], result[i + 2]) = (result[i + 2], result[i + 1]);
            }
        }
    }

    private static bool ValidatePolygon(IReadOnlyList<Point2> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            if (DistanceSquared(points[i], next) <= Epsilon * Epsilon)
            {
                return false;
            }
        }

        var uniqueCount = points.Distinct().Count();
        return uniqueCount >= 3;
    }

    private static bool HasSelfIntersection(IReadOnlyList<Point2> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var a0 = points[i];
            var a1 = points[(i + 1) % points.Count];
            for (var j = i + 1; j < points.Count; j++)
            {
                var b0 = points[j];
                var b1 = points[(j + 1) % points.Count];
                if (i == j || (i + 1) % points.Count == j || i == (j + 1) % points.Count)
                {
                    continue;
                }

                if (SegmentsIntersect(a0, a1, b0, b1))
                {
                    return true;
                }
            }
        }

        return false;
    }


    private static bool SegmentsIntersect(Point2 a0, Point2 a1, Point2 b0, Point2 b1)
    {
        var o1 = Orientation(a0, a1, b0);
        var o2 = Orientation(a0, a1, b1);
        var o3 = Orientation(b0, b1, a0);
        var o4 = Orientation(b0, b1, a1);

        if ((o1 * o2 < -Epsilon) && (o3 * o4 < -Epsilon))
        {
            return true;
        }

        if (double.Abs(o1) <= Epsilon && OnSegment(a0, b0, a1)) return true;
        if (double.Abs(o2) <= Epsilon && OnSegment(a0, b1, a1)) return true;
        if (double.Abs(o3) <= Epsilon && OnSegment(b0, a0, b1)) return true;
        if (double.Abs(o4) <= Epsilon && OnSegment(b0, a1, b1)) return true;
        return false;
    }

    private static bool SegmentsShareEndpoint(Point2 a0, Point2 a1, Point2 b0, Point2 b1)
        => PointsAlmostEqual(a0, b0)
           || PointsAlmostEqual(a0, b1)
           || PointsAlmostEqual(a1, b0)
           || PointsAlmostEqual(a1, b1);

    private static bool IsConvex(Point2 a, Point2 b, Point2 c, bool isCounterClockwise)
    {
        var cross = Orientation(a, b, c);
        return isCounterClockwise ? cross > Epsilon : cross < -Epsilon;
    }

    private static bool PointInTriangle(Point2 p, Point2 a, Point2 b, Point2 c)
    {
        var o1 = Orientation(a, b, p);
        var o2 = Orientation(b, c, p);
        var o3 = Orientation(c, a, p);

        var hasNeg = o1 < -Epsilon || o2 < -Epsilon || o3 < -Epsilon;
        var hasPos = o1 > Epsilon || o2 > Epsilon || o3 > Epsilon;
        return !(hasNeg && hasPos);
    }

    private static bool PointInTriangleStrict(Point2 p, Point2 a, Point2 b, Point2 c)
    {
        var o1 = Orientation(a, b, p);
        var o2 = Orientation(b, c, p);
        var o3 = Orientation(c, a, p);

        var allPositive = o1 > Epsilon && o2 > Epsilon && o3 > Epsilon;
        var allNegative = o1 < -Epsilon && o2 < -Epsilon && o3 < -Epsilon;
        return allPositive || allNegative;
    }

    private static double SignedArea(IReadOnlyList<Point2> points)
    {
        var area = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area * 0.5d;
    }

    private static double Orientation(Point2 a, Point2 b, Point2 c)
        => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static bool OnSegment(Point2 a, Point2 p, Point2 b)
        => p.X >= double.Min(a.X, b.X) - Epsilon
           && p.X <= double.Max(a.X, b.X) + Epsilon
           && p.Y >= double.Min(a.Y, b.Y) - Epsilon
           && p.Y <= double.Max(a.Y, b.Y) + Epsilon;

    private static bool PointsAlmostEqual(Point2 a, Point2 b)
        => DistanceSquared(a, b) <= Epsilon * Epsilon;

    private static double DistanceSquared(Point2 a, Point2 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private readonly record struct PlaneProjection(Point3D Origin, Vector3D Normal, Vector3D UAxis, Vector3D VAxis);

    private sealed class Node(int index, Point2 point)
    {
        public int Index { get; } = index;

        public Point2 Point { get; } = point;

        public Node? Prev { get; set; }

        public Node? Next { get; set; }
    }

    private readonly record struct HoleEntry(int Order, Node RingStart, Node Anchor);

    private readonly record struct Point2(double X, double Y);
}
