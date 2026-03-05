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
    private const double AreaEps = 1e-8d;
    private const double PointOnSurfaceEps = 1e-5d;
    private const double AngleUnwrapEps = 1e-8d;
    private const double ContainmentEps = 1e-8d;

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
            var advancedFaceOffset = faceEntity.Arguments.Count >= 4
                && (faceEntity.Arguments[0] is Step242StringValue || faceEntity.Arguments[0] is Step242OmittedValue)
                ? 1
                : 0;

            var surfaceResult = Step242SubsetDecoder.ReadEntityRefOrInlineConstructor(faceEntity, advancedFaceOffset + 1, "ADVANCED_FACE surface");
            if (!surfaceResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(surfaceResult.Diagnostics);
            }

            Step242ParsedEntity? surfaceEntity = null;
            string surfaceName;
            IReadOnlyList<Step242Value>? inlineSurfaceArguments = null;

            if (surfaceResult.Value.IsReference)
            {
                var surfaceEntityResult = document.TryGetEntity(surfaceResult.Value.ReferenceId!.Value);
                if (!surfaceEntityResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(surfaceEntityResult.Diagnostics);
                }

                surfaceEntity = surfaceEntityResult.Value;
                surfaceName = surfaceEntityResult.Value.Name;
            }
            else
            {
                surfaceName = surfaceResult.Value.InlineName!;
                inlineSurfaceArguments = surfaceResult.Value.InlineArguments;
            }

            var isSphericalFace = string.Equals(surfaceName, "SPHERICAL_SURFACE", StringComparison.OrdinalIgnoreCase);

            var boundRefsResult = Step242SubsetDecoder.ReadAdvancedFaceBounds(faceEntity, advancedFaceOffset, "ADVANCED_FACE bounds");
            if (!boundRefsResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(boundRefsResult.Diagnostics);
            }

            if (boundRefsResult.Value.Count == 0 && !isSphericalFace)
            {
                return Failure("ADVANCED_FACE without bounds is unsupported in M23 subset.", $"Entity:{faceEntity.Id}");
            }

            var faceSameSenseResult = Step242SubsetDecoder.ReadBoolean(faceEntity, advancedFaceOffset + 2, "ADVANCED_FACE same_sense");
            if (!faceSameSenseResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(faceSameSenseResult.Diagnostics);
            }

            var bindSurfaceResult = DecodeSurfaceGeometry(document, surfaceEntity, surfaceName, inlineSurfaceArguments, faceSameSenseResult.Value, nextSurfaceGeometryId, faceEntity.Id);
            if (!bindSurfaceResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(bindSurfaceResult.Diagnostics);
            }

            var loopData = new List<LoopBuildData>(boundRefsResult.Value.Count);
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

                var loopCoedges = new List<Coedge>(orientedEdgeRefsResult.Value.Count);
                var loopSamples = new List<Point3D>();

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

                    var coedge = new Coedge(
                        coedgeIds[i],
                        edgeIdResult.Value,
                        loopId,
                        coedgeIds[(i + 1) % coedgeIds.Count],
                        coedgeIds[(i + coedgeIds.Count - 1) % coedgeIds.Count],
                        IsReversed: isReversed);

                    loopCoedges.Add(coedge);
                    var sampleResult = SampleCoedgePoints(bindings.GetEdgeBinding(edgeIdResult.Value), geometry, coedge.IsReversed);
                    if (!sampleResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(sampleResult.Diagnostics);
                    }

                    AppendLoopSamples(loopSamples, sampleResult.Value);
                }

                builder.AddLoop(new Loop(loopId, coedgeIds));
                loopData.Add(new LoopBuildData(loopId, loopCoedges, loopSamples));
            }

            var classifyResult = ClassifyAndNormalizeFaceLoops(loopData, bindSurfaceResult.Value.SurfaceGeometry);
            if (!classifyResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(classifyResult.Diagnostics);
            }

            foreach (var loop in classifyResult.Value)
            {
                coedges.AddRange(loop.Coedges);
            }

            var faceLoopIds = classifyResult.Value.Select(l => l.LoopId).ToList();

            var faceId = builder.AddFace(faceLoopIds);
            faceIds.Add(faceId);

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
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, curveGeometryId, bindCurveResult.Value.TrimInterval, edgeSameSense));

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

        if (string.Equals(curveEntity.Name, "AETHERIS_PLANAR_UNSUPPORTED_CURVE", StringComparison.OrdinalIgnoreCase))
        {
            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromUnsupported(curveEntity.Name),
                new ParameterInterval(0d, 1d)));
        }

        return FailureCurveBinding($"EDGE_CURVE geometry '{curveEntity.Name}' is unsupported.", SourceFor(curveEntity.Id, "Importer.EntityFamily"));
    }

    private static KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)> DecodeSurfaceGeometry(
        Step242ParsedDocument document,
        Step242ParsedEntity? surfaceEntity,
        string surfaceName,
        IReadOnlyList<Step242Value>? inlineSurfaceArguments,
        bool faceSameSense,
        int nextSurfaceGeometryId,
        int faceEntityId)
    {
        var normalizedName = surfaceName.ToUpperInvariant();
        var surfaceToDecodeResult = surfaceEntity is null
            ? BuildInlineSurfaceEntity(faceEntityId, normalizedName, inlineSurfaceArguments)
            : KernelResult<Step242ParsedEntity>.Success(surfaceEntity);
        if (!surfaceToDecodeResult.IsSuccess)
        {
            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(surfaceToDecodeResult.Diagnostics);
        }

        var surfaceToDecode = surfaceToDecodeResult.Value;
        var geometryId = new SurfaceGeometryId(nextSurfaceGeometryId);

        if (string.Equals(normalizedName, "PLANE", StringComparison.Ordinal))
        {
            var planeResult = Step242SubsetDecoder.ReadPlaneSurface(document, surfaceToDecode);
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
                        SourceFor(surfaceToDecode.Id, "Importer.Orientation.AdvancedFaceSense"));
                }

                faceSurface = new PlaneSurface(faceSurface.Origin, reversedNormal, faceSurface.UAxis);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromPlane(faceSurface)));
        }

        if (string.Equals(normalizedName, "CYLINDRICAL_SURFACE", StringComparison.Ordinal))
        {
            var cylinderResult = Step242SubsetDecoder.ReadCylindricalSurface(document, surfaceToDecode);
            if (!cylinderResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(cylinderResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromCylinder(cylinderResult.Value)));
        }

        if (string.Equals(normalizedName, "SPHERICAL_SURFACE", StringComparison.Ordinal))
        {
            var sphereResult = Step242SubsetDecoder.ReadSphericalSurface(document, surfaceToDecode);
            if (!sphereResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(sphereResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromSphere(sphereResult.Value)));
        }

        if (string.Equals(normalizedName, "CONICAL_SURFACE", StringComparison.Ordinal))
        {
            var coneResult = Step242SubsetDecoder.ReadConicalSurface(document, surfaceToDecode);
            if (!coneResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(coneResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromCone(coneResult.Value)));
        }

        return FailureSurfaceBinding($"ADVANCED_FACE surface '{surfaceName}' is unsupported.", SourceFor(surfaceToDecode.Id, "Importer.EntityFamily"));
    }

    private static KernelResult<Step242ParsedEntity> BuildInlineSurfaceEntity(int faceEntityId, string surfaceName, IReadOnlyList<Step242Value>? inlineArguments)
    {
        if (inlineArguments is null)
        {
            return FailureInlineSurface<Step242ParsedEntity>("ADVANCED_FACE surface: inline constructor arguments are required.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (!string.Equals(surfaceName, "PLANE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "CYLINDRICAL_SURFACE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "CONICAL_SURFACE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "SPHERICAL_SURFACE", StringComparison.Ordinal))
        {
            return FailureInlineSurface<Step242ParsedEntity>($"ADVANCED_FACE surface: inline constructor '{surfaceName}' is not supported in this subset.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        var normalizedArgumentsResult = NormalizeInlineSurfaceArguments(surfaceName, inlineArguments, faceEntityId);
        if (!normalizedArgumentsResult.IsSuccess)
        {
            return KernelResult<Step242ParsedEntity>.Failure(normalizedArgumentsResult.Diagnostics);
        }

        return KernelResult<Step242ParsedEntity>.Success(new Step242ParsedEntity(
            faceEntityId,
            new Step242SimpleEntityInstance(new Step242EntityConstructor(surfaceName, normalizedArgumentsResult.Value))));
    }

    private static KernelResult<IReadOnlyList<Step242Value>> NormalizeInlineSurfaceArguments(string surfaceName, IReadOnlyList<Step242Value> inlineArguments, int faceEntityId)
    {
        if (string.Equals(surfaceName, "PLANE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 1)
            {
                if (inlineArguments[0] is not Step242EntityReference)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("ADVANCED_FACE surface: inline PLANE constructor expects PLANE(#axis2_placement_3d).", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0]
                ]);
            }

            if (inlineArguments.Count == 2)
            {
                if (inlineArguments[0] is not Step242OmittedValue || inlineArguments[1] is not Step242EntityReference)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("ADVANCED_FACE surface: inline PLANE constructor expects PLANE(#axis2_placement_3d).", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("ADVANCED_FACE surface: inline PLANE constructor expects PLANE(#axis2_placement_3d).", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (string.Equals(surfaceName, "CYLINDRICAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 2)
            {
                if (inlineArguments[0] is not Step242EntityReference || inlineArguments[1] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0],
                    inlineArguments[1]
                ]);
            }

            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242OmittedValue || inlineArguments[1] is not Step242EntityReference || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CYLINDRICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (string.Equals(surfaceName, "CONICAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242EntityReference
                    || inlineArguments[1] is not Step242NumberValue
                    || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0],
                    inlineArguments[1],
                    inlineArguments[2]
                ]);
            }

            if (inlineArguments.Count == 4)
            {
                if (inlineArguments[0] is not Step242OmittedValue
                    || inlineArguments[1] is not Step242EntityReference
                    || inlineArguments[2] is not Step242NumberValue
                    || inlineArguments[3] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'CONICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        if (string.Equals(surfaceName, "SPHERICAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 2)
            {
                if (inlineArguments[0] is not Step242EntityReference || inlineArguments[1] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'SPHERICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success([
                    Step242OmittedValue.Instance,
                    inlineArguments[0],
                    inlineArguments[1]
                ]);
            }

            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242OmittedValue || inlineArguments[1] is not Step242EntityReference || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'SPHERICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'SPHERICAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
        }

        return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
    }

    private static KernelResult<T> FailureInlineSurface<T>(string message, string source)
        => KernelResult<T>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.NotImplemented,
                KernelDiagnosticSeverity.Error,
                message,
                source)
        ]);

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

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizeFaceLoops(
        IReadOnlyList<LoopBuildData> loops,
        SurfaceGeometry surface)
    {
        if (loops.Count <= 1)
        {
            return KernelResult<IReadOnlyList<LoopBuildData>>.Success(loops);
        }

        return surface.Kind switch
        {
            SurfaceGeometryKind.Plane => ClassifyAndNormalizePlanarLoops(loops, surface.Plane!.Value),
            SurfaceGeometryKind.Cylinder => LoopRoleFailure<IReadOnlyList<LoopBuildData>>(
                "Cylinder multi-loop hole classification is not yet safely supported.",
                "Importer.LoopRole.CylinderMappingFailed"),
            SurfaceGeometryKind.Cone or SurfaceGeometryKind.Sphere => LoopRoleFailure<IReadOnlyList<LoopBuildData>>(
                "Multi-loop hole classification is unsupported for this surface type.",
                "Importer.LoopRole.UnsupportedSurfaceForHoles"),
            _ => LoopRoleFailure<IReadOnlyList<LoopBuildData>>(
                "Multi-loop hole classification is unsupported for this surface type.",
                "Importer.LoopRole.UnsupportedSurfaceForHoles")
        };
    }

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizePlanarLoops(
        IReadOnlyList<LoopBuildData> loops,
        PlaneSurface plane)
    {
        var infos = new List<PlanarLoopInfo>(loops.Count);
        foreach (var loop in loops)
        {
            var projected = ProjectLoopToPlane(loop.Samples, plane);
            var uniqueCount = CountUniquePoints(projected);
            if (uniqueCount < 3)
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Loop projection is degenerate.", "Importer.LoopRole.DegenerateLoop");
            }

            var area = ComputeSignedArea(projected);
            infos.Add(new PlanarLoopInfo(loop, projected, area));
        }

        var orderedByArea = infos
            .OrderByDescending(info => double.Abs(info.SignedArea))
            .ToList();

        var outer = orderedByArea[0];
        if (orderedByArea.Count > 1 && double.Abs(double.Abs(outer.SignedArea) - double.Abs(orderedByArea[1].SignedArea)) <= AreaEps)
        {
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to choose a unique outer loop.", "Importer.LoopRole.AmbiguousOuter");
        }

        foreach (var candidate in infos.Where(i => i.Loop.LoopId != outer.Loop.LoopId))
        {
            var testPointResult = ChooseContainmentPoint(candidate.ProjectedPoints);
            if (!testPointResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<LoopBuildData>>.Failure(testPointResult.Diagnostics);
            }

            if (!IsPointInPolygon(testPointResult.Value, outer.ProjectedPoints))
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Inner loop is not contained by outer loop.", "Importer.LoopRole.InnerNotContained");
            }
        }

        var innerLoops = infos.Where(i => i.Loop.LoopId != outer.Loop.LoopId).ToList();
        for (var i = 0; i < innerLoops.Count; i++)
        {
            for (var j = i + 1; j < innerLoops.Count; j++)
            {
                if (LoopsOverlap(innerLoops[i], innerLoops[j]))
                {
                    return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Inner loops overlap or are nested.", "Importer.LoopRole.InnerOverlap");
                }
            }
        }

        var normalizedOuter = NormalizeLoopWinding(outer.Loop, outer.SignedArea, shouldBePositive: true);
        var normalizedInners = innerLoops
            .OrderBy(i => i.Loop.LoopId.Value)
            .Select(i => NormalizeLoopWinding(i.Loop, i.SignedArea, shouldBePositive: false))
            .ToList();

        var ordered = new List<LoopBuildData>(1 + normalizedInners.Count) { normalizedOuter };
        ordered.AddRange(normalizedInners);
        return KernelResult<IReadOnlyList<LoopBuildData>>.Success(ordered);
    }

    private static LoopBuildData NormalizeLoopWinding(LoopBuildData loop, double signedArea, bool shouldBePositive)
    {
        if (shouldBePositive ? signedArea >= 0d : signedArea <= 0d)
        {
            return loop;
        }

        var flippedCoedges = loop.Coedges
            .Select(c => c with { IsReversed = !c.IsReversed })
            .ToList();

        return new LoopBuildData(loop.LoopId, flippedCoedges, loop.Samples);
    }

    private static bool LoopsOverlap(PlanarLoopInfo a, PlanarLoopInfo b)
    {
        var aPoint = ChooseContainmentPoint(a.ProjectedPoints);
        var bPoint = ChooseContainmentPoint(b.ProjectedPoints);
        if (!aPoint.IsSuccess || !bPoint.IsSuccess)
        {
            return true;
        }

        return IsPointInPolygon(aPoint.Value, b.ProjectedPoints) || IsPointInPolygon(bPoint.Value, a.ProjectedPoints);
    }

    private static KernelResult<UvPoint> ChooseContainmentPoint(IReadOnlyList<UvPoint> polygon)
    {
        if (polygon.Count < 4)
        {
            return LoopRoleFailure<UvPoint>("Unable to choose a containment sample point.", "Importer.LoopRole.DegenerateLoop");
        }

        var centroid = ComputePolygonCentroid(polygon);
        if (!IsPointNearPolygonEdge(centroid, polygon) && IsPointInPolygon(centroid, polygon))
        {
            return KernelResult<UvPoint>.Success(centroid);
        }

        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var start = polygon[i];
            var end = polygon[i + 1];
            var midpoint = new UvPoint((start.X + end.X) * 0.5d, (start.Y + end.Y) * 0.5d);
            var towardCentroid = centroid - midpoint;
            var candidate = midpoint + (towardCentroid * 0.125d);
            if (!IsPointNearPolygonEdge(candidate, polygon) && IsPointInPolygon(candidate, polygon))
            {
                return KernelResult<UvPoint>.Success(candidate);
            }
        }

        return LoopRoleFailure<UvPoint>("Unable to choose a containment sample point.", "Importer.LoopRole.DegenerateLoop");
    }

    private static UvPoint ComputePolygonCentroid(IReadOnlyList<UvPoint> polygon)
    {
        var sumX = 0d;
        var sumY = 0d;
        var count = 0;

        for (var i = 0; i < polygon.Count - 1; i++)
        {
            sumX += polygon[i].X;
            sumY += polygon[i].Y;
            count++;
        }

        if (count == 0)
        {
            return new UvPoint(0d, 0d);
        }

        return new UvPoint(sumX / count, sumY / count);
    }

    private static bool IsPointNearPolygonEdge(UvPoint point, IReadOnlyList<UvPoint> polygon)
    {
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            if (DistancePointToSegment(point, polygon[i], polygon[i + 1]) <= ContainmentEps)
            {
                return true;
            }
        }

        return false;
    }

    private static double DistancePointToSegment(UvPoint p, UvPoint a, UvPoint b)
    {
        var ab = b - a;
        var ap = p - a;
        var denom = ab.Dot(ab);
        if (denom <= AreaEps)
        {
            return (p - a).Length;
        }

        var t = ap.Dot(ab) / denom;
        if (t < 0d)
        {
            t = 0d;
        }
        else if (t > 1d)
        {
            t = 1d;
        }
        var closest = a + (ab * t);
        return (p - closest).Length;
    }

    private static bool IsPointInPolygon(UvPoint point, IReadOnlyList<UvPoint> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var a = polygon[i];
            var b = polygon[i + 1];

            if (DistancePointToSegment(point, a, b) <= ContainmentEps)
            {
                return true;
            }

            var intersects = ((a.Y > point.Y) != (b.Y > point.Y))
                             && (point.X < ((b.X - a.X) * (point.Y - a.Y) / ((b.Y - a.Y) + AngleUnwrapEps)) + a.X);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static int CountUniquePoints(IReadOnlyList<UvPoint> points)
    {
        var unique = new List<UvPoint>();
        foreach (var point in points)
        {
            if (unique.Any(u => (u - point).Length <= ContainmentEps))
            {
                continue;
            }

            unique.Add(point);
        }

        return unique.Count;
    }

    private static List<UvPoint> ProjectLoopToPlane(IReadOnlyList<Point3D> samples, PlaneSurface plane)
    {
        var uv = new List<UvPoint>(samples.Count + 1);
        foreach (var sample in samples)
        {
            var offset = sample - plane.Origin;
            uv.Add(new UvPoint(offset.Dot(plane.UAxis.ToVector()), offset.Dot(plane.VAxis.ToVector())));
        }

        if (uv.Count > 0 && (uv[0] - uv[^1]).Length > ContainmentEps)
        {
            uv.Add(uv[0]);
        }

        return uv;
    }

    private static double ComputeSignedArea(IReadOnlyList<UvPoint> polygon)
    {
        var area = 0d;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var p0 = polygon[i];
            var p1 = polygon[i + 1];
            area += (p0.X * p1.Y) - (p1.X * p0.Y);
        }

        return 0.5d * area;
    }

    private static void AppendLoopSamples(ICollection<Point3D> loopSamples, IReadOnlyList<Point3D> edgeSamples)
    {
        foreach (var point in edgeSamples)
        {
            if (loopSamples.Count > 0)
            {
                var last = loopSamples.Last();
                if ((last - point).Length <= PointOnSurfaceEps)
                {
                    continue;
                }
            }

            loopSamples.Add(point);
        }
    }

    private static KernelResult<IReadOnlyList<Point3D>> SampleCoedgePoints(EdgeGeometryBinding edgeBinding, BrepGeometryStore geometry, bool isReversed)
    {
        if (!geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve) || curve is null)
        {
            return LoopRoleFailure<IReadOnlyList<Point3D>>("Missing edge curve geometry for loop sampling.", "Importer.LoopRole.WindingConflict");
        }

        List<Point3D> points;
        switch (curve.Kind)
        {
            case CurveGeometryKind.Line3:
                var line = curve.Line3!.Value;
                var lineRange = edgeBinding.TrimInterval ?? new ParameterInterval(0d, 1d);
                points = [line.Evaluate(lineRange.Start), line.Evaluate(lineRange.End)];
                break;
            case CurveGeometryKind.Circle3:
                var circle = curve.Circle3!.Value;
                var trim = edgeBinding.TrimInterval ?? new ParameterInterval(0d, 2d * double.Pi);
                var mid = trim.Start + ((trim.End - trim.Start) * 0.5d);
                points = [circle.Evaluate(trim.Start), circle.Evaluate(mid), circle.Evaluate(trim.End)];
                break;
            default:
                points = [];
                break;
        }

        if (isReversed)
        {
            points.Reverse();
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(points);
    }

    private static KernelResult<T> LoopRoleFailure<T>(string message, string source) =>
        KernelResult<T>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, source)]);

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

    private sealed record LoopBuildData(LoopId LoopId, IReadOnlyList<Coedge> Coedges, IReadOnlyList<Point3D> Samples);

    private sealed record PlanarLoopInfo(LoopBuildData Loop, IReadOnlyList<UvPoint> ProjectedPoints, double SignedArea);

    private readonly record struct UvPoint(double X, double Y)
    {
        public static UvPoint operator +(UvPoint a, UvPoint b) => new(a.X + b.X, a.Y + b.Y);

        public static UvPoint operator -(UvPoint a, UvPoint b) => new(a.X - b.X, a.Y - b.Y);

        public static UvPoint operator *(UvPoint a, double s) => new(a.X * s, a.Y * s);

        public double Dot(UvPoint other) => (X * other.X) + (Y * other.Y);

        public double Length => double.Sqrt((X * X) + (Y * Y));
    }
}
