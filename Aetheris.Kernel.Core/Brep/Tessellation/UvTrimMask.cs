namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal sealed class UvTrimMask
{
    private readonly UvTrimLoop _outerLoop;
    private readonly UvTrimLoop[] _innerLoops;
    private readonly IReadOnlyList<IReadOnlyList<UvPoint>> _innerLoopVertices;

    internal UvTrimMask(IReadOnlyList<UvPoint> outerLoop, IReadOnlyList<IReadOnlyList<UvPoint>> innerLoops)
    {
        _outerLoop = new UvTrimLoop(outerLoop);
        _innerLoops = innerLoops.Select(loop => new UvTrimLoop(loop)).ToArray();
        _innerLoopVertices = _innerLoops.Select(loop => (IReadOnlyList<UvPoint>)loop.Vertices).ToArray();
    }

    internal IReadOnlyList<UvPoint> OuterLoop => _outerLoop.Vertices;

    internal IReadOnlyList<IReadOnlyList<UvPoint>> InnerLoops => _innerLoopVertices;

    internal bool Contains(UvPoint point)
    {
        if (!_outerLoop.Contains(point))
        {
            return false;
        }

        for (var i = 0; i < _innerLoops.Length; i++)
        {
            if (_innerLoops[i].Contains(point))
            {
                return false;
            }
        }

        return true;
    }

    internal bool KeepCellByCorners(UvPoint bottomLeft, UvPoint bottomRight, UvPoint topLeft, UvPoint topRight)
        => Contains(bottomLeft)
            && Contains(bottomRight)
            && Contains(topLeft)
            && Contains(topRight);
}

internal sealed class UvTrimLoop
{
    private const double SegmentTolerance = 1e-9d;

    private readonly UvPoint[] _vertices;

    internal UvTrimLoop(IReadOnlyList<UvPoint> vertices)
    {
        if (vertices is null)
        {
            throw new ArgumentNullException(nameof(vertices));
        }

        if (vertices.Count < 3)
        {
            throw new ArgumentException("UV trim loops require at least three vertices.", nameof(vertices));
        }

        _vertices = vertices.ToArray();
    }

    internal IReadOnlyList<UvPoint> Vertices => _vertices;

    internal bool Contains(UvPoint point)
    {
        var inside = false;
        for (var i = 0; i < _vertices.Length; i++)
        {
            var a = _vertices[i];
            var b = _vertices[(i + 1) % _vertices.Length];
            if (IsPointOnSegment(point, a, b))
            {
                return true;
            }

            var crosses = ((a.V > point.V) != (b.V > point.V))
                && (point.U < (((b.U - a.U) * (point.V - a.V)) / (b.V - a.V)) + a.U);
            if (crosses)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsPointOnSegment(UvPoint point, UvPoint a, UvPoint b)
    {
        var cross = ((point.U - a.U) * (b.V - a.V)) - ((point.V - a.V) * (b.U - a.U));
        if (double.Abs(cross) > SegmentTolerance)
        {
            return false;
        }

        var dot = ((point.U - a.U) * (b.U - a.U)) + ((point.V - a.V) * (b.V - a.V));
        if (dot < -SegmentTolerance)
        {
            return false;
        }

        var lengthSquared = ((b.U - a.U) * (b.U - a.U)) + ((b.V - a.V) * (b.V - a.V));
        return dot <= lengthSquared + SegmentTolerance;
    }
}
