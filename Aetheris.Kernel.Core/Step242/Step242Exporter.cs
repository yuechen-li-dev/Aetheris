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
    {
        options ??= new Step242ExportOptions();

        var model = body.Topology;
        var bodyNodes = model.Bodies.OrderBy(b => b.Id.Value).ToArray();
        if (bodyNodes.Length != 1)
        {
            return Failure("Only single-body export is supported.", "Topology.Bodies");
        }

        var shellIds = bodyNodes[0].ShellIds.OrderBy(s => s.Value).ToArray();
        if (shellIds.Length != 1)
        {
            return Failure("Only one shell per body is supported.", "Topology.Shells");
        }

        var shell = model.GetShell(shellIds[0]);
        var writer = new Step242TextWriter();

        var vertexPointIds = new Dictionary<VertexId, string>();
        var cartesianPointIds = new Dictionary<VertexId, string>();

        var edgeCurveIds = new Dictionary<EdgeId, string>();
        var orientedEdgeIds = new Dictionary<CoedgeId, string>();
        var lineIds = new Dictionary<EdgeId, string>();

        var faceIds = new List<string>();

        foreach (var face in shell.FaceIds.OrderBy(id => id.Value).Select(model.GetFace))
        {
            if (face.LoopIds.Count == 0)
            {
                return Failure("Faces without boundary loops are not supported in M22 export subset.", $"Face:{face.Id.Value}");
            }

            if (!body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding))
            {
                return Failure("Face is missing surface binding.", $"Face:{face.Id.Value}");
            }

            if (!body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface) || surface is null)
            {
                return Failure("Face surface geometry was not found.", $"Surface:{faceBinding.SurfaceGeometryId.Value}");
            }

            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is null)
            {
                return Failure($"Unsupported surface kind '{surface.Kind}'. M22 supports planar faces only.", $"Face:{face.Id.Value}");
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
                        var edgeResult = BuildEdgeCurve(body, model, writer, coedge.EdgeId, cartesianPointIds, vertexPointIds, lineIds);
                        if (!edgeResult.IsSuccess)
                        {
                            return KernelResult<string>.Failure(edgeResult.Diagnostics);
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
                var boundId = writer.AddEntity(boundEntity, "$", Step242TextWriter.Ref(edgeLoopId), Step242TextWriter.BooleanLogical(true));
                loopBoundIds.Add(boundId);
            }

            var planeId = BuildPlane(writer, surface.Plane.Value);
            var advancedFaceId = writer.AddEntity(
                "ADVANCED_FACE",
                Step242TextWriter.List(loopBoundIds.ToArray()),
                Step242TextWriter.Ref(planeId),
                Step242TextWriter.BooleanLogical(true));

            faceIds.Add(advancedFaceId);
        }

        var closedShellId = writer.AddEntity("CLOSED_SHELL", "$", Step242TextWriter.List(faceIds.ToArray()));
        var brepId = writer.AddEntity("MANIFOLD_SOLID_BREP", Step242TextWriter.String(options.ProductName), Step242TextWriter.Ref(closedShellId));

        var appContextId = writer.AddEntity("APPLICATION_CONTEXT", Step242TextWriter.String("mechanical design"));
        var productContextId = writer.AddEntity("PRODUCT_CONTEXT", Step242TextWriter.String(""), Step242TextWriter.Ref(appContextId), Step242TextWriter.String("mechanical"));
        var productId = writer.AddEntity("PRODUCT", Step242TextWriter.String("AETHERIS"), Step242TextWriter.String(options.ProductName), Step242TextWriter.String(""), Step242TextWriter.List(productContextId));
        var formationId = writer.AddEntity("PRODUCT_DEFINITION_FORMATION", Step242TextWriter.String(""), Step242TextWriter.String(""), Step242TextWriter.Ref(productId));
        var definitionContextId = writer.AddEntity("PRODUCT_DEFINITION_CONTEXT", Step242TextWriter.String("design"), Step242TextWriter.Ref(appContextId), Step242TextWriter.String("design"));
        var definitionId = writer.AddEntity("PRODUCT_DEFINITION", Step242TextWriter.String(""), Step242TextWriter.String(""), Step242TextWriter.Ref(formationId), Step242TextWriter.Ref(definitionContextId));
        var shapeId = writer.AddEntity("PRODUCT_DEFINITION_SHAPE", Step242TextWriter.String(""), Step242TextWriter.String(""), Step242TextWriter.Ref(definitionId));
        var repContextId = writer.AddEntity("GEOMETRIC_REPRESENTATION_CONTEXT", "3");
        var shapeRepresentationId = writer.AddEntity("SHAPE_REPRESENTATION", Step242TextWriter.String(options.ProductName), Step242TextWriter.List(brepId), Step242TextWriter.Ref(repContextId));
        writer.AddEntity("SHAPE_DEFINITION_REPRESENTATION", Step242TextWriter.Ref(shapeId), Step242TextWriter.Ref(shapeRepresentationId));

        return KernelResult<string>.Success(writer.Build(options.ApplicationName));
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

    private static KernelResult<string> BuildEdgeCurve(
        BrepBody body,
        TopologyModel model,
        Step242TextWriter writer,
        EdgeId edgeId,
        IDictionary<VertexId, string> cartesianPointIds,
        IDictionary<VertexId, string> vertexPointIds,
        IDictionary<EdgeId, string> lineIds)
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

        if (curve.Kind != CurveGeometryKind.Line3 || curve.Line3 is null)
        {
            return Failure($"Unsupported curve kind '{curve.Kind}'. M22 supports line edges only.", $"Edge:{edgeId.Value}");
        }

        if (edgeBinding.TrimInterval is null)
        {
            return Failure("Line edge must provide trim interval for vertex mapping.", $"Edge:{edgeId.Value}");
        }

        var line = curve.Line3.Value;
        var startPoint = line.Evaluate(edgeBinding.TrimInterval.Value.Start);
        var endPoint = line.Evaluate(edgeBinding.TrimInterval.Value.End);

        var startVertexId = EnsureVertex(writer, edge.StartVertexId, startPoint, cartesianPointIds, vertexPointIds);
        var endVertexId = EnsureVertex(writer, edge.EndVertexId, endPoint, cartesianPointIds, vertexPointIds);

        if (!lineIds.TryGetValue(edgeId, out var lineId))
        {
            var originId = writer.AddEntity("CARTESIAN_POINT", "$", PointList(line.Origin));
            var direction = line.Direction.ToVector();
            var directionId = writer.AddEntity("DIRECTION", "$", Step242TextWriter.List(Step242TextWriter.Number(direction.X), Step242TextWriter.Number(direction.Y), Step242TextWriter.Number(direction.Z)));
            var vectorId = writer.AddEntity("VECTOR", "$", Step242TextWriter.Ref(directionId), Step242TextWriter.Number(1d));
            lineId = writer.AddEntity("LINE", "$", Step242TextWriter.Ref(originId), Step242TextWriter.Ref(vectorId));
            lineIds[edgeId] = lineId;
        }

        var edgeCurveId = writer.AddEntity("EDGE_CURVE", "$", Step242TextWriter.Ref(startVertexId), Step242TextWriter.Ref(endVertexId), Step242TextWriter.Ref(lineId), Step242TextWriter.BooleanLogical(true));
        return KernelResult<string>.Success(edgeCurveId);
    }

    private static string EnsureVertex(
        Step242TextWriter writer,
        VertexId vertexId,
        Point3D point,
        IDictionary<VertexId, string> cartesianPointIds,
        IDictionary<VertexId, string> vertexPointIds)
    {
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
}
