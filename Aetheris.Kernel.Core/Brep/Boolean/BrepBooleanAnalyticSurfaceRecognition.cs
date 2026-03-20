using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public enum AnalyticSurfaceKind
{
    Cylinder,
    Cone,
    Sphere,
    Torus,
}

public readonly record struct RecognizedCone(
    Point3D AxisOrigin,
    Direction3D Axis,
    double MinAxisParameter,
    double MaxAxisParameter,
    double SemiAngleRadians,
    double RadiusAtMinAxisParameter,
    double RadiusAtMaxAxisParameter)
{
    public Point3D MinCenter => AxisOrigin + (Axis.ToVector() * MinAxisParameter);

    public Point3D MaxCenter => AxisOrigin + (Axis.ToVector() * MaxAxisParameter);

    public double MaxRadius => System.Math.Max(RadiusAtMinAxisParameter, RadiusAtMaxAxisParameter);

    public double RadiusScale => double.Tan(SemiAngleRadians);

    public Point3D PointAtAxisParameter(double axisParameter) => AxisOrigin + (Axis.ToVector() * axisParameter);

    public double RadiusAtAxisParameter(double axisParameter) => axisParameter * RadiusScale;
}

public readonly record struct RecognizedSphere(Point3D Center, double Radius);

public readonly record struct RecognizedTorus(Point3D Center, Direction3D Axis, double MajorRadius, double MinorRadius);

public readonly record struct AnalyticSurface(
    AnalyticSurfaceKind Kind,
    RecognizedCylinder? Cylinder = null,
    RecognizedCone? Cone = null,
    RecognizedSphere? Sphere = null,
    RecognizedTorus? Torus = null);

public static class BrepBooleanAnalyticSurfaceRecognition
{
    public static bool TryRecognizeCylinder(BrepBody body, ToleranceContext tolerance, out AnalyticSurface surface, out string reason)
    {
        var recognized = BrepBooleanCylinderRecognition.TryRecognizeCylinder(body, tolerance, out var cylinder, out reason);
        surface = recognized
            ? new AnalyticSurface(AnalyticSurfaceKind.Cylinder, Cylinder: cylinder)
            : default;
        return recognized;
    }

    public static bool TryRecognizeCone(BrepBody body, ToleranceContext tolerance, out AnalyticSurface surface, out string reason)
    {
        surface = default;
        reason = string.Empty;

        var faceBindings = body.Bindings.FaceBindings.ToArray();
        if (faceBindings.Length < 2 || faceBindings.Length > 3)
        {
            reason = "cone recognition requires one conical face plus one or two planar cap faces.";
            return false;
        }

        if (body.Bindings.EdgeBindings.Count() < 2 || body.Bindings.EdgeBindings.Count() > 3)
        {
            reason = "cone recognition requires one seam edge plus one or two circular cap edges.";
            return false;
        }

        var coneBindings = new List<FaceGeometryBinding>(1);
        var planeBindings = new List<(FaceGeometryBinding Binding, PlaneSurface Plane)>(2);

        foreach (var binding in faceBindings)
        {
            var currentSurface = body.Geometry.GetSurface(binding.SurfaceGeometryId);
            switch (currentSurface.Kind)
            {
                case SurfaceGeometryKind.Cone when currentSurface.Cone is ConeSurface:
                    coneBindings.Add(binding);
                    break;
                case SurfaceGeometryKind.Plane when currentSurface.Plane is PlaneSurface plane:
                    planeBindings.Add((binding, plane));
                    break;
                default:
                    reason = "cone recognition requires one conical face plus one or two planar cap faces.";
                    return false;
            }
        }

        if (coneBindings.Count != 1 || planeBindings.Count is < 1 or > 2)
        {
            reason = "cone recognition requires one conical face plus one or two planar cap faces.";
            return false;
        }

        var coneSurface = body.Geometry.GetSurface(coneBindings[0].SurfaceGeometryId).Cone!.Value;
        var axis = coneSurface.Axis;
        var axisVector = axis.ToVector();

        foreach (var edgeBinding in body.Bindings.EdgeBindings)
        {
            var edgeCurve = body.Geometry.GetCurve(edgeBinding.CurveGeometryId);
            if (edgeCurve.Kind != CurveGeometryKind.Line3 && edgeCurve.Kind != CurveGeometryKind.Circle3)
            {
                reason = "cone recognition requires one line seam and circular cap edges only.";
                return false;
            }
        }

        var samples = new List<(double AxisParameter, double Radius)>(2);
        foreach (var (_, plane) in planeBindings)
        {
            var normal = plane.Normal.ToVector();
            var dot = normal.Dot(axisVector);
            if (!ToleranceMath.AlmostEqual(double.Abs(dot), 1d, tolerance))
            {
                reason = "cone recognition requires cap plane normals parallel to the cone axis.";
                return false;
            }

            var centerOffset = plane.Origin - coneSurface.Apex;
            var axisParameter = centerOffset.Dot(axisVector);
            var radialOffset = centerOffset - (axisVector * axisParameter);
            if (radialOffset.Length > tolerance.Linear)
            {
                reason = "cone recognition requires cap centers to stay on the cone axis.";
                return false;
            }

            samples.Add((axisParameter, System.Math.Abs(axisParameter) * System.Math.Tan(coneSurface.SemiAngleRadians)));
        }

        if (planeBindings.Count == 1)
        {
            samples.Add((0d, 0d));
        }

        samples.Sort((a, b) => a.AxisParameter.CompareTo(b.AxisParameter));
        if ((samples[1].AxisParameter - samples[0].AxisParameter) <= tolerance.Linear)
        {
            reason = "recognized cone height must be positive.";
            return false;
        }

        surface = new AnalyticSurface(
            AnalyticSurfaceKind.Cone,
            Cone: new RecognizedCone(
                coneSurface.Apex,
                axis,
                samples[0].AxisParameter,
                samples[1].AxisParameter,
                coneSurface.SemiAngleRadians,
                samples[0].Radius,
                samples[1].Radius));
        return true;
    }

