using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Geometry.Curves;

public enum CylinderIntersectionBranch { PositiveCutterAxis, NegativeCutterAxis }

public enum IntersectionCurveSense { Forward, Reverse }

public readonly record struct CylinderIntersectionUv(double U, double V);

/// <summary>
/// Exact intersection of perpendicular cylinders. The parameter runs around the cutter cross-section; the branch
/// selects the side of the host along the cutter axis. No BRep topology is implied.
/// <para>
/// The cutter's axis crosses the host's centreline (<see cref="Create"/>), or passes it at an offset e along their
/// common perpendicular (<see cref="CreateOffset"/>). In cutter-section coordinates y = e + r·cos t, z = r·sin t,
/// the point along the cutter axis is x = ±√(R² − y²). The cutter stays strictly inside the host's silhouette
/// (|e| + r &lt; R), or touches it from inside at one point (|e| + r = R: a fillet whose wall is tangent to the
/// host). There R² − y² has a double root, R ∓ y = 2r·sin²(t/2) or 2r·cos²(t/2), and the evaluator takes its
/// square root exactly, so the curve is analytic through the touching point.
/// </para>
/// </summary>
public sealed class CylinderCylinderIntersectionCurve
{
    private const double MachineEpsilon = 2.2204460492503131e-16d;
    private readonly Vector3D _x;
    private readonly Vector3D _y;
    private readonly Vector3D _z;
    private readonly double _hostAxialOffset;
    private readonly double _toolAxialOffset;
    private readonly double _hostUAtStart;
    private readonly double _toolUAtStart;
    private readonly double _offset;
    private readonly Touching _touching;

    private enum Touching { None, AtPositiveY, AtNegativeY }

    private CylinderCylinderIntersectionCurve(
        CylinderSurface host, CylinderSurface tool, Point3D center,
        CylinderIntersectionBranch branch, ParameterInterval domain,
        IntersectionCurveSense sense, double offset = 0d, Touching touching = Touching.None)
    {
        _offset = offset;
        _touching = touching;
        Host = host;
        Tool = tool;
        Center = center;
        Branch = branch;
        Domain = domain;
        Sense = sense;
        _x = tool.Axis.ToVector();
        _z = host.Axis.ToVector();
        _y = _z.Cross(_x);
        _hostAxialOffset = (center - host.Origin).Dot(_z);
        _toolAxialOffset = (center - tool.Origin).Dot(_x);
        var start = Evaluate(domain.Start);
        _hostUAtStart = PrincipalAngle(start - host.Origin, host.XAxis, host.YAxis);
        _toolUAtStart = PrincipalAngle(start - tool.Origin, tool.XAxis, tool.YAxis);
    }

    public CylinderSurface Host { get; }
    public CylinderSurface Tool { get; }
    public Point3D Center { get; }
    public CylinderIntersectionBranch Branch { get; }
    public ParameterInterval Domain { get; }
    public IntersectionCurveSense Sense { get; }

    /// <summary>The cutter axis's offset from the host centreline along their common perpendicular (0 when they cross).</summary>
    public double Offset => _offset;

