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
            var boundRefsResult = Step242SubsetDecoder.ReadReferenceList(faceEntity, 0, "ADVANCED_FACE bounds");
            if (!boundRefsResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(boundRefsResult.Diagnostics);
            }

            if (boundRefsResult.Value.Count == 0)
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

                    var orientedSenseResult = Step242SubsetDecoder.ReadBoolean(orientedEdgeEntity, 4, "ORIENTED_EDGE orientation");
                    if (!orientedSenseResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(orientedSenseResult.Diagnostics);
                    }

                    coedges.Add(new Coedge(
                        coedgeIds[i],
                        edgeIdResult.Value,
                        loopId,
                        coedgeIds[(i + 1) % coedgeIds.Count],
                        coedgeIds[(i + coedgeIds.Count - 1) % coedgeIds.Count],
                        IsReversed: !orientedSenseResult.Value));
                }

                builder.AddLoop(new Loop(loopId, coedgeIds));
                loopIds.Add(loopId);
            }

            var faceId = builder.AddFace(loopIds);
            faceIds.Add(faceId);

            var planeRefResult = Step242SubsetDecoder.ReadReference(faceEntity, 1, "ADVANCED_FACE surface");
            if (!planeRefResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(planeRefResult.Diagnostics);
            }

            var planeEntityResult = document.TryGetEntity(planeRefResult.Value.TargetId, "PLANE");
            if (!planeEntityResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(planeEntityResult.Diagnostics);
            }

            var planeResult = Step242SubsetDecoder.ReadPlaneSurface(document, planeEntityResult.Value);
            if (!planeResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(planeResult.Diagnostics);
            }

            var surfaceGeometryId = new SurfaceGeometryId(nextSurfaceGeometryId++);
            geometry.AddSurface(surfaceGeometryId, SurfaceGeometry.FromPlane(planeResult.Value));
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

        if (!sameSenseResult.Value)
        {
            return KernelResult<EdgeId>.Failure([
                new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, "EDGE_CURVE with same_sense=.F. is unsupported in M23 subset.", SourceFor(edgeCurveEntity.Id, "Importer.EdgeCurveSense"))
            ]);
        }

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

        var lineEntityResult = document.TryGetEntity(lineRefResult.Value.TargetId, "LINE");
        if (!lineEntityResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(lineEntityResult.Diagnostics);
        }

        var lineResult = Step242SubsetDecoder.ReadLineCurve(document, lineEntityResult.Value);
        if (!lineResult.IsSuccess)
        {
            return KernelResult<EdgeId>.Failure(lineResult.Diagnostics);
        }

        var edgeId = builder.AddEdge(startVertexResult.Value.VertexId, endVertexResult.Value.VertexId);
        edgeMap.Add(edgeCurveEntity.Id, edgeId);

        var startParameter = ComputeLineParameter(lineResult.Value, startVertexResult.Value.Point);
        var endParameter = ComputeLineParameter(lineResult.Value, endVertexResult.Value.Point);
        var curveGeometryId = new CurveGeometryId(nextCurveGeometryId++);

        geometry.AddCurve(curveGeometryId, CurveGeometry.FromLine(lineResult.Value));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, curveGeometryId, new ParameterInterval(startParameter, endParameter)));

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
        return string.Equals(entity.Name, "SPHERICAL_SURFACE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Name, "CIRCLE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Name, "CYLINDRICAL_SURFACE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entity.Name, "CONICAL_SURFACE", StringComparison.OrdinalIgnoreCase);
    }

    private static double ComputeLineParameter(Line3Curve line, Point3D point)
    {
        var offset = point - line.Origin;
        return offset.Dot(line.Direction.ToVector());
    }

    private static KernelResult<BrepBody> Failure(string message, string source) =>
        KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static string SourceFor(int _entityId, string stableSource) => stableSource;
}
