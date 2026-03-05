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