    /// <summary>
    /// Perpendicular cylinders whose axes need not cross: the centre is the foot of their common perpendicular on
    /// the host axis. Admits a cutter strictly inside the host's silhouette, or touching it from inside at one point;
    /// refuses one that reaches outside it (the curve would end where it turns back).
    /// </summary>
    public static KernelResult<CylinderCylinderIntersectionCurve> CreateOffset(
        CylinderSurface host, CylinderSurface tool,
        CylinderIntersectionBranch branch, ParameterInterval domain,
        IntersectionCurveSense sense = IntersectionCurveSense.Forward,
        ToleranceContext? tolerance = null)
    {
        var tol = tolerance ?? ToleranceContext.Default;
        if (domain.End <= domain.Start || domain.End - domain.Start > 2d * double.Pi + tol.Angular)
            return Failure("Intersection domain must be nonempty and span no more than one revolution.", "Geometry.CylinderIntersection.InvalidDomain");
        if (!Enum.IsDefined(branch) || !Enum.IsDefined(sense))
            return Failure("Intersection branch and sense must be recognized values.", "Geometry.CylinderIntersection.InvalidOrientation");
        var x = tool.Axis.ToVector();
        var z = host.Axis.ToVector();
        if (double.Abs(x.Dot(z)) > double.Min(tol.Angular, 64d * MachineEpsilon))
            return Failure("Only perpendicular cylinder axes are admitted.", "Geometry.CylinderIntersection.ObliqueAxes");
        var y = z.Cross(x);
        // Foot of the common perpendicular on the host axis, and the cutter axis's offset from it along y.
        var d = tool.Origin - host.Origin;
        var center = host.Origin + z * d.Dot(z);
        var offset = d.Dot(y);
        var scale = double.Max(1d, double.Max(
            double.Max(double.Abs(center.X), double.Abs(center.Y)),
            double.Max(double.Abs(center.Z), host.Radius + tool.Radius + double.Abs(offset))));
        var reach = double.Abs(offset) + tool.Radius;
        var touching = Touching.None;
        if (double.Abs(reach - host.Radius) <= 64d * MachineEpsilon * scale)
            touching = offset >= 0 ? Touching.AtPositiveY : Touching.AtNegativeY;
        else if (reach >= host.Radius - tol.Linear)
            return Failure("The cutter must stay inside the host's silhouette, or touch it from inside.", "Geometry.CylinderIntersection.TangentOrOversize");
        return KernelResult<CylinderCylinderIntersectionCurve>.Success(
            new CylinderCylinderIntersectionCurve(host, tool, center, branch, domain, sense, offset, touching));
    }

    /// <summary>
    /// A bound on the fourth derivative |P⁽⁴⁾| over [start, end], for the cubic Hermite remainder |P⁽⁴⁾|·h⁴/384.
    /// Crossing axes keep the closed-form chain-rule bound. Offset or touching cutters use a Cauchy estimate: P is
    /// analytic in the strip |Im t| &lt; ρ, so |P⁽⁴⁾| ≤ 4!·M/ρ⁴ with M bounding |P − centre| there.
    /// </summary>
    internal double FourthDerivativeBound(double start, double end)
    {
        if (_offset == 0d && _touching == Touching.None) return CrossingFourthDerivativeBound(Host.Radius, Tool.Radius, start, end);
        var (R, r, e) = (Host.Radius, Tool.Radius, double.Abs(_offset));
        var best = double.PositiveInfinity;
        for (var i = 1; i <= 64; i++)
        {
            var rho = 3d * i / 64d;
            var c = double.Cosh(rho);
            // In the strip |cos t| ≤ cosh ρ, so |y| ≤ e + r·cosh ρ. Each square-rooted factor must keep a positive
            // real part there (no branch point): R - y and R + y, or the one left after the double root comes out.
            double axial;
            if (_touching == Touching.None)
            {
                if (R - e - r * c <= 0) break;
                axial = R + e + r * c;
            }
            else
            {
                // Remaining factor R + e - r·cos t (Re ≥ R + e - r·cosh ρ), times √(2r)·|cos(t/2)| ≤ √(2r)·cosh(ρ/2).
                if (R + e - r * c <= 0) break;
                axial = double.Sqrt(R + e + r * c) * double.Sqrt(2d * r) * double.Cosh(rho / 2d);
            }
            var section = e + r * c;
            var m = double.Sqrt(axial * axial + 2d * section * section);
            best = double.Min(best, 24d * m / double.Pow(rho, 4d));
        }
        return best;
    }