    public static bool TryRecognizeSphere(BrepBody body, ToleranceContext _, out AnalyticSurface surface, out string reason)
    {
        surface = default;
        reason = string.Empty;

        var faceBindings = body.Bindings.FaceBindings.ToArray();
        if (faceBindings.Length != 1)
        {
            reason = "sphere recognition requires exactly one spherical face.";
            return false;
        }

        var recognizedSurface = body.Geometry.GetSurface(faceBindings[0].SurfaceGeometryId);
        if (recognizedSurface.Kind != SurfaceGeometryKind.Sphere || recognizedSurface.Sphere is not SphereSurface sphere)
        {
            reason = "sphere recognition requires a spherical face binding.";
            return false;
        }

        surface = new AnalyticSurface(AnalyticSurfaceKind.Sphere, Sphere: new RecognizedSphere(sphere.Center, sphere.Radius));
        return true;
    }

    public static bool TryRecognizeTorus(BrepBody body, ToleranceContext _, out AnalyticSurface surface, out string reason)
    {
        surface = default;
        reason = string.Empty;

        var faceBindings = body.Bindings.FaceBindings.ToArray();
        if (faceBindings.Length != 1)
        {
            reason = "torus recognition requires exactly one toroidal face.";
            return false;
        }

        var recognizedSurface = body.Geometry.GetSurface(faceBindings[0].SurfaceGeometryId);
        if (recognizedSurface.Kind != SurfaceGeometryKind.Torus || recognizedSurface.Torus is not TorusSurface torus)
        {
            reason = "torus recognition requires a toroidal face binding.";
            return false;
        }

        surface = new AnalyticSurface(AnalyticSurfaceKind.Torus, Torus: new RecognizedTorus(torus.Center, torus.Axis, torus.MajorRadius, torus.MinorRadius));
        return true;
    }

    public static bool TryRecognizeAnalyticSurface(BrepBody body, ToleranceContext tolerance, out AnalyticSurface surface, out string reason)
        => TryRecognizeCylinder(body, tolerance, out surface, out reason)
           || TryRecognizeCone(body, tolerance, out surface, out reason)
           || TryRecognizeSphere(body, tolerance, out surface, out reason)
           || TryRecognizeTorus(body, tolerance, out surface, out reason);
}
