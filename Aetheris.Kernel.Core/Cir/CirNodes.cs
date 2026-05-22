using System.Linq;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Cir;

public sealed record CirBoxNode(double Width, double Height, double Depth) : CirNode(CirNodeKind.Box)
{
    public override CirBounds Bounds => new(new Point3D(-Width * 0.5d, -Height * 0.5d, -Depth * 0.5d), new Point3D(Width * 0.5d, Height * 0.5d, Depth * 0.5d));

    public override double Evaluate(Point3D point)
    {
        var hx = Width * 0.5d;
        var hy = Height * 0.5d;
        var hz = Depth * 0.5d;
        var dx = double.Abs(point.X) - hx;
        var dy = double.Abs(point.Y) - hy;
        var dz = double.Abs(point.Z) - hz;
        var outsideX = double.Max(dx, 0d);
        var outsideY = double.Max(dy, 0d);
        var outsideZ = double.Max(dz, 0d);
        var outside = double.Sqrt((outsideX * outsideX) + (outsideY * outsideY) + (outsideZ * outsideZ));
        var inside = double.Min(double.Max(dx, double.Max(dy, dz)), 0d);
        return outside + inside;
    }
}

public sealed record CirCylinderNode(double Radius, double Height) : CirNode(CirNodeKind.Cylinder)
{
    public override CirBounds Bounds => new(new Point3D(-Radius, -Radius, -Height * 0.5d), new Point3D(Radius, Radius, Height * 0.5d));

    public override double Evaluate(Point3D point)
    {
        var radial = double.Sqrt((point.X * point.X) + (point.Y * point.Y));
        var dr = radial - Radius;
        var dz = double.Abs(point.Z) - (Height * 0.5d);
        var outsideR = double.Max(dr, 0d);
        var outsideZ = double.Max(dz, 0d);
        var outside = double.Sqrt((outsideR * outsideR) + (outsideZ * outsideZ));
        var inside = double.Min(double.Max(dr, dz), 0d);
        return outside + inside;
    }
}



public sealed record CirConeNode : CirNode
{
    public CirConeNode(double bottomRadius, double topRadius, double height) : base(CirNodeKind.Cone)
    {
        if (height <= 0d || double.IsNaN(height) || double.IsInfinity(height))
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Cone height must be finite and > 0.");
        }

        if (bottomRadius < 0d || double.IsNaN(bottomRadius) || double.IsInfinity(bottomRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(bottomRadius), "Cone bottom radius must be finite and >= 0.");
        }

        if (topRadius < 0d || double.IsNaN(topRadius) || double.IsInfinity(topRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(topRadius), "Cone top radius must be finite and >= 0.");
        }

        if (bottomRadius <= 0d && topRadius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(topRadius), "At least one cone radius must be > 0.");
        }

        BottomRadius = bottomRadius;
        TopRadius = topRadius;
        Height = height;
    }

    public double BottomRadius { get; }
    public double TopRadius { get; }
    public double Height { get; }

    public override CirBounds Bounds
    {
        get
        {
            var r = double.Max(BottomRadius, TopRadius);
            var hz = Height * 0.5d;
            return new CirBounds(new Point3D(-r, -r, -hz), new Point3D(r, r, hz));
        }
    }

    public override double Evaluate(Point3D point) => EvaluateFiniteCone(point, BottomRadius, TopRadius, Height);

    internal static double EvaluateFiniteCone(Point3D point, double bottomRadius, double topRadius, double height)
    {
        var hz = height * 0.5d;
        var qx = double.Sqrt((point.X * point.X) + (point.Y * point.Y));
        var qy = point.Z;

        var k1x = topRadius;
        var k1y = hz;
        var k2x = topRadius - bottomRadius;
        var k2y = height;

        var caX = qx - double.Min(qx, qy < 0d ? bottomRadius : topRadius);
        var caY = double.Abs(qy) - hz;

        var dotNumerator = ((k1x - qx) * k2x) + ((k1y - qy) * k2y);
        var dotDenominator = (k2x * k2x) + (k2y * k2y);
        var h = dotDenominator <= 1e-18d ? 0d : Clamp(dotNumerator / dotDenominator, 0d, 1d);

        var cbX = qx - k1x + (k2x * h);
        var cbY = qy - k1y + (k2y * h);

        var s = (cbX < 0d && caY < 0d) ? -1d : 1d;
        return s * double.Sqrt(double.Min((caX * caX) + (caY * caY), (cbX * cbX) + (cbY * cbY)));
    }

    private static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
