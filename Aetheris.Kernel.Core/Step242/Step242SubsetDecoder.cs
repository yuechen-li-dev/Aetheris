using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

internal static class Step242SubsetDecoder
{
    public static KernelResult<Step242EntityReference> ReadReference(Step242ParsedEntity entity, int argumentIndex, string context)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return FailureRef($"{context}: missing argument at index {argumentIndex}.", $"Entity:{entity.Id}");
        }

        if (entity.Arguments[argumentIndex] is not Step242EntityReference reference)
        {
            return FailureRef($"{context}: expected entity reference argument.", $"Entity:{entity.Id}");
        }

        return KernelResult<Step242EntityReference>.Success(reference);
    }

    public static KernelResult<IReadOnlyList<Step242EntityReference>> ReadReferenceList(Step242ParsedEntity entity, int argumentIndex, string context)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return FailureList($"{context}: missing argument at index {argumentIndex}.", $"Entity:{entity.Id}");
        }

        if (entity.Arguments[argumentIndex] is not Step242ListValue list)
        {
            return FailureList($"{context}: expected list argument.", $"Entity:{entity.Id}");
        }

        var refs = new List<Step242EntityReference>(list.Items.Count);
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is not Step242EntityReference reference)
            {
                return FailureList($"{context}: list item {i} must be an entity reference.", $"Entity:{entity.Id}");
            }

            refs.Add(reference);
        }

        return KernelResult<IReadOnlyList<Step242EntityReference>>.Success(refs);
    }

    public static KernelResult<bool> ReadBoolean(Step242ParsedEntity entity, int argumentIndex, string context)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return FailureBool($"{context}: missing argument at index {argumentIndex}.", $"Entity:{entity.Id}");
        }

        if (entity.Arguments[argumentIndex] is not Step242BooleanValue value)
        {
            return FailureBool($"{context}: expected boolean logical (.T. / .F.).", $"Entity:{entity.Id}");
        }

        return KernelResult<bool>.Success(value.Value);
    }

    public static KernelResult<Point3D> ReadVertexPoint(Step242ParsedDocument document, int vertexPointEntityId)
    {
        var vertexPointEntityResult = document.TryGetEntity(vertexPointEntityId, "VERTEX_POINT");
        if (!vertexPointEntityResult.IsSuccess)
        {
            return KernelResult<Point3D>.Failure(vertexPointEntityResult.Diagnostics);
        }

        var pointRefResult = ReadReference(vertexPointEntityResult.Value, 1, "VERTEX_POINT point");
        if (!pointRefResult.IsSuccess)
        {
            return KernelResult<Point3D>.Failure(pointRefResult.Diagnostics);
        }

        var pointEntityResult = document.TryGetEntity(pointRefResult.Value.TargetId, "CARTESIAN_POINT");
        if (!pointEntityResult.IsSuccess)
        {
            return KernelResult<Point3D>.Failure(pointEntityResult.Diagnostics);
        }

        return ReadCartesianPoint(pointEntityResult.Value, "VERTEX_POINT point");
    }

    public static KernelResult<Line3Curve> ReadLineCurve(Step242ParsedDocument document, Step242ParsedEntity lineEntity)
    {
        var originRefResult = ReadReference(lineEntity, 1, "LINE origin");
        if (!originRefResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(originRefResult.Diagnostics);
        }

        var originEntityResult = document.TryGetEntity(originRefResult.Value.TargetId, "CARTESIAN_POINT");
        if (!originEntityResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(originEntityResult.Diagnostics);
        }

        var originResult = ReadCartesianPoint(originEntityResult.Value, "LINE origin point");
        if (!originResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(originResult.Diagnostics);
        }

        var vectorRefResult = ReadReference(lineEntity, 2, "LINE vector");
        if (!vectorRefResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(vectorRefResult.Diagnostics);
        }

        var vectorEntityResult = document.TryGetEntity(vectorRefResult.Value.TargetId, "VECTOR");
        if (!vectorEntityResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(vectorEntityResult.Diagnostics);
        }

        var directionRefResult = ReadReference(vectorEntityResult.Value, 1, "VECTOR direction");
        if (!directionRefResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(directionRefResult.Diagnostics);
        }

        var directionEntityResult = document.TryGetEntity(directionRefResult.Value.TargetId, "DIRECTION");
        if (!directionEntityResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(directionEntityResult.Diagnostics);
        }

        var directionResult = ReadDirection(directionEntityResult.Value, "VECTOR direction");
        if (!directionResult.IsSuccess)
        {
            return KernelResult<Line3Curve>.Failure(directionResult.Diagnostics);
        }

        return KernelResult<Line3Curve>.Success(new Line3Curve(originResult.Value, directionResult.Value));
    }

    public static KernelResult<PlaneSurface> ReadPlaneSurface(Step242ParsedDocument document, Step242ParsedEntity planeEntity)
    {
        var placementRefResult = ReadReference(planeEntity, 1, "PLANE position");
        if (!placementRefResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(placementRefResult.Diagnostics);
        }

        var placementEntityResult = document.TryGetEntity(placementRefResult.Value.TargetId, "AXIS2_PLACEMENT_3D");
        if (!placementEntityResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(placementEntityResult.Diagnostics);
        }

        var originRefResult = ReadReference(placementEntityResult.Value, 1, "AXIS2_PLACEMENT_3D origin");
        if (!originRefResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(originRefResult.Diagnostics);
        }

        var originEntityResult = document.TryGetEntity(originRefResult.Value.TargetId, "CARTESIAN_POINT");
        if (!originEntityResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(originEntityResult.Diagnostics);
        }

        var originResult = ReadCartesianPoint(originEntityResult.Value, "AXIS2_PLACEMENT_3D origin");
        if (!originResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(originResult.Diagnostics);
        }

        var normalRefResult = ReadReference(placementEntityResult.Value, 2, "AXIS2_PLACEMENT_3D axis");
        if (!normalRefResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(normalRefResult.Diagnostics);
        }

        var normalEntityResult = document.TryGetEntity(normalRefResult.Value.TargetId, "DIRECTION");
        if (!normalEntityResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(normalEntityResult.Diagnostics);
        }

        var normalResult = ReadDirection(normalEntityResult.Value, "AXIS2_PLACEMENT_3D axis");
        if (!normalResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(normalResult.Diagnostics);
        }

        var refDirRefResult = ReadReference(placementEntityResult.Value, 3, "AXIS2_PLACEMENT_3D ref direction");
        if (!refDirRefResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(refDirRefResult.Diagnostics);
        }

        var refDirEntityResult = document.TryGetEntity(refDirRefResult.Value.TargetId, "DIRECTION");
        if (!refDirEntityResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(refDirEntityResult.Diagnostics);
        }

        var refDirResult = ReadDirection(refDirEntityResult.Value, "AXIS2_PLACEMENT_3D ref direction");
        if (!refDirResult.IsSuccess)
        {
            return KernelResult<PlaneSurface>.Failure(refDirResult.Diagnostics);
        }

        try
        {
            return KernelResult<PlaneSurface>.Success(new PlaneSurface(originResult.Value, normalResult.Value, refDirResult.Value));
        }
        catch (ArgumentException)
        {
            return FailurePlane("Degenerate AXIS2_PLACEMENT_3D produced invalid plane basis.", SourceFor(planeEntity.Id, "Importer.Geometry.Plane"));
        }
    }

    public static KernelResult<Circle3Curve> ReadCircleCurve(Step242ParsedDocument document, Step242ParsedEntity circleEntity)
    {
        var placementResult = ReadAxis2Placement3D(document, circleEntity, 1, "CIRCLE position", "Importer.Geometry.Circle");
        if (!placementResult.IsSuccess)
        {
            return KernelResult<Circle3Curve>.Failure(placementResult.Diagnostics);
        }

        var radiusResult = ReadPositiveNumber(circleEntity, 2, "CIRCLE radius", "Importer.Geometry.Circle");
        if (!radiusResult.IsSuccess)
        {
            return KernelResult<Circle3Curve>.Failure(radiusResult.Diagnostics);
        }

        try
        {
            return KernelResult<Circle3Curve>.Success(new Circle3Curve(placementResult.Value.Origin, placementResult.Value.Axis, radiusResult.Value, placementResult.Value.ReferenceAxis));
        }
        catch (ArgumentException)
        {
            return FailureCircle("Invalid CIRCLE placement produced degenerate frame.", "Importer.Geometry.Circle");
        }
    }

    public static KernelResult<CylinderSurface> ReadCylindricalSurface(Step242ParsedDocument document, Step242ParsedEntity cylinderEntity)
    {
        var placementResult = ReadAxis2Placement3D(document, cylinderEntity, 1, "CYLINDRICAL_SURFACE position", "Importer.Geometry.Cylinder");
        if (!placementResult.IsSuccess)
        {
            return KernelResult<CylinderSurface>.Failure(placementResult.Diagnostics);
        }

        var radiusResult = ReadPositiveNumber(cylinderEntity, 2, "CYLINDRICAL_SURFACE radius", "Importer.Geometry.Cylinder");
        if (!radiusResult.IsSuccess)
        {
            return KernelResult<CylinderSurface>.Failure(radiusResult.Diagnostics);
        }

        try
        {
            return KernelResult<CylinderSurface>.Success(new CylinderSurface(placementResult.Value.Origin, placementResult.Value.Axis, radiusResult.Value, placementResult.Value.ReferenceAxis));
        }
        catch (ArgumentException)
        {
            return FailureCylinder("Invalid CYLINDRICAL_SURFACE placement produced degenerate frame.", "Importer.Geometry.Cylinder");
        }
    }

    public static KernelResult<SphereSurface> ReadSphericalSurface(Step242ParsedDocument document, Step242ParsedEntity sphereEntity)
    {
        var placementResult = ReadAxis2Placement3D(document, sphereEntity, 1, "SPHERICAL_SURFACE position", "Importer.Geometry.Sphere");
        if (!placementResult.IsSuccess)
        {
            return KernelResult<SphereSurface>.Failure(placementResult.Diagnostics);
        }

        var radiusResult = ReadPositiveNumber(sphereEntity, 2, "SPHERICAL_SURFACE radius", "Importer.Geometry.Sphere");
        if (!radiusResult.IsSuccess)
        {
            return KernelResult<SphereSurface>.Failure(radiusResult.Diagnostics);
        }

        try
        {
            return KernelResult<SphereSurface>.Success(new SphereSurface(placementResult.Value.Origin, placementResult.Value.Axis, radiusResult.Value, placementResult.Value.ReferenceAxis));
        }
        catch (ArgumentException)
        {
            return FailureSphere("Invalid SPHERICAL_SURFACE placement produced degenerate frame.", "Importer.Geometry.Sphere");
        }
    }

    public static KernelResult<ConeSurface> ReadConicalSurface(Step242ParsedDocument document, Step242ParsedEntity coneEntity)
    {
        var placementResult = ReadAxis2Placement3D(document, coneEntity, 1, "CONICAL_SURFACE position", "Importer.Geometry.Cone");
        if (!placementResult.IsSuccess)
        {
            return KernelResult<ConeSurface>.Failure(placementResult.Diagnostics);
        }

        var radiusResult = ReadPositiveNumber(coneEntity, 2, "CONICAL_SURFACE radius", "Importer.Geometry.Cone");
        if (!radiusResult.IsSuccess)
        {
            return KernelResult<ConeSurface>.Failure(radiusResult.Diagnostics);
        }

        var semiAngleResult = ReadNumberArgument(coneEntity, 3, "CONICAL_SURFACE semi_angle", "Importer.Geometry.Cone");
        if (!semiAngleResult.IsSuccess)
        {
            return KernelResult<ConeSurface>.Failure(semiAngleResult.Diagnostics);
        }

        if (semiAngleResult.Value <= 0d || semiAngleResult.Value >= (double.Pi / 2d))
        {
            return FailureCone("CONICAL_SURFACE semi-angle must be in the range (0, pi/2).", "Importer.Geometry.Cone");
        }

        var offset = placementResult.Value.Axis.ToVector() * (radiusResult.Value / double.Tan(semiAngleResult.Value));
        var apex = placementResult.Value.Origin - offset;

        try
        {
            return KernelResult<ConeSurface>.Success(new ConeSurface(apex, placementResult.Value.Axis, semiAngleResult.Value, placementResult.Value.ReferenceAxis));
        }
        catch (ArgumentException)
        {
            return FailureCone("Invalid CONICAL_SURFACE placement produced degenerate frame.", "Importer.Geometry.Cone");
        }
    }

    private static KernelResult<(Point3D Origin, Direction3D Axis, Direction3D ReferenceAxis)> ReadAxis2Placement3D(
        Step242ParsedDocument document,
        Step242ParsedEntity ownerEntity,
        int argumentIndex,
        string context,
        string geometrySource)
    {
        var placementRefResult = ReadReference(ownerEntity, argumentIndex, context);
        if (!placementRefResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(placementRefResult.Diagnostics);
        }

        var placementEntityResult = document.TryGetEntity(placementRefResult.Value.TargetId, "AXIS2_PLACEMENT_3D");
        if (!placementEntityResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(placementEntityResult.Diagnostics);
        }

        var placementEntity = placementEntityResult.Value;

        var originRefResult = ReadReference(placementEntity, 1, "AXIS2_PLACEMENT_3D origin");
        if (!originRefResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(originRefResult.Diagnostics);
        }

        var originEntityResult = document.TryGetEntity(originRefResult.Value.TargetId, "CARTESIAN_POINT");
        if (!originEntityResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(originEntityResult.Diagnostics);
        }

        var originResult = ReadCartesianPoint(originEntityResult.Value, "AXIS2_PLACEMENT_3D origin");
        if (!originResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(originResult.Diagnostics);
        }

        var axisRefResult = ReadReference(placementEntity, 2, "AXIS2_PLACEMENT_3D axis");
        if (!axisRefResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(axisRefResult.Diagnostics);
        }

        var axisEntityResult = document.TryGetEntity(axisRefResult.Value.TargetId, "DIRECTION");
        if (!axisEntityResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(axisEntityResult.Diagnostics);
        }

        var axisResult = ReadDirection(axisEntityResult.Value, "AXIS2_PLACEMENT_3D axis");
        if (!axisResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(axisResult.Diagnostics);
        }

        var refDirRefResult = ReadReference(placementEntity, 3, "AXIS2_PLACEMENT_3D ref direction");
        if (!refDirRefResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(refDirRefResult.Diagnostics);
        }

        var refDirEntityResult = document.TryGetEntity(refDirRefResult.Value.TargetId, "DIRECTION");
        if (!refDirEntityResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(refDirEntityResult.Diagnostics);
        }

        var refDirResult = ReadDirection(refDirEntityResult.Value, "AXIS2_PLACEMENT_3D ref direction");
        if (!refDirResult.IsSuccess)
        {
            return KernelResult<(Point3D, Direction3D, Direction3D)>.Failure(refDirResult.Diagnostics);
        }

        if (double.Abs(axisResult.Value.ToVector().Dot(refDirResult.Value.ToVector())) >= 1d - 1e-12d)
        {
            return FailureAxisPlacement("AXIS2_PLACEMENT_3D axis and ref direction must not be parallel.", geometrySource);
        }

        return KernelResult<(Point3D, Direction3D, Direction3D)>.Success((originResult.Value, axisResult.Value, refDirResult.Value));
    }

    private static KernelResult<double> ReadNumberArgument(Step242ParsedEntity entity, int argumentIndex, string context, string source)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return FailureNumberCode($"{context}: missing argument at index {argumentIndex}.", source);
        }

        return ReadNumberCode(entity.Arguments[argumentIndex], context, source);
    }

    private static KernelResult<double> ReadPositiveNumber(Step242ParsedEntity entity, int argumentIndex, string context, string source)
    {
        var numberResult = ReadNumberArgument(entity, argumentIndex, context, source);
        if (!numberResult.IsSuccess)
        {
            return numberResult;
        }

        if (numberResult.Value <= 0d)
        {
            return FailureNumberCode($"{context}: value must be greater than zero.", source);
        }

        return numberResult;
    }

    private static KernelResult<Point3D> ReadCartesianPoint(Step242ParsedEntity pointEntity, string context)
    {
        if (pointEntity.Arguments.Count < 2)
        {
            return FailurePoint($"{context}: CARTESIAN_POINT requires coordinate list argument.", $"Entity:{pointEntity.Id}");
        }

        if (pointEntity.Arguments[1] is not Step242ListValue coordinates || coordinates.Items.Count != 3)
        {
            return FailurePoint($"{context}: CARTESIAN_POINT coordinates must be a 3-item list.", $"Entity:{pointEntity.Id}");
        }

        var xResult = ReadNumber(coordinates.Items[0], context, pointEntity.Id);
        if (!xResult.IsSuccess)
        {
            return KernelResult<Point3D>.Failure(xResult.Diagnostics);
        }

        var yResult = ReadNumber(coordinates.Items[1], context, pointEntity.Id);
        if (!yResult.IsSuccess)
        {
            return KernelResult<Point3D>.Failure(yResult.Diagnostics);
        }

        var zResult = ReadNumber(coordinates.Items[2], context, pointEntity.Id);
        if (!zResult.IsSuccess)
        {
            return KernelResult<Point3D>.Failure(zResult.Diagnostics);
        }

        return KernelResult<Point3D>.Success(new Point3D(xResult.Value, yResult.Value, zResult.Value));
    }

    private static KernelResult<Direction3D> ReadDirection(Step242ParsedEntity directionEntity, string context)
    {
        var pointResult = ReadCartesianPoint(directionEntity, context);
        if (!pointResult.IsSuccess)
        {
            return KernelResult<Direction3D>.Failure(pointResult.Diagnostics);
        }

        try
        {
            return KernelResult<Direction3D>.Success(Direction3D.Create(pointResult.Value - Point3D.Origin));
        }
        catch (ArgumentException)
        {
            return FailureDirection("Degenerate direction vector is not supported.", SourceFor(directionEntity.Id, "Importer.Geometry.Direction"));
        }
    }

    private static KernelResult<double> ReadNumber(Step242Value value, string context, int entityId)
    {
        if (value is not Step242NumberValue number)
        {
            return FailureNumber($"{context}: expected numeric value.", $"Entity:{entityId}");
        }

        if (!double.IsFinite(number.Value))
        {
            return FailureNumber($"{context}: numeric value must be finite.", $"Entity:{entityId}");
        }

        return KernelResult<double>.Success(number.Value);
    }

    private static KernelResult<double> ReadNumberCode(Step242Value value, string context, string source)
    {
        if (value is not Step242NumberValue number)
        {
            return Failure<double>(KernelDiagnosticCode.InvalidArgument, $"{context}: expected numeric value.", source);
        }

        if (!double.IsFinite(number.Value))
        {
            return Failure<double>(KernelDiagnosticCode.InvalidArgument, $"{context}: numeric value must be finite.", source);
        }

        return KernelResult<double>.Success(number.Value);
    }

    private static KernelResult<Step242EntityReference> FailureRef(string message, string source) => Failure<Step242EntityReference>(message, source);

    private static KernelResult<IReadOnlyList<Step242EntityReference>> FailureList(string message, string source) => Failure<IReadOnlyList<Step242EntityReference>>(message, source);

    private static KernelResult<bool> FailureBool(string message, string source) => Failure<bool>(message, source);

    private static KernelResult<Point3D> FailurePoint(string message, string source) => Failure<Point3D>(message, source);

    private static KernelResult<double> FailureNumber(string message, string source) => Failure<double>(message, source);

    private static KernelResult<Direction3D> FailureDirection(string message, string source) => Failure<Direction3D>(message, source);

    private static KernelResult<PlaneSurface> FailurePlane(string message, string source) => Failure<PlaneSurface>(message, source);

    private static KernelResult<Circle3Curve> FailureCircle(string message, string source) => Failure<Circle3Curve>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<CylinderSurface> FailureCylinder(string message, string source) => Failure<CylinderSurface>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<SphereSurface> FailureSphere(string message, string source) => Failure<SphereSurface>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<ConeSurface> FailureCone(string message, string source) => Failure<ConeSurface>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<(Point3D Origin, Direction3D Axis, Direction3D ReferenceAxis)> FailureAxisPlacement(string message, string source)
        => Failure<(Point3D Origin, Direction3D Axis, Direction3D ReferenceAxis)>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<double> FailureNumberCode(string message, string source) => Failure<double>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static string SourceFor(int _entityId, string stableSource) => stableSource;

    private static KernelResult<T> Failure<T>(string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<T> Failure<T>(KernelDiagnosticCode code, string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(code, KernelDiagnosticSeverity.Error, message, source)]);
}
