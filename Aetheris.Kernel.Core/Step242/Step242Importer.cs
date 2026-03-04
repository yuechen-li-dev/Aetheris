using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

public static class Step242Importer
{
    public static KernelResult<BrepBody> ImportBody(string stepText)
    {
        var parseResult = Step242SubsetParser.Parse(stepText);
        if (!parseResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(parseResult.Diagnostics);
        }

        try
        {
            return MapSubset(parseResult.Value);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"Importer rejected parseable STEP input: {ex.Message}",
                    "Importer.Guardrail")
            ]);
        }
    }

    private static KernelResult<BrepBody> MapSubset(Step242ParsedDocument document)
    {
        var unsupportedEntity = document.Entities.FirstOrDefault(IsClearlyUnsupportedEntity);
        if (unsupportedEntity is not null)
        {
            return Failure($"Entity family '{unsupportedEntity.Name}' is unsupported in M23 import subset.", SourceFor(unsupportedEntity.Id, "Importer.EntityFamily"));
        }

        var manifoldSolidBreps = document.Entities
            .Where(e => string.Equals(e.Name, "MANIFOLD_SOLID_BREP", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (manifoldSolidBreps.Count == 0)
        {
            return Failure("Missing MANIFOLD_SOLID_BREP root entity.", "Importer.TopologyRoot");
        }

        if (manifoldSolidBreps.Count > 1)
        {
            return Failure("Multiple MANIFOLD_SOLID_BREP roots are unsupported in M23 import subset.", "Importer.SingleSolid");
        }

        var brepEntity = manifoldSolidBreps[0];

        var shellRefResult = Step242SubsetDecoder.ReadReference(brepEntity, 1, "MANIFOLD_SOLID_BREP shell");
        if (!shellRefResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(shellRefResult.Diagnostics);
        }

        var shellEntityResult = document.TryGetEntity(shellRefResult.Value.TargetId, "CLOSED_SHELL");
        if (!shellEntityResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(shellEntityResult.Diagnostics);
        }

        var faceRefsResult = Step242SubsetDecoder.ReadReferenceList(shellEntityResult.Value, 1, "CLOSED_SHELL faces");
        if (!faceRefsResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(faceRefsResult.Diagnostics);
        }

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();

        var vertexMap = new Dictionary<int, (VertexId VertexId, Point3D Point)>();
        var edgeMap = new Dictionary<int, EdgeId>();
        var coedges = new List<Coedge>();

        var nextCurveGeometryId = 1;
        var nextSurfaceGeometryId = 1;
        var faceIds = new List<FaceId>();

        foreach (var faceRef in faceRefsResult.Value)
        {
            var faceEntityResult = document.TryGetEntity(faceRef.TargetId, "ADVANCED_FACE");
            if (!faceEntityResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(faceEntityResult.Diagnostics);
            }

            var faceEntity = faceEntityResult.Value;
            var surfaceRefResult = Step242SubsetDecoder.ReadReference(faceEntity, 1, "ADVANCED_FACE surface");
            if (!surfaceRefResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(surfaceRefResult.Diagnostics);
            }

            var surfaceEntityResult = document.TryGetEntity(surfaceRefResult.Value.TargetId);
            if (!surfaceEntityResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(surfaceEntityResult.Diagnostics);
            }

            var isSphericalFace = string.Equals(surfaceEntityResult.Value.Name, "SPHERICAL_SURFACE", StringComparison.OrdinalIgnoreCase);

            var boundRefsResult = Step242SubsetDecoder.ReadReferenceList(faceEntity, 0, "ADVANCED_FACE bounds");
            if (!boundRefsResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(boundRefsResult.Diagnostics);
            }

            if (boundRefsResult.Value.Count == 0 && !isSphericalFace)
            {
                return Failure("ADVANCED_FACE without bounds is unsupported in M23 subset.", $"Entity:{faceEntity.Id}");
            }

            var loopIds = new List<LoopId>(boundRefsResult.Value.Count);
            foreach (var boundRef in boundRefsResult.Value)
            {
                var boundEntityResult = document.TryGetEntity(boundRef.TargetId);
                if (!boundEntityResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(boundEntityResult.Diagnostics);
                }

                var boundEntity = boundEntityResult.Value;
                var isFaceBound = string.Equals(boundEntity.Name, "FACE_BOUND", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(boundEntity.Name, "FACE_OUTER_BOUND", StringComparison.OrdinalIgnoreCase);
                if (!isFaceBound)
                {
                    return Failure($"Entity '{boundEntity.Name}' is unsupported in M23 import subset.", $"Entity:{boundEntity.Id}");
                }

                var loopRefResult = Step242SubsetDecoder.ReadReference(boundEntity, 1, "FACE_BOUND loop");
                if (!loopRefResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(loopRefResult.Diagnostics);
                }

                var boundOrientationResult = Step242SubsetDecoder.ReadBoolean(boundEntity, 2, "FACE_BOUND orientation");
                if (!boundOrientationResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(boundOrientationResult.Diagnostics);
                }

                var boundOrientation = boundOrientationResult.Value;

                var loopEntityResult = document.TryGetEntity(loopRefResult.Value.TargetId, "EDGE_LOOP");
                if (!loopEntityResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(loopEntityResult.Diagnostics);
                }

                var orientedEdgeRefsResult = Step242SubsetDecoder.ReadReferenceList(loopEntityResult.Value, 1, "EDGE_LOOP coedges");
                if (!orientedEdgeRefsResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(orientedEdgeRefsResult.Diagnostics);
                }

                if (orientedEdgeRefsResult.Value.Count == 0)
                {
                    return Failure("EDGE_LOOP must contain at least one ORIENTED_EDGE.", $"Entity:{loopEntityResult.Value.Id}");
                }

                var loopId = builder.AllocateLoopId();
                var coedgeIds = new List<CoedgeId>(orientedEdgeRefsResult.Value.Count);
                for (var i = 0; i < orientedEdgeRefsResult.Value.Count; i++)
                {
                    coedgeIds.Add(builder.AllocateCoedgeId());
                }

                for (var i = 0; i < orientedEdgeRefsResult.Value.Count; i++)
                {
                    var orientedEdgeEntityResult = document.TryGetEntity(orientedEdgeRefsResult.Value[i].TargetId, "ORIENTED_EDGE");
                    if (!orientedEdgeEntityResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(orientedEdgeEntityResult.Diagnostics);
                    }

                    var orientedEdgeEntity = orientedEdgeEntityResult.Value;
                    var edgeCurveRefResult = Step242SubsetDecoder.ReadReference(orientedEdgeEntity, 3, "ORIENTED_EDGE edge element");
                    if (!edgeCurveRefResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeCurveRefResult.Diagnostics);
                    }

                    var edgeCurveEntityResult = document.TryGetEntity(edgeCurveRefResult.Value.TargetId, "EDGE_CURVE");
                    if (!edgeCurveEntityResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeCurveEntityResult.Diagnostics);
                    }

                    var edgeIdResult = EnsureEdge(
                        document,
                        edgeCurveEntityResult.Value,
                        builder,
                        geometry,
                        bindings,
                        vertexMap,
                        edgeMap,
                        ref nextCurveGeometryId);

                    if (!edgeIdResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeIdResult.Diagnostics);
                    }

                    var edgeSameSenseResult = Step242SubsetDecoder.ReadBoolean(edgeCurveEntityResult.Value, 4, "EDGE_CURVE same_sense");
                    if (!edgeSameSenseResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(edgeSameSenseResult.Diagnostics);
                    }

                    var orientedSenseResult = Step242SubsetDecoder.ReadBoolean(orientedEdgeEntity, 4, "ORIENTED_EDGE orientation");
                    if (!orientedSenseResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(orientedSenseResult.Diagnostics);
                    }

                    var isReversed = orientedSenseResult.Value != edgeSameSenseResult.Value;
                    if (!boundOrientation)
                    {
                        isReversed = !isReversed;
                    }

                    coedges.Add(new Coedge(
                        coedgeIds[i],
                        edgeIdResult.Value,
                        loopId,
                        coedgeIds[(i + 1) % coedgeIds.Count],
                        coedgeIds[(i + coedgeIds.Count - 1) % coedgeIds.Count],
                        IsReversed: isReversed));
                }

                builder.AddLoop(new Loop(loopId, coedgeIds));
                loopIds.Add(loopId);
            }

            var faceId = builder.AddFace(loopIds);
            faceIds.Add(faceId);

            var faceSameSenseResult = Step242SubsetDecoder.ReadBoolean(faceEntity, 2, "ADVANCED_FACE same_sense");
            if (!faceSameSenseResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(faceSameSenseResult.Diagnostics);
            }

            var bindSurfaceResult = DecodeSurfaceGeometry(document, surfaceEntityResult.Value, faceSameSenseResult.Value, nextSurfaceGeometryId);
            if (!bindSurfaceResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(bindSurfaceResult.Diagnostics);
            }

            var (surfaceGeometryId, surfaceGeometry) = bindSurfaceResult.Value;
            nextSurfaceGeometryId++;
            geometry.AddSurface(surfaceGeometryId, surfaceGeometry);
            bindings.AddFaceBinding(new FaceGeometryBinding(faceId, surfaceGeometryId));
        }

        foreach (var coedge in coedges)
        {
            builder.AddCoedge(coedge);
        }

        var shellId = builder.AddShell(faceIds);
        builder.AddBody([shellId]);

        var body = new BrepBody(builder.Model, geometry, bindings);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(validation.Diagnostics);
        }

        return KernelResult<BrepBody>.Success(body, validation.Diagnostics);
    }

    private static KernelResult<EdgeId> EnsureEdge(
        Step242ParsedDocument document,
        Step242ParsedEntity edgeCurveEntity,
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        IDictionary<int, (VertexId VertexId, Point3D Point)> vertexMap,
        IDictionary<int, EdgeId> edgeMap,
        ref int nextCurveGeometryId)
    {
        if (edgeMap.TryGetValue(edgeCurveEntity.Id, out var existingEdgeId))
        {
            return KernelResult<EdgeId>.Success(existingEdgeId);
        }

        var startRefResult = Step242SubsetDecoder.ReadReference(edgeCurveEntity, 1, "EDGE_CURVE start");
        if (!startRefResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(startRefResult.Diagnostics);
        }

        var endRefResult = Step242SubsetDecoder.ReadReference(edgeCurveEntity, 2, "EDGE_CURVE end");
        if (!endRefResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(endRefResult.Diagnostics);
        }

        var lineRefResult = Step242SubsetDecoder.ReadReference(edgeCurveEntity, 3, "EDGE_CURVE geometry");
        if (!lineRefResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(lineRefResult.Diagnostics);
        }

        var sameSenseResult = Step242SubsetDecoder.ReadBoolean(edgeCurveEntity, 4, "EDGE_CURVE same_sense");
        if (!sameSenseResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(sameSenseResult.Diagnostics);
        }

        var edgeSameSense = sameSenseResult.Value;

        var startVertexResult = EnsureVertex(document, startRefResult.Value.TargetId, builder, vertexMap);
        if (!startVertexResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(startVertexResult.Diagnostics);
        }

        var endVertexResult = EnsureVertex(document, endRefResult.Value.TargetId, builder, vertexMap);
        if (!endVertexResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(endVertexResult.Diagnostics);
        }

        var curveEntityResult = document.TryGetEntity(lineRefResult.Value.TargetId);
        if (!curveEntityResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(curveEntityResult.Diagnostics);
        }

        var edgeId = builder.AddEdge(startVertexResult.Value.VertexId, endVertexResult.Value.VertexId);
        edgeMap.Add(edgeCurveEntity.Id, edgeId);

        var curveGeometryId = new CurveGeometryId(nextCurveGeometryId++);

        var bindCurveResult = DecodeCurveGeometry(
            document,
            curveEntityResult.Value,
            startVertexResult.Value.Point,
            endVertexResult.Value.Point,
            edgeSameSense,
            edgeCurveEntity.Id);
        if (!bindCurveResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(bindCurveResult.Diagnostics);
        }

        geometry.AddCurve(curveGeometryId, bindCurveResult.Value.CurveGeometry);
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, curveGeometryId, bindCurveResult.Value.TrimInterval));

        return KernelResult<EdgeId>.Success(edgeId);
    }

    private static KernelResult<(VertexId VertexId, Point3D Point)> EnsureVertex(
        Step242ParsedDocument document,
        int vertexPointEntityId,
        TopologyBuilder builder,
        IDictionary<int, (VertexId VertexId, Point3D Point)> vertexMap)
    {
        if (vertexMap.TryGetValue(vertexPointEntityId, out var existingVertex))
        {
            return KernelResult<(VertexId VertexId, Point3D Point)>.Success(existingVertex);
        }

        var pointResult = Step242SubsetDecoder.ReadVertexPoint(document, vertexPointEntityId);
        if (!pointResult.IsSuccess)
        {
            return KernelResult<(VertexId VertexId, Point3D Point)>.Failure(pointResult.Diagnostics);
        }

        var vertex = (builder.AddVertex(), pointResult.Value);
        vertexMap.Add(vertexPointEntityId, vertex);
        return KernelResult<(VertexId VertexId, Point3D Point)>.Success(vertex);
    }

    private static bool IsClearlyUnsupportedEntity(Step242ParsedEntity entity)
    {
        return string.Equals(entity.Name, "TOROIDAL_SURFACE", StringComparison.OrdinalIgnoreCase);
    }

    private static KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)> DecodeCurveGeometry(
        Step242ParsedDocument document,
        Step242ParsedEntity curveEntity,
        Point3D startPoint,
        Point3D endPoint,
        bool edgeSameSense,
        int edgeCurveEntityId)
    {
        if (string.Equals(curveEntity.Name, "LINE", StringComparison.OrdinalIgnoreCase))
        {
            var lineResult = Step242SubsetDecoder.ReadLineCurve(document, curveEntity);
            if (!lineResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(lineResult.Diagnostics);
            }

            var startParameter = ComputeLineParameter(lineResult.Value, startPoint);
            var endParameter = ComputeLineParameter(lineResult.Value, endPoint);

            if (endParameter < startParameter)
            {
                if (!edgeSameSense)
                {
                    return OrientationFailure<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>(
                        "EDGE_CURVE same_sense semantics unsupported for this edge",
                        SourceFor(edgeCurveEntityId, "Importer.Orientation.EdgeCurveSense"));
                }

                return FailureCurveBinding("EDGE_CURVE line parameterization is opposite to vertex ordering.", SourceFor(edgeCurveEntityId, "Importer.Geometry.EdgeCurveParameters"));
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromLine(lineResult.Value),
                new ParameterInterval(startParameter, endParameter)));
        }

        if (string.Equals(curveEntity.Name, "CIRCLE", StringComparison.OrdinalIgnoreCase))
        {
            var circleResult = Step242SubsetDecoder.ReadCircleCurve(document, curveEntity);
            if (!circleResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(circleResult.Diagnostics);
            }

            var trimResult = ComputeCircleTrim(circleResult.Value, startPoint, endPoint);
            if (!trimResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(trimResult.Diagnostics);
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromCircle(circleResult.Value),
                trimResult.Value));
        }

        return FailureCurveBinding($"EDGE_CURVE geometry '{curveEntity.Name}' is unsupported.", SourceFor(curveEntity.Id, "Importer.EntityFamily"));
    }

    private static KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)> DecodeSurfaceGeometry(
        Step242ParsedDocument document,
        Step242ParsedEntity surfaceEntity,
        bool faceSameSense,
        int nextSurfaceGeometryId)
    {
        var geometryId = new SurfaceGeometryId(nextSurfaceGeometryId);

        if (string.Equals(surfaceEntity.Name, "PLANE", StringComparison.OrdinalIgnoreCase))
        {
            var planeResult = Step242SubsetDecoder.ReadPlaneSurface(document, surfaceEntity);
            if (!planeResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(planeResult.Diagnostics);
            }

            var faceSurface = planeResult.Value;
            if (!faceSameSense)
            {
                if (!Direction3D.TryCreate(-faceSurface.Normal.ToVector(), out var reversedNormal))
                {
                    return OrientationFailure<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>(
                        "ADVANCED_FACE same_sense not supported for this face",
                        SourceFor(surfaceEntity.Id, "Importer.Orientation.AdvancedFaceSense"));
                }

                faceSurface = new PlaneSurface(faceSurface.Origin, reversedNormal, faceSurface.UAxis);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromPlane(faceSurface)));
        }

        if (string.Equals(surfaceEntity.Name, "CYLINDRICAL_SURFACE", StringComparison.OrdinalIgnoreCase))
        {
            var cylinderResult = Step242SubsetDecoder.ReadCylindricalSurface(document, surfaceEntity);
            if (!cylinderResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(cylinderResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromCylinder(cylinderResult.Value)));
        }

        if (string.Equals(surfaceEntity.Name, "SPHERICAL_SURFACE", StringComparison.OrdinalIgnoreCase))
        {
            var sphereResult = Step242SubsetDecoder.ReadSphericalSurface(document, surfaceEntity);
            if (!sphereResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(sphereResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromSphere(sphereResult.Value)));
        }

        if (string.Equals(surfaceEntity.Name, "CONICAL_SURFACE", StringComparison.OrdinalIgnoreCase))
        {
            var coneResult = Step242SubsetDecoder.ReadConicalSurface(document, surfaceEntity);
            if (!coneResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(coneResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromCone(coneResult.Value)));
        }

        return FailureSurfaceBinding($"ADVANCED_FACE surface '{surfaceEntity.Name}' is unsupported.", SourceFor(surfaceEntity.Id, "Importer.EntityFamily"));
    }

    private static KernelResult<ParameterInterval> ComputeCircleTrim(Circle3Curve circle, Point3D startPoint, Point3D endPoint)
    {
        const double tolerance = 1e-6d;
        if ((startPoint - endPoint).Length <= tolerance)
        {
            return KernelResult<ParameterInterval>.Success(new ParameterInterval(0d, 2d * double.Pi));
        }

        var startAngleResult = ProjectPointToCircleAngle(circle, startPoint, tolerance);
        if (!startAngleResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(startAngleResult.Diagnostics);
        }

        var endAngleResult = ProjectPointToCircleAngle(circle, endPoint, tolerance);
        if (!endAngleResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(endAngleResult.Diagnostics);
        }

        var start = startAngleResult.Value;
        var end = endAngleResult.Value;
        if (end < start)
        {
            end += 2d * double.Pi;
        }

        if (end <= start)
        {
            return FailureCircleTrim("Unable to compute circle trim interval with a positive span.", "Importer.Geometry.CircleTrim");
        }

        return KernelResult<ParameterInterval>.Success(new ParameterInterval(start, end));
    }

    private static KernelResult<double> ProjectPointToCircleAngle(Circle3Curve circle, Point3D point, double tolerance)
    {
        var radial = point - circle.Center;
        var normalComponent = radial.Dot(circle.Normal.ToVector());
        var inPlane = radial - (circle.Normal.ToVector() * normalComponent);
        var inPlaneLength = inPlane.Length;

        if (double.Abs(normalComponent) > tolerance || double.Abs(inPlaneLength - circle.Radius) > tolerance)
        {
            return FailureCircleTrimAngle("Unable to compute circle trim from supplied vertices.", "Importer.Geometry.CircleTrim");
        }

        var x = inPlane.Dot(circle.XAxis.ToVector());
        var y = inPlane.Dot(circle.YAxis.ToVector());
        var angle = double.Atan2(y, x);
        if (angle < 0d)
        {
            angle += 2d * double.Pi;
        }

        return KernelResult<double>.Success(angle);
    }

    private static double ComputeLineParameter(Line3Curve line, Point3D point)
    {
        var offset = point - line.Origin;
        return offset.Dot(line.Direction.ToVector());
    }

    private static KernelResult<BrepBody> Failure(string message, string source) =>
        KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<EdgeId> FailureEdge(string message, string source) =>
        KernelResult<EdgeId>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)> FailureCurveBinding(string message, string source) =>
        KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)> FailureSurfaceBinding(string message, string source) =>
        KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<ParameterInterval> FailureCircleTrim(string message, string source) =>
        KernelResult<ParameterInterval>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<double> FailureCircleTrimAngle(string message, string source) =>
        KernelResult<double>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<T> OrientationFailure<T>(string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

    private static string SourceFor(int _entityId, string stableSource) => stableSource;
}
