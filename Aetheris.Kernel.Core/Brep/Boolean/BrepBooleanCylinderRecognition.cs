using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public readonly record struct RecognizedCylinder(
    Point3D AxisOrigin,
    Direction3D Axis,
    double Radius,
    double MinAxisParameter,
    double MaxAxisParameter)
{
    public double Height => MaxAxisParameter - MinAxisParameter;

    public Point3D MinCenter => AxisOrigin + (Axis.ToVector() * MinAxisParameter);

    public Point3D MaxCenter => AxisOrigin + (Axis.ToVector() * MaxAxisParameter);
}

public static class BrepBooleanCylinderRecognition
{
    public static bool TryRecognizeCylinder(BrepBody body, ToleranceContext tolerance, out RecognizedCylinder cylinder, out string reason)
    {
        cylinder = default;
        reason = string.Empty;

        if (body.Topology.Vertices.Count() != 4 || body.Topology.Edges.Count() != 3 || body.Topology.Faces.Count() != 3)
        {
            reason = "topology does not match M08 cylinder counts.";
            return false;
        }

        var faceBindings = body.Bindings.FaceBindings.ToArray();
        if (faceBindings.Length != 3)
        {
            reason = "cylinder recognition requires exactly three face bindings.";
            return false;
        }

        if (body.Bindings.EdgeBindings.Count() != 3)
        {
            reason = "cylinder recognition requires exactly three edge bindings.";
            return false;
        }

        var cylinderBindings = new List<FaceGeometryBinding>(1);
        var planeBindings = new List<(FaceGeometryBinding Binding, PlaneSurface Plane)>(2);

        foreach (var binding in faceBindings)
        {
            var surface = body.Geometry.GetSurface(binding.SurfaceGeometryId);
            switch (surface.Kind)
            {
                case SurfaceGeometryKind.Cylinder when surface.Cylinder is CylinderSurface:
                    cylinderBindings.Add(binding);
                    break;
                case SurfaceGeometryKind.Plane when surface.Plane is PlaneSurface plane:
                    planeBindings.Add((binding, plane));
                    break;
                default:
                    reason = "cylinder recognition requires exactly one cylindrical face and two planar cap faces.";
                    return false;
            }
        }

        if (cylinderBindings.Count != 1 || planeBindings.Count != 2)
        {
            reason = "cylinder recognition requires exactly one cylindrical face and two planar cap faces.";
            return false;
        }

        var cylinderSurface = body.Geometry.GetSurface(cylinderBindings[0].SurfaceGeometryId).Cylinder!.Value;
        var axis = cylinderSurface.Axis;
        var axisVector = axis.ToVector();

        foreach (var edgeBinding in body.Bindings.EdgeBindings)
        {
            var edgeCurve = body.Geometry.GetCurve(edgeBinding.CurveGeometryId);
            if (edgeCurve.Kind != CurveGeometryKind.Line3 && edgeCurve.Kind != CurveGeometryKind.Circle3)
            {
                reason = "cylinder recognition requires one line seam and circular cap edges only.";
                return false;
            }
        }

        var axisParameters = new List<double>(2);
        var sawPositiveCap = false;
        var sawNegativeCap = false;

        foreach (var (_, plane) in planeBindings)
        {
            var normal = plane.Normal.ToVector();
            var dot = normal.Dot(axisVector);
            if (!ToleranceMath.AlmostEqual(double.Abs(dot), 1d, tolerance))
            {
                reason = "cylinder recognition requires cap plane normals parallel to the cylinder axis.";
                return false;
            }

            if (dot > 0d)
            {
                sawPositiveCap = true;
            }
            else
            {
                sawNegativeCap = true;
            }

            var centerOffset = plane.Origin - cylinderSurface.Origin;
            var axisParameter = centerOffset.Dot(axisVector);
            var radialOffset = centerOffset - (axisVector * axisParameter);
            if (radialOffset.Length > tolerance.Linear)
            {
                reason = "cylinder recognition requires cap centers to stay on the cylinder axis.";
                return false;
            }

            axisParameters.Add(axisParameter);
        }

        if (!sawPositiveCap || !sawNegativeCap)
        {
            reason = "cylinder recognition requires opposite cap normals along the cylinder axis.";
            return false;
        }

        axisParameters.Sort();
        if ((axisParameters[1] - axisParameters[0]) <= tolerance.Linear)
        {
            reason = "recognized cylinder height must be positive.";
            return false;
        }

        cylinder = new RecognizedCylinder(
            cylinderSurface.Origin,
            axis,
            cylinderSurface.Radius,
            axisParameters[0],
            axisParameters[1]);
        return true;
    }

    public static bool ValidateAxisAlignedZ(in RecognizedCylinder cylinder, ToleranceContext tolerance)
    {
        var axis = cylinder.Axis.ToVector();
        return ToleranceMath.AlmostZero(axis.X, tolerance)
            && ToleranceMath.AlmostZero(axis.Y, tolerance)
            && ToleranceMath.AlmostEqual(double.Abs(axis.Z), 1d, tolerance);
    }

    public static bool ValidateThroughHole(AxisAlignedBoxExtents box, in RecognizedCylinder cylinder, ToleranceContext tolerance, out BooleanDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (!ValidateAxisAlignedZ(cylinder, tolerance))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(BooleanOperation.Subtract.ToString(), "cylinder axis is not aligned with the box Z axis.");
            return false;
        }

