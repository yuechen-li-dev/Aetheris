using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

public static class Step242Exporter
{
    public static KernelResult<string> ExportBody(BrepBody body, Step242ExportOptions? options = null)
        => ExportBody(body, semanticPmi: null, options);

    public static KernelResult<string> ExportBody(
        BrepBody body,
        IReadOnlyList<Step242SemanticPmi>? semanticPmi,
        Step242ExportOptions? options = null)
    {
        options ??= new Step242ExportOptions();

        var model = body.Topology;
        var bodyNodes = model.Bodies.OrderBy(b => b.Id.Value).ToArray();
        if (bodyNodes.Length != 1)
        {
            return Failure("Only single-body export is supported.", "Topology.Bodies");
        }

        var rootDecision = StepSolidRootExportPlanner.Decide(body);
        var shellRepresentation = body.ShellRepresentation;
        if (rootDecision.Kind == StepSolidRootExportKind.Unsupported)
        {
            var plannerReason = rootDecision.Evaluations
                .FirstOrDefault(e => e.PolicyName == "UnsupportedShellTopologyPolicy")?
                .Diagnostics.FirstOrDefault() ?? "Unsupported shell topology for STEP solid root export.";
            return Failure(plannerReason, "Topology.Shells");
        }

        if (shellRepresentation is null)
        {
            var shellIds = bodyNodes[0].ShellIds.OrderBy(s => s.Value).ToArray();
            shellRepresentation = new BrepBodyShellRepresentation(shellIds[0], []);
        }

        var writer = new Step242TextWriter();

        var vertexPointIds = new Dictionary<VertexId, string>();
        var cartesianPointIds = new Dictionary<VertexId, string>();
        var vertexPoints = new Dictionary<VertexId, Point3D>();

        var edgeCurveIds = new Dictionary<EdgeId, string>();
        var orientedEdgeIds = new Dictionary<CoedgeId, string>();
        var lineIds = new Dictionary<EdgeId, string>();
        var circleIds = new Dictionary<EdgeId, string>();
        var bsplineIds = new Dictionary<EdgeId, string>();
        var ellipseIds = new Dictionary<EdgeId, string>();

        var outerClosedShellId = BuildClosedShell(writer, body, model, shellRepresentation.OuterShellId, vertexPoints, cartesianPointIds, vertexPointIds, edgeCurveIds, orientedEdgeIds, lineIds, circleIds, bsplineIds, ellipseIds);
        if (outerClosedShellId is null)
        {
            return Failure($"Shell {shellRepresentation.OuterShellId.Value} could not be exported.", $"Shell:{shellRepresentation.OuterShellId.Value}");
        }

        string brepId;
        if (rootDecision.Kind == StepSolidRootExportKind.ManifoldSolidBrep)
        {
            brepId = writer.AddEntity("MANIFOLD_SOLID_BREP", Step242TextWriter.String(options.ProductName), Step242TextWriter.Ref(outerClosedShellId));
        }
        else
        {
            var orientedVoidShellIds = new List<string>();
            foreach (var innerShellId in shellRepresentation.InnerShellIds.OrderBy(id => id.Value))
            {
                var innerClosedShellId = BuildClosedShell(writer, body, model, innerShellId, vertexPoints, cartesianPointIds, vertexPointIds, edgeCurveIds, orientedEdgeIds, lineIds, circleIds, bsplineIds, ellipseIds);
                if (innerClosedShellId is null)
                {
                    return Failure($"Shell {innerShellId.Value} could not be exported.", $"Shell:{innerShellId.Value}");
                }

                var orientedClosedShellId = writer.AddEntity(
                    "ORIENTED_CLOSED_SHELL",
                    "$",
                    Step242TextWriter.Ref(innerClosedShellId),
                    Step242TextWriter.BooleanLogical(false));

                orientedVoidShellIds.Add(orientedClosedShellId);
            }

            brepId = writer.AddEntity(
                "BREP_WITH_VOIDS",
                Step242TextWriter.String(options.ProductName),
                Step242TextWriter.Ref(outerClosedShellId),
                Step242TextWriter.List(orientedVoidShellIds.ToArray()));
        }

        var appContextId = writer.AddEntity("APPLICATION_CONTEXT", Step242TextWriter.String("mechanical design"));
        var productContextId = writer.AddEntity("PRODUCT_CONTEXT", Step242TextWriter.String(""), Step242TextWriter.Ref(appContextId), Step242TextWriter.String("mechanical"));
        var productId = writer.AddEntity("PRODUCT", Step242TextWriter.String("AETHERIS"), Step242TextWriter.String(options.ProductName), Step242TextWriter.String(""), Step242TextWriter.List(productContextId));
        var formationId = writer.AddEntity("PRODUCT_DEFINITION_FORMATION", Step242TextWriter.String(""), Step242TextWriter.String(""), Step242TextWriter.Ref(productId));
        var definitionContextId = writer.AddEntity("PRODUCT_DEFINITION_CONTEXT", Step242TextWriter.String("design"), Step242TextWriter.Ref(appContextId), Step242TextWriter.String("design"));
        var definitionId = writer.AddEntity("PRODUCT_DEFINITION", Step242TextWriter.String(""), Step242TextWriter.String(""), Step242TextWriter.Ref(formationId), Step242TextWriter.Ref(definitionContextId));
        var shapeId = writer.AddEntity("PRODUCT_DEFINITION_SHAPE", Step242TextWriter.String(""), Step242TextWriter.String(""), Step242TextWriter.Ref(definitionId));
        var lengthUnitId = writer.AddRawEntity("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))");
        var planeAngleUnitId = writer.AddRawEntity("(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.))");
        var solidAngleUnitId = writer.AddRawEntity("(NAMED_UNIT(*)SI_UNIT($,.STERADIAN.)SOLID_ANGLE_UNIT())");
        var repContextId = writer.AddRawEntity($"(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNIT_ASSIGNED_CONTEXT(({lengthUnitId},{planeAngleUnitId},{solidAngleUnitId}))REPRESENTATION_CONTEXT('3','3D'))");
        EmitSemanticPmi(writer, shapeId, repContextId, lengthUnitId, semanticPmi);

        var shapeRepresentationId = writer.AddEntity("SHAPE_REPRESENTATION", Step242TextWriter.String(options.ProductName), Step242TextWriter.List(brepId), Step242TextWriter.Ref(repContextId));
        writer.AddEntity("SHAPE_DEFINITION_REPRESENTATION", Step242TextWriter.Ref(shapeId), Step242TextWriter.Ref(shapeRepresentationId));

        return KernelResult<string>.Success(writer.Build(options.ApplicationName));
    }

