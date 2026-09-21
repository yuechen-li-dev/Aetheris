namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Robust ear-clipping triangulation of a simple polygon with holes, ported from the well-known earcut
/// algorithm (hole bridging, local-intersection curing and polygon splitting fallbacks). Used as a second
/// opinion when <see cref="PlanarPolygonTriangulator"/> declines a polygon that is nevertheless simple, which
/// happens for long, finely sampled trim loops with many near-collinear vertices.
/// </summary>
internal static class EarCutTriangulator
{
    public static bool TryTriangulate(
        IReadOnlyList<(double X, double Y)> outer,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> holes,
        out List<(double X, double Y)> points,
        out List<int> indices)
    {
        points = new List<(double X, double Y)>(outer);
        indices = new List<int>();
        var holeRanges = new List<(int Start, int End)>(holes.Count);
        foreach (var hole in holes)
        {
            var start = points.Count;
            points.AddRange(hole);
            holeRanges.Add((start, points.Count));
        }

        var outerNode = CreateRing(points, 0, outer.Count, clockwise: true);
        if (outerNode is null || outerNode.Next == outerNode.Prev)
        {
            return false;
        }

        if (holeRanges.Count > 0)
        {
            outerNode = EliminateHoles(points, holeRanges, outerNode);
            if (outerNode is null)
            {
                return false;
            }
        }

        EarcutLinked(outerNode, indices, 0);
        if (indices.Count < 3)
        {
            return false;
        }

        // Guard: the triangles must tile the polygon-with-holes area.
        var expected = System.Math.Abs(RingArea(points, 0, outer.Count));
        foreach (var (start, end) in holeRanges)
        {
            expected -= System.Math.Abs(RingArea(points, start, end));
        }

        var actual = 0d;
        for (var i = 0; i + 2 < indices.Count; i += 3)
        {
            var a = points[indices[i]];
            var b = points[indices[i + 1]];
            var c = points[indices[i + 2]];
            actual += System.Math.Abs(((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X))) * 0.5d;
        }

        return System.Math.Abs(actual - expected) <= System.Math.Max(1e-9d, expected * 1e-6d);
    }

    private sealed class Node(int index, double x, double y)
    {
        public int Index { get; } = index;

        public double X { get; } = x;

        public double Y { get; } = y;

        public Node Prev { get; set; } = null!;

        public Node Next { get; set; } = null!;

        public bool Steiner { get; set; }
    }

    private static double RingArea(List<(double X, double Y)> pts, int start, int end)
    {
        var sum = 0d;
        for (int i = start, j = end - 1; i < end; j = i++)
        {
            sum += (pts[j].X - pts[i].X) * (pts[i].Y + pts[j].Y);
        }

        return sum * 0.5d;
    }

    private static Node? CreateRing(List<(double X, double Y)> pts, int start, int end, bool clockwise)
    {
        Node? last = null;
        if (clockwise == (SignedArea(pts, start, end) > 0d))
        {
            for (var i = start; i < end; i++)
            {
                last = InsertNode(i, pts[i].X, pts[i].Y, last);
            }
        }
        else
        {
            for (var i = end - 1; i >= start; i--)
            {
                last = InsertNode(i, pts[i].X, pts[i].Y, last);
            }
        }

        if (last is not null && Same(last, last.Next))
        {
            RemoveNode(last);
            last = last.Next;
        }

        return last;
    }

    private static double SignedArea(List<(double X, double Y)> pts, int start, int end)
    {
        var sum = 0d;
        for (int i = start, j = end - 1; i < end; j = i++)
        {
            sum += (pts[j].X - pts[i].X) * (pts[i].Y + pts[j].Y);
        }

        return sum;
    }

    private static Node? FilterPoints(Node? start, Node? end = null)
    {
        if (start is null)
        {
            return null;
        }

        end ??= start;
        var p = start;
        bool again;
        do
        {
            again = false;
            if (!p.Steiner && (Same(p, p.Next) || System.Math.Abs(Area(p.Prev, p, p.Next)) <= 1e-14d))
            {
                RemoveNode(p);
                p = end = p.Prev;
                if (p == p.Next)
                {
                    break;
                }

                again = true;
            }
            else
            {
                p = p.Next;
            }
        }
        while (again || p != end);

        return end;
    }

    private static void EarcutLinked(Node? ear, List<int> triangles, int pass)
    {
        if (ear is null)
        {
            return;
        }

        var stop = ear;
        while (ear.Prev != ear.Next)
        {
            var prev = ear.Prev;
            var next = ear.Next;
            if (IsEar(ear))
            {
                triangles.Add(prev.Index);
                triangles.Add(ear.Index);
                triangles.Add(next.Index);
                RemoveNode(ear);
                ear = next.Next;
                stop = next.Next;
                continue;
            }

            ear = next;
            if (ear == stop)
            {
                if (pass == 0)
                {
                    EarcutLinked(FilterPoints(ear), triangles, 1);
                }
                else if (pass == 1)
                {
                    var cured = CureLocalIntersections(FilterPoints(ear)!, triangles);
                    EarcutLinked(cured, triangles, 2);
                }
                else if (pass == 2)
                {
                    SplitEarcut(ear, triangles);
                }

                break;
            }
        }
    }

