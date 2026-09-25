using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

public sealed record BrepPcurveRecoveryResult(bool IsSuccess, int Count, double MaximumResidual, IReadOnlyList<BrepPcurveRecoveryDiagnostic> Diagnostics);
public sealed record BrepPcurveRecoveryDiagnostic(string Code, string Message, string? Entity = null);

/// <summary>Builds deterministic face-local pcurves from edge and support geometry.</summary>
public static class BrepPcurveRecovery
{
    public static BrepPcurveRecoveryResult Populate(TopologyModel topology, BrepGeometryStore geometry, BrepBindingModel bindings,
        double tolerance = 1e-5, int sampleCount = 129)
    {
        var diagnostics = new List<BrepPcurveRecoveryDiagnostic>();
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
                var qualificationTolerance = ResolveQualificationTolerance(surface, curve, tolerance);
                if (bindings.TryGetPcurveBinding(coedge.Id, out var retained))
                {
                    var residual = MaximumResidual(surface, curve, retained.Pcurve, interval, sampleCount);
                    if (retained.FaceId != face.Id || retained.SurfaceGeometryId != faceBinding.SurfaceGeometryId || residual > qualificationTolerance)
                        diagnostics.Add(new("surf-pcurve-invalid", "Retained pcurve has inconsistent ownership or geometry.", coedge.Id.Value.ToString()));
                    maximum = double.Max(maximum, residual); count++; continue;
                }
                (PcurveGeometry? Pcurve, double MaximumResidual, string? Diagnostic) built;
                try { built = Build(surface, curve, interval, qualificationTolerance, sampleCount); }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
                {
                    diagnostics.Add(new("surf-pcurve-invalid",
                        $"Face {face.Id.Value}, edge {coedge.EdgeId.Value}, {surface.Kind}: {exception.Message}",
                        coedge.Id.Value.ToString()));
                    continue;
                }
                if (built.Pcurve is null)
                {
                    diagnostics.Add(new("surf-pcurve-invalid", built.Diagnostic ?? "Pcurve construction failed.", coedge.Id.Value.ToString()));
                    continue;
                }
                var liftSamples = surface.BSplineSurfaceWithKnots is null ? 1025 : 8193;
                var liftResidual = MaximumResidual(surface, curve, built.Pcurve, interval, liftSamples);
                if (!double.IsFinite(liftResidual) || liftResidual > qualificationTolerance)
                {
                    diagnostics.Add(new("surf-pcurve-invalid",
                        $"Face {face.Id.Value}, edge {coedge.EdgeId.Value}, {surface.Kind}: lifted pcurve residual {liftResidual:R} mm exceeds {qualificationTolerance:R} mm over {liftSamples} samples.",
                        coedge.Id.Value.ToString()));
                    continue;
                }
                var origin = surface.BSplineSurfaceWithKnots is null
                    ? PcurveBindingOrigin.RecoveredAnalytic : PcurveBindingOrigin.RecoveredSpline;
                bindings.AddPcurveBinding(new(coedge.Id, face.Id, faceBinding.SurfaceGeometryId, built.Pcurve,
                    Qualification: new(origin, liftResidual, qualificationTolerance, liftSamples,
                        built.Pcurve.Kind.ToString(), surface.Kind.ToString())));
                maximum = double.Max(maximum, liftResidual);
                count++;
            }
        }
        return new(diagnostics.Count == 0, count, maximum, diagnostics);
    }

    private static double ResolveQualificationTolerance(SurfaceGeometry surface, CurveGeometry curve, double baseTolerance)
    {
        var curveRecovery = curve.RecoveryProvenance;
        var surfaceRecovery = surface.RecoveryProvenance;
        if (curveRecovery is null && surfaceRecovery is null) return baseTolerance;
        var budget = double.Min(curveRecovery?.RecoveryToleranceMillimetres ?? double.PositiveInfinity,
            surfaceRecovery?.RecoveryToleranceMillimetres ?? double.PositiveInfinity);
        var observed = (curveRecovery?.MeasuredMaxDeviationMillimetres ?? 0d)
            + (surfaceRecovery?.MeasuredMaxDeviationMillimetres ?? 0d);
        return double.Min(budget, double.Max(baseTolerance, baseTolerance + observed * 1.25d));
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
            if (curve.BSpline3 is { } polynomial)
            {
                // Affine projection commutes with B-spline evaluation. The hull bound
                // certifies the whole 3D curve lies on this support, without fitting.
                var bound = polynomial.ControlPoints.Max(p => double.Abs((p - plane.Origin).Dot(plane.Normal.ToVector())));
                if (bound > tolerance) return (null, bound, "Polynomial edge control hull is not on the planar support.");
                var controls = polynomial.ControlPoints.Select(p => { var uv = PlaneUv(plane, p); return new Point3D(uv.U, uv.V, 0); }).ToArray();
                var uvCurve = new BSpline3Curve(polynomial.Degree, controls, polynomial.KnotMultiplicities, polynomial.KnotValues,
                    polynomial.CurveForm, polynomial.ClosedCurve, polynomial.SelfIntersect, polynomial.KnotSpec);
                return (PcurveGeometry.Polynomial(interval, uvCurve), bound, null);
            }
            var lineCandidate = PcurveGeometry.Line(interval, start, end);
            var lineResidual = MaximumResidual(surface, curve, lineCandidate, interval, 67);
            if (lineResidual <= tolerance) return (lineCandidate, lineResidual, null);
            const int denseSamples = 4097;
            return (PcurveGeometry.Polyline(interval, Sample(interval, denseSamples, t => PlaneUv(plane, EvaluateCurve(curve, t)))), 0d, null);
        }
        if (surface.Cylinder is { } cylinder)
            return BuildPeriodicAnalytic(surface, curve, interval, tolerance, point => CylinderUv(cylinder, point));
        if (surface.Cone is { } cone)
            return BuildPeriodicAnalytic(surface, curve, interval, tolerance, point => ConeUv(cone, point));
        if (surface.Sphere is { } sphere)
            return BuildPeriodicAnalytic(surface, curve, interval, tolerance, point => SphereUv(sphere, point));
        if (surface.Torus is { } torus)
            return BuildPeriodicAnalytic(surface, curve, interval, tolerance, point => TorusUv(torus, point), unwrapV: true);
        if (surface.BSplineSurfaceWithKnots is not { } spline)
            return (null, double.PositiveInfinity, $"Surface family {surface.Kind} is outside the qualified pcurve matrix.");

        var startInverse = Invert(spline, EvaluateCurve(curve, interval.Start), null, tolerance);
        var endInverse = Invert(spline, EvaluateCurve(curve, interval.End), startInverse.Success ? startInverse.Uv : null, tolerance);
        if (startInverse.Success && endInverse.Success)
        {
            var lineCandidate = PcurveGeometry.Line(interval, startInverse.Uv, endInverse.Uv);
            var lineResidual = MaximumResidual(surface, curve, lineCandidate, interval, 67);
            if (lineResidual <= tolerance) return (lineCandidate, lineResidual, null);
        }

        var bestResidual = double.PositiveInfinity;
        for (var inverseSamples = 129; inverseSamples <= 4097; inverseSamples = (inverseSamples - 1) * 2 + 1)
        {
            var uv = new List<SurfaceParameterPoint>(inverseSamples);
            SurfaceParameterPoint? seed = null;
            var inversionFailed = false;
            for (var index = 0; index < inverseSamples; index++)
            {
                var t = interval.Start + ((interval.End - interval.Start) * index / (inverseSamples - 1d));
                var inverse = Invert(spline, EvaluateCurve(curve, t), seed, tolerance);
                if (!inverse.Success)
                {
                    bestResidual = double.Min(bestResidual, inverse.Residual);
                    inversionFailed = true;
                    break;
                }
                seed = inverse.Uv;
                uv.Add(inverse.Uv);
            }
            if (inversionFailed) continue;
            var candidate = PcurveGeometry.Polyline(interval, uv);
            var residual = MaximumResidual(surface, curve, candidate, interval, (inverseSamples - 1) * 2 + 1);
            bestResidual = double.Min(bestResidual, residual);
            if (residual <= tolerance) return (candidate, residual, null);
        }
        return (null, bestResidual,
            $"Spline pcurve inversion/refinement exceeded 4097 samples; best lifted residual={bestResidual:R} mm, tolerance={tolerance:R} mm.");
    }

    private static (bool Success, SurfaceParameterPoint Uv, double Residual) Invert(BSplineSurfaceWithKnots spline, Point3D target, SurfaceParameterPoint? prior, double tolerance)
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
        return (finalResidual <= tolerance, best, finalResidual);
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
        // Trim endpoints commonly lie on a support-domain boundary. A narrow patch can
        // be missed by a two-dimensional grid even when its boundary is easy to sample.
        const int boundaryDivisions = 2048;
        for (var i = 0; i <= boundaryDivisions; i++)
        {
            var u = spline.DomainStartU + (spline.DomainEndU - spline.DomainStartU) * i / boundaryDivisions;
            var v = spline.DomainStartV + (spline.DomainEndV - spline.DomainStartV) * i / boundaryDivisions;
            Check(u, spline.DomainStartV);
            Check(u, spline.DomainEndV);
            Check(spline.DomainStartU, v);
            Check(spline.DomainEndU, v);
        }
        return best;

        void Check(double u, double v)
        {
            var d = (spline.Evaluate(u, v) - target).LengthSquared;
            if (d < distance) { distance = d; best = new(u, v); }
        }
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
    private static SurfaceParameterPoint ConeUv(ConeSurface cone, Point3D point)
    {
        var fromApex = point - cone.Apex;
        var axial = fromApex.Dot(cone.Axis.ToVector());
        var originRadial = cone.Evaluate(0d, 1d) - cone.Apex - cone.Axis.ToVector();
        var xAxis = Direction3D.Create(originRadial);
        var yAxis = Direction3D.Create(cone.Axis.ToVector().Cross(xAxis.ToVector()));
        return new(double.Atan2(fromApex.Dot(yAxis.ToVector()), fromApex.Dot(xAxis.ToVector())), axial);
    }
    private static SurfaceParameterPoint SphereUv(SphereSurface sphere, Point3D point)
    {
        var d = point - sphere.Center;
        var axial = d.Dot(sphere.Axis.ToVector());
        return new(double.Atan2(d.Dot(sphere.YAxis.ToVector()), d.Dot(sphere.XAxis.ToVector())),
            double.Atan2(axial, double.Sqrt(double.Max(0d, d.LengthSquared - axial * axial))));
    }
    private static SurfaceParameterPoint TorusUv(TorusSurface torus, Point3D point)
    {
        var d = point - torus.Center;
        var x = d.Dot(torus.XAxis.ToVector());
        var y = d.Dot(torus.YAxis.ToVector());
        var axial = d.Dot(torus.Axis.ToVector());
        return new(double.Atan2(y, x), double.Atan2(axial, double.Sqrt(x * x + y * y) - torus.MajorRadius));
    }
    private static (PcurveGeometry? Pcurve, double MaximumResidual, string? Diagnostic) BuildPeriodicAnalytic(
        SurfaceGeometry surface, CurveGeometry curve, ParameterInterval interval, double tolerance,
        Func<Point3D, SurfaceParameterPoint> inverse, bool unwrapV = false)
    {
        var best = double.PositiveInfinity;
        for (var count = 17; count <= 513; count = (count - 1) * 2 + 1)
        {
            var uv = Sample(interval, count, t => inverse(EvaluateCurve(curve, t))).ToArray();
            UnwrapAngles(uv, unwrapV);
            var controls = new List<Point3D>((count - 1) * 3 + 1);
            var knots = new List<double>(count);
            var multiplicities = new List<int>(count);
            for (var index = 0; index < count - 1; index++)
            {
                var start = interval.Start + (interval.End - interval.Start) * index / (count - 1d);
                var end = interval.Start + (interval.End - interval.Start) * (index + 1) / (count - 1d);
                var delta = (end - start) * 1e-4d;
                var nearStart = inverse(EvaluateCurve(curve, start + delta));
                var nearEnd = inverse(EvaluateCurve(curve, end - delta));
                var p0 = uv[index];
                var p3 = uv[index + 1];
                nearStart = nearStart with { U = UnwrapNear(nearStart.U, p0.U) };
                nearEnd = nearEnd with { U = UnwrapNear(nearEnd.U, p3.U) };
                if (unwrapV)
                {
                    nearStart = nearStart with { V = UnwrapNear(nearStart.V, p0.V) };
                    nearEnd = nearEnd with { V = UnwrapNear(nearEnd.V, p3.V) };
                }
                var scale = (end - start) / (3d * delta);
                var p1 = new SurfaceParameterPoint(p0.U + (nearStart.U - p0.U) * scale,
                    p0.V + (nearStart.V - p0.V) * scale);
                var p2 = new SurfaceParameterPoint(p3.U - (p3.U - nearEnd.U) * scale,
                    p3.V - (p3.V - nearEnd.V) * scale);
                if (index == 0)
                {
                    controls.Add(new Point3D(p0.U, p0.V, 0d));
                    knots.Add(start);
                    multiplicities.Add(4);
                }
                controls.Add(new Point3D(p1.U, p1.V, 0d));
                controls.Add(new Point3D(p2.U, p2.V, 0d));
                controls.Add(new Point3D(p3.U, p3.V, 0d));
                knots.Add(end);
                multiplicities.Add(index == count - 2 ? 4 : 3);
            }
            var polynomial = new BSpline3Curve(3, controls, multiplicities, knots,
                "UNSPECIFIED", false, false, "UNSPECIFIED");
            var candidate = PcurveGeometry.Polynomial(interval, polynomial);
            var residual = MaximumResidual(surface, curve, candidate, interval, (count - 1) * 4 + 1);
            best = double.Min(best, residual);
            if (residual <= tolerance) return (candidate, residual, null);
        }
        return (null, best, $"{surface.Kind} pcurve refinement could not meet {tolerance:R} mm (best sampled lift residual {best:R} mm).");
    }

    private static double UnwrapNear(double value, double reference)
    {
        while (value - reference > double.Pi) value -= 2d * double.Pi;
        while (value - reference < -double.Pi) value += 2d * double.Pi;
        return value;
    }
    private static void UnwrapAngles(SurfaceParameterPoint[] points, bool unwrapV)
    {
        for (var index = 1; index < points.Length; index++)
        {
            var u = points[index].U;
            while (u - points[index - 1].U > double.Pi) u -= 2d * double.Pi;
            while (u - points[index - 1].U < -double.Pi) u += 2d * double.Pi;
            points[index] = points[index] with { U = u,
                V = unwrapV ? UnwrapNear(points[index].V, points[index - 1].V) : points[index].V };
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
                SurfaceGeometryKind.Cylinder => surface.Cylinder!.Value.Evaluate(uv.U, uv.V),
                SurfaceGeometryKind.Cone => surface.Cone!.Value.Evaluate(uv.U, uv.V),
                SurfaceGeometryKind.Sphere => surface.Sphere!.Value.Evaluate(uv.U, uv.V),
                SurfaceGeometryKind.Torus => surface.Torus!.Value.Evaluate(uv.U, uv.V),
                SurfaceGeometryKind.BSplineSurfaceWithKnots => surface.BSplineSurfaceWithKnots!.Evaluate(uv.U, uv.V),
                _ => throw new InvalidOperationException()
            };
            maximum = double.Max(maximum, (onSurface - EvaluateCurve(curve, parameter)).Length);
        }
        return maximum;
    }
}

