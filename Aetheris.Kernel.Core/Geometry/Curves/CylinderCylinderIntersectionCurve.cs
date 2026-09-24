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
/// Exact intersection of perpendicular, centerline-crossing cylinders with cutter radius
/// strictly inside host radius. The parameter runs around the cutter cross-section;
/// the branch selects the side of the host along the cutter axis. No BRep topology is implied.
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

    private CylinderCylinderIntersectionCurve(
        CylinderSurface host, CylinderSurface tool, Point3D center,
        CylinderIntersectionBranch branch, ParameterInterval domain,
        IntersectionCurveSense sense)
    {
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
        var y = r * double.Cos(t);
        var z = r * double.Sin(t);
        var x = BranchSign * double.Sqrt((Host.Radius * Host.Radius) - (y * y));
        return Center + (_x * x) + (_y * y) + (_z * z);
    }

    public Vector3D Derivative(double t)
    {
        CheckParameter(t);
        var r = Tool.Radius;
        var sin = double.Sin(t);
        var cos = double.Cos(t);
        var root = double.Sqrt((Host.Radius * Host.Radius) - (r * r * cos * cos));
        var dx = BranchSign * r * r * cos * sin / root;
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
