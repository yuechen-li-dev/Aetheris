using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Regions.Analytic;

/// <summary>Independent fixture references. Approximation strategies do not call these estimators.</summary>
public static class AnalyticContinuumReferences
{
    public static double BlockWithCylindricalHoleVolume(BoundingBox3D bounds, double radius)
    {
        var block = (bounds.Max.X - bounds.Min.X) * (bounds.Max.Y - bounds.Min.Y) * (bounds.Max.Z - bounds.Min.Z);
        return block - (double.Pi * radius * radius * (bounds.Max.Z - bounds.Min.Z));
    }

    public static double CylindricalWallArea(double radius, double height) => 2d * double.Pi * radius * height;

    public static double CylinderLocalOffset(double radius, double tangentCoordinate) =>
        double.Sqrt((radius * radius) - (tangentCoordinate * tangentCoordinate)) - radius;

    public static Vector3D CylinderLocalMaterialSideNormal(
        double radius,
        double tangentCoordinate,
        Vector3D referenceNormal,
        Vector3D tangent)
    {
        var offset = CylinderLocalOffset(radius, tangentCoordinate);
        var candidate = (referenceNormal * ((radius + offset) / radius)) + (tangent * (tangentCoordinate / radius));
        if (!candidate.TryNormalize(out candidate))
        {
            throw new InvalidOperationException("Analytic cylinder reference produced a degenerate normal.");
        }

        return candidate;
    }

    public static double PlaneLocalOffset() => 0d;
    public static Vector3D PlaneMaterialSideNormal(Vector3D planeNormal) => -planeNormal;

    public static Point3D ProjectCylinder(Point3D point, Point3D axisPoint, double radius)
    {
        var radial = new Vector3D(point.X - axisPoint.X, point.Y - axisPoint.Y, 0d);
        if (!radial.TryNormalize(out radial))
        {
            throw new ArgumentException("Point on the cylinder axis has no unique projection.", nameof(point));
        }

        return new Point3D(axisPoint.X + (radial.X * radius), axisPoint.Y + (radial.Y * radius), point.Z);
    }

    public static Vector3D CylinderMaterialSideNormal(Point3D boundaryPoint, Point3D axisPoint)
    {
        var radial = new Vector3D(boundaryPoint.X - axisPoint.X, boundaryPoint.Y - axisPoint.Y, 0d);
        if (!radial.TryNormalize(out radial))
        {
            throw new ArgumentException("Cylinder normal is undefined on its axis.", nameof(boundaryPoint));
        }

        return radial;
    }

    public static double CylinderMaterialOccupancy(BoundingBox3D cell, Point3D center, double radius)
    {
        // Independent high-order reference integral of the circular void clipped by the XY cell.
        const int intervals = 2048;
        var dx = (cell.Max.X - cell.Min.X) / intervals;
        var sum = VoidHeight(cell.Min.X) + VoidHeight(cell.Max.X);
        for (var i = 1; i < intervals; i++)
        {
            sum += (i % 2 == 0 ? 2d : 4d) * VoidHeight(cell.Min.X + (i * dx));
        }

        var voidArea = sum * dx / 3d;
        var cellArea = (cell.Max.X - cell.Min.X) * (cell.Max.Y - cell.Min.Y);
        return double.Clamp(1d - (voidArea / cellArea), 0d, 1d);

        double VoidHeight(double x)
        {
            var relativeX = x - center.X;
            var square = (radius * radius) - (relativeX * relativeX);
            if (square <= 0d)
            {
                return 0d;
            }

            var half = double.Sqrt(square);
            var low = double.Max(cell.Min.Y, center.Y - half);
            var high = double.Min(cell.Max.Y, center.Y + half);
            return double.Max(0d, high - low);
        }
    }

    public static double CylinderArcLength(BoundingBox3D cell, Point3D center, double radius)
    {
        var angles = new List<double> { 0d, double.Pi * 0.5d, double.Pi, double.Pi * 1.5d, double.Pi * 2d };
        AddCos(cell.Min.X); AddCos(cell.Max.X); AddSin(cell.Min.Y); AddSin(cell.Max.Y);
        var ordered = angles.Where(angle => angle >= 0d && angle <= double.Pi * 2d)
            .DistinctBy(angle => double.Round(angle, 14)).Order().ToArray();
        var measure = 0d;
        for (var i = 0; i < ordered.Length - 1; i++)
        {
            var mid = (ordered[i] + ordered[i + 1]) * 0.5d;
            var x = center.X + (radius * double.Cos(mid));
            var y = center.Y + (radius * double.Sin(mid));
            if (x >= cell.Min.X - 1e-12d && x <= cell.Max.X + 1e-12d
                && y >= cell.Min.Y - 1e-12d && y <= cell.Max.Y + 1e-12d)
            {
                measure += ordered[i + 1] - ordered[i];
            }
        }

        return measure * radius;

        void AddCos(double x)
        {
            var value = (x - center.X) / radius;
            if (value is < -1d or > 1d) return;
            var a = double.Acos(value);
            angles.Add(a); angles.Add((2d * double.Pi) - a);
        }

        void AddSin(double y)
        {
            var value = (y - center.Y) / radius;
            if (value is < -1d or > 1d) return;
            var a = double.Asin(value);
            if (a < 0d) a += 2d * double.Pi;
            angles.Add(a); angles.Add(double.Pi - a < 0d ? (3d * double.Pi) - a : double.Pi - a);
        }
    }

    public static double ObliqueFixtureVolume() => 4d;
    public static double ObliqueFixtureBoundaryArea() => 4d * double.Sqrt(2d);
}