    private static string? BuildClosedShell(
        Step242TextWriter writer,
        BrepBody body,
        TopologyModel model,
        ShellId shellId,
        Dictionary<VertexId, Point3D> vertexPoints,
        Dictionary<VertexId, string> cartesianPointIds,
        Dictionary<VertexId, string> vertexPointIds,
        Dictionary<EdgeId, string> edgeCurveIds,
        Dictionary<CoedgeId, string> orientedEdgeIds,
        Dictionary<EdgeId, string> lineIds,
        Dictionary<EdgeId, string> circleIds,
        Dictionary<EdgeId, string> bsplineIds,
        Dictionary<EdgeId, string> ellipseIds)
    {
        if (!model.TryGetShell(shellId, out var shell) || shell is null)
        {
            return null;
        }

        var faceIds = new List<string>();
        foreach (var face in shell.FaceIds.OrderBy(id => id.Value).Select(model.GetFace))
        {
            if (!body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding))
            {
                return null;
            }

            if (!body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface) || surface is null)
            {
                return null;
            }

            if (face.LoopIds.Count == 0 && !CanExportLooplessFace(body, face, surface))
            {
                return null;
            }

            var surfaceIdResult = BuildSurface(writer, surface, face.Id);
            if (!surfaceIdResult.IsSuccess)
            {
                return null;
            }

            var loopBoundIds = new List<string>();
            foreach (var loopId in face.LoopIds.OrderBy(id => id.Value))
            {
                var loop = model.GetLoop(loopId);
                var oriented = new List<string>();

                foreach (var coedgeId in loop.CoedgeIds.OrderBy(id => id.Value))
                {
                    var coedge = model.GetCoedge(coedgeId);

                    if (!edgeCurveIds.TryGetValue(coedge.EdgeId, out var edgeCurveId))
                    {
                        var edgeResult = BuildEdgeCurve(body, model, writer, coedge.EdgeId, vertexPoints, cartesianPointIds, vertexPointIds, lineIds, circleIds, bsplineIds, ellipseIds);
                        if (!edgeResult.IsSuccess)
                        {
                            return null;
                        }

                        edgeCurveId = edgeResult.Value;
                        edgeCurveIds[coedge.EdgeId] = edgeCurveId;
                    }

                    var orientedEdgeId = writer.AddEntity(
                        "ORIENTED_EDGE",
                        "$",
                        "$",
                        "$",
                        Step242TextWriter.Ref(edgeCurveId),
                        Step242TextWriter.BooleanLogical(!coedge.IsReversed));

                    orientedEdgeIds[coedgeId] = orientedEdgeId;
                    oriented.Add(orientedEdgeId);
                }

                var edgeLoopId = writer.AddEntity("EDGE_LOOP", "$", Step242TextWriter.List(oriented.ToArray()));
                var boundEntity = loopBoundIds.Count == 0 ? "FACE_OUTER_BOUND" : "FACE_BOUND";
                var boundId = writer.AddEntity(boundEntity, Step242TextWriter.String(string.Empty), Step242TextWriter.Ref(edgeLoopId), Step242TextWriter.BooleanLogical(true));
                loopBoundIds.Add(boundId);
            }

