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

public readonly record struct SupportedSubtractProfile(
    SupportedBooleanHoleSpanKind SpanKind,
    double CenterX,
    double CenterY,
    Point3D StartCenter,
    Point3D EndCenter,
    Direction3D Axis,
    Direction3D ReferenceAxis,
    double StartZ,
    double EndZ,
    double StartRadius,
    double EndRadius);

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
        var success = TryValidateCylinderSubtractProfile(box, cylinder, tolerance, out var profile, out diagnostic, featureId);
        return success && profile.SpanKind == SupportedBooleanHoleSpanKind.Through;
    }

    public static bool TryValidateCylinderSubtractProfile(AxisAlignedBoxExtents box, in RecognizedCylinder cylinder, ToleranceContext tolerance, out SupportedSubtractProfile profile, out BooleanDiagnostic? diagnostic, string? featureId = null)
    {
        profile = default;
        diagnostic = null;
        var diagnosticContext = new BooleanDiagnosticContext(BooleanOperation.Subtract, featureId);
        if (!TryResolveBoundaryAxisParameter(cylinder.AxisOrigin, cylinder.Axis, box.MinZ, tolerance, out var bottomAtZ)
            || !TryResolveBoundaryAxisParameter(cylinder.AxisOrigin, cylinder.Axis, box.MaxZ, tolerance, out var topAtZ))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(diagnosticContext, "does not match the bounded arbitrary-axis subtract subset because the tool axis is nearly parallel to the box top/bottom planes.");
            return false;
        }

        var coversBottom = bottomAtZ >= (cylinder.MinAxisParameter - tolerance.Linear) && bottomAtZ <= (cylinder.MaxAxisParameter + tolerance.Linear);
        var coversTop = topAtZ >= (cylinder.MinAxisParameter - tolerance.Linear) && topAtZ <= (cylinder.MaxAxisParameter + tolerance.Linear);
        var bottomCenter = cylinder.AxisOrigin + (cylinder.Axis.ToVector() * bottomAtZ);
        var topCenter = cylinder.AxisOrigin + (cylinder.Axis.ToVector() * topAtZ);
        var averageCenter = new Point3D((bottomCenter.X + topCenter.X) * 0.5d, (bottomCenter.Y + topCenter.Y) * 0.5d, (bottomCenter.Z + topCenter.Z) * 0.5d);
        var radialBound = cylinder.Radius / System.Math.Abs(cylinder.Axis.ToVector().Z);

        if (coversBottom && coversTop)
        {
            if (!ValidateCircleInsideBoxFootprint(box, bottomCenter.X, bottomCenter.Y, radialBound, tolerance, out diagnostic, "bottom boundary section", diagnosticContext)
                || !ValidateCircleInsideBoxFootprint(box, topCenter.X, topCenter.Y, radialBound, tolerance, out diagnostic, "top boundary section", diagnosticContext))
            {
                return false;
            }

            profile = new SupportedSubtractProfile(SupportedBooleanHoleSpanKind.Through, averageCenter.X, averageCenter.Y, bottomCenter, topCenter, cylinder.Axis, ResolveReferenceAxis(cylinder.Axis), box.MinZ, box.MaxZ, cylinder.Radius, cylinder.Radius);
            return true;
        }

        if (coversTop)
        {
            if (!ValidateAxisAlignedZ(cylinder, tolerance))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "currently supports arbitrary-axis cylinder subtract only for through-holes; blind arbitrary-axis cylinder holes remain deferred in this milestone.");
                return false;
            }

            var termination = cylinder.MinCenter.Z < cylinder.MaxCenter.Z ? cylinder.MinCenter : cylinder.MaxCenter;
            if (termination.Z <= (box.MinZ + tolerance.Linear) || termination.Z >= (box.MaxZ - tolerance.Linear))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "does not match the supported subtract span family; cylinder blind holes must terminate strictly inside the box.");
                return false;
            }

            if (!ValidateCircleInsideBoxFootprint(box, topCenter.X, topCenter.Y, radialBound, tolerance, out diagnostic, "top boundary section", diagnosticContext)
                || !ValidateCircleInsideBoxFootprint(box, termination.X, termination.Y, cylinder.Radius, tolerance, out diagnostic, "blind bottom circle", diagnosticContext))
            {
                return false;
            }

            profile = new SupportedSubtractProfile(SupportedBooleanHoleSpanKind.BlindFromTop, topCenter.X, topCenter.Y, topCenter, termination, cylinder.Axis, ResolveReferenceAxis(cylinder.Axis), box.MaxZ, termination.Z, cylinder.Radius, cylinder.Radius);
            return true;
        }

        if (coversBottom)
        {
            if (!ValidateAxisAlignedZ(cylinder, tolerance))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "currently supports arbitrary-axis cylinder subtract only for through-holes; blind arbitrary-axis cylinder holes remain deferred in this milestone.");
                return false;
            }

            var termination = cylinder.MinCenter.Z > cylinder.MaxCenter.Z ? cylinder.MinCenter : cylinder.MaxCenter;
            if (termination.Z <= (box.MinZ + tolerance.Linear) || termination.Z >= (box.MaxZ - tolerance.Linear))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "does not match the supported subtract span family; cylinder blind holes must terminate strictly inside the box.");
                return false;
            }

            if (!ValidateCircleInsideBoxFootprint(box, bottomCenter.X, bottomCenter.Y, radialBound, tolerance, out diagnostic, "bottom boundary section", diagnosticContext)
                || !ValidateCircleInsideBoxFootprint(box, termination.X, termination.Y, cylinder.Radius, tolerance, out diagnostic, "blind bottom circle", diagnosticContext))
            {
                return false;
            }

            profile = new SupportedSubtractProfile(SupportedBooleanHoleSpanKind.BlindFromBottom, bottomCenter.X, bottomCenter.Y, bottomCenter, termination, cylinder.Axis, ResolveReferenceAxis(cylinder.Axis), box.MinZ, termination.Z, cylinder.Radius, cylinder.Radius);
            return true;
        }

        diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "does not match the supported subtract span family; only through-holes or one-sided blind holes are allowed in this milestone.");
        return false;
    }

    public static bool ValidateThroughHole(AxisAlignedBoxExtents box, in RecognizedCone cone, ToleranceContext tolerance, out BooleanDiagnostic? diagnostic, string? featureId = null)
    {
        var success = TryValidateConeSubtractProfile(box, cone, tolerance, out var profile, out diagnostic, featureId);
        return success && profile.SpanKind == SupportedBooleanHoleSpanKind.Through;
    }

    public static bool TryValidateConeSubtractProfile(AxisAlignedBoxExtents box, in RecognizedCone cone, ToleranceContext tolerance, out SupportedSubtractProfile profile, out BooleanDiagnostic? diagnostic, string? featureId = null)
    {
        profile = default;
        diagnostic = null;
        var diagnosticContext = new BooleanDiagnosticContext(BooleanOperation.Subtract, featureId);
        if (!TryResolveBoundaryAxisParameter(cone.AxisOrigin, cone.Axis, box.MinZ, tolerance, out var bottomAxisParameter)
            || !TryResolveBoundaryAxisParameter(cone.AxisOrigin, cone.Axis, box.MaxZ, tolerance, out var topAxisParameter))
        {
            diagnostic = CreateAxisNotAlignedDiagnostic(diagnosticContext, "does not match the bounded arbitrary-axis subtract subset because the tool axis is nearly parallel to the box top/bottom planes.");
            return false;
        }

        var bottomCenter = cone.PointAtAxisParameter(bottomAxisParameter);
        var topCenter = cone.PointAtAxisParameter(topAxisParameter);
        var centerX = (bottomCenter.X + topCenter.X) * 0.5d;
        var centerY = (bottomCenter.Y + topCenter.Y) * 0.5d;
        var coversBottom = bottomAxisParameter >= (cone.MinAxisParameter - tolerance.Linear)
            && bottomAxisParameter <= (cone.MaxAxisParameter + tolerance.Linear);
        var coversTop = topAxisParameter >= (cone.MinAxisParameter - tolerance.Linear)
            && topAxisParameter <= (cone.MaxAxisParameter + tolerance.Linear);
        var axisZ = System.Math.Abs(cone.Axis.ToVector().Z);

        if (coversBottom && coversTop)
        {
            var bottomRadius = cone.RadiusAtAxisParameter(bottomAxisParameter);
            var topRadius = cone.RadiusAtAxisParameter(topAxisParameter);
            if (bottomRadius <= tolerance.Linear || topRadius <= tolerance.Linear)
            {
                diagnostic = CreateDegenerateBoundarySectionDiagnostic(diagnosticContext, "requires non-degenerate circular sections where the cone meets the two box boundary planes. Increase the boundary radii at those planes by moving the apex farther away or changing the cone taper.");
                return false;
            }

            if (!ValidateCircleInsideBoxFootprint(box, bottomCenter.X, bottomCenter.Y, bottomRadius / axisZ, tolerance, out diagnostic, "bottom boundary circle", diagnosticContext)
                || !ValidateCircleInsideBoxFootprint(box, topCenter.X, topCenter.Y, topRadius / axisZ, tolerance, out diagnostic, "top boundary circle", diagnosticContext))
            {
                return false;
            }

            profile = new SupportedSubtractProfile(SupportedBooleanHoleSpanKind.Through, centerX, centerY, bottomCenter, topCenter, cone.Axis, ResolveReferenceAxis(cone.Axis), box.MinZ, box.MaxZ, bottomRadius, topRadius);
            return true;
        }

        if (coversTop && !coversBottom)
        {
            if (!IsAxisAlignedWithWorldZ(cone.Axis, tolerance))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "currently supports arbitrary-axis cone subtract only for through-holes; blind arbitrary-axis cone holes remain deferred in this milestone.");
                return false;
            }

            var minEndIsLower = cone.MinCenter.Z <= cone.MaxCenter.Z;
            var terminationZ = System.Math.Min(cone.MinCenter.Z, cone.MaxCenter.Z);
            if (terminationZ <= (box.MinZ + tolerance.Linear) || terminationZ >= (box.MaxZ - tolerance.Linear))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "does not match the supported subtract span family; cone blind holes must terminate strictly inside the box.");
                return false;
            }

            var topRadius = cone.RadiusAtAxisParameter(topAxisParameter);
            var endRadius = minEndIsLower ? cone.RadiusAtMinAxisParameter : cone.RadiusAtMaxAxisParameter;
            if (topRadius <= tolerance.Linear || endRadius <= tolerance.Linear)
            {
                diagnostic = CreateDegenerateBoundarySectionDiagnostic(diagnosticContext, "requires non-degenerate entry and bottom sections for supported cone blind holes.");
                return false;
            }

            var endCenter = minEndIsLower ? cone.MinCenter : cone.MaxCenter;
            if (!ValidateCircleInsideBoxFootprint(box, topCenter.X, topCenter.Y, topRadius / axisZ, tolerance, out diagnostic, "top boundary circle", diagnosticContext)
                || !ValidateCircleInsideBoxFootprint(box, endCenter.X, endCenter.Y, endRadius, tolerance, out diagnostic, "blind bottom circle", diagnosticContext))
            {
                return false;
            }

            profile = new SupportedSubtractProfile(SupportedBooleanHoleSpanKind.BlindFromTop, topCenter.X, topCenter.Y, topCenter, endCenter, cone.Axis, ResolveReferenceAxis(cone.Axis), box.MaxZ, terminationZ, topRadius, endRadius);
            return true;
        }

        if (coversBottom && !coversTop)
        {
            if (!IsAxisAlignedWithWorldZ(cone.Axis, tolerance))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "currently supports arbitrary-axis cone subtract only for through-holes; blind arbitrary-axis cone holes remain deferred in this milestone.");
                return false;
            }

            var maxEndIsHigher = cone.MaxCenter.Z >= cone.MinCenter.Z;
            var terminationZ = System.Math.Max(cone.MinCenter.Z, cone.MaxCenter.Z);
            if (terminationZ <= (box.MinZ + tolerance.Linear) || terminationZ >= (box.MaxZ - tolerance.Linear))
            {
                diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "does not match the supported subtract span family; cone blind holes must terminate strictly inside the box.");
                return false;
            }

            var bottomRadius = cone.RadiusAtAxisParameter(bottomAxisParameter);
            var endRadius = maxEndIsHigher ? cone.RadiusAtMaxAxisParameter : cone.RadiusAtMinAxisParameter;
            if (bottomRadius <= tolerance.Linear || endRadius <= tolerance.Linear)
            {
                diagnostic = CreateDegenerateBoundarySectionDiagnostic(diagnosticContext, "requires non-degenerate entry and bottom sections for supported cone blind holes.");
                return false;
            }

            var endCenter = maxEndIsHigher ? cone.MaxCenter : cone.MinCenter;
            if (!ValidateCircleInsideBoxFootprint(box, bottomCenter.X, bottomCenter.Y, bottomRadius / axisZ, tolerance, out diagnostic, "bottom boundary circle", diagnosticContext)
                || !ValidateCircleInsideBoxFootprint(box, endCenter.X, endCenter.Y, endRadius, tolerance, out diagnostic, "blind bottom circle", diagnosticContext))
            {
                return false;
            }

            profile = new SupportedSubtractProfile(SupportedBooleanHoleSpanKind.BlindFromBottom, bottomCenter.X, bottomCenter.Y, bottomCenter, endCenter, cone.Axis, ResolveReferenceAxis(cone.Axis), box.MinZ, terminationZ, bottomRadius, endRadius);
            return true;
        }

        diagnostic = CreateNotFullySpanningDiagnostic(diagnosticContext, "does not match the supported subtract span family; only through-holes or one-sided blind holes are allowed in this milestone.");
        return false;
    }

    private static bool TryResolveBoundaryAxisParameter(Point3D axisOrigin, Direction3D axis, double z, ToleranceContext tolerance, out double axisParameter)
    {
        var axisZ = axis.ToVector().Z;
        if (ToleranceMath.AlmostZero(axisZ, tolerance))
        {
            axisParameter = 0d;
            return false;
        }

        axisParameter = (z - axisOrigin.Z) / axisZ;
        return true;
    }

    private static Direction3D ResolveReferenceAxis(Direction3D axis)
    {
        var axisVector = axis.ToVector();
        var seed = System.Math.Abs(axisVector.Z) < 0.9d
            ? new Vector3D(0d, 0d, 1d)
            : new Vector3D(1d, 0d, 0d);
        var projected = seed - (axisVector * seed.Dot(axisVector));
        return Direction3D.Create(projected);
    }

    private static bool IsAxisAlignedWithWorldZ(Direction3D axis, ToleranceContext tolerance)
    {
        var v = axis.ToVector();
        return ToleranceMath.AlmostZero(v.X, tolerance)
            && ToleranceMath.AlmostZero(v.Y, tolerance)
            && ToleranceMath.AlmostEqual(System.Math.Abs(v.Z), 1d, tolerance);
    }

    private static double AxisParameterAtZ(in RecognizedCone cone, double z, ToleranceContext tolerance)
    {
        _ = tolerance;
        var axisZ = cone.Axis.ToVector().Z;
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
        in BooleanDiagnosticContext diagnosticContext)
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
            diagnostic = CreateTangentContactDiagnostic(diagnosticContext, $"has {circleLabel} tangent to a box side wall; tangent analytic-hole cases are rejected to avoid zero-thickness geometry. Move the cone inward or reduce the boundary radius at that plane.");
            return false;
        }

        if (minFootprintX < (box.MinX - tolerance.Linear)
            || maxFootprintX > (box.MaxX + tolerance.Linear)
            || minFootprintY < (box.MinY - tolerance.Linear)
            || maxFootprintY > (box.MaxY + tolerance.Linear))
        {
            diagnostic = CreateRadiusExceedsBoundaryDiagnostic(diagnosticContext, $"has {circleLabel} extending outside the box side-wall footprint. Reduce the boundary radius or move the cone center farther inside the box XY boundary.");
            return false;
        }

        return true;
    }

    public static BooleanDiagnostic CreateAxisNotAlignedDiagnostic(string operation, string detail)
        => CreateAxisNotAlignedDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateAxisNotAlignedDiagnostic(string operation, string? featureId, string detail)
        => CreateAxisNotAlignedDiagnostic(CreateContext(operation, featureId), detail);

    public static BooleanDiagnostic CreateAxisNotAlignedDiagnostic(in BooleanDiagnosticContext context, string detail)
        => context.Error(BooleanDiagnosticCode.AxisNotAligned, detail, "BrepBoolean.AnalyticHole.AxisNotAligned");

    public static BooleanDiagnostic CreateNotFullySpanningDiagnostic(string operation, string detail)
        => CreateNotFullySpanningDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateNotFullySpanningDiagnostic(string operation, string? featureId, string detail)
        => CreateNotFullySpanningDiagnostic(CreateContext(operation, featureId), detail);

    public static BooleanDiagnostic CreateNotFullySpanningDiagnostic(in BooleanDiagnosticContext context, string detail)
        => context.Error(BooleanDiagnosticCode.NotFullySpanning, detail, "BrepBoolean.AnalyticHole.NotFullySpanning");

    public static BooleanDiagnostic CreateDegenerateBoundarySectionDiagnostic(string operation, string? featureId, string detail)
        => CreateDegenerateBoundarySectionDiagnostic(CreateContext(operation, featureId), detail);

    public static BooleanDiagnostic CreateDegenerateBoundarySectionDiagnostic(in BooleanDiagnosticContext context, string detail)
        => context.Error(BooleanDiagnosticCode.DegenerateBoundarySection, detail, "BrepBoolean.AnalyticHole.DegenerateBoundarySection");

    public static BooleanDiagnostic CreateRadiusExceedsBoundaryDiagnostic(string operation, string detail)
        => CreateRadiusExceedsBoundaryDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateRadiusExceedsBoundaryDiagnostic(string operation, string? featureId, string detail)
        => CreateRadiusExceedsBoundaryDiagnostic(CreateContext(operation, featureId), detail);

    public static BooleanDiagnostic CreateRadiusExceedsBoundaryDiagnostic(in BooleanDiagnosticContext context, string detail)
        => context.Error(BooleanDiagnosticCode.RadiusExceedsBoundary, detail, "BrepBoolean.AnalyticHole.RadiusExceedsBoundary");

    public static BooleanDiagnostic CreateTangentContactDiagnostic(string operation, string detail)
        => CreateTangentContactDiagnostic(operation, null, detail);

    public static BooleanDiagnostic CreateTangentContactDiagnostic(string operation, string? featureId, string detail)
        => CreateTangentContactDiagnostic(CreateContext(operation, featureId), detail);

    public static BooleanDiagnostic CreateTangentContactDiagnostic(in BooleanDiagnosticContext context, string detail)
        => context.Error(BooleanDiagnosticCode.TangentContact, detail, "BrepBoolean.AnalyticHole.TangentContact");

    public static BooleanDiagnostic CreateMultiBodyResultDiagnostic(string operation, string? featureId, string detail)
        => CreateMultiBodyResultDiagnostic(CreateContext(operation, featureId), detail);

    public static BooleanDiagnostic CreateMultiBodyResultDiagnostic(in BooleanDiagnosticContext context, string detail)
        => context.Error(BooleanDiagnosticCode.MultiBodyResult, detail, "BrepBoolean.AnalyticHole.MultiBodyResult");

    public static BooleanDiagnostic CreateUnsupportedAnalyticSurfaceKindDiagnostic(string operation, AnalyticSurfaceKind kind)
        => CreateUnsupportedAnalyticSurfaceKindDiagnostic(operation, kind, null);

    public static BooleanDiagnostic CreateUnsupportedAnalyticSurfaceKindDiagnostic(string operation, AnalyticSurfaceKind kind, string? featureId)
        => CreateUnsupportedAnalyticSurfaceKindDiagnostic(CreateContext(operation, featureId), kind);

    public static BooleanDiagnostic CreateUnsupportedAnalyticSurfaceKindDiagnostic(in BooleanDiagnosticContext context, AnalyticSurfaceKind kind)
        => context.Error(
            BooleanDiagnosticCode.UnsupportedAnalyticSurfaceKind,
            $"does not support analytic tool surface kind '{kind}' in the safe boolean family. Use a cylinder or cone through-hole instead.",
            "BrepBoolean.UnsupportedAnalyticSurfaceKind");

    public static string CreateBooleanMessage(string operation, string? featureId, string detail)
        => CreateContext(operation, featureId).FormatMessage(detail);

    private static BooleanDiagnosticContext CreateContext(string operation, string? featureId)
        => Enum.TryParse<BooleanOperation>(operation, ignoreCase: true, out var parsedOperation)
            ? new BooleanDiagnosticContext(parsedOperation, featureId)
            : new BooleanDiagnosticContext(BooleanOperation.Subtract, featureId);
}
