using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>
/// Native exact ellipsoid octant. Longitude is [0, pi/2], latitude is [0, pi/2]
/// or [-pi/2, 0]. STEP projects this quadric to an exact rational patch; the
/// rational net is an interchange representation, never the native authority.
/// </summary>
public sealed record EllipsoidSurface(Point3D Center, Direction3D Axis, Direction3D XAxis,
    double RadiusX, double RadiusY, double RadiusZ, bool Northern)
{
    public Direction3D YAxis => Direction3D.Create(Axis.ToVector().Cross(XAxis.ToVector()));
    public Point3D Evaluate(double u, double v) => Center
        + XAxis.ToVector() * (RadiusX * double.Cos(u) * double.Cos(v))
        + YAxis.ToVector() * (RadiusY * double.Sin(u) * double.Cos(v))
        + Axis.ToVector() * (RadiusZ * double.Sin(v));
    public Vector3D UnitCoordinates(Point3D point)
    {
        var p = point - Center;
        return new(p.Dot(XAxis.ToVector()) / RadiusX, p.Dot(YAxis.ToVector()) / RadiusY, p.Dot(Axis.ToVector()) / RadiusZ);
    }
    public Point3D FromUnit(Point3D p) => Center + XAxis.ToVector() * (p.X * RadiusX)
        + YAxis.ToVector() * (p.Y * RadiusY) + Axis.ToVector() * (p.Z * RadiusZ);
    public Vector3D Normal(Point3D p)
    {
        var q = UnitCoordinates(p);
        var gradient = new Vector3D(q.X / RadiusX, q.Y / RadiusY, q.Z / RadiusZ);
        var scale = double.Max(double.Abs(gradient.X), double.Max(double.Abs(gradient.Y), double.Abs(gradient.Z)));
        return Direction3D.Create(XAxis.ToVector() * (gradient.X / scale)
            + YAxis.ToVector() * (gradient.Y / scale) + Axis.ToVector() * (gradient.Z / scale)).ToVector();
    }

    public BSplineSurfaceWithKnots ToStepPatch()
    {
        (double X, double Y)[] u = [(1, 0), (1, 1), (0, 1)];
        (double R, double Z)[] v = Northern ? [(1, 0), (1, 1), (0, 1)] : [(0, -1), (1, -1), (1, 0)];
        var net = u.Select(p => (IReadOnlyList<Point3D>)v.Select(q => FromUnit(new(p.X * q.R, p.Y * q.R, q.Z))).ToArray()).ToArray();
        var w = double.Sqrt(0.5);
        var weights = Enumerable.Range(0, 3).Select(j => (IReadOnlyList<double>)Enumerable.Range(0, 3).Select(k => (j == 1 ? w : 1) * (k == 1 ? w : 1)).ToArray()).ToArray();
        return new(2, 2, net, "UNSPECIFIED", false, false, false, [3, 3], [3, 3], [0, 1], [0, 1], "UNSPECIFIED", weights);
    }

    /// <summary>Recognize only the exact canonical octant control net and weights, not a nearby freeform fit.</summary>
    public static bool TryRecover(BSplineSurfaceWithKnots s, out EllipsoidSurface surface)
    {
        surface = null!;
        if (s.DegreeU != 2 || s.DegreeV != 2 || s.ControlPoints.Count != 3 || s.ControlPoints[0].Count != 3
            || s.Weights is null || !s.KnotMultiplicitiesU.SequenceEqual([3, 3]) || !s.KnotMultiplicitiesV.SequenceEqual([3, 3])
            || !s.KnotValuesU.SequenceEqual([0d, 1d]) || !s.KnotValuesV.SequenceEqual([0d, 1d]) || s.UClosed || s.VClosed || s.SelfIntersect) return false;
        var p = s.ControlPoints;
        var scale = (p[0][0] - p[2][2]).Length; var tolerance = double.Max(1e-10, scale * 1e-11);
        var north = (p[0][2] - p[2][2]).Length <= tolerance;
        var south = (p[0][0] - p[2][0]).Length <= tolerance;
        if (north == south) return false;
        var equator = north ? 0 : 2; var pole = north ? p[0][2] : p[0][0];
        var center = p[0][equator] + (p[2][equator] - p[1][equator]);
        var a = p[0][equator] - center; var b = p[2][equator] - center; var c = pole - center;
        if (new[] { a.Length, b.Length, c.Length }.Any(r => !double.IsFinite(r) || r < 1e-7)) return false;
        var x = Direction3D.Create(a); var y = Direction3D.Create(b); var axis = Direction3D.Create(x.ToVector().Cross(y.ToVector()));
        if (double.Abs(x.ToVector().Dot(y.ToVector())) > 1e-11 || double.Abs(a.Dot(c) / (a.Length * c.Length)) > 1e-11
            || double.Abs(b.Dot(c) / (b.Length * c.Length)) > 1e-11 || (axis.ToVector().Dot(c) > 0) != north) return false;
        var candidate = new EllipsoidSurface(center, axis, x, a.Length, b.Length, c.Length, north);
        var expected = candidate.ToStepPatch();
        var weightScale = s.Weights[0][0];
        for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++)
            if ((p[i][j] - expected.ControlPoints[i][j]).Length > tolerance
                || double.Abs(s.Weights[i][j] / weightScale - expected.Weights![i][j]) > 1e-12) return false;
        surface = candidate; return true;
    }
}
