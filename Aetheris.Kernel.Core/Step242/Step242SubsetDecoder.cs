using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Step242;

internal static class Step242SubsetDecoder
{
    internal readonly record struct EntityReferenceOrInlineConstructor
    {
        private EntityReferenceOrInlineConstructor(int? referenceId, string? inlineName, IReadOnlyList<Step242Value>? inlineArguments)
        {
            ReferenceId = referenceId;
            InlineName = inlineName;
            InlineArguments = inlineArguments;
        }

        public int? ReferenceId { get; }

        public string? InlineName { get; }

        public IReadOnlyList<Step242Value>? InlineArguments { get; }

        public bool IsReference => ReferenceId.HasValue;

        public static EntityReferenceOrInlineConstructor FromReference(int targetId) => new(targetId, null, null);

        public static EntityReferenceOrInlineConstructor FromInlineConstructor(string name, IReadOnlyList<Step242Value> arguments) => new(null, name, arguments);
    }

    public static Step242EntityConstructor? TryGetConstructor(Step242EntityInstance entityValue, string name)
    {
        if (entityValue is Step242SimpleEntityInstance simple)
        {
            return string.Equals(simple.Constructor.Name, name, StringComparison.Ordinal)
                ? simple.Constructor
                : null;
        }

        if (entityValue is not Step242ComplexEntityInstance complex)
        {
            return null;
        }

        for (var i = 0; i < complex.Constructors.Count; i++)
        {
            if (string.Equals(complex.Constructors[i].Name, name, StringComparison.Ordinal))
            {
                return complex.Constructors[i];
            }
        }

        return null;
    }

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