public sealed record CirSphereNode(double Radius) : CirNode(CirNodeKind.Sphere)
{
    public override CirBounds Bounds => new(new Point3D(-Radius, -Radius, -Radius), new Point3D(Radius, Radius, Radius));

    public override double Evaluate(Point3D point) => double.Sqrt((point.X * point.X) + (point.Y * point.Y) + (point.Z * point.Z)) - Radius;
}

public sealed record CirTorusNode(double MajorRadius, double MinorRadius) : CirNode(CirNodeKind.Torus)
{
    public override CirBounds Bounds => new(
        new Point3D(-(MajorRadius + MinorRadius), -(MajorRadius + MinorRadius), -MinorRadius),
        new Point3D(MajorRadius + MinorRadius, MajorRadius + MinorRadius, MinorRadius));

    public override double Evaluate(Point3D point)
    {
        var qx = double.Sqrt((point.X * point.X) + (point.Y * point.Y)) - MajorRadius;
        return double.Sqrt((qx * qx) + (point.Z * point.Z)) - MinorRadius;
    }
}

public sealed record CirUnionNode(CirNode Left, CirNode Right) : CirNode(CirNodeKind.Union)
{
    public override CirBounds Bounds => CirBounds.Union(Left.Bounds, Right.Bounds);
    public override double Evaluate(Point3D point) => double.Min(Left.Evaluate(point), Right.Evaluate(point));
}

public sealed record CirSubtractNode(CirNode Left, CirNode Right) : CirNode(CirNodeKind.Subtract)
{
    public override CirBounds Bounds => Left.Bounds;
    public override double Evaluate(Point3D point) => double.Max(Left.Evaluate(point), -Right.Evaluate(point));
}

public sealed record CirIntersectNode(CirNode Left, CirNode Right) : CirNode(CirNodeKind.Intersect)
{
    public override CirBounds Bounds => CirBounds.Union(Left.Bounds, Right.Bounds);
    public override double Evaluate(Point3D point) => double.Max(Left.Evaluate(point), Right.Evaluate(point));
}

public sealed record CirTransformNode(CirNode Child, Transform3D Transform) : CirNode(CirNodeKind.Transform)
{
    public override CirBounds Bounds
    {
        get
        {
            var b = Child.Bounds;
            var corners = new[]
            {
                new Point3D(b.Min.X, b.Min.Y, b.Min.Z),
                new Point3D(b.Min.X, b.Min.Y, b.Max.Z),
                new Point3D(b.Min.X, b.Max.Y, b.Min.Z),
                new Point3D(b.Min.X, b.Max.Y, b.Max.Z),
                new Point3D(b.Max.X, b.Min.Y, b.Min.Z),
                new Point3D(b.Max.X, b.Min.Y, b.Max.Z),
                new Point3D(b.Max.X, b.Max.Y, b.Min.Z),
                new Point3D(b.Max.X, b.Max.Y, b.Max.Z),
            };

            var transformed = corners.Select(TransformPoint).ToArray();
            return new CirBounds(
                new Point3D(transformed.Min(p => p.X), transformed.Min(p => p.Y), transformed.Min(p => p.Z)),
                new Point3D(transformed.Max(p => p.X), transformed.Max(p => p.Y), transformed.Max(p => p.Z)));
        }
    }

    public override double Evaluate(Point3D point)
    {
        var inverse = Transform.Inverse();
        return Child.Evaluate(inverse.Apply(point));
    }

    private Point3D TransformPoint(Point3D p) => Transform.Apply(p);
}

public static class CirVolumeEstimator
{
    public static double EstimateVolume(CirNode node, int resolution)
    {
        var bounds = node.Bounds;
        var dx = bounds.SizeX / resolution;
        var dy = bounds.SizeY / resolution;
        var dz = bounds.SizeZ / resolution;
        var cellVolume = dx * dy * dz;
        var insideCount = 0;

        for (var ix = 0; ix < resolution; ix++)
        for (var iy = 0; iy < resolution; iy++)
        for (var iz = 0; iz < resolution; iz++)
        {
            var p = new Point3D(bounds.Min.X + ((ix + 0.5d) * dx), bounds.Min.Y + ((iy + 0.5d) * dy), bounds.Min.Z + ((iz + 0.5d) * dz));
            if (node.Evaluate(p) <= 0d)
            {
                insideCount++;
            }
        }

        return insideCount * cellVolume;
    }
}
