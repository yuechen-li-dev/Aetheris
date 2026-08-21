using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

public sealed record PcurveBuildResult(bool IsSuccess, int Count, double MaximumResidual, IReadOnlyList<SculptDiagnostic> Diagnostics);

/// <summary>Builds deterministic face-local pcurves for the qualified Plane/Cylinder/non-rational B-spline matrix.</summary>
public static class BoundedPcurveBuilder
{
    public static PcurveBuildResult Populate(TopologyModel topology, BrepGeometryStore geometry, BrepBindingModel bindings,
        double tolerance = 1e-5, int sampleCount = 129)
    {
        var diagnostics = new List<SculptDiagnostic>();
        var count = 0;
        var maximum = 0d;
        foreach (var face in topology.Faces.OrderBy(item => item.Id.Value))
        {
            if (!bindings.TryGetFaceBinding(face.Id, out var faceBinding) || !geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface) || surface is null)
                continue;
            foreach (var coedge in face.LoopIds.Select(topology.GetLoop).SelectMany(loop => loop.CoedgeIds.Select(topology.GetCoedge)))
            {
                if (!bindings.TryGetEdgeBinding(coedge.EdgeId, out var edgeBinding) || !geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve) || curve is null
                    || edgeBinding.TrimInterval is not { } interval)
                {
                    diagnostics.Add(new("surf-pcurve-invalid", $"Coedge {coedge.Id.Value} has no bounded 3D edge curve.", coedge.Id.Value.ToString()));
                    continue;
                }
                var built = Build(surface, curve, interval, tolerance, sampleCount);
                if (built.Pcurve is null)
                {
                    diagnostics.Add(new("surf-pcurve-invalid", built.Diagnostic ?? "Pcurve construction failed.", coedge.Id.Value.ToString()));
                    continue;
                }
                bindings.AddPcurveBinding(new(coedge.Id, face.Id, faceBinding.SurfaceGeometryId, built.Pcurve));
                maximum = double.Max(maximum, built.MaximumResidual);
                count++;
            }
        }
        return new(diagnostics.Count == 0 && maximum <= tolerance, count, maximum, diagnostics);
    }

    private static (PcurveGeometry? Pcurve, double MaximumResidual, string? Diagnostic) Build(
        SurfaceGeometry surface, CurveGeometry curve, ParameterInterval interval, double tolerance, int samples)
    {
        if (surface.Plane is { } plane)
        {
            var start = PlaneUv(plane, EvaluateCurve(curve, interval.Start));
            var end = PlaneUv(plane, EvaluateCurve(curve, interval.End));
            if (curve.Kind == CurveGeometryKind.Line3) return (PcurveGeometry.Line(interval, start, end), 0d, null);
            if (curve.Kind == CurveGeometryKind.Circle3 && curve.Circle3 is { } circle)
            {
                var center = PlaneUv(plane, circle.Center);
                var cosinePoint = PlaneUv(plane, circle.Evaluate(0d));
                var sinePoint = PlaneUv(plane, circle.Evaluate(double.Pi / 2d));
                return (PcurveGeometry.Ellipse(interval, center,
                    new(cosinePoint.U - center.U, cosinePoint.V - center.V),
                    new(sinePoint.U - center.U, sinePoint.V - center.V)), 0d, null);
            }
            var lineCandidate = PcurveGeometry.Line(interval, start, end);
            var lineResidual = MaximumResidual(surface, curve, lineCandidate, interval, 67);
            if (lineResidual <= tolerance) return (lineCandidate, lineResidual, null);
            const int denseSamples = 4097;
            return (PcurveGeometry.Polyline(interval, Sample(interval, denseSamples, t => PlaneUv(plane, EvaluateCurve(curve, t)))), 0d, null);
        }
        if (surface.Cylinder is { } cylinder)
        {
            var points = Sample(interval, samples, t => CylinderUv(cylinder, EvaluateCurve(curve, t))).ToArray();
            UnwrapAngles(points);
            return (PcurveGeometry.Polyline(interval, points), 0d, null);
        }
        if (surface.BSplineSurfaceWithKnots is not { } spline)
            return (null, double.PositiveInfinity, $"Surface family {surface.Kind} is outside the qualified pcurve matrix.");

        var startInverse = Invert(spline, EvaluateCurve(curve, interval.Start), null);
        var endInverse = Invert(spline, EvaluateCurve(curve, interval.End), startInverse.Success ? startInverse.Uv : null);
        if (startInverse.Success && endInverse.Success)
        {
            var lineCandidate = PcurveGeometry.Line(interval, startInverse.Uv, endInverse.Uv);
            var lineResidual = MaximumResidual(surface, curve, lineCandidate, interval, 67);
            if (lineResidual <= tolerance) return (lineCandidate, lineResidual, null);
        }

        const int inverseSamples = 4097;
        var uv = new List<SurfaceParameterPoint>(inverseSamples);
        SurfaceParameterPoint? seed = null;
        var maximum = 0d;
        for (var index = 0; index < inverseSamples; index++)
        {
            var t = interval.Start + ((interval.End - interval.Start) * index / (inverseSamples - 1d));
            var point = EvaluateCurve(curve, t);
            var inverse = Invert(spline, point, seed);
            if (!inverse.Success || inverse.Residual > tolerance)
                return (null, inverse.Residual, $"Non-rational B-spline inverse failed at sample {index}; residual={inverse.Residual:R}, tolerance={tolerance:R}.");
            seed = inverse.Uv;
            uv.Add(inverse.Uv);
            maximum = double.Max(maximum, inverse.Residual);
        }
        return (PcurveGeometry.Polyline(interval, uv), maximum, null);
    }

    private static (bool Success, SurfaceParameterPoint Uv, double Residual) Invert(BSplineSurfaceWithKnots spline, Point3D target, SurfaceParameterPoint? prior)
    {
        // Intersection traces are parameter-continuous, so the preceding inverse is
        // the deterministic seed after the first sample. Avoiding a fresh global grid
        // search at every dense qualification point keeps high-accuracy pcurves bounded.
        var best = prior ?? GridSeed(spline, target);
        for (var iteration = 0; iteration < 32; iteration++)
        {
            var point = spline.Evaluate(best.U, best.V);
            var residual = point - target;
            if (residual.Length <= 1e-8d) return (true, best, residual.Length);
            var hu = double.Max((spline.DomainEndU - spline.DomainStartU) * 1e-4d, 1e-7d);
            var hv = double.Max((spline.DomainEndV - spline.DomainStartV) * 1e-4d, 1e-7d);
            var du = (spline.Evaluate(double.Min(best.U + hu, spline.DomainEndU), best.V) - spline.Evaluate(double.Max(best.U - hu, spline.DomainStartU), best.V))
                / (double.Min(best.U + hu, spline.DomainEndU) - double.Max(best.U - hu, spline.DomainStartU));
            var dv = (spline.Evaluate(best.U, double.Min(best.V + hv, spline.DomainEndV)) - spline.Evaluate(best.U, double.Max(best.V - hv, spline.DomainStartV)))
                / (double.Min(best.V + hv, spline.DomainEndV) - double.Max(best.V - hv, spline.DomainStartV));
            var a = du.Dot(du); var b = du.Dot(dv); var c = dv.Dot(dv);
            var r1 = du.Dot(residual); var r2 = dv.Dot(residual);
            var determinant = (a * c) - (b * b);
            if (double.Abs(determinant) <= 1e-18d) break;
            var deltaU = ((c * r1) - (b * r2)) / determinant;
            var deltaV = ((a * r2) - (b * r1)) / determinant;
            var accepted = false;
            for (var damping = 1d; damping >= 1d / 64d; damping *= .5d)
            {
                var candidate = new SurfaceParameterPoint(
                    System.Math.Clamp(best.U - (deltaU * damping), spline.DomainStartU, spline.DomainEndU),
                    System.Math.Clamp(best.V - (deltaV * damping), spline.DomainStartV, spline.DomainEndV));
                if ((spline.Evaluate(candidate.U, candidate.V) - target).LengthSquared >= residual.LengthSquared) continue;
                best = candidate; accepted = true; break;
            }
            if (!accepted) break;
        }
        // Deterministic bounded pattern refinement handles domain-boundary minima where
        // the two-column normal equation can become poorly conditioned.
        var stepU = (spline.DomainEndU - spline.DomainStartU) / 32d;
        var stepV = (spline.DomainEndV - spline.DomainStartV) / 32d;
        var bestDistance = (spline.Evaluate(best.U, best.V) - target).LengthSquared;
        for (var iteration = 0; iteration < 64 && (stepU > 1e-13d || stepV > 1e-13d); iteration++)
        {
            var improved = false;
            foreach (var offsetU in new[] { -stepU, 0d, stepU }) foreach (var offsetV in new[] { -stepV, 0d, stepV })
            {
                if (offsetU == 0d && offsetV == 0d) continue;
                var candidate = new SurfaceParameterPoint(
                    System.Math.Clamp(best.U + offsetU, spline.DomainStartU, spline.DomainEndU),
                    System.Math.Clamp(best.V + offsetV, spline.DomainStartV, spline.DomainEndV));
                var distance = (spline.Evaluate(candidate.U, candidate.V) - target).LengthSquared;
                if (distance >= bestDistance) continue;
                best = candidate; bestDistance = distance; improved = true;
            }
            if (!improved) { stepU *= .5d; stepV *= .5d; }
        }
        var finalResidual = (spline.Evaluate(best.U, best.V) - target).Length;
        return (finalResidual <= 1e-5d, best, finalResidual);
    }

    private static SurfaceParameterPoint GridSeed(BSplineSurfaceWithKnots spline, Point3D target)
    {
        var best = new SurfaceParameterPoint(spline.DomainStartU, spline.DomainStartV);
        var distance = double.PositiveInfinity;
        const int divisions = 32;
        for (var i = 0; i <= divisions; i++) for (var j = 0; j <= divisions; j++)
        {
            var u = spline.DomainStartU + ((spline.DomainEndU - spline.DomainStartU) * i / divisions);
            var v = spline.DomainStartV + ((spline.DomainEndV - spline.DomainStartV) * j / divisions);
            var d = (spline.Evaluate(u, v) - target).LengthSquared;
            if (d < distance) { distance = d; best = new(u, v); }
        }
        return best;
    }

    private static Point3D EvaluateCurve(CurveGeometry curve, double parameter) => curve.Kind switch
    {
        CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(parameter),
        CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(parameter),
        CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(parameter),
        CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(parameter),
        CurveGeometryKind.Hyperbola3 => curve.Hyperbola3!.Value.Evaluate(parameter),
        _ => throw new NotSupportedException($"Curve family {curve.Kind} is outside the qualified pcurve matrix.")
    };
    private static SurfaceParameterPoint PlaneUv(PlaneSurface plane, Point3D point)
    {
        var d = point - plane.Origin;
        return new(d.Dot(plane.UAxis.ToVector()), d.Dot(plane.VAxis.ToVector()));
    }
    private static SurfaceParameterPoint CylinderUv(CylinderSurface cylinder, Point3D point)
    {
        var d = point - cylinder.Origin;
        return new(double.Atan2(d.Dot(cylinder.YAxis.ToVector()), d.Dot(cylinder.XAxis.ToVector())), d.Dot(cylinder.Axis.ToVector()));
    }
    private static void UnwrapAngles(SurfaceParameterPoint[] points)
    {
        for (var index = 1; index < points.Length; index++)
        {
            var u = points[index].U;
            while (u - points[index - 1].U > double.Pi) u -= 2d * double.Pi;
            while (u - points[index - 1].U < -double.Pi) u += 2d * double.Pi;
            points[index] = points[index] with { U = u };
        }
    }
    private static IReadOnlyList<SurfaceParameterPoint> Sample(ParameterInterval interval, int count, Func<double, SurfaceParameterPoint> evaluator)
        => Enumerable.Range(0, count).Select(index => evaluator(interval.Start + ((interval.End - interval.Start) * index / (count - 1d)))).ToArray();

    private static double MaximumResidual(SurfaceGeometry surface, CurveGeometry curve, PcurveGeometry pcurve, ParameterInterval interval, int count)
    {
        var maximum = 0d;
        for (var index = 0; index < count; index++)
        {
            var parameter = interval.Start + ((interval.End - interval.Start) * index / (count - 1d));
            var uv = pcurve.Evaluate(parameter);
            var onSurface = surface.Kind switch
            {
                SurfaceGeometryKind.Plane => surface.Plane!.Value.Evaluate(uv.U, uv.V),
                SurfaceGeometryKind.BSplineSurfaceWithKnots => surface.BSplineSurfaceWithKnots!.Evaluate(uv.U, uv.V),
                _ => throw new InvalidOperationException()
            };
            maximum = double.Max(maximum, (onSurface - EvaluateCurve(curve, parameter)).Length);
        }
        return maximum;
    }
}
