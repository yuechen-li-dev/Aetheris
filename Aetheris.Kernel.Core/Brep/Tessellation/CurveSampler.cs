using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal static class CurveSampler
{
    private const double AngularStepRadians = 10d * (double.Pi / 180d);
    private const int MinimumCircleSegments = 12;
    private const int MaximumCircleSegments = 360;

    public static IReadOnlyList<Point3D> SampleLine(Line3Curve line, ParameterInterval interval)
        => [line.Evaluate(interval.Start), line.Evaluate(interval.End)];

    public static IReadOnlyList<Point3D> SampleCircleArc(Circle3Curve circle, double startAngle, double delta)
    {
        var segmentCount = ResolveCircleSegmentCount(delta);
        var points = new List<Point3D>(segmentCount + 1);
        for (var i = 0; i <= segmentCount; i++)
        {
            var t = (double)i / segmentCount;
            points.Add(circle.Evaluate(startAngle + (delta * t)));
        }

        return points;
    }

    public static bool TrySampleTrimmedCircleArc(
        Circle3Curve circle,
        Point3D startPoint,
        Point3D endPoint,
        bool orientedEdgeSense,
        out IReadOnlyList<Point3D> points,
        out bool isClosed,
        out bool usedShorterArcFallback)
    {
        const double endpointTolerance = 1e-6d;
        const double fullCircleEpsilon = 1e-6d;

        usedShorterArcFallback = false;
        isClosed = false;

        var normal = circle.Normal.ToVector();
        var distinctEndpoints = (startPoint - endPoint).Length > endpointTolerance;

        var startProjected = ProjectPointToCirclePlane(circle.Center, normal, startPoint);
        var endProjected = ProjectPointToCirclePlane(circle.Center, normal, endPoint);

        var startRadius = (startProjected - circle.Center).Length;
        var endRadius = (endProjected - circle.Center).Length;
        if (double.Abs(startRadius - circle.Radius) > endpointTolerance || double.Abs(endRadius - circle.Radius) > endpointTolerance)
        {
            points = Array.Empty<Point3D>();
            return false;
        }

        var u = circle.XAxis.ToVector();
        var v = circle.YAxis.ToVector();

        var startOffset = startProjected - circle.Center;
        var endOffset = endProjected - circle.Center;
        var thetaStart = NormalizeToZeroTwoPi(double.Atan2(startOffset.Dot(v), startOffset.Dot(u)));
        var thetaEnd = NormalizeToZeroTwoPi(double.Atan2(endOffset.Dot(v), endOffset.Dot(u)));

        var deltaForward = NormalizeToZeroTwoPi(thetaEnd - thetaStart);
        var deltaBackward = NormalizeToZeroTwoPi(thetaStart - thetaEnd);

        double delta;
        if (!distinctEndpoints)
        {
            delta = 2d * double.Pi;
            isClosed = true;
        }
        else if (orientedEdgeSense)
        {
            delta = deltaForward;
        }
        else
        {
            delta = -deltaBackward;
        }

        if (distinctEndpoints && double.Abs(double.Abs(delta) - (2d * double.Pi)) <= fullCircleEpsilon)
        {
            var shorterForward = deltaForward <= deltaBackward;
            delta = shorterForward ? deltaForward : -deltaBackward;
            usedShorterArcFallback = true;
        }

        if (distinctEndpoints)
        {
            var minDelta = 1e-9d;
            var maxDelta = (2d * double.Pi) - 1e-9d;
            var sign = delta >= 0d ? 1d : -1d;
            delta = sign * System.Math.Clamp(double.Abs(delta), minDelta, maxDelta);
        }

        var sampled = SampleCircleArc(circle, thetaStart, delta).ToArray();
        sampled[0] = startProjected;
        sampled[^1] = endProjected;
        points = sampled;
        return true;
    }

    private static Point3D ProjectPointToCirclePlane(Point3D center, Vector3D normal, Point3D point)
    {
        var offset = point - center;
        var distance = offset.Dot(normal);
        return point - (normal * distance);
    }

    private static double NormalizeToZeroTwoPi(double angle)
    {
        var twoPi = 2d * double.Pi;
        var normalized = angle % twoPi;
        if (normalized < 0d)
        {
            normalized += twoPi;
        }

        return normalized;
    }

    private static int ResolveCircleSegmentCount(double delta)
    {
        var absoluteDelta = double.Abs(delta);
        if (absoluteDelta <= 1e-12d)
        {
            return MinimumCircleSegments;
        }

        var segments = (int)double.Ceiling(absoluteDelta / AngularStepRadians);
        return System.Math.Clamp(segments, MinimumCircleSegments, MaximumCircleSegments);
    }
}