    public static KernelResult<EntityReferenceOrInlineConstructor> ReadEntityRefOrInlineConstructor(Step242ParsedEntity entity, int argumentIndex, string context)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return Failure<EntityReferenceOrInlineConstructor>($"{context}: missing argument at index {argumentIndex}.", $"Entity:{entity.Id}");
        }

        var value = entity.Arguments[argumentIndex];
        if (value is Step242EntityReference reference)
        {
            return KernelResult<EntityReferenceOrInlineConstructor>.Success(EntityReferenceOrInlineConstructor.FromReference(reference.TargetId));
        }

        if (value is Step242TypedValue typed)
        {
            return KernelResult<EntityReferenceOrInlineConstructor>.Success(EntityReferenceOrInlineConstructor.FromInlineConstructor(typed.Name, typed.Arguments));
        }

        return Failure<EntityReferenceOrInlineConstructor>(
            $"{context}: expected entity reference or inline entity constructor.",
            "Importer.StepSyntax.InlineEntity");
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

    public static KernelResult<IReadOnlyList<Step242EntityReference>> ReadAdvancedFaceBounds(Step242ParsedEntity entity, int argumentIndex, string context)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return FailureList($"{context}: missing argument at index {argumentIndex}.", "Importer.StepSyntax.AdvancedFaceBounds");
        }

        return ReadAdvancedFaceBoundsValue(entity.Arguments[argumentIndex], context);
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

    public static KernelResult<bool?> ReadLogical(Step242ParsedEntity entity, int argumentIndex, string context)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return Failure<bool?>($"{context}: missing argument at index {argumentIndex}.", $"Entity:{entity.Id}");
        }

        if (entity.Arguments[argumentIndex] is Step242BooleanValue boolValue)
        {
            return KernelResult<bool?>.Success(boolValue.Value);
        }

        if (entity.Arguments[argumentIndex] is Step242EnumValue enumValue
            && IsUnknownLogicalToken(enumValue.Value))
        {
            return KernelResult<bool?>.Success(null);
        }

        return Failure<bool?>(
            $"{context}: expected logical (.T. / .F. / .U.).",
            $"Entity:{entity.Id}");
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


    public static KernelResult<TorusSurface> ReadToroidalSurface(Step242ParsedDocument document, Step242ParsedEntity torusEntity)
    {
        var placementResult = ReadAxis2Placement3D(document, torusEntity, 1, "TOROIDAL_SURFACE position", "Importer.Geometry.Torus");
        if (!placementResult.IsSuccess)
        {
            return KernelResult<TorusSurface>.Failure(placementResult.Diagnostics);
        }

        var majorRadiusResult = ReadPositiveNumber(torusEntity, 2, "TOROIDAL_SURFACE major_radius", "Importer.Geometry.Torus");
        if (!majorRadiusResult.IsSuccess)
        {
            return KernelResult<TorusSurface>.Failure(majorRadiusResult.Diagnostics);
        }

        var minorRadiusResult = ReadPositiveNumber(torusEntity, 3, "TOROIDAL_SURFACE minor_radius", "Importer.Geometry.Torus");
        if (!minorRadiusResult.IsSuccess)
        {
            return KernelResult<TorusSurface>.Failure(minorRadiusResult.Diagnostics);
        }

        try
        {
            return KernelResult<TorusSurface>.Success(new TorusSurface(placementResult.Value.Origin, placementResult.Value.Axis, majorRadiusResult.Value, minorRadiusResult.Value, placementResult.Value.ReferenceAxis));
        }
        catch (ArgumentException)
        {
            return FailureTorus("Invalid TOROIDAL_SURFACE placement produced degenerate frame.", "Importer.Geometry.Torus");
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




    public static KernelResult<Ellipse3Curve> ReadEllipseCurve(Step242ParsedDocument document, Step242ParsedEntity ellipseEntity)
    {
        var placementResult = ReadAxis2Placement3D(document, ellipseEntity, 1, "ELLIPSE position", "Importer.Geometry.Ellipse");
        if (!placementResult.IsSuccess)
        {
            return KernelResult<Ellipse3Curve>.Failure(placementResult.Diagnostics);
        }

        var majorRadiusResult = ReadPositiveNumber(ellipseEntity, 2, "ELLIPSE semi_axis_1", "Importer.Geometry.Ellipse");
        if (!majorRadiusResult.IsSuccess)
        {
            return KernelResult<Ellipse3Curve>.Failure(majorRadiusResult.Diagnostics);
        }

        var minorRadiusResult = ReadPositiveNumber(ellipseEntity, 3, "ELLIPSE semi_axis_2", "Importer.Geometry.Ellipse");
        if (!minorRadiusResult.IsSuccess)
        {
            return KernelResult<Ellipse3Curve>.Failure(minorRadiusResult.Diagnostics);
        }

        if (minorRadiusResult.Value > majorRadiusResult.Value)
        {
            return FailureEllipse("ELLIPSE requires semi_axis_2 <= semi_axis_1 in M85 subset.", "Importer.Geometry.Ellipse");
        }

        try
        {
            return KernelResult<Ellipse3Curve>.Success(new Ellipse3Curve(placementResult.Value.Origin, placementResult.Value.Axis, majorRadiusResult.Value, minorRadiusResult.Value, placementResult.Value.ReferenceAxis));
        }
        catch (ArgumentException)
        {
            return FailureEllipse("Invalid ELLIPSE placement produced degenerate frame.", "Importer.Geometry.Ellipse");
        }
    }

    public static KernelResult<BSpline3Curve> ReadBSplineCurveWithKnots(Step242ParsedDocument document, Step242ParsedEntity splineEntity)
    {
        var degreeResult = ReadIntArgument(splineEntity, 1, "B_SPLINE_CURVE_WITH_KNOTS degree", "Importer.Geometry.BSplineCurve");
        if (!degreeResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(degreeResult.Diagnostics);
        }

        var controlPointsResult = ReadCartesianPointReferenceList(document, splineEntity, 2, "B_SPLINE_CURVE_WITH_KNOTS control_points_list");
        if (!controlPointsResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(controlPointsResult.Diagnostics);
        }

        var curveFormResult = ReadEnumArgument(splineEntity, 3, "B_SPLINE_CURVE_WITH_KNOTS curve_form", "Importer.Geometry.BSplineCurve");
        if (!curveFormResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(curveFormResult.Diagnostics);
        }

        var closedCurveResult = ReadBoolean(splineEntity, 4, "B_SPLINE_CURVE_WITH_KNOTS closed_curve");
        if (!closedCurveResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(closedCurveResult.Diagnostics);
        }

        var selfIntersectResult = ReadLogical(splineEntity, 5, "B_SPLINE_CURVE_WITH_KNOTS self_intersect");
        if (!selfIntersectResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(selfIntersectResult.Diagnostics);
        }

        var multiplicitiesResult = ReadIntegerListArgument(splineEntity, 6, "B_SPLINE_CURVE_WITH_KNOTS knot_multiplicities", "Importer.Geometry.BSplineCurve");
        if (!multiplicitiesResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(multiplicitiesResult.Diagnostics);
        }

        var knotValuesResult = ReadNumberListArgument(splineEntity, 7, "B_SPLINE_CURVE_WITH_KNOTS knots", "Importer.Geometry.BSplineCurve");
        if (!knotValuesResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(knotValuesResult.Diagnostics);
        }

        var knotSpecResult = ReadEnumArgument(splineEntity, 8, "B_SPLINE_CURVE_WITH_KNOTS knot_spec", "Importer.Geometry.BSplineCurve");
        if (!knotSpecResult.IsSuccess)
        {
            return KernelResult<BSpline3Curve>.Failure(knotSpecResult.Diagnostics);
        }

        try
        {
            return KernelResult<BSpline3Curve>.Success(new BSpline3Curve(
                degreeResult.Value,
                controlPointsResult.Value,
                multiplicitiesResult.Value,
                knotValuesResult.Value,
                curveFormResult.Value,
                closedCurveResult.Value,
                selfIntersectResult.Value ?? false,
                knotSpecResult.Value));
        }
        catch (ArgumentException ex)
        {
            return FailureBSplineCurve(ex.Message, "Importer.Geometry.BSplineCurve");
        }
    }

    public static KernelResult<BSplineSurfaceWithKnots> ReadBSplineSurfaceWithKnots(Step242ParsedDocument document, Step242ParsedEntity splineEntity)
    {
        var degreeUResult = ReadIntArgument(splineEntity, 1, "B_SPLINE_SURFACE_WITH_KNOTS degree_u", "Importer.Geometry.BSplineSurface");
        if (!degreeUResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(degreeUResult.Diagnostics);
        }

        var degreeVResult = ReadIntArgument(splineEntity, 2, "B_SPLINE_SURFACE_WITH_KNOTS degree_v", "Importer.Geometry.BSplineSurface");
        if (!degreeVResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(degreeVResult.Diagnostics);
        }

        var controlPointsResult = ReadCartesianPointReferenceNet(
            document,
            splineEntity,
            3,
            "B_SPLINE_SURFACE_WITH_KNOTS control_points_list",
            "Importer.Geometry.BSplineSurface");
        if (!controlPointsResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(controlPointsResult.Diagnostics);
        }

        var surfaceFormResult = ReadEnumArgument(splineEntity, 4, "B_SPLINE_SURFACE_WITH_KNOTS surface_form", "Importer.Geometry.BSplineSurface");
        if (!surfaceFormResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(surfaceFormResult.Diagnostics);
        }

        var uClosedResult = ReadLogical(splineEntity, 5, "B_SPLINE_SURFACE_WITH_KNOTS u_closed");
        if (!uClosedResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(uClosedResult.Diagnostics);
        }

        var vClosedResult = ReadLogical(splineEntity, 6, "B_SPLINE_SURFACE_WITH_KNOTS v_closed");
        if (!vClosedResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(vClosedResult.Diagnostics);
        }

        var selfIntersectResult = ReadLogical(splineEntity, 7, "B_SPLINE_SURFACE_WITH_KNOTS self_intersect");
        if (!selfIntersectResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(selfIntersectResult.Diagnostics);
        }

        var multiplicitiesUResult = ReadIntegerListArgument(splineEntity, 8, "B_SPLINE_SURFACE_WITH_KNOTS u_multiplicities", "Importer.Geometry.BSplineSurface");
        if (!multiplicitiesUResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(multiplicitiesUResult.Diagnostics);
        }

        var multiplicitiesVResult = ReadIntegerListArgument(splineEntity, 9, "B_SPLINE_SURFACE_WITH_KNOTS v_multiplicities", "Importer.Geometry.BSplineSurface");
        if (!multiplicitiesVResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(multiplicitiesVResult.Diagnostics);
        }

        var knotsUResult = ReadNumberListArgument(splineEntity, 10, "B_SPLINE_SURFACE_WITH_KNOTS u_knots", "Importer.Geometry.BSplineSurface");
        if (!knotsUResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(knotsUResult.Diagnostics);
        }

        var knotsVResult = ReadNumberListArgument(splineEntity, 11, "B_SPLINE_SURFACE_WITH_KNOTS v_knots", "Importer.Geometry.BSplineSurface");
        if (!knotsVResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(knotsVResult.Diagnostics);
        }

        var knotSpecResult = ReadEnumArgument(splineEntity, 12, "B_SPLINE_SURFACE_WITH_KNOTS knot_spec", "Importer.Geometry.BSplineSurface");
        if (!knotSpecResult.IsSuccess)
        {
            return KernelResult<BSplineSurfaceWithKnots>.Failure(knotSpecResult.Diagnostics);
        }

        try
        {
            return KernelResult<BSplineSurfaceWithKnots>.Success(new BSplineSurfaceWithKnots(
                degreeUResult.Value,
                degreeVResult.Value,
                controlPointsResult.Value,
                surfaceFormResult.Value,
                uClosedResult.Value ?? false,
                vClosedResult.Value ?? false,
                selfIntersectResult.Value ?? false,
                multiplicitiesUResult.Value,
                multiplicitiesVResult.Value,
                knotsUResult.Value,
                knotsVResult.Value,
                knotSpecResult.Value));
        }
        catch (ArgumentException ex)
        {
            return Failure<BSplineSurfaceWithKnots>(KernelDiagnosticCode.InvalidArgument, ex.Message, "Importer.Geometry.BSplineSurface");
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

        var radiusResult = ReadNonNegativeNumber(coneEntity, 2, "CONICAL_SURFACE radius", "Importer.Geometry.Cone");
        if (!radiusResult.IsSuccess)
        {
            return KernelResult<ConeSurface>.Failure(radiusResult.Diagnostics);
        }

        var semiAngleResult = ReadNumberArgument(coneEntity, 3, "CONICAL_SURFACE semi_angle", "Importer.Geometry.Cone");
        if (!semiAngleResult.IsSuccess)
        {
            return KernelResult<ConeSurface>.Failure(semiAngleResult.Diagnostics);
        }

        var normalizedSemiAngleResult = NormalizeConicalSemiAngle(document, semiAngleResult.Value);
        if (!normalizedSemiAngleResult.IsSuccess)
        {
            return KernelResult<ConeSurface>.Failure(normalizedSemiAngleResult.Diagnostics);
        }

        var normalizedSemiAngle = normalizedSemiAngleResult.Value;
        var offset = placementResult.Value.Axis.ToVector() * (radiusResult.Value / double.Tan(normalizedSemiAngle));
        var apex = placementResult.Value.Origin - offset;

        try
        {
            return KernelResult<ConeSurface>.Success(new ConeSurface(apex, placementResult.Value.Axis, normalizedSemiAngle, placementResult.Value.ReferenceAxis));
        }
        catch (ArgumentException)
        {
            return FailureCone("Invalid CONICAL_SURFACE placement produced degenerate frame.", "Importer.Geometry.Cone");
        }
    }

    private static KernelResult<double> NormalizeConicalSemiAngle(Step242ParsedDocument document, double rawSemiAngle)
    {
        if (!double.IsFinite(rawSemiAngle) || rawSemiAngle <= 0d)
        {
            return FailureNumberCode("CONICAL_SURFACE semi-angle must be greater than zero.", "Importer.Geometry.Cone");
        }

        var normalized = rawSemiAngle * document.PlaneAngleToRadiansScale;
        if (!double.IsFinite(normalized) || normalized <= 0d || normalized >= (double.Pi / 2d))
        {
            return FailureNumberCode("CONICAL_SURFACE semi-angle must be in the range (0, pi/2) radians after applying plane angle unit context.", "Importer.Geometry.Cone");
        }

        return KernelResult<double>.Success(normalized);
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

    private static KernelResult<double> ReadNonNegativeNumber(Step242ParsedEntity entity, int argumentIndex, string context, string source)
    {
        var numberResult = ReadNumberArgument(entity, argumentIndex, context, source);
        if (!numberResult.IsSuccess)
        {
            return numberResult;
        }

        if (numberResult.Value < 0d)
        {
            return FailureNumberCode($"{context}: value must be greater than or equal to zero.", source);
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
        var unwrappedValueResult = UnwrapTypedValue(value, context, "Importer.StepSyntax.TypedValue");
        if (!unwrappedValueResult.IsSuccess)
        {
            return KernelResult<double>.Failure(unwrappedValueResult.Diagnostics);
        }

        if (unwrappedValueResult.Value is not Step242NumberValue number)
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
        var unwrappedValueResult = UnwrapTypedValue(value, context, "Importer.StepSyntax.TypedValue");
        if (!unwrappedValueResult.IsSuccess)
        {
            return KernelResult<double>.Failure(unwrappedValueResult.Diagnostics);
        }

        if (unwrappedValueResult.Value is not Step242NumberValue number)
        {
            return Failure<double>(KernelDiagnosticCode.InvalidArgument, $"{context}: expected numeric value.", source);
        }

        if (!double.IsFinite(number.Value))
        {
            return Failure<double>(KernelDiagnosticCode.InvalidArgument, $"{context}: numeric value must be finite.", source);
        }

        return KernelResult<double>.Success(number.Value);
    }

    private static KernelResult<IReadOnlyList<Point3D>> ReadCartesianPointReferenceList(
        Step242ParsedDocument document,
        Step242ParsedEntity ownerEntity,
        int argumentIndex,
        string context)
    {
        if (argumentIndex < 0 || argumentIndex >= ownerEntity.Arguments.Count)
        {
            return Failure<IReadOnlyList<Point3D>>(KernelDiagnosticCode.InvalidArgument, $"{context}: missing argument at index {argumentIndex}.", "Importer.Geometry.BSplineCurve");
        }

        if (ownerEntity.Arguments[argumentIndex] is not Step242ListValue list)
        {
            return Failure<IReadOnlyList<Point3D>>(KernelDiagnosticCode.InvalidArgument, $"{context}: expected aggregate list of CARTESIAN_POINT references.", "Importer.Geometry.BSplineCurve");
        }

        var points = new List<Point3D>(list.Items.Count);
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is not Step242EntityReference pointReference)
            {
                return Failure<IReadOnlyList<Point3D>>(KernelDiagnosticCode.InvalidArgument, $"{context}: item {i} must be an entity reference (#id).", "Importer.Geometry.BSplineCurve");
            }

            var pointEntityResult = document.TryGetEntity(pointReference.TargetId, "CARTESIAN_POINT");
            if (!pointEntityResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<Point3D>>.Failure(pointEntityResult.Diagnostics);
            }

            var pointResult = ReadCartesianPoint(pointEntityResult.Value, context);
            if (!pointResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<Point3D>>.Failure(pointResult.Diagnostics);
            }

            points.Add(pointResult.Value);
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(points);
    }

    private static KernelResult<IReadOnlyList<IReadOnlyList<Point3D>>> ReadCartesianPointReferenceNet(
        Step242ParsedDocument document,
        Step242ParsedEntity ownerEntity,
        int argumentIndex,
        string context,
        string source)
    {
        if (argumentIndex < 0 || argumentIndex >= ownerEntity.Arguments.Count)
        {
            return Failure<IReadOnlyList<IReadOnlyList<Point3D>>>(KernelDiagnosticCode.InvalidArgument, $"{context}: missing argument at index {argumentIndex}.", source);
        }

        if (ownerEntity.Arguments[argumentIndex] is not Step242ListValue rows)
        {
            return Failure<IReadOnlyList<IReadOnlyList<Point3D>>>(KernelDiagnosticCode.InvalidArgument, $"{context}: expected aggregate list of control-point rows.", source);
        }

        var net = new List<IReadOnlyList<Point3D>>(rows.Items.Count);
        for (var rowIndex = 0; rowIndex < rows.Items.Count; rowIndex++)
        {
            if (rows.Items[rowIndex] is not Step242ListValue row)
            {
                return Failure<IReadOnlyList<IReadOnlyList<Point3D>>>(KernelDiagnosticCode.InvalidArgument, $"{context}: row {rowIndex} must be a list of CARTESIAN_POINT references.", source);
            }

            var points = new List<Point3D>(row.Items.Count);
            for (var colIndex = 0; colIndex < row.Items.Count; colIndex++)
            {
                if (row.Items[colIndex] is not Step242EntityReference pointReference)
                {
                    return Failure<IReadOnlyList<IReadOnlyList<Point3D>>>(KernelDiagnosticCode.InvalidArgument, $"{context}: row {rowIndex}, column {colIndex} must be an entity reference (#id).", source);
                }

                var pointEntityResult = document.TryGetEntity(pointReference.TargetId, "CARTESIAN_POINT");
                if (!pointEntityResult.IsSuccess)
                {
                    return KernelResult<IReadOnlyList<IReadOnlyList<Point3D>>>.Failure(pointEntityResult.Diagnostics);
                }

                var pointResult = ReadCartesianPoint(pointEntityResult.Value, context);
                if (!pointResult.IsSuccess)
                {
                    return KernelResult<IReadOnlyList<IReadOnlyList<Point3D>>>.Failure(pointResult.Diagnostics);
                }

                points.Add(pointResult.Value);
            }

            net.Add(points);
        }

        return KernelResult<IReadOnlyList<IReadOnlyList<Point3D>>>.Success(net);
    }

    private static KernelResult<IReadOnlyList<int>> ReadIntegerListArgument(
        Step242ParsedEntity entity,
        int argumentIndex,
        string context,
        string source)
    {
        var numbersResult = ReadNumberListArgument(entity, argumentIndex, context, source);
        if (!numbersResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<int>>.Failure(numbersResult.Diagnostics);
        }

        var ints = new List<int>(numbersResult.Value.Count);
        for (var i = 0; i < numbersResult.Value.Count; i++)
        {
            var number = numbersResult.Value[i];
            var rounded = System.Math.Round(number);
            if (double.Abs(number - rounded) > 1e-9d)
            {
                return Failure<IReadOnlyList<int>>(KernelDiagnosticCode.InvalidArgument, $"{context}: item {i} must be an integer value.", source);
            }

            if (rounded < int.MinValue || rounded > int.MaxValue)
            {
                return Failure<IReadOnlyList<int>>(KernelDiagnosticCode.InvalidArgument, $"{context}: item {i} is outside integer range.", source);
            }

            ints.Add((int)rounded);
        }

        return KernelResult<IReadOnlyList<int>>.Success(ints);
    }

    private static KernelResult<IReadOnlyList<double>> ReadNumberListArgument(
        Step242ParsedEntity entity,
        int argumentIndex,
        string context,
        string source)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return Failure<IReadOnlyList<double>>(KernelDiagnosticCode.InvalidArgument, $"{context}: missing argument at index {argumentIndex}.", source);
        }

        if (entity.Arguments[argumentIndex] is not Step242ListValue list)
        {
            return Failure<IReadOnlyList<double>>(KernelDiagnosticCode.InvalidArgument, $"{context}: expected aggregate list.", source);
        }

        var values = new List<double>(list.Items.Count);
        for (var i = 0; i < list.Items.Count; i++)
        {
            var valueResult = ReadNumberCode(list.Items[i], context, source);
            if (!valueResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<double>>.Failure(valueResult.Diagnostics);
            }

            values.Add(valueResult.Value);
        }

        return KernelResult<IReadOnlyList<double>>.Success(values);
    }

    private static KernelResult<string> ReadEnumArgument(Step242ParsedEntity entity, int argumentIndex, string context, string source)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count)
        {
            return Failure<string>(KernelDiagnosticCode.InvalidArgument, $"{context}: missing argument at index {argumentIndex}.", source);
        }

        var unwrappedResult = UnwrapTypedValue(entity.Arguments[argumentIndex], context, source);
        if (!unwrappedResult.IsSuccess)
        {
            return KernelResult<string>.Failure(unwrappedResult.Diagnostics);
        }

        if (unwrappedResult.Value is not Step242EnumValue enumValue)
        {
            return Failure<string>(KernelDiagnosticCode.InvalidArgument, $"{context}: expected enumeration value.", source);
        }

        return KernelResult<string>.Success(enumValue.Value);
    }

    private static KernelResult<int> ReadIntArgument(Step242ParsedEntity entity, int argumentIndex, string context, string source)
    {
        var valueResult = ReadNumberArgument(entity, argumentIndex, context, source);
        if (!valueResult.IsSuccess)
        {
            return KernelResult<int>.Failure(valueResult.Diagnostics);
        }

        var rounded = System.Math.Round(valueResult.Value);
        if (double.Abs(valueResult.Value - rounded) > 1e-9d)
        {
            return Failure<int>(KernelDiagnosticCode.InvalidArgument, $"{context}: expected integer value.", source);
        }

        if (rounded < int.MinValue || rounded > int.MaxValue)
        {
            return Failure<int>(KernelDiagnosticCode.InvalidArgument, $"{context}: value is outside integer range.", source);
        }

        return KernelResult<int>.Success((int)rounded);
    }

    private static KernelResult<Step242Value> UnwrapTypedValue(Step242Value value, string context, string source)
    {
        if (value is not Step242TypedValue typed)
        {
            return KernelResult<Step242Value>.Success(value);
        }

        if (typed.Arguments.Count != 1)
        {
            return Failure<Step242Value>(
                KernelDiagnosticCode.ValidationFailed,
                $"{context}: typed value '{typed.Name}' requires exactly one argument.",
                source);
        }

        return UnwrapTypedValue(typed.Arguments[0], context, source);
    }

    private static KernelResult<IReadOnlyList<Step242EntityReference>> ReadAdvancedFaceBoundsValue(Step242Value value, string context)
    {
        const string source = "Importer.StepSyntax.AdvancedFaceBounds";

        if (value is not Step242ListValue list)
        {
            return FailureList($"{context}: expected aggregate list of face bounds.", source);
        }

        if (list.Items.Count == 1 && list.Items[0] is Step242ListValue nested)
        {
            list = nested;
        }

        var refs = new List<Step242EntityReference>(list.Items.Count);
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is not Step242EntityReference reference)
            {
                return FailureList($"{context}: aggregate item {i} must be an entity reference (#id).", source);
            }

            refs.Add(reference);
        }

        return KernelResult<IReadOnlyList<Step242EntityReference>>.Success(refs);
    }

    private static KernelResult<Step242EntityReference> FailureRef(string message, string source) => Failure<Step242EntityReference>(message, source);

    private static KernelResult<IReadOnlyList<Step242EntityReference>> FailureList(string message, string source) => Failure<IReadOnlyList<Step242EntityReference>>(message, source);

    private static KernelResult<bool> FailureBool(string message, string source) => Failure<bool>(message, source);

    private static KernelResult<Point3D> FailurePoint(string message, string source) => Failure<Point3D>(message, source);

    private static KernelResult<double> FailureNumber(string message, string source) => Failure<double>(message, source);

    private static KernelResult<Direction3D> FailureDirection(string message, string source) => Failure<Direction3D>(message, source);

    private static KernelResult<PlaneSurface> FailurePlane(string message, string source) => Failure<PlaneSurface>(message, source);

    private static KernelResult<Circle3Curve> FailureCircle(string message, string source) => Failure<Circle3Curve>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<Ellipse3Curve> FailureEllipse(string message, string source) => Failure<Ellipse3Curve>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<BSpline3Curve> FailureBSplineCurve(string message, string source) => Failure<BSpline3Curve>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<CylinderSurface> FailureCylinder(string message, string source) => Failure<CylinderSurface>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<SphereSurface> FailureSphere(string message, string source) => Failure<SphereSurface>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<TorusSurface> FailureTorus(string message, string source) => Failure<TorusSurface>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<ConeSurface> FailureCone(string message, string source) => Failure<ConeSurface>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<(Point3D Origin, Direction3D Axis, Direction3D ReferenceAxis)> FailureAxisPlacement(string message, string source)
        => Failure<(Point3D Origin, Direction3D Axis, Direction3D ReferenceAxis)>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static KernelResult<double> FailureNumberCode(string message, string source) => Failure<double>(KernelDiagnosticCode.InvalidArgument, message, source);

    private static bool IsUnknownLogicalToken(string token)
        => string.Equals(token, "U", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "UNKNOWN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "UNSET", StringComparison.OrdinalIgnoreCase);

    private static string SourceFor(int _entityId, string stableSource) => stableSource;

    private static KernelResult<T> Failure<T>(string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<T> Failure<T>(KernelDiagnosticCode code, string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(code, KernelDiagnosticSeverity.Error, message, source)]);
}