        var minCenter = cylinder.MinCenter;
        var maxCenter = cylinder.MaxCenter;
        if (!ToleranceMath.AlmostEqual(minCenter.X, maxCenter.X, tolerance) || !ToleranceMath.AlmostEqual(minCenter.Y, maxCenter.Y, tolerance))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(BooleanOperation.Subtract.ToString(), "cylinder cap centers are not vertically aligned in XY.");
            return false;
        }

        var centerX = minCenter.X;
        var centerY = minCenter.Y;
        var minZ = System.Math.Min(minCenter.Z, maxCenter.Z);
        var maxZ = System.Math.Max(minCenter.Z, maxCenter.Z);

        if (minZ > (box.MinZ + tolerance.Linear) || maxZ < (box.MaxZ - tolerance.Linear))
        {
            diagnostic = CreateNotFullySpanningDiagnostic(BooleanOperation.Subtract.ToString(), "cylinder does not fully span the box Z range.");
            return false;
        }

        var minFootprintX = centerX - cylinder.Radius;
        var maxFootprintX = centerX + cylinder.Radius;
        var minFootprintY = centerY - cylinder.Radius;
        var maxFootprintY = centerY + cylinder.Radius;

        var tangentContact =
            ToleranceMath.AlmostEqual(minFootprintX, box.MinX, tolerance)
            || ToleranceMath.AlmostEqual(maxFootprintX, box.MaxX, tolerance)
            || ToleranceMath.AlmostEqual(minFootprintY, box.MinY, tolerance)
            || ToleranceMath.AlmostEqual(maxFootprintY, box.MaxY, tolerance);
        if (tangentContact)
        {
            diagnostic = CreateTangentContactDiagnostic(BooleanOperation.Subtract.ToString(), "cylinder is tangent to the box XY boundary.");
            return false;
        }

        if (minFootprintX < (box.MinX - tolerance.Linear)
            || maxFootprintX > (box.MaxX + tolerance.Linear)
            || minFootprintY < (box.MinY - tolerance.Linear)
            || maxFootprintY > (box.MaxY + tolerance.Linear))
        {
            diagnostic = CreateRadiusExceedsBoundaryDiagnostic(BooleanOperation.Subtract.ToString(), "cylinder radial footprint exceeds the box XY boundary.");
            return false;
        }

        return true;
    }

    public static bool ValidateThroughHole(AxisAlignedBoxExtents box, in RecognizedCone cone, ToleranceContext tolerance, out string reason)
    {
        reason = string.Empty;

        var axis = cone.Axis.ToVector();
        if (!ToleranceMath.AlmostZero(axis.X, tolerance)
            || !ToleranceMath.AlmostZero(axis.Y, tolerance)
            || !ToleranceMath.AlmostEqual(double.Abs(axis.Z), 1d, tolerance))
        {
            reason = "cone axis is not aligned with the box Z axis.";
            return false;
        }

        var minCenter = cone.MinCenter;
        var maxCenter = cone.MaxCenter;
        if (!ToleranceMath.AlmostEqual(minCenter.X, maxCenter.X, tolerance) || !ToleranceMath.AlmostEqual(minCenter.Y, maxCenter.Y, tolerance))
        {
            reason = "cone cap centers are not vertically aligned in XY.";
            return false;
        }

        var centerX = minCenter.X;
        var centerY = minCenter.Y;
        var minZ = System.Math.Min(minCenter.Z, maxCenter.Z);
        var maxZ = System.Math.Max(minCenter.Z, maxCenter.Z);
        var maxRadius = cone.MaxRadius;

        if (minZ > (box.MinZ + tolerance.Linear) || maxZ < (box.MaxZ - tolerance.Linear))
        {
            reason = "cone does not fully span the box Z range.";
            return false;
        }

        if ((centerX - maxRadius) <= (box.MinX + tolerance.Linear)
            || (centerX + maxRadius) >= (box.MaxX - tolerance.Linear)
            || (centerY - maxRadius) <= (box.MinY + tolerance.Linear)
            || (centerY + maxRadius) >= (box.MaxY - tolerance.Linear))
        {
            reason = "cone radial footprint must stay strictly inside the box XY footprint.";
            return false;
        }

        return true;
    }

    public static BooleanDiagnostic CreateAxisNotAlignedDiagnostic(string operation, string detail)
        => new(
            BooleanDiagnosticCode.AxisNotAligned,
            $"Boolean {operation}: analytic hole candidate failed diagnostic AxisNotAligned ({detail}).",
            "BrepBoolean.AnalyticHole.AxisNotAligned");

    public static BooleanDiagnostic CreateNotFullySpanningDiagnostic(string operation, string detail)
        => new(
            BooleanDiagnosticCode.NotFullySpanning,
            $"Boolean {operation}: analytic hole candidate failed diagnostic NotFullySpanning ({detail}).",
            "BrepBoolean.AnalyticHole.NotFullySpanning");

    public static BooleanDiagnostic CreateRadiusExceedsBoundaryDiagnostic(string operation, string detail)
        => new(
            BooleanDiagnosticCode.RadiusExceedsBoundary,
            $"Boolean {operation}: analytic hole candidate failed diagnostic RadiusExceedsBoundary ({detail}).",
            "BrepBoolean.AnalyticHole.RadiusExceedsBoundary");

    public static BooleanDiagnostic CreateTangentContactDiagnostic(string operation, string detail)
        => new(
            BooleanDiagnosticCode.TangentContact,
            $"Boolean {operation}: analytic hole candidate failed diagnostic TangentContact ({detail}).",
            "BrepBoolean.AnalyticHole.TangentContact");

    public static BooleanDiagnostic CreateUnsupportedAnalyticSurfaceKindDiagnostic(string operation, AnalyticSurfaceKind kind)
        => new(
            BooleanDiagnosticCode.UnsupportedAnalyticSurfaceKind,
            $"Boolean {operation}: analytic hole surface kind '{kind}' is recognized but not implemented for M13 reconstruction.",
            "BrepBoolean.UnsupportedAnalyticSurfaceKind");
}
