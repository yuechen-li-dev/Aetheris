using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Boundaries;

public sealed record BoundaryMapCellEstimate(
    double OccupancyFraction,
    double BoundaryArea,
    Vector3D BoundaryFirstMoment,
    Vector3D AreaWeightedNormal);

/// <summary>
/// Integrates a Z-extruded cell against a piecewise-linear section of a sampled offset graph.
/// This is a distinct map-assisted geometry estimator, not point-count coverage or solver quadrature.
/// </summary>
public static class BoundaryOffsetMapIntegrator
{
    private readonly record struct Point2(double U, double W);

    public static BoundaryMapCellEstimate IntegrateColumn(IBoundaryOffsetMap map, BoundingBox3D bounds)
    {
        if (double.Abs(map.LocalFrame.TangentV.X) > 1e-12d
            || double.Abs(map.LocalFrame.TangentV.Y) > 1e-12d
            || double.Abs(double.Abs(map.LocalFrame.TangentV.Z) - 1d) > 1e-12d)
        {
            throw new NotSupportedException("M1 map integration supports boundaries extruded along lattice Z.");
        }

        var frame = map.LocalFrame;
        var polygon = new List<Point2>
        {
            Transform(bounds.Min.X, bounds.Min.Y, frame),
            Transform(bounds.Max.X, bounds.Min.Y, frame),
            Transform(bounds.Max.X, bounds.Max.Y, frame),
            Transform(bounds.Min.X, bounds.Max.Y, frame),
        };
        var uNodes = map.Samples.Select(sample => sample.U).Distinct().Order().ToArray();
        var v = (map.Domain.MinimumV + map.Domain.MaximumV) * 0.5d;
        var offsets = uNodes.Select(u => map.Evaluate(u, v).Offset).ToArray();
        var occupiedArea = 0d;
        var boundaryLength = 0d;
        var moment = Vector3D.Zero;
        var normalIntegral = Vector3D.Zero;
        var height = bounds.Max.Z - bounds.Min.Z;

        for (var i = 0; i < uNodes.Length - 1; i++)
        {
            var u0 = uNodes[i];
            var u1 = uNodes[i + 1];
            var h0 = offsets[i];
            var h1 = offsets[i + 1];
            var slope = (h1 - h0) / (u1 - u0);
            var intercept = h0 - (slope * u0);
            var clipped = Clip(polygon, point => point.U - u0);
            clipped = Clip(clipped, point => u1 - point.U);
            clipped = Clip(clipped, point => point.W - ((slope * point.U) + intercept));
            occupiedArea += Area(clipped);

            foreach (var interval in SegmentIntervalsInsideCell(u0, h0, u1, h1, frame, bounds))
            {
                var du = u1 - u0;
                var dh = h1 - h0;
                var fullLength = double.Sqrt((du * du) + (dh * dh));
                var length = fullLength * (interval.End - interval.Start);
                var t = (interval.Start + interval.End) * 0.5d;
                var u = u0 + (du * t);
                var w = h0 + (dh * t);
                var localPoint = frame.Origin + (frame.TangentU * u) + (frame.Normal * w);
                var midpoint = new Point3D(localPoint.X, localPoint.Y, (bounds.Min.Z + bounds.Max.Z) * 0.5d);
                var area = length * height;
                boundaryLength += length;
                moment += new Vector3D(midpoint.X * area, midpoint.Y * area, midpoint.Z * area);
                normalIntegral += map.Evaluate(u, v).Normal * area;
            }
        }

        var cellArea = (bounds.Max.X - bounds.Min.X) * (bounds.Max.Y - bounds.Min.Y);
        var boundaryArea = boundaryLength * height;
        return new BoundaryMapCellEstimate(
            double.Clamp(occupiedArea / cellArea, 0d, 1d),
            boundaryArea,
            moment,
            normalIntegral);
    }

    private static Point2 Transform(double x, double y, BoundaryLocalFrame frame)
    {
        var delta = new Point3D(x, y, frame.Origin.Z) - frame.Origin;
        return new Point2(delta.Dot(frame.TangentU), delta.Dot(frame.Normal));
    }

    private static List<Point2> Clip(IReadOnlyList<Point2> input, Func<Point2, double> signedDistance)
    {
        if (input.Count == 0)
        {
            return [];
        }

        const double epsilon = 1e-14d;
        var output = new List<Point2>();
        var previous = input[^1];
        var previousDistance = signedDistance(previous);
        foreach (var current in input)
        {
            var currentDistance = signedDistance(current);
            var previousInside = previousDistance >= -epsilon;
            var currentInside = currentDistance >= -epsilon;
            if (previousInside != currentInside)
            {
                var t = previousDistance / (previousDistance - currentDistance);
                output.Add(new Point2(
                    previous.U + ((current.U - previous.U) * t),
                    previous.W + ((current.W - previous.W) * t)));
            }

            if (currentInside)
            {
                output.Add(current);
            }

            previous = current;
            previousDistance = currentDistance;
        }

        return output;
    }

    private static double Area(IReadOnlyList<Point2> polygon)
    {
        if (polygon.Count < 3)
        {
            return 0d;
        }

        var twice = 0d;
        for (var i = 0; i < polygon.Count; i++)
        {
            var next = polygon[(i + 1) % polygon.Count];
            twice += (polygon[i].U * next.W) - (next.U * polygon[i].W);
        }

        return double.Abs(twice) * 0.5d;
    }

    private static IReadOnlyList<(double Start, double End)> SegmentIntervalsInsideCell(
        double u0,
        double w0,
        double u1,
        double w1,
        BoundaryLocalFrame frame,
        BoundingBox3D bounds)
    {
        var p0 = frame.Origin + (frame.TangentU * u0) + (frame.Normal * w0);
        var p1 = frame.Origin + (frame.TangentU * u1) + (frame.Normal * w1);
        var values = new List<double> { 0d, 1d };
        AddCrossing(values, p0.X, p1.X, bounds.Min.X);
        AddCrossing(values, p0.X, p1.X, bounds.Max.X);
        AddCrossing(values, p0.Y, p1.Y, bounds.Min.Y);
        AddCrossing(values, p0.Y, p1.Y, bounds.Max.Y);
        values.Sort();
        var unique = values.DistinctBy(value => double.Round(value, 14)).ToArray();
        var intervals = new List<(double, double)>();
        for (var i = 0; i < unique.Length - 1; i++)
        {
            var mid = (unique[i] + unique[i + 1]) * 0.5d;
            var x = p0.X + ((p1.X - p0.X) * mid);
            var y = p0.Y + ((p1.Y - p0.Y) * mid);
            if (x >= bounds.Min.X - 1e-12d && x <= bounds.Max.X + 1e-12d
                && y >= bounds.Min.Y - 1e-12d && y <= bounds.Max.Y + 1e-12d)
            {
                intervals.Add((unique[i], unique[i + 1]));
            }
        }

        return intervals;
    }

    private static void AddCrossing(List<double> values, double a, double b, double boundary)
    {
        var denominator = b - a;
        if (double.Abs(denominator) <= 1e-15d)
        {
            return;
        }

        var t = (boundary - a) / denominator;
        if (t > 0d && t < 1d)
        {
            values.Add(t);
        }
    }
}
