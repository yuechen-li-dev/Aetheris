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

    public static bool ValidateThroughHole(AxisAlignedBoxExtents box, in RecognizedCylinder cylinder, ToleranceContext tolerance, out string reason)
    {
        reason = string.Empty;

        if (!ValidateAxisAlignedZ(cylinder, tolerance))
        {
            reason = "cylinder axis is not aligned with the box Z axis.";
            return false;
        }

        var minCenter = cylinder.MinCenter;
        var maxCenter = cylinder.MaxCenter;
        if (!ToleranceMath.AlmostEqual(minCenter.X, maxCenter.X, tolerance) || !ToleranceMath.AlmostEqual(minCenter.Y, maxCenter.Y, tolerance))
        {
            reason = "cylinder cap centers are not vertically aligned in XY.";
            return false;
        }

        var centerX = minCenter.X;
        var centerY = minCenter.Y;
        var minZ = System.Math.Min(minCenter.Z, maxCenter.Z);
        var maxZ = System.Math.Max(minCenter.Z, maxCenter.Z);

        if (minZ > (box.MinZ + tolerance.Linear) || maxZ < (box.MaxZ - tolerance.Linear))
        {
            reason = "cylinder does not fully span the box Z range.";
            return false;
        }

        if ((centerX - cylinder.Radius) <= (box.MinX + tolerance.Linear)
            || (centerX + cylinder.Radius) >= (box.MaxX - tolerance.Linear)
            || (centerY - cylinder.Radius) <= (box.MinY + tolerance.Linear)
            || (centerY + cylinder.Radius) >= (box.MaxY - tolerance.Linear))
        {
            reason = "cylinder radial footprint must stay strictly inside the box XY footprint.";
            return false;
        }

        return true;
    }

    public static KernelDiagnostic CreateUnsupportedThroughHoleDiagnostic(string operation, string reason)
        => new(
            KernelDiagnosticCode.NotImplemented,
            KernelDiagnosticSeverity.Error,
            $"Boolean {operation}: box-cylinder subtract only supports a strict Z-aligned through-hole subset ({reason}).",
            Source: "BrepBoolean.RebuildResult");
}