            var advancedFaceId = writer.AddEntity(
                "ADVANCED_FACE",
                Step242TextWriter.String(string.Empty),
                Step242TextWriter.List(loopBoundIds.ToArray()),
                Step242TextWriter.Ref(surfaceIdResult.Value),
                Step242TextWriter.BooleanLogical(true));

            faceIds.Add(advancedFaceId);
        }

        return writer.AddEntity("CLOSED_SHELL", "$", Step242TextWriter.List(faceIds.ToArray()));
    }

    private static void EmitSemanticPmi(
        Step242TextWriter writer,
        string shapeId,
        string repContextId,
        string lengthUnitId,
        IReadOnlyList<Step242SemanticPmi>? semanticPmi)
    {
        if (semanticPmi is null || semanticPmi.Count == 0)
        {
            return;
        }

        foreach (var item in semanticPmi)
        {
            switch (item)
            {
                case Step242SemanticPmiHole hole:
                    EmitHoleSemanticPmi(writer, shapeId, repContextId, lengthUnitId, hole);
                    break;
                case Step242SemanticPmiDatum datum:
                    EmitDatumSemanticPmi(writer, shapeId, datum);
                    break;
                case Step242SemanticPmiNote note:
                    EmitNoteSemanticPmi(writer, shapeId, note);
                    break;
            }
        }
    }

    private static void EmitHoleSemanticPmi(
        Step242TextWriter writer,
        string shapeId,
        string repContextId,
        string lengthUnitId,
        Step242SemanticPmiHole hole)
    {
            var featureShapeAspectId = writer.AddEntity(
                "SHAPE_ASPECT",
                Step242TextWriter.String($"firmament-feature:{hole.FeatureId}"),
                Step242TextWriter.String("supported cylinder through-hole feature"),
                Step242TextWriter.Ref(shapeId),
                Step242TextWriter.Enum("FALSE"));

            var propertyDefinitionId = writer.AddEntity(
                "PROPERTY_DEFINITION",
                Step242TextWriter.String($"diameter:{hole.FeatureId}"),
                Step242TextWriter.String("auto-derived semantic PMI diameter"),
                Step242TextWriter.Ref(featureShapeAspectId));

            var measureItemId = writer.AddEntity(
                "MEASURE_REPRESENTATION_ITEM",
                Step242TextWriter.String("diameter"),
                Step242TextWriter.Number(hole.Diameter),
                Step242TextWriter.Ref(lengthUnitId));

            var representationId = writer.AddEntity(
                "SHAPE_DIMENSION_REPRESENTATION",
                Step242TextWriter.String($"diameter:{hole.FeatureId}"),
                Step242TextWriter.List(measureItemId),
                Step242TextWriter.Ref(repContextId));

            writer.AddEntity(
                "PROPERTY_DEFINITION_REPRESENTATION",
                Step242TextWriter.Ref(propertyDefinitionId),
                Step242TextWriter.Ref(representationId));

        if (hole.Depth.HasValue)
        {
            var depthItemId = writer.AddEntity(
                "MEASURE_REPRESENTATION_ITEM",
                Step242TextWriter.String("depth"),
                Step242TextWriter.Number(hole.Depth.Value),
                Step242TextWriter.Ref(lengthUnitId));
            var depthRepId = writer.AddEntity(
                "SHAPE_DIMENSION_REPRESENTATION",
                Step242TextWriter.String($"depth:{hole.FeatureId}"),
                Step242TextWriter.List(depthItemId),
                Step242TextWriter.Ref(repContextId));
            var depthPropertyDefinitionId = writer.AddEntity(
                "PROPERTY_DEFINITION",
                Step242TextWriter.String($"depth:{hole.FeatureId}"),
                Step242TextWriter.String("semantic PMI depth"),
                Step242TextWriter.Ref(featureShapeAspectId));
            writer.AddEntity(
                "PROPERTY_DEFINITION_REPRESENTATION",
                Step242TextWriter.Ref(depthPropertyDefinitionId),
                Step242TextWriter.Ref(depthRepId));
        }
    }

    private static void EmitDatumSemanticPmi(
        Step242TextWriter writer,
        string shapeId,
        Step242SemanticPmiDatum datum)
    {
        var aspectId = writer.AddEntity(
            "SHAPE_ASPECT",
            Step242TextWriter.String($"firmament-datum:{datum.Label}"),
            Step242TextWriter.String($"semantic datum {datum.DatumKind} target={datum.Target}"),
            Step242TextWriter.Ref(shapeId),
            Step242TextWriter.Enum("FALSE"));

        writer.AddEntity(
            "PROPERTY_DEFINITION",
            Step242TextWriter.String($"datum:{datum.Label}:{datum.FeatureId}"),
            Step242TextWriter.String($"semantic datum {datum.DatumKind}"),
            Step242TextWriter.Ref(aspectId));
    }

    private static void EmitNoteSemanticPmi(
        Step242TextWriter writer,
        string shapeId,
        Step242SemanticPmiNote note)
    {
        var aspectId = writer.AddEntity(
            "SHAPE_ASPECT",
            Step242TextWriter.String($"firmament-note:{note.FeatureId}"),
            Step242TextWriter.String($"semantic note target={note.Target}"),
            Step242TextWriter.Ref(shapeId),
            Step242TextWriter.Enum("FALSE"));

        writer.AddEntity(
            "PROPERTY_DEFINITION",
            Step242TextWriter.String($"note:{note.FeatureId}"),
            Step242TextWriter.String(note.Text),
            Step242TextWriter.Ref(aspectId));
    }

    private static string BuildPlane(Step242TextWriter writer, PlaneSurface plane)
    {
        var originId = writer.AddEntity("CARTESIAN_POINT", "$", Step242TextWriter.List(Step242TextWriter.Number(plane.Origin.X), Step242TextWriter.Number(plane.Origin.Y), Step242TextWriter.Number(plane.Origin.Z)));
        var normal = plane.Normal.ToVector();
        var uAxis = plane.UAxis.ToVector();
        var normalId = writer.AddEntity("DIRECTION", "$", Step242TextWriter.List(Step242TextWriter.Number(normal.X), Step242TextWriter.Number(normal.Y), Step242TextWriter.Number(normal.Z)));
        var refDirId = writer.AddEntity("DIRECTION", "$", Step242TextWriter.List(Step242TextWriter.Number(uAxis.X), Step242TextWriter.Number(uAxis.Y), Step242TextWriter.Number(uAxis.Z)));
        var axisPlacementId = writer.AddEntity("AXIS2_PLACEMENT_3D", "$", Step242TextWriter.Ref(originId), Step242TextWriter.Ref(normalId), Step242TextWriter.Ref(refDirId));
        return writer.AddEntity("PLANE", "$", Step242TextWriter.Ref(axisPlacementId));
    }


    private static string BuildCylinder(Step242TextWriter writer, CylinderSurface cylinder)
    {
        var axisPlacementId = BuildAxisPlacement(writer, cylinder.Origin, cylinder.Axis, cylinder.XAxis);
        return writer.AddEntity("CYLINDRICAL_SURFACE", "$", Step242TextWriter.Ref(axisPlacementId), Step242TextWriter.Number(cylinder.Radius));
    }

    private static string BuildCone(Step242TextWriter writer, ConeSurface cone)
    {
        var axisPlacementId = BuildAxisPlacement(writer, cone.PlacementOrigin, cone.Axis, cone.ReferenceAxis);
        return writer.AddEntity("CONICAL_SURFACE", "$", Step242TextWriter.Ref(axisPlacementId), Step242TextWriter.Number(cone.PlacementRadius), Step242TextWriter.Number(cone.SemiAngleRadians));
    }

    private static string BuildSphere(Step242TextWriter writer, SphereSurface sphere)
    {
        var axisPlacementId = BuildAxisPlacement(writer, sphere.Center, sphere.Axis, sphere.XAxis);
        return writer.AddEntity("SPHERICAL_SURFACE", "$", Step242TextWriter.Ref(axisPlacementId), Step242TextWriter.Number(sphere.Radius));
    }

    private static string BuildTorus(Step242TextWriter writer, TorusSurface torus)
    {
        var axisPlacementId = BuildAxisPlacement(writer, torus.Center, torus.Axis, torus.XAxis);
        return writer.AddEntity("TOROIDAL_SURFACE", "$", Step242TextWriter.Ref(axisPlacementId), Step242TextWriter.Number(torus.MajorRadius), Step242TextWriter.Number(torus.MinorRadius));
    }

    private static string BuildBSplineSurfaceWithKnots(Step242TextWriter writer, BSplineSurfaceWithKnots surface)
    {
        var controlPointRows = surface.ControlPoints
            .Select(row =>
            {
                var rowPointIds = row
                    .Select(point => writer.AddEntity("CARTESIAN_POINT", "$", PointList(point)))
                    .Select(Step242TextWriter.Ref)
                    .ToArray();
                return Step242TextWriter.List(rowPointIds);
            })
            .ToArray();

        var multiplicitiesU = surface.KnotMultiplicitiesU
            .Select(multiplicity => multiplicity.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        var multiplicitiesV = surface.KnotMultiplicitiesV
            .Select(multiplicity => multiplicity.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        var knotValuesU = surface.KnotValuesU.Select(Step242TextWriter.Number).ToArray();
        var knotValuesV = surface.KnotValuesV.Select(Step242TextWriter.Number).ToArray();

        return writer.AddEntity(
            "B_SPLINE_SURFACE_WITH_KNOTS",
            "$",
            surface.DegreeU.ToString(System.Globalization.CultureInfo.InvariantCulture),
            surface.DegreeV.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Step242TextWriter.List(controlPointRows),
            Step242TextWriter.Enum(surface.SurfaceForm),
            Step242TextWriter.BooleanLogical(surface.UClosed),
            Step242TextWriter.BooleanLogical(surface.VClosed),
            Step242TextWriter.BooleanLogical(surface.SelfIntersect),
            Step242TextWriter.List(multiplicitiesU),
            Step242TextWriter.List(multiplicitiesV),
            Step242TextWriter.List(knotValuesU),
            Step242TextWriter.List(knotValuesV),
            Step242TextWriter.Enum(surface.KnotSpec));
    }

    private static KernelResult<string> BuildSurface(Step242TextWriter writer, SurfaceGeometry surface, FaceId faceId)
    {
        return surface.Kind switch
        {
            SurfaceGeometryKind.Plane when surface.Plane is PlaneSurface plane => KernelResult<string>.Success(BuildPlane(writer, plane)),
            SurfaceGeometryKind.Cylinder when surface.Cylinder is CylinderSurface cylinder => KernelResult<string>.Success(BuildCylinder(writer, cylinder)),
            SurfaceGeometryKind.Cone when surface.Cone is ConeSurface cone => KernelResult<string>.Success(BuildCone(writer, cone)),
            SurfaceGeometryKind.Sphere when surface.Sphere is SphereSurface sphere => KernelResult<string>.Success(BuildSphere(writer, sphere)),
            SurfaceGeometryKind.Torus when surface.Torus is TorusSurface torus => KernelResult<string>.Success(BuildTorus(writer, torus)),
            SurfaceGeometryKind.BSplineSurfaceWithKnots when surface.BSplineSurfaceWithKnots is BSplineSurfaceWithKnots bSplineSurface => KernelResult<string>.Success(BuildBSplineSurfaceWithKnots(writer, bSplineSurface)),
            _ => Failure($"Unsupported surface kind '{surface.Kind}'.", $"Face:{faceId.Value}")
        };
    }

    private static bool CanExportLooplessFace(BrepBody _, Face face, SurfaceGeometry surface) =>
        surface.Kind == SurfaceGeometryKind.Sphere
        && face.LoopIds.Count == 0;

    private static string BuildAxisPlacement(Step242TextWriter writer, Point3D origin, Direction3D axis, Direction3D referenceAxis)
    {
        var originId = writer.AddEntity("CARTESIAN_POINT", "$", PointList(origin));
        var axisVector = axis.ToVector();
        var axisId = writer.AddEntity("DIRECTION", "$", Step242TextWriter.List(Step242TextWriter.Number(axisVector.X), Step242TextWriter.Number(axisVector.Y), Step242TextWriter.Number(axisVector.Z)));
        var referenceVector = referenceAxis.ToVector();
        var referenceId = writer.AddEntity("DIRECTION", "$", Step242TextWriter.List(Step242TextWriter.Number(referenceVector.X), Step242TextWriter.Number(referenceVector.Y), Step242TextWriter.Number(referenceVector.Z)));
        return writer.AddEntity("AXIS2_PLACEMENT_3D", "$", Step242TextWriter.Ref(originId), Step242TextWriter.Ref(axisId), Step242TextWriter.Ref(referenceId));
    }

    private static KernelResult<string> BuildEdgeCurve(
        BrepBody body,
        TopologyModel model,
        Step242TextWriter writer,
        EdgeId edgeId,
        IDictionary<VertexId, Point3D> vertexPoints,
        IDictionary<VertexId, string> cartesianPointIds,
        IDictionary<VertexId, string> vertexPointIds,
        IDictionary<EdgeId, string> lineIds,
        IDictionary<EdgeId, string> circleIds,
        IDictionary<EdgeId, string> bsplineIds,
        IDictionary<EdgeId, string> ellipseIds)
    {
        var edge = model.GetEdge(edgeId);

        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var edgeBinding))
        {
            return Failure("Edge is missing curve binding.", $"Edge:{edgeId.Value}");
        }

        if (!body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve) || curve is null)
        {
            return Failure("Edge curve geometry was not found.", $"Curve:{edgeBinding.CurveGeometryId.Value}");
        }

        if (curve.Kind != CurveGeometryKind.Line3
            && curve.Kind != CurveGeometryKind.Circle3
            && curve.Kind != CurveGeometryKind.BSpline3
            && curve.Kind != CurveGeometryKind.Ellipse3)
        {
            return Failure($"Unsupported curve kind '{curve.Kind}'.", $"Edge:{edgeId.Value}");
        }

        if (edgeBinding.TrimInterval is null)
        {
            return Failure("Edge must provide trim interval for vertex mapping.", $"Edge:{edgeId.Value}");
        }

        var startPointResult = ResolveVertexPoint(body, model, edge.StartVertexId, vertexPoints);
        if (!startPointResult.IsSuccess)
        {
            return KernelResult<string>.Failure(startPointResult.Diagnostics);
        }

        var endPointResult = ResolveVertexPoint(body, model, edge.EndVertexId, vertexPoints);
        if (!endPointResult.IsSuccess)
        {
            return KernelResult<string>.Failure(endPointResult.Diagnostics);
        }

        var startPoint = startPointResult.Value;
        var endPoint = endPointResult.Value;

        var startVertexId = EnsureVertex(writer, edge.StartVertexId, startPoint, vertexPoints, cartesianPointIds, vertexPointIds);
        var endVertexId = EnsureVertex(writer, edge.EndVertexId, endPoint, vertexPoints, cartesianPointIds, vertexPointIds);

        string geometryCurveId;
        if (curve.Kind == CurveGeometryKind.Line3 && curve.Line3 is Line3Curve line)
        {
            if (!lineIds.TryGetValue(edgeId, out var lineId))
            {
                var edgeVector = endPoint - startPoint;
                if (edgeVector.LengthSquared <= 1e-24d || !edgeVector.TryNormalize(out var endpointDirection))
                {
                    return Failure("Edge endpoints resolve to a degenerate line direction.", $"Edge:{edgeId.Value}");
                }

                var direction = endpointDirection;
                var curveDirection = line.Direction.ToVector();
                if (curveDirection.TryNormalize(out var normalizedCurveDirection))
                {
                    var alignment = normalizedCurveDirection.Dot(endpointDirection);
                    if (alignment >= 0.999999999999d)
                    {
                        direction = normalizedCurveDirection;
                    }
                }

                var originId = writer.AddEntity("CARTESIAN_POINT", "$", PointList(startPoint));
                var directionId = writer.AddEntity("DIRECTION", "$", Step242TextWriter.List(Step242TextWriter.Number(direction.X), Step242TextWriter.Number(direction.Y), Step242TextWriter.Number(direction.Z)));
                var vectorId = writer.AddEntity("VECTOR", "$", Step242TextWriter.Ref(directionId), Step242TextWriter.Number(1d));
                lineId = writer.AddEntity("LINE", "$", Step242TextWriter.Ref(originId), Step242TextWriter.Ref(vectorId));
                lineIds[edgeId] = lineId;
            }

            geometryCurveId = lineId;
        }
        else if (curve.Kind == CurveGeometryKind.Circle3 && curve.Circle3 is Circle3Curve circle)
        {
            if (!circleIds.TryGetValue(edgeId, out var circleId))
            {
                var axisPlacementId = BuildAxisPlacement(writer, circle.Center, circle.Normal, circle.XAxis);
                circleId = writer.AddEntity("CIRCLE", "$", Step242TextWriter.Ref(axisPlacementId), Step242TextWriter.Number(circle.Radius));
                circleIds[edgeId] = circleId;
            }

            geometryCurveId = circleId;
        }
        else if (curve.Kind == CurveGeometryKind.BSpline3 && curve.BSpline3 is BSpline3Curve spline)
        {
            if (!bsplineIds.TryGetValue(edgeId, out var bsplineId))
            {
                var controlPointIds = spline.ControlPoints
                    .Select(point => writer.AddEntity("CARTESIAN_POINT", "$", PointList(point)))
                    .Select(Step242TextWriter.Ref)
                    .ToArray();

                var multiplicities = spline.KnotMultiplicities.Select(multiplicity => multiplicity.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var knotValues = spline.KnotValues.Select(Step242TextWriter.Number).ToArray();

                bsplineId = writer.AddEntity(
                    "B_SPLINE_CURVE_WITH_KNOTS",
                    "$",
                    spline.Degree.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Step242TextWriter.List(controlPointIds),
                    Step242TextWriter.Enum(spline.CurveForm),
                    Step242TextWriter.BooleanLogical(spline.ClosedCurve),
                    Step242TextWriter.BooleanLogical(spline.SelfIntersect),
                    Step242TextWriter.List(multiplicities),
                    Step242TextWriter.List(knotValues),
                    Step242TextWriter.Enum(spline.KnotSpec));

                bsplineIds[edgeId] = bsplineId;
            }

            geometryCurveId = bsplineId;
        }
        else if (curve.Kind == CurveGeometryKind.Ellipse3 && curve.Ellipse3 is Ellipse3Curve ellipse)
        {
            if (!ellipseIds.TryGetValue(edgeId, out var ellipseId))
            {
                var axisPlacementId = BuildAxisPlacement(writer, ellipse.Center, ellipse.Normal, ellipse.XAxis);
                ellipseId = writer.AddEntity("ELLIPSE", "$", Step242TextWriter.Ref(axisPlacementId), Step242TextWriter.Number(ellipse.MajorRadius), Step242TextWriter.Number(ellipse.MinorRadius));
                ellipseIds[edgeId] = ellipseId;
            }

            geometryCurveId = ellipseId;
        }
        else
        {
            return Failure($"Unsupported curve kind '{curve.Kind}'.", $"Edge:{edgeId.Value}");
        }

        var edgeCurveId = writer.AddEntity("EDGE_CURVE", "$", Step242TextWriter.Ref(startVertexId), Step242TextWriter.Ref(endVertexId), Step242TextWriter.Ref(geometryCurveId), Step242TextWriter.BooleanLogical(true));
        return KernelResult<string>.Success(edgeCurveId);
    }

    private static string EnsureVertex(
        Step242TextWriter writer,
        VertexId vertexId,
        Point3D point,
        IDictionary<VertexId, Point3D> vertexPoints,
        IDictionary<VertexId, string> cartesianPointIds,
        IDictionary<VertexId, string> vertexPointIds)
    {
        const double tolerance = 1e-9;
        if (vertexPoints.TryGetValue(vertexId, out var existingPoint))
        {
            var delta = existingPoint - point;
            if (delta.LengthSquared > tolerance * tolerance)
            {
#if DEBUG
                throw new InvalidOperationException($"Vertex {vertexId.Value} point inconsistency detected during STEP export.");
#endif
            }
        }
        else
        {
            vertexPoints[vertexId] = point;
        }

        if (!cartesianPointIds.TryGetValue(vertexId, out var pointId))
        {
            pointId = writer.AddEntity("CARTESIAN_POINT", "$", PointList(point));
            cartesianPointIds[vertexId] = pointId;
        }

        if (!vertexPointIds.TryGetValue(vertexId, out var vertexPointId))
        {
            vertexPointId = writer.AddEntity("VERTEX_POINT", "$", Step242TextWriter.Ref(pointId));
            vertexPointIds[vertexId] = vertexPointId;
        }

        return vertexPointId;
    }

    private static KernelResult<Point3D> ResolveVertexPoint(
        BrepBody body,
        TopologyModel model,
        VertexId vertexId,
        IDictionary<VertexId, Point3D> vertexPoints)
    {
        if (vertexPoints.TryGetValue(vertexId, out var resolved))
        {
            return KernelResult<Point3D>.Success(resolved);
        }

        var preferEdgeEndpoint = VertexTouchesNonPlanarFace(body, model, vertexId);
        if (!preferEdgeEndpoint)
        {
            var fromPlanes = ResolveVertexFromIncidentPlanes(body, model, vertexId);
            if (fromPlanes.IsSuccess)
            {
                vertexPoints[vertexId] = fromPlanes.Value;
                return fromPlanes;
            }
        }

        foreach (var edge in model.Edges.OrderBy(e => e.Id.Value))
        {
            var useStart = false;
            if (edge.StartVertexId == vertexId)
            {
                useStart = true;
            }
            else if (edge.EndVertexId != vertexId)
            {
                continue;
            }

            var pointResult = EvaluateEdgeEndpoint(body, edge.Id, useStart);
            if (!pointResult.IsSuccess)
            {
                continue;
            }

            vertexPoints[vertexId] = pointResult.Value;
            return KernelResult<Point3D>.Success(pointResult.Value);
        }

        return FailurePoint($"Vertex {vertexId.Value} cannot be resolved to a geometric point for STEP export.", $"Vertex:{vertexId.Value}");
    }

    private static bool VertexTouchesNonPlanarFace(BrepBody body, TopologyModel model, VertexId vertexId)
    {
        foreach (var face in model.Faces.OrderBy(f => f.Id.Value))
        {
            var touchesVertex = false;
            foreach (var loopId in face.LoopIds)
            {
                var loop = model.GetLoop(loopId);
                foreach (var coedgeId in loop.CoedgeIds)
                {
                    var coedge = model.GetCoedge(coedgeId);
                    var edge = model.GetEdge(coedge.EdgeId);
                    if (edge.StartVertexId == vertexId || edge.EndVertexId == vertexId)
                    {
                        touchesVertex = true;
                        break;
                    }
                }

                if (touchesVertex)
                {
                    break;
                }
            }

            if (!touchesVertex || !body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding))
            {
                continue;
            }

            if (!body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface) || surface is null)
            {
                continue;
            }

            if (surface.Kind != SurfaceGeometryKind.Plane)
            {
                return true;
            }
        }

        return false;
    }

    private static KernelResult<Point3D> ResolveVertexFromIncidentPlanes(BrepBody body, TopologyModel model, VertexId vertexId)
    {
        var planes = new List<PlaneSurface>();
        foreach (var face in model.Faces.OrderBy(f => f.Id.Value))
        {
            var touchesVertex = false;
            foreach (var loopId in face.LoopIds)
            {
                var loop = model.GetLoop(loopId);
                foreach (var coedgeId in loop.CoedgeIds)
                {
                    var coedge = model.GetCoedge(coedgeId);
                    var edge = model.GetEdge(coedge.EdgeId);
                    if (edge.StartVertexId == vertexId || edge.EndVertexId == vertexId)
                    {
                        touchesVertex = true;
                        break;
                    }
                }

                if (touchesVertex)
                {
                    break;
                }
            }

            if (!touchesVertex || !body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding))
            {
                continue;
            }

            if (!body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface) || surface?.Plane is null)
            {
                continue;
            }

            planes.Add(surface.Plane.Value);
        }

        for (var i = 0; i < planes.Count; i++)
        {
            for (var j = i + 1; j < planes.Count; j++)
            {
                for (var k = j + 1; k < planes.Count; k++)
                {
                    if (TryIntersectPlanes(planes[i], planes[j], planes[k], out var intersection))
                    {
                        return KernelResult<Point3D>.Success(intersection);
                    }
                }
            }
        }

        return FailurePoint($"Vertex {vertexId.Value} cannot be resolved from incident face planes.", $"Vertex:{vertexId.Value}");
    }

    private static bool TryIntersectPlanes(PlaneSurface a, PlaneSurface b, PlaneSurface c, out Point3D point)
    {
        var n1 = a.Normal.ToVector();
        var n2 = b.Normal.ToVector();
        var n3 = c.Normal.ToVector();
        var denominator = n1.Dot(n2.Cross(n3));
        if (double.Abs(denominator) <= 1e-12d)
        {
            point = Point3D.Origin;
            return false;
        }

        var d1 = n1.Dot(new Vector3D(a.Origin.X, a.Origin.Y, a.Origin.Z));
        var d2 = n2.Dot(new Vector3D(b.Origin.X, b.Origin.Y, b.Origin.Z));
        var d3 = n3.Dot(new Vector3D(c.Origin.X, c.Origin.Y, c.Origin.Z));

        var numerator = (n2.Cross(n3) * d1) + (n3.Cross(n1) * d2) + (n1.Cross(n2) * d3);
        point = new Point3D(numerator.X / denominator, numerator.Y / denominator, numerator.Z / denominator);
        return double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
    }

    private static KernelResult<Point3D> EvaluateEdgeEndpoint(BrepBody body, EdgeId edgeId, bool useStart)
    {
        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var edgeBinding))
        {
            return FailurePoint("Edge is missing curve binding.", $"Edge:{edgeId.Value}");
        }

        if (edgeBinding.TrimInterval is null)
        {
            return FailurePoint("Edge must provide trim interval for vertex mapping.", $"Edge:{edgeId.Value}");
        }

        if (!body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve) || curve is null)
        {
            return FailurePoint("Edge curve geometry was not found.", $"Curve:{edgeBinding.CurveGeometryId.Value}");
        }

        var useTrimStart = useStart == edgeBinding.OrientedEdgeSense;
        var parameter = useTrimStart ? edgeBinding.TrimInterval.Value.Start : edgeBinding.TrimInterval.Value.End;
        return curve.Kind switch
        {
            CurveGeometryKind.Line3 when curve.Line3 is Line3Curve line => KernelResult<Point3D>.Success(line.Evaluate(parameter)),
            CurveGeometryKind.Circle3 when curve.Circle3 is Circle3Curve circle => KernelResult<Point3D>.Success(circle.Evaluate(parameter)),
            CurveGeometryKind.BSpline3 when curve.BSpline3 is BSpline3Curve spline => KernelResult<Point3D>.Success(spline.Evaluate(parameter)),
            CurveGeometryKind.Ellipse3 when curve.Ellipse3 is Ellipse3Curve ellipse => KernelResult<Point3D>.Success(ellipse.Evaluate(parameter)),
            _ => FailurePoint($"Unsupported curve kind '{curve.Kind}'.", $"Edge:{edgeId.Value}")
        };
    }

    private static string PointList(Point3D point) => Step242TextWriter.List(
        Step242TextWriter.Number(point.X),
        Step242TextWriter.Number(point.Y),
        Step242TextWriter.Number(point.Z));

    private static KernelResult<string> Failure(string message, string source) =>
        KernelResult<string>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                message,
                source)
        ]);

    private static KernelResult<Point3D> FailurePoint(string message, string source) =>
        KernelResult<Point3D>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                message,
                source)
        ]);
}