    private static bool IsEar(Node ear)
    {
        var a = ear.Prev;
        var b = ear;
        var c = ear.Next;
        if (Area(a, b, c) >= 0d)
        {
            return false;
        }

        var p = c.Next;
        while (p != a)
        {
            if (!SamePosition(p, a) && !SamePosition(p, b) && !SamePosition(p, c)
                && PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y)
                && Area(p.Prev, p, p.Next) >= 0d)
            {
                return false;
            }

            p = p.Next;
        }

        return true;
    }

    private static Node? CureLocalIntersections(Node start, List<int> triangles)
    {
        var p = start;
        do
        {
            var a = p.Prev;
            var b = p.Next.Next;
            if (!Same(a, b) && Intersects(a, p, p.Next, b) && LocallyInside(a, b) && LocallyInside(b, a))
            {
                triangles.Add(a.Index);
                triangles.Add(p.Index);
                triangles.Add(b.Index);
                RemoveNode(p);
                RemoveNode(p.Next);
                p = start = b;
            }

            p = p.Next;
        }
        while (p != start);

        return FilterPoints(p);
    }

    private static void SplitEarcut(Node start, List<int> triangles)
    {
        var a = start;
        do
        {
            var b = a.Next.Next;
            while (b != a.Prev)
            {
                if (a.Index != b.Index && IsValidDiagonal(a, b))
                {
                    var c = SplitPolygon(a, b);
                    a = FilterPoints(a, a.Next)!;
                    c = FilterPoints(c, c.Next)!;
                    EarcutLinked(a, triangles, 0);
                    EarcutLinked(c, triangles, 0);
                    return;
                }

                b = b.Next;
            }

            a = a.Next;
        }
        while (a != start);
    }

    private static Node? EliminateHoles(List<(double X, double Y)> pts, List<(int Start, int End)> holeRanges, Node outerNode)
    {
        var queue = new List<Node>(holeRanges.Count);
        foreach (var (start, end) in holeRanges)
        {
            var list = CreateRing(pts, start, end, clockwise: false);
            if (list is null)
            {
                continue;
            }

            if (list == list.Next)
            {
                list.Steiner = true;
            }

            queue.Add(GetLeftmost(list));
        }

        queue.Sort(static (x, y) => x.X != y.X ? x.X.CompareTo(y.X) : x.Y.CompareTo(y.Y));
        Node? current = outerNode;
        foreach (var hole in queue)
        {
            current = EliminateHole(hole, current!);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static Node? EliminateHole(Node hole, Node outerNode)
    {
        var bridge = FindHoleBridge(hole, outerNode);
        if (bridge is null)
        {
            return null;
        }

        var bridgeReverse = SplitPolygon(bridge, hole);
        FilterPoints(bridgeReverse, bridgeReverse.Next);
        return FilterPoints(bridge, bridge.Next);
    }

    private static Node? FindHoleBridge(Node hole, Node outerNode)
    {
        var p = outerNode;
        var hx = hole.X;
        var hy = hole.Y;
        var qx = double.NegativeInfinity;
        Node? m = null;
        do
        {
            if (hy <= p.Y && hy >= p.Next.Y && p.Next.Y != p.Y)
            {
                var x = p.X + ((hy - p.Y) * (p.Next.X - p.X) / (p.Next.Y - p.Y));
                if (x <= hx && x > qx)
                {
                    qx = x;
                    m = p.X < p.Next.X ? p : p.Next;
                    if (x == hx)
                    {
                        return m;
                    }
                }
            }

            p = p.Next;
        }
        while (p != outerNode);

        if (m is null)
        {
            return null;
        }

        var stop = m;
        var mx = m.X;
        var my = m.Y;
        var tanMin = double.PositiveInfinity;
        p = m;
        do
        {
            if (hx >= p.X && p.X >= mx && hx != p.X
                && PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.X, p.Y))
            {
                var tan = System.Math.Abs(hy - p.Y) / (hx - p.X);
                if (LocallyInside(p, hole)
                    && (tan < tanMin || (tan == tanMin && (p.X > m.X || (p.X == m.X && SectorContainsSector(m, p))))))
                {
                    m = p;
                    tanMin = tan;
                }
            }

            p = p.Next;
        }
        while (p != stop);

        return m;
    }

    private static bool SectorContainsSector(Node m, Node p)
        => Area(m.Prev, m, p.Prev) < 0d && Area(p.Next, m, m.Next) < 0d;

    private static Node GetLeftmost(Node start)
    {
        var p = start;
        var leftmost = start;
        do
        {
            if (p.X < leftmost.X || (p.X == leftmost.X && p.Y < leftmost.Y))
            {
                leftmost = p;
            }

            p = p.Next;
        }
        while (p != start);

        return leftmost;
    }

    private static bool PointInTriangle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py)
        => ((cx - px) * (ay - py)) >= ((ax - px) * (cy - py))
            && ((ax - px) * (by - py)) >= ((bx - px) * (ay - py))
            && ((bx - px) * (cy - py)) >= ((cx - px) * (by - py));

    private static bool IsValidDiagonal(Node a, Node b)
        => a.Next.Index != b.Index && a.Prev.Index != b.Index && !IntersectsPolygon(a, b)
            && ((LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b)
                    && (Area(a.Prev, a, b.Prev) != 0d || Area(a, b.Prev, b) != 0d))
                || (Same(a, b) && Area(a.Prev, a, a.Next) > 0d && Area(b.Prev, b, b.Next) > 0d));

    private static double Area(Node p, Node q, Node r)
        => ((q.Y - p.Y) * (r.X - q.X)) - ((q.X - p.X) * (r.Y - q.Y));

    private static bool Same(Node p1, Node p2) => p1.X == p2.X && p1.Y == p2.Y;

    private static bool SamePosition(Node p1, Node p2) => p1.X == p2.X && p1.Y == p2.Y;

    private static bool Intersects(Node p1, Node q1, Node p2, Node q2)
    {
        var o1 = System.Math.Sign(Area(p1, q1, p2));
        var o2 = System.Math.Sign(Area(p1, q1, q2));
        var o3 = System.Math.Sign(Area(p2, q2, p1));
        var o4 = System.Math.Sign(Area(p2, q2, q1));
        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        if (o1 == 0 && OnSegment(p1, p2, q1))
        {
            return true;
        }

        if (o2 == 0 && OnSegment(p1, q2, q1))
        {
            return true;
        }

        if (o3 == 0 && OnSegment(p2, p1, q2))
        {
            return true;
        }

        return o4 == 0 && OnSegment(p2, q1, q2);
    }

    private static bool OnSegment(Node p, Node q, Node r)
        => q.X <= System.Math.Max(p.X, r.X) && q.X >= System.Math.Min(p.X, r.X)
            && q.Y <= System.Math.Max(p.Y, r.Y) && q.Y >= System.Math.Min(p.Y, r.Y);

    private static bool IntersectsPolygon(Node a, Node b)
    {
        var p = a;
        do
        {
            if (p.Index != a.Index && p.Next.Index != a.Index && p.Index != b.Index && p.Next.Index != b.Index
                && Intersects(p, p.Next, a, b))
            {
                return true;
            }

            p = p.Next;
        }
        while (p != a);

        return false;
    }

    private static bool LocallyInside(Node a, Node b)
        => Area(a.Prev, a, a.Next) < 0d
            ? Area(a, b, a.Next) >= 0d && Area(a, a.Prev, b) >= 0d
            : Area(a, b, a.Prev) < 0d || Area(a, a.Next, b) < 0d;

    private static bool MiddleInside(Node a, Node b)
    {
        var p = a;
        var inside = false;
        var px = (a.X + b.X) / 2d;
        var py = (a.Y + b.Y) / 2d;
        do
        {
            if (((p.Y > py) != (p.Next.Y > py)) && p.Next.Y != p.Y
                && (px < (((p.Next.X - p.X) * (py - p.Y)) / (p.Next.Y - p.Y)) + p.X))
            {
                inside = !inside;
            }

            p = p.Next;
        }
        while (p != a);

        return inside;
    }

    private static Node SplitPolygon(Node a, Node b)
    {
        var a2 = new Node(a.Index, a.X, a.Y);
        var b2 = new Node(b.Index, b.X, b.Y);
        var an = a.Next;
        var bp = b.Prev;
        a.Next = b;
        b.Prev = a;
        a2.Next = an;
        an.Prev = a2;
        b2.Next = a2;
        a2.Prev = b2;
        bp.Next = b2;
        b2.Prev = bp;
        return b2;
    }

    private static Node InsertNode(int index, double x, double y, Node? last)
    {
        var p = new Node(index, x, y);
        if (last is null)
        {
            p.Prev = p;
            p.Next = p;
        }
        else
        {
            p.Next = last.Next;
            p.Prev = last;
            last.Next.Prev = p;
            last.Next = p;
        }

        return p;
    }

    private static void RemoveNode(Node p)
    {
        p.Next.Prev = p.Prev;
        p.Prev.Next = p.Next;
    }
}