    private static double CrossingFourthDerivativeBound(double hostRadius, double toolRadius, double start, double end)
    {
        var r2 = toolRadius * toolRadius;
        var maxCosSq = double.Max(double.Pow(double.Cos(start), 2d), double.Pow(double.Cos(end), 2d));
        if (double.Ceiling(start / double.Pi) * double.Pi <= end)
            maxCosSq = 1d;
        var minG = hostRadius * hostRadius - r2 * maxCosSq;
        if (minG <= 0d) return double.PositiveInfinity;
        var sqrtG = double.Sqrt(minG);
        var g3 = minG * sqrtG;
        var g5 = minG * g3;
        var g7 = minG * g5;
        // For x=sqrt(g), g=R²-r²cos²(t): the first four derivatives of g are bounded by r², 2r², 4r² and 8r².
        // The chain rule bounds the fourth derivative of x by the expression below. Both remaining
        // coordinates are r*cos(t), r*sin(t), with fourth derivatives <=r.
        var xFourth = (15d * double.Pow(r2, 4d) / (16d * g7))
            + (4.5d * double.Pow(r2, 3d) / g5)
            + (7d * r2 * r2 / g3)
            + (4d * r2 / sqrtG);
        return double.Sqrt(xFourth * xFourth + 2d * toolRadius * toolRadius);
    }

