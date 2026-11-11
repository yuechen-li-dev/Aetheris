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

    public static IReadOnlyList<Point3D> SampleCircleArc(Circle3Curve circle, double startAngle, double delta, DisplayTessellationOptions? options = null)
    {
        var segmentCount = ResolveCircleSegmentCount(delta, circle.Radius, options);
        var points = new List<Point3D>(segmentCount + 1);
        for (var i = 0; i <= segmentCount; i++)
        {
            var t = (double)i / segmentCount;
            points.Add(circle.Evaluate(startAngle + (delta * t)));
        }

        return points;
    }


    public static IReadOnlyList<Point3D> SampleBSpline(BSpline3Curve spline, ParameterInterval interval)
    {
        var minSegments = 8;
        var maxSegments = 128;
        var controlDriven = spline.ControlPoints.Count * 2;
        var segmentCount = System.Math.Clamp(controlDriven, minSegments, maxSegments);

        var points = new List<Point3D>(segmentCount + 1);
        var span = interval.End - interval.Start;
        for (var i = 0; i <= segmentCount; i++)
        {
            var t = (double)i / segmentCount;
            points.Add(spline.Evaluate(interval.Start + (span * t)));
        }

        return points;
    }

    /// <summary>The control-driven samples, refined until every chord is within the options' chord tolerance of the
    /// curve at its midpoint and turns by no more than the angular tolerance.</summary>
    public static IReadOnlyList<Point3D> SampleBSpline(BSpline3Curve spline, ParameterInterval interval, DisplayTessellationOptions options)
    {
        var coarse = SampleBSpline(spline, interval);
        var span = interval.End - interval.Start;
        var result = new List<Point3D> { coarse[0] };
        var budget = options.MaximumSegments * 4;
        for (var i = 0; i + 1 < coarse.Count; i++)
        {
            var t0 = interval.Start + (span * i / (coarse.Count - 1));
            var t1 = interval.Start + (span * (i + 1) / (coarse.Count - 1));
            Refine(t0, coarse[i], t1, coarse[i + 1], 0);
        }
        return result;

        void Refine(double ta, Point3D a, double tb, Point3D b, int depth)
        {
            var tm = (ta + tb) / 2d;
            var m = spline.Evaluate(tm);
            var chord = b - a;
            var sag = chord.Length <= 1e-12 ? (m - a).Length : (m - a).Cross(chord).Length / chord.Length;
            var turn = double.Acos(double.Clamp((m - a).Dot(b - m) / double.Max(1e-300, (m - a).Length * (b - m).Length), -1d, 1d));
            if (depth < 10 && result.Count < budget && (sag > options.ChordTolerance || turn > options.AngularToleranceRadians))
            {
                Refine(ta, a, tm, m, depth + 1);
                Refine(tm, m, tb, b, depth + 1);
                return;
            }
            result.Add(b);
        }
    }

    public static bool TrySampleTrimmedCircleArc(
        Circle3Curve circle,
        Point3D startPoint,
        Point3D endPoint,
        bool orientedEdgeSense,
        out IReadOnlyList<Point3D> points,
        out bool isClosed,
        out bool usedShorterArcFallback,
        DisplayTessellationOptions? options = null)
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

        var sampled = SampleCircleArc(circle, thetaStart, delta, options).ToArray();
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

    private static int ResolveCircleSegmentCount(double delta, double radius, DisplayTessellationOptions? options)
    {
        var absoluteDelta = double.Abs(delta);
        if (absoluteDelta <= 1e-12d)
        {
            return MinimumCircleSegments;
        }

        if (options is null)
        {
            var displaySegments = (int)double.Ceiling(absoluteDelta / AngularStepRadians);
            return System.Math.Clamp(displaySegments, MinimumCircleSegments, MaximumCircleSegments);
        }

        var angular = (int)double.Ceiling(absoluteDelta / options.AngularToleranceRadians);
        var chord = 0;
        if (radius > options.ChordTolerance)
        {
            var halfStep = double.Acos(System.Math.Clamp(1d - (options.ChordTolerance / radius), -1d, 1d));
            chord = halfStep > 0d ? (int)double.Ceiling(absoluteDelta / (2d * halfStep)) : 0;
        }

        return System.Math.Clamp(System.Math.Max(options.MinimumSegments, System.Math.Max(angular, chord)), options.MinimumSegments, options.MaximumSegments);
    }
}
