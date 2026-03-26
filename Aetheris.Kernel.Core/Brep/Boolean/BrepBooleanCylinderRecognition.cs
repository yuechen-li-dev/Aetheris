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

    public static bool ValidateThroughHole(AxisAlignedBoxExtents box, in RecognizedCylinder cylinder, ToleranceContext tolerance, out BooleanDiagnostic? diagnostic, string? featureId = null)
    {
        diagnostic = null;

        if (!ValidateAxisAlignedZ(cylinder, tolerance))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "requires the cylinder axis to be parallel to world Z; the current cylinder axis is not aligned with the box Z axis. Rotate or redefine the tool so both cylinder caps stay on a world-Z through-hole axis.");
            return false;
        }

        var minCenter = cylinder.MinCenter;
        var maxCenter = cylinder.MaxCenter;
        if (!ToleranceMath.AlmostEqual(minCenter.X, maxCenter.X, tolerance) || !ToleranceMath.AlmostEqual(minCenter.Y, maxCenter.Y, tolerance))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "requires one vertical through-hole centerline; the cylinder cap centers drift in XY instead of staying on a single world-Z axis. Rebuild the tool so the cap centers share the same X/Y location.");
            return false;
        }

        var centerX = minCenter.X;
        var centerY = minCenter.Y;
        var minZ = System.Math.Min(minCenter.Z, maxCenter.Z);
        var maxZ = System.Math.Max(minCenter.Z, maxCenter.Z);

        if (minZ > (box.MinZ + tolerance.Linear) || maxZ < (box.MaxZ - tolerance.Linear))
        {
            diagnostic = CreateNotFullySpanningDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "does not reach both box boundary planes; partial-depth cylinder cuts are outside the supported safe boolean family. Extend the cylinder so it spans the full box Z range.");
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
            diagnostic = CreateTangentContactDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "is tangent to a box side wall; tangent analytic-hole cases are rejected to avoid zero-thickness geometry. Move the hole inward or reduce the radius.");
            return false;
        }

        if (minFootprintX < (box.MinX - tolerance.Linear)
            || maxFootprintX > (box.MaxX + tolerance.Linear)
            || minFootprintY < (box.MinY - tolerance.Linear)
            || maxFootprintY > (box.MaxY + tolerance.Linear))
        {
            diagnostic = CreateRadiusExceedsBoundaryDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "extends outside the box side-wall footprint. Reduce the cylinder radius or move the center farther inside the box XY boundary.");
            return false;
        }

        return true;
    }

    public static bool ValidateThroughHole(AxisAlignedBoxExtents box, in RecognizedCone cone, ToleranceContext tolerance, out BooleanDiagnostic? diagnostic, string? featureId = null)
    {
        diagnostic = null;

        var axis = cone.Axis.ToVector();
        if (!ToleranceMath.AlmostZero(axis.X, tolerance)
            || !ToleranceMath.AlmostZero(axis.Y, tolerance)
            || !ToleranceMath.AlmostEqual(double.Abs(axis.Z), 1d, tolerance))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "requires the cone axis to be parallel to world Z; the current cone axis is not aligned with the box Z axis. Rotate or redefine the tool so the cone reconstructs as a world-Z through-hole.");
            return false;
        }

        var minCenter = cone.MinCenter;
        var maxCenter = cone.MaxCenter;
        if (!ToleranceMath.AlmostEqual(minCenter.X, maxCenter.X, tolerance) || !ToleranceMath.AlmostEqual(minCenter.Y, maxCenter.Y, tolerance))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "requires one vertical through-hole centerline; the cone boundary circles do not share a single X/Y center. Rebuild the cone so both boundary sections stay on one world-Z axis.");
            return false;
        }

        var bottomAxisParameter = AxisParameterAtZ(cone, box.MinZ, tolerance);
        var topAxisParameter = AxisParameterAtZ(cone, box.MaxZ, tolerance);

        if (bottomAxisParameter < (cone.MinAxisParameter - tolerance.Linear)
            || bottomAxisParameter > (cone.MaxAxisParameter + tolerance.Linear)
            || topAxisParameter < (cone.MinAxisParameter - tolerance.Linear)
            || topAxisParameter > (cone.MaxAxisParameter + tolerance.Linear))
        {
            diagnostic = CreateNotFullySpanningDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "does not reach both box boundary planes; partial-depth cone cuts are outside the supported safe boolean family. Extend the cone so it spans the full box Z range.");
            return false;
        }

        if (bottomAxisParameter <= tolerance.Linear || topAxisParameter <= tolerance.Linear)
        {
            diagnostic = CreateDegenerateBoundarySectionDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "cannot produce circular sections on both box boundary planes because the apex or cone termination lands inside the box span. Move the apex outside the box span or lengthen the cone.");
            return false;
        }

        var centerX = cone.AxisOrigin.X;
        var centerY = cone.AxisOrigin.Y;
        var bottomRadius = cone.RadiusAtAxisParameter(bottomAxisParameter);
        var topRadius = cone.RadiusAtAxisParameter(topAxisParameter);

        if (bottomRadius <= tolerance.Linear || topRadius <= tolerance.Linear)
        {
            diagnostic = CreateDegenerateBoundarySectionDiagnostic(BooleanOperation.Subtract.ToString(), featureId, "requires non-degenerate circular sections where the cone meets the two box boundary planes. Increase the boundary radii at those planes by moving the apex farther away or changing the cone taper.");
            return false;
        }

        if (!ValidateCircleInsideBoxFootprint(box, centerX, centerY, bottomRadius, tolerance, out diagnostic, "bottom boundary circle", featureId)
            || !ValidateCircleInsideBoxFootprint(box, centerX, centerY, topRadius, tolerance, out diagnostic, "top boundary circle", featureId))
        {
            return false;
        }

        return true;
    }

    private static double AxisParameterAtZ(in RecognizedCone cone, double z, ToleranceContext tolerance)
    {
        var axisZ = cone.Axis.ToVector().Z;
        if (ToleranceMath.AlmostZero(axisZ, tolerance))
        {
            throw new InvalidOperationException("AxisParameterAtZ requires a cone axis aligned with Z.");
        }

        return (z - cone.AxisOrigin.Z) / axisZ;
    }

    private static bool ValidateCircleInsideBoxFootprint(
        AxisAlignedBoxExtents box,
        double centerX,
        double centerY,
        double radius,
        ToleranceContext tolerance,
        out BooleanDiagnostic? diagnostic,
        string circleLabel,
        string? featureId)
    {
        diagnostic = null;

        var minFootprintX = centerX - radius;
        var maxFootprintX = centerX + radius;
        var minFootprintY = centerY - radius;
        var maxFootprintY = centerY + radius;

        var tangentContact =
            ToleranceMath.AlmostEqual(minFootprintX, box.MinX, tolerance)
            || ToleranceMath.AlmostEqual(maxFootprintX, box.MaxX, tolerance)
            || ToleranceMath.AlmostEqual(minFootprintY, box.MinY, tolerance)
            || ToleranceMath.AlmostEqual(maxFootprintY, box.MaxY, tolerance);
        if (tangentContact)
        {
            diagnostic = CreateTangentContactDiagnostic(BooleanOperation.Subtract.ToString(), featureId, $"has {circleLabel} tangent to a box side wall; tangent analytic-hole cases are rejected to avoid zero-thickness geometry. Move the cone inward or reduce the boundary radius at that plane.");
            return false;
        }

        if (minFootprintX < (box.MinX - tolerance.Linear)
            || maxFootprintX > (box.MaxX + tolerance.Linear)
            || minFootprintY < (box.MinY - tolerance.Linear)
            || maxFootprintY > (box.MaxY + tolerance.Linear))
        {
            diagnostic = CreateRadiusExceedsBoundaryDiagnostic(BooleanOperation.Subtract.ToString(), featureId, $"has {circleLabel} extending outside the box side-wall footprint. Reduce the boundary radius or move the cone center farther inside the box XY boundary.");
            return false;
        }

        return true;
    }

    public static BooleanDiagnostic CreateAxisNotAlignedDiagnostic(string operation, string detail)
        => CreateAxisNotAlignedDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateAxisNotAlignedDiagnostic(string operation, string? featureId, string detail)
        => new(
            BooleanDiagnosticCode.AxisNotAligned,
            CreateBooleanMessage(operation, featureId, detail),
            "BrepBoolean.AnalyticHole.AxisNotAligned");

    public static BooleanDiagnostic CreateNotFullySpanningDiagnostic(string operation, string detail)
        => CreateNotFullySpanningDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateNotFullySpanningDiagnostic(string operation, string? featureId, string detail)
        => new(
            BooleanDiagnosticCode.NotFullySpanning,
            CreateBooleanMessage(operation, featureId, detail),
            "BrepBoolean.AnalyticHole.NotFullySpanning");

    public static BooleanDiagnostic CreateDegenerateBoundarySectionDiagnostic(string operation, string? featureId, string detail)
        => new(
            BooleanDiagnosticCode.DegenerateBoundarySection,
            CreateBooleanMessage(operation, featureId, detail),
            "BrepBoolean.AnalyticHole.DegenerateBoundarySection");

    public static BooleanDiagnostic CreateRadiusExceedsBoundaryDiagnostic(string operation, string detail)
        => CreateRadiusExceedsBoundaryDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateRadiusExceedsBoundaryDiagnostic(string operation, string? featureId, string detail)
        => new(
            BooleanDiagnosticCode.RadiusExceedsBoundary,
            CreateBooleanMessage(operation, featureId, detail),
            "BrepBoolean.AnalyticHole.RadiusExceedsBoundary");

    public static BooleanDiagnostic CreateTangentContactDiagnostic(string operation, string detail)
        => CreateTangentContactDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateTangentContactDiagnostic(string operation, string? featureId, string detail)
        => new(
            BooleanDiagnosticCode.TangentContact,
            CreateBooleanMessage(operation, featureId, detail),
            "BrepBoolean.AnalyticHole.TangentContact");

    public static BooleanDiagnostic CreateMultiBodyResultDiagnostic(string operation, string? featureId, string detail)
        => new(
            BooleanDiagnosticCode.MultiBodyResult,
            CreateBooleanMessage(operation, featureId, detail),
            "BrepBoolean.AnalyticHole.MultiBodyResult");

    public static BooleanDiagnostic CreateUnsupportedAnalyticSurfaceKindDiagnostic(string operation, AnalyticSurfaceKind kind)
        => CreateUnsupportedAnalyticSurfaceKindDiagnostic(operation, kind, null);

    public static BooleanDiagnostic CreateUnsupportedAnalyticSurfaceKindDiagnostic(string operation, AnalyticSurfaceKind kind, string? featureId)
        => new(
            BooleanDiagnosticCode.UnsupportedAnalyticSurfaceKind,
            CreateBooleanMessage(operation, featureId, $"does not support analytic tool surface kind '{kind}' in the safe boolean family. Use a cylinder or cone through-hole instead."),
            "BrepBoolean.UnsupportedAnalyticSurfaceKind");

    public static string CreateBooleanMessage(string operation, string? featureId, string detail)
    {
        var subject = string.IsNullOrWhiteSpace(featureId)
            ? $"Boolean {operation}"
            : $"Boolean feature '{featureId}' ({operation.ToLowerInvariant()})";
        return $"{subject} {detail}";
    }
}