    public static KernelResult<CylinderCylinderIntersectionCurve> Create(
        CylinderSurface host, CylinderSurface tool, Point3D center,
        CylinderIntersectionBranch branch, ParameterInterval domain,
        IntersectionCurveSense sense = IntersectionCurveSense.Forward,
        ToleranceContext? tolerance = null)
    {
        var tol = tolerance ?? ToleranceContext.Default;
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(center.Z))
            return Failure("Intersection center must be finite.", "Geometry.CylinderIntersection.NonFiniteCenter");
        if (domain.End <= domain.Start || domain.End - domain.Start > 2d * double.Pi + tol.Angular)
            return Failure("Intersection domain must be nonempty and span no more than one revolution.", "Geometry.CylinderIntersection.InvalidDomain");
        if (!Enum.IsDefined(branch) || !Enum.IsDefined(sense))
            return Failure("Intersection branch and sense must be recognized values.", "Geometry.CylinderIntersection.InvalidOrientation");
        var x = tool.Axis.ToVector();
        var z = host.Axis.ToVector();
        var scale = double.Max(1d, double.Max(
            double.Max(double.Abs(center.X), double.Abs(center.Y)),
            double.Max(double.Abs(center.Z), host.Radius + tool.Radius)));
        // A model-scale tolerance is too loose for an authority asserting that its
        // evaluated points lie on both exact surfaces. Admit only numeric alignment.
        var angularAlignment = double.Min(tol.Angular, 64d * MachineEpsilon);
        var linearAlignment = double.Min(tol.Linear, 64d * MachineEpsilon * scale);
        if (double.Abs(x.Dot(z)) > angularAlignment)
            return Failure("Only perpendicular cylinder axes are admitted.", "Geometry.CylinderIntersection.ObliqueAxes");
        if (DistanceFromAxis(center, host.Origin, z) > linearAlignment
            || DistanceFromAxis(center, tool.Origin, x) > linearAlignment)
            return Failure("Cutter and host centerlines must cross at the declared intersection center.", "Geometry.CylinderIntersection.EccentricAxes");
        if (tool.Radius >= host.Radius - tol.Linear)
            return Failure("The cutter radius must remain strictly inside the host radius with linear clearance.", "Geometry.CylinderIntersection.TangentOrOversize");
        return KernelResult<CylinderCylinderIntersectionCurve>.Success(
            new CylinderCylinderIntersectionCurve(host, tool, center, branch, domain, sense));
    }

    public Point3D Evaluate(double t)
    {
        CheckParameter(t);
        var r = Tool.Radius;
        var y = _offset + r * double.Cos(t);
        var z = r * double.Sin(t);
        return Center + (_x * Axial(t)) + (_y * y) + (_z * z);
    }

    // x = ±√((R - y)(R + y)); a touching cutter has one factor 2r·sin²(t/2) or 2r·cos²(t/2), rooted exactly.
    private double Axial(double t)
    {
        var (R, r) = (Host.Radius, Tool.Radius);
        var y = _offset + r * double.Cos(t);
        return BranchSign * _touching switch
        {
            Touching.None => double.Sqrt((R * R) - (y * y)),
            // e = R - r: R - y = r(1 - cos t) = 2r·sin²(t/2).
            Touching.AtPositiveY => double.Sqrt(R + y) * double.Sqrt(2d * r) * double.Sin(t / 2d),
            // e = r - R: R + y = r(1 + cos t) = 2r·cos²(t/2).
            _ => double.Sqrt(R - y) * double.Sqrt(2d * r) * double.Cos(t / 2d),
        };
    }

    public Vector3D Derivative(double t)
    {
        CheckParameter(t);
        var (R, r) = (Host.Radius, Tool.Radius);
        var sin = double.Sin(t);
        var cos = double.Cos(t);
        var y = _offset + r * cos;
        double dx;
        switch (_touching)
        {
            case Touching.None:
                // d/dt √(R² - y²) = -y·y' / √(R² - y²), with y' = -r sin t.
                dx = BranchSign * y * r * sin / double.Sqrt((R * R) - (y * y));
                break;
            case Touching.AtPositiveY:
            {
                // x = √(2r)·√(R + y)·sin(t/2), with (R + y)' = -r sin t.
                var f = double.Sqrt(R + y);
                dx = BranchSign * double.Sqrt(2d * r) * ((-r * sin / (2d * f)) * double.Sin(t / 2d) + f * double.Cos(t / 2d) / 2d);
                break;
            }
            default:
            {
                // x = √(2r)·√(R - y)·cos(t/2), with (R - y)' = r sin t.
                var f = double.Sqrt(R - y);
                dx = BranchSign * double.Sqrt(2d * r) * ((r * sin / (2d * f)) * double.Cos(t / 2d) - f * double.Sin(t / 2d) / 2d);
                break;
            }
        }
        return (_x * dx) - (_y * (r * sin)) + (_z * (r * cos));
    }


    public CylinderIntersectionUv EvaluateHostPcurve(double t)
    {
        var point = Evaluate(t);
        var principal = PrincipalAngle(point - Host.Origin, Host.XAxis, Host.YAxis);
        // On one branch r < R keeps the angular sweep below pi. Unwrap around the
        // start point so crossing the host's chosen U seam does not split geometry.
        var delta = principal - _hostUAtStart;
        if (delta > double.Pi) delta -= 2d * double.Pi;
        if (delta < -double.Pi) delta += 2d * double.Pi;
        return new CylinderIntersectionUv(_hostUAtStart + delta,
            _hostAxialOffset + Tool.Radius * double.Sin(t));
    }

    public CylinderIntersectionUv EvaluateToolPcurve(double t)
    {
        var point = Evaluate(t);
        return new CylinderIntersectionUv(_toolUAtStart + (t - Domain.Start),
            (point - Tool.Origin).Dot(_x));
    }

    private double BranchSign => Branch == CylinderIntersectionBranch.PositiveCutterAxis ? 1d : -1d;

    private void CheckParameter(double t)
    {
        if (!double.IsFinite(t) || t < Domain.Start || t > Domain.End)
            throw new ArgumentOutOfRangeException(nameof(t), "Parameter must lie in the intersection domain.");
    }

    private static double PrincipalAngle(Vector3D offset, Direction3D x, Direction3D y)
        => double.Atan2(offset.Dot(y.ToVector()), offset.Dot(x.ToVector()));

    private static double DistanceFromAxis(Point3D point, Point3D origin, Vector3D axis)
    {
        var offset = point - origin;
        return (offset - axis * offset.Dot(axis)).Length;
    }

    private static KernelResult<CylinderCylinderIntersectionCurve> Failure(string message, string source)
        => KernelResult<CylinderCylinderIntersectionCurve>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source)]);
}
