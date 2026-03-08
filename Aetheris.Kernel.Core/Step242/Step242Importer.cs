using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using System.Threading;

namespace Aetheris.Kernel.Core.Step242;

public static class Step242Importer
{
    private const double AreaEps = 1e-8d;
    private const double PointOnSurfaceEps = 1e-5d;
    private const double AngleUnwrapEps = 1e-8d;
    private const double ContainmentEps = 1e-8d;
    private static readonly AsyncLocal<ICollection<LoopRoleCircularSamplingDiagnostic>?> CircularSamplingDiagnosticsSink = new();

    public static IDisposable CaptureLoopRoleCircularSamplingDiagnostics(ICollection<LoopRoleCircularSamplingDiagnostic> sink)
    {
        var previous = CircularSamplingDiagnosticsSink.Value;
        CircularSamplingDiagnosticsSink.Value = sink;
        return new CircularSamplingDiagnosticsScope(previous);
    }

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

                var loopEntityResult = document.TryGetEntity(loopRefResult.Value.TargetId);
                if (!loopEntityResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(loopEntityResult.Diagnostics);
                }

                if (string.Equals(loopEntityResult.Value.Name, "VERTEX_LOOP", StringComparison.OrdinalIgnoreCase))
                {
                    if (!isSphericalFace)
                    {
                        return Failure("FACE_BOUND loop type 'VERTEX_LOOP' is unsupported for non-spherical faces in M23 import subset.", $"Entity:{loopEntityResult.Value.Id}");
                    }

                    var vertexLoopRefResult = Step242SubsetDecoder.ReadReference(loopEntityResult.Value, 1, "VERTEX_LOOP vertex");
                    if (!vertexLoopRefResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(vertexLoopRefResult.Diagnostics);
                    }

                    var vertexPointResult = Step242SubsetDecoder.ReadVertexPoint(document, vertexLoopRefResult.Value.TargetId);
                    if (!vertexPointResult.IsSuccess)
                    {
                        return KernelResult<BrepBody>.Failure(vertexPointResult.Diagnostics);
                    }

                    // Singular spherical bounds can be represented with VERTEX_LOOP. They do not map
                    // to edge/coedge topology in the current subset, so they are treated as degenerate trims.
                    continue;
                }

                if (!string.Equals(loopEntityResult.Value.Name, "EDGE_LOOP", StringComparison.OrdinalIgnoreCase))
                {
                    return Failure($"FACE_BOUND loop type '{loopEntityResult.Value.Name}' is unsupported in M23 import subset.", $"Entity:{loopEntityResult.Value.Id}");
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
                    var sampleResult = SampleCoedgePoints(
                        bindings.GetEdgeBinding(edgeIdResult.Value),
                        geometry,
                        coedge.IsReversed,
                        faceEntity.Id,
                        loopId.Value,
                        coedge.Id.Value,
                        edgeIdResult.Value.Value);
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

    private static KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)> DecodeCurveGeometry(
        Step242ParsedDocument document,
        Step242ParsedEntity curveEntity,
        Point3D startPoint,
        Point3D endPoint,
        bool edgeSameSense,
        int edgeCurveEntityId)
    {
        var lineConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "LINE");
        if (lineConstructor is not null)
        {
            var lineResult = Step242SubsetDecoder.ReadLineCurve(document, WithConstructor(curveEntity, lineConstructor));
            if (!lineResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(lineResult.Diagnostics);
            }

            var startParameter = ComputeLineParameter(lineResult.Value, startPoint);
            var endParameter = ComputeLineParameter(lineResult.Value, endPoint);

            if (endParameter < startParameter)
            {
                if (edgeSameSense)
                {
                    return FailureCurveBinding("EDGE_CURVE line parameterization is opposite to vertex ordering.", SourceFor(edgeCurveEntityId, "Importer.Geometry.EdgeCurveParameters"));
                }

                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                    CurveGeometry.FromLine(lineResult.Value),
                    new ParameterInterval(endParameter, startParameter)));
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromLine(lineResult.Value),
                new ParameterInterval(startParameter, endParameter)));
        }

        var circleConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "CIRCLE");
        if (circleConstructor is not null)
        {
            var circleResult = Step242SubsetDecoder.ReadCircleCurve(document, WithConstructor(curveEntity, circleConstructor));
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

        var splineEntity = ResolveBSplineCurveEntity(curveEntity);
        if (splineEntity is not null)
        {
            var splineResult = Step242SubsetDecoder.ReadBSplineCurveWithKnots(document, splineEntity);
            if (!splineResult.IsSuccess)
            {
                return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Failure(splineResult.Diagnostics);
            }

            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromBSpline(splineResult.Value),
                new ParameterInterval(splineResult.Value.DomainStart, splineResult.Value.DomainEnd)));
        }

        if (string.Equals(curveEntity.Name, "AETHERIS_PLANAR_UNSUPPORTED_CURVE", StringComparison.OrdinalIgnoreCase))
        {
            return KernelResult<(CurveGeometry CurveGeometry, ParameterInterval TrimInterval)>.Success((
                CurveGeometry.FromUnsupported(curveEntity.Name),
                new ParameterInterval(0d, 1d)));
        }

        return FailureCurveBinding($"EDGE_CURVE geometry '{curveEntity.Name}' is unsupported.", SourceFor(curveEntity.Id, "Importer.EntityFamily"));
    }

    private static Step242ParsedEntity? ResolveBSplineCurveEntity(Step242ParsedEntity curveEntity)
    {
        var splineWithKnotsConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "B_SPLINE_CURVE_WITH_KNOTS");
        if (splineWithKnotsConstructor is null)
        {
            return null;
        }

        if (splineWithKnotsConstructor.Arguments.Count >= 9)
        {
            return WithConstructor(curveEntity, splineWithKnotsConstructor);
        }

        var splineConstructor = Step242SubsetDecoder.TryGetConstructor(curveEntity.Instance, "B_SPLINE_CURVE");
        if (splineConstructor is null || splineConstructor.Arguments.Count < 5 || splineWithKnotsConstructor.Arguments.Count < 3)
        {
            return null;
        }

        var normalizedArguments = new List<Step242Value>(9)
        {
            Step242OmittedValue.Instance,
            splineConstructor.Arguments[0],
            splineConstructor.Arguments[1],
            splineConstructor.Arguments[2],
            splineConstructor.Arguments[3],
            splineConstructor.Arguments[4],
            splineWithKnotsConstructor.Arguments[0],
            splineWithKnotsConstructor.Arguments[1],
            splineWithKnotsConstructor.Arguments[2]
        };

        var normalizedConstructor = new Step242EntityConstructor("B_SPLINE_CURVE_WITH_KNOTS", normalizedArguments);
        return WithConstructor(curveEntity, normalizedConstructor);
    }

    private static Step242ParsedEntity WithConstructor(Step242ParsedEntity entity, Step242EntityConstructor constructor) =>
        new(entity.Id, new Step242SimpleEntityInstance(constructor));

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

        if (string.Equals(normalizedName, "TOROIDAL_SURFACE", StringComparison.Ordinal))
        {
            var torusResult = Step242SubsetDecoder.ReadToroidalSurface(document, surfaceToDecode);
            if (!torusResult.IsSuccess)
            {
                return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Failure(torusResult.Diagnostics);
            }

            return KernelResult<(SurfaceGeometryId SurfaceGeometryId, SurfaceGeometry SurfaceGeometry)>.Success((geometryId, SurfaceGeometry.FromTorus(torusResult.Value)));
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
            && !string.Equals(surfaceName, "SPHERICAL_SURFACE", StringComparison.Ordinal)
            && !string.Equals(surfaceName, "TOROIDAL_SURFACE", StringComparison.Ordinal))
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

        if (string.Equals(surfaceName, "TOROIDAL_SURFACE", StringComparison.Ordinal))
        {
            if (inlineArguments.Count == 3)
            {
                if (inlineArguments[0] is not Step242EntityReference
                    || inlineArguments[1] is not Step242NumberValue
                    || inlineArguments[2] is not Step242NumberValue)
                {
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'TOROIDAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
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
                    return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'TOROIDAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
                }

                return KernelResult<IReadOnlyList<Step242Value>>.Success(inlineArguments);
            }

            return FailureInlineSurface<IReadOnlyList<Step242Value>>("Inline ADVANCED_FACE.surface constructor 'TOROIDAL_SURFACE' has unsupported argument shape.", SourceFor(faceEntityId, "Importer.StepSyntax.InlineEntity"));
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
        var tolerance = ComputeCircleTrimTolerance(circle, startPoint, endPoint);
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

        var intervalResult = CanonicalizeCircleTrimInterval(startAngleResult.Value, endAngleResult.Value, tolerance);
        if (!intervalResult.IsSuccess)
        {
            return KernelResult<ParameterInterval>.Failure(intervalResult.Diagnostics);
        }

        return KernelResult<ParameterInterval>.Success(intervalResult.Value);
    }

    private static KernelResult<double> ProjectPointToCircleAngle(Circle3Curve circle, Point3D point, double tolerance)
    {
        var radial = point - circle.Center;
        var normalComponent = radial.Dot(circle.Normal.ToVector());
        var inPlane = radial - (circle.Normal.ToVector() * normalComponent);
        var inPlaneLength = inPlane.Length;

        if (double.Abs(normalComponent) > tolerance)
        {
            return FailureCircleTrimAngle($"Unable to project circular trim point: off-circle plane deviation {double.Abs(normalComponent):G17} exceeds tolerance {tolerance:G17}.", "Importer.Geometry.CircleTrim.OffPlane");
        }

        if (double.Abs(inPlaneLength - circle.Radius) > tolerance)
        {
            return FailureCircleTrimAngle($"Unable to project circular trim point: radial deviation {double.Abs(inPlaneLength - circle.Radius):G17} exceeds tolerance {tolerance:G17}.", "Importer.Geometry.CircleTrim.OffRadius");
        }

        if (inPlaneLength <= tolerance)
        {
            return FailureCircleTrimAngle("Unable to project circular trim point: projected in-plane radius is degenerate.", "Importer.Geometry.CircleTrim.DegeneratePoint");
        }

        var projected = inPlane * (circle.Radius / inPlaneLength);
        var x = projected.Dot(circle.XAxis.ToVector());
        var y = projected.Dot(circle.YAxis.ToVector());
        var angle = double.Atan2(y, x);
        if (angle < 0d)
        {
            angle += 2d * double.Pi;
        }

        return KernelResult<double>.Success(angle);
    }

    private static double ComputeCircleTrimTolerance(Circle3Curve circle, Point3D startPoint, Point3D endPoint)
    {
        const double baseTolerance = 1e-6d;
        // Circle-trim projection tolerance is intentionally bounded and geometry-scale-aware:
        // - lower-bounded by baseTolerance to avoid denorm/zero-scale instability,
        // - scaled by local circle/endpoint size to absorb exporter drift observed in AP242 corpus files,
        // - scoped to circle-trim recovery only (not a global tolerance loosening knob).
        const double scaleFactor = 5e-4d;
        var startRadius = (startPoint - circle.Center).Length;
        var endRadius = (endPoint - circle.Center).Length;
        var scale = double.Max(1d, double.Max(circle.Radius, double.Max(startRadius, endRadius)));
        return double.Max(baseTolerance, scale * scaleFactor);
    }

    private static KernelResult<ParameterInterval> CanonicalizeCircleTrimInterval(double startAngle, double endAngle, double tolerance)
    {
        var start = NormalizeAngle(startAngle);
        var end = NormalizeAngle(endAngle);
        var span = NormalizePositiveAngle(end - start);
        var angleTolerance = tolerance;

        if (span <= angleTolerance)
        {
            return KernelResult<ParameterInterval>.Success(new ParameterInterval(0d, 2d * double.Pi));
        }

        if (span >= (2d * double.Pi) - angleTolerance)
        {
            return KernelResult<ParameterInterval>.Success(new ParameterInterval(0d, 2d * double.Pi));
        }

        var normalizedEnd = start + span;
        if (normalizedEnd <= start)
        {
            return FailureCircleTrim("Unable to compute circle trim interval with a positive span after canonicalization.", "Importer.Geometry.CircleTrim.Interval");
        }

        return KernelResult<ParameterInterval>.Success(new ParameterInterval(start, normalizedEnd));
    }

    private static double NormalizeAngle(double angle)
    {
        var period = 2d * double.Pi;
        var normalized = angle % period;
        if (normalized < 0d)
        {
            normalized += period;
        }

        if (normalized >= period)
        {
            normalized -= period;
        }

        return normalized;
    }

    private static double NormalizePositiveAngle(double angle)
    {
        var period = 2d * double.Pi;
        var normalized = angle % period;
        if (normalized < 0d)
        {
            normalized += period;
        }

        return normalized;
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
            SurfaceGeometryKind.Cylinder => ClassifyAndNormalizeCylindricalLoops(loops, surface.Cylinder!.Value),
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
            var projected = SimplifyClosedPolygon(ProjectLoopToPlane(loop.Samples, plane), ContainmentEps);
            var uniqueCount = CountUniquePoints(projected, ContainmentEps);
            var area = ComputeSignedArea(projected);
            if (uniqueCount < 3 || double.Abs(area) <= AreaEps)
            {
                continue;
            }

            infos.Add(new PlanarLoopInfo(loop, projected, area));
        }

        if (infos.Count == 0)
        {
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Loop projection is degenerate and no non-degenerate boundary remained.", "Importer.LoopRole.DegenerateLoop");
        }

        var containmentTolerance = ComputeContainmentTolerance(infos.SelectMany(i => i.ProjectedPoints));
        var areaTolerance = ComputeAreaTolerance(infos.SelectMany(i => i.ProjectedPoints));

        var infosWithSamples = new List<PlanarLoopInfoWithSample>(infos.Count);
        foreach (var info in infos)
        {
            var testPointResult = ChooseContainmentPoint(info.ProjectedPoints, containmentTolerance);
            if (!testPointResult.IsSuccess)
            {
                return KernelResult<IReadOnlyList<LoopBuildData>>.Failure(testPointResult.Diagnostics);
            }

            infosWithSamples.Add(new PlanarLoopInfoWithSample(info, testPointResult.Value));
        }

        var orderedByContainmentThenArea = infosWithSamples
            .Select(candidate =>
            {
                var containmentCount = infosWithSamples.Count(other => other.Info.Loop.LoopId != candidate.Info.Loop.LoopId
                    && IsLoopContainedByOuter(other.Info.ProjectedPoints, candidate.Info.ProjectedPoints, containmentTolerance));
                return new PlanarLoopOuterCandidate(candidate.Info, candidate.SamplePoint, containmentCount);
            })
            .OrderByDescending(c => c.ContainmentCount)
            .ThenByDescending(c => double.Abs(c.Info.SignedArea))
            .ThenBy(c => c.Info.Loop.LoopId.Value)
            .ToList();

        var outer = orderedByContainmentThenArea[0];
        if (orderedByContainmentThenArea.Count > 1
            && outer.ContainmentCount == orderedByContainmentThenArea[1].ContainmentCount
            && double.Abs(double.Abs(outer.Info.SignedArea) - double.Abs(orderedByContainmentThenArea[1].Info.SignedArea)) <= areaTolerance)
        {
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to choose a unique outer loop.", "Importer.LoopRole.AmbiguousOuter");
        }

        var containedInners = new List<PlanarLoopInfo>();

        foreach (var candidate in infosWithSamples.Where(i => i.Info.Loop.LoopId != outer.Info.Loop.LoopId))
        {
            var containment = EvaluateContainment(candidate.Info.ProjectedPoints, outer.Info.ProjectedPoints, containmentTolerance);
            var intersectionCount = CountPolygonIntersections(candidate.Info.ProjectedPoints, outer.Info.ProjectedPoints, containmentTolerance);
            var intersectsOuter = intersectionCount > 0;
            if ((!intersectsOuter && containment.OutsideCount == 0)
                || (intersectsOuter
                    && containment.OutsideCount == 0
                    && containment.MinDistanceToOuter > (containmentTolerance * 8d)))
            {
                containedInners.Add(candidate.Info);
                continue;
            }

            if (double.Abs(candidate.Info.SignedArea) <= areaTolerance * 4d
                || IsPointNearPolygonEdge(candidate.SamplePoint, outer.Info.ProjectedPoints, containmentTolerance * 4d))
            {
                continue;
            }

            var failure = BuildInnerContainmentFailure(candidate.Info, outer.Info, containmentTolerance, areaTolerance, intersectionCount);
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>(failure.Message, failure.Source);
        }

        var innerLoops = containedInners;
        for (var i = 0; i < innerLoops.Count; i++)
        {
            for (var j = i + 1; j < innerLoops.Count; j++)
            {
                if (LoopsOverlap(innerLoops[i], innerLoops[j], containmentTolerance))
                {
                    return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Inner loops overlap or are nested.", "Importer.LoopRole.InnerOverlap");
                }
            }
        }

        var normalizedOuter = NormalizeLoopWinding(outer.Info.Loop, outer.Info.SignedArea, shouldBePositive: true);
        var normalizedInners = innerLoops
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
            .Select(i => NormalizeLoopWinding(i.Loop, i.SignedArea, shouldBePositive: false))
            .ToList();

        var ordered = new List<LoopBuildData>(1 + normalizedInners.Count) { normalizedOuter };
        ordered.AddRange(normalizedInners);
        return KernelResult<IReadOnlyList<LoopBuildData>>.Success(ordered);
    }

    private static KernelResult<IReadOnlyList<LoopBuildData>> ClassifyAndNormalizeCylindricalLoops(
        IReadOnlyList<LoopBuildData> loops,
        CylinderSurface cylinder)
    {
        const string nonNormalizableSource = "Importer.LoopRole.CylinderNonNormalizableDegenerateProjection";
        const string ambiguousOuterSource = "Importer.LoopRole.CylinderAmbiguousOuter";
        const string containmentSource = "Importer.LoopRole.CylinderInnerContainmentFailed";

        var infos = new List<CylindricalLoopInfo>(loops.Count);
        var maxUniqueCount = 0;
        var maxAbsArea = 0d;
        foreach (var loop in loops)
        {
            var projected = ProjectLoopToCylinder(loop.Samples, cylinder);
            var uniqueCount = CountUniquePoints(projected, ContainmentEps);
            var area = ComputeSignedArea(projected);
            maxUniqueCount = System.Math.Max(maxUniqueCount, uniqueCount);
            maxAbsArea = System.Math.Max(maxAbsArea, double.Abs(area));
            if (uniqueCount < 3 || double.Abs(area) <= AreaEps)
            {
                continue;
            }

            infos.Add(new CylindricalLoopInfo(loop, projected, area, ComputePolygonCentroid(projected)));
        }

        if (infos.Count == 0)
        {
            return LoopRoleFailure<IReadOnlyList<LoopBuildData>>($"Cylinder loop normalization failed: all {loops.Count} loop(s) projected degenerate (maxUnique={maxUniqueCount}, maxAbsArea={maxAbsArea:E6}).", nonNormalizableSource);
        }

        var orderedByArea = infos
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
            .ToArray();

        var outer = orderedByArea[0];
        if (orderedByArea.Length > 1)
        {
            var areaTolerance = ComputeAreaTolerance(orderedByArea.SelectMany(i => i.ProjectedPoints));
            if (double.Abs(double.Abs(outer.SignedArea) - double.Abs(orderedByArea[1].SignedArea)) <= areaTolerance)
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to choose a unique cylindrical outer loop.", ambiguousOuterSource);
            }
        }

        var containmentTolerance = ComputeContainmentTolerance(outer.ProjectedPoints);
        var containedInners = new List<CylindricalLoopInfo>();
        foreach (var candidate in infos.Where(i => i.Loop.LoopId != outer.Loop.LoopId))
        {
            var alignedCandidate = AlignPolygonToReference(candidate.ProjectedPoints, candidate.Centroid.X, outer.Centroid.X);
            var sampleResult = ChooseContainmentPoint(alignedCandidate, containmentTolerance);
            if (!sampleResult.IsSuccess)
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>("Unable to classify cylindrical inner loop containment.", containmentSource);
            }

            if (!IsPointInPolygon(sampleResult.Value, outer.ProjectedPoints, containmentTolerance))
            {
                return LoopRoleFailure<IReadOnlyList<LoopBuildData>>($"Cylindrical inner loop {candidate.Loop.LoopId.Value} could not be normalized inside selected outer loop {outer.Loop.LoopId.Value}.", containmentSource);
            }

            containedInners.Add(candidate with { ProjectedPoints = alignedCandidate, Centroid = ComputePolygonCentroid(alignedCandidate) });
        }

        var normalizedOuter = NormalizeLoopWinding(outer.Loop, outer.SignedArea, shouldBePositive: true);
        var normalizedInners = containedInners
            .OrderByDescending(i => double.Abs(i.SignedArea))
            .ThenBy(i => i.Loop.LoopId.Value)
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

    private static bool LoopsOverlap(PlanarLoopInfo a, PlanarLoopInfo b, double containmentTolerance)
    {
        var aPoint = ChooseContainmentPoint(a.ProjectedPoints, containmentTolerance);
        var bPoint = ChooseContainmentPoint(b.ProjectedPoints, containmentTolerance);
        if (!aPoint.IsSuccess || !bPoint.IsSuccess)
        {
            return true;
        }

        return IsPointInPolygon(aPoint.Value, b.ProjectedPoints, containmentTolerance) || IsPointInPolygon(bPoint.Value, a.ProjectedPoints, containmentTolerance);
    }

    private static ContainmentFailure BuildInnerContainmentFailure(
        PlanarLoopInfo inner,
        PlanarLoopInfo outer,
        double containmentTolerance,
        double areaTolerance,
        int intersectionCount)
    {
        var containment = EvaluateContainment(inner.ProjectedPoints, outer.ProjectedPoints, containmentTolerance);
        var crossingOuter = intersectionCount > 0;

        var areaRatio = double.Abs(outer.SignedArea) <= areaTolerance
            ? 0d
            : double.Abs(inner.SignedArea) / double.Abs(outer.SignedArea);

        var reason = crossingOuter
            ? (containment.OutsideCount == 0 ? "crosses_outer_boundary_with_all_vertices_inside" : "crosses_outer_boundary_with_outside_vertices")
            : (containment.OutsideCount == containment.VertexCount ? "disjoint" : "partially_outside");

        var source = reason switch
        {
            "crosses_outer_boundary_with_all_vertices_inside" => "Importer.LoopRole.InnerBoundaryIntersectionWithContainedVerticesAfterNormalization",
            "crosses_outer_boundary_with_outside_vertices" => "Importer.LoopRole.InnerBoundaryIntersectionWithOutsideVerticesAfterNormalization",
            "disjoint" => "Importer.LoopRole.InnerDisjointAfterNormalization",
            _ => "Importer.LoopRole.InnerPartiallyOutsideAfterNormalization"
        };

        var message = $"Inner loop could not be normalized: {reason}. innerLoopId={inner.Loop.LoopId.Value}, outerLoopId={outer.Loop.LoopId.Value}, outsideVertices={containment.OutsideCount}/{containment.VertexCount}, nearestOuterDistance={containment.MinDistanceToOuter:E6}, areaRatio={areaRatio:E6}, intersections={intersectionCount}.";
        return new ContainmentFailure(message, source);
    }

    private static ContainmentEvaluation EvaluateContainment(
        IReadOnlyList<UvPoint> inner,
        IReadOnlyList<UvPoint> outer,
        double containmentTolerance)
    {
        var outsideCount = 0;
        var vertexCount = 0;
        var minDistanceToOuter = double.MaxValue;

        for (var i = 0; i < inner.Count - 1; i++)
        {
            var point = inner[i];
            vertexCount++;
            minDistanceToOuter = double.Min(minDistanceToOuter, DistancePointToPolygon(point, outer));
            if (!IsPointInPolygon(point, outer, containmentTolerance))
            {
                outsideCount++;
            }
        }

        if (double.IsPositiveInfinity(minDistanceToOuter) || minDistanceToOuter == double.MaxValue)
        {
            minDistanceToOuter = 0d;
        }

        return new ContainmentEvaluation(outsideCount, vertexCount, minDistanceToOuter);
    }

    private static bool IsLoopContainedByOuter(
        IReadOnlyList<UvPoint> inner,
        IReadOnlyList<UvPoint> outer,
        double containmentTolerance)
    {
        if (CountPolygonIntersections(inner, outer, containmentTolerance) > 0)
        {
            return false;
        }

        var containment = EvaluateContainment(inner, outer, containmentTolerance);
        return containment.OutsideCount == 0;
    }

    private static double DistancePointToPolygon(UvPoint point, IReadOnlyList<UvPoint> polygon)
    {
        var minDistance = double.MaxValue;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            minDistance = double.Min(minDistance, DistancePointToSegment(point, polygon[i], polygon[i + 1]));
        }

        return minDistance;
    }

    private static int CountPolygonIntersections(IReadOnlyList<UvPoint> a, IReadOnlyList<UvPoint> b, double tolerance)
    {
        var intersections = 0;
        for (var i = 0; i < a.Count - 1; i++)
        {
            if (SegmentLengthSquared(a[i], a[i + 1]) <= (tolerance * tolerance))
            {
                continue;
            }

            for (var j = 0; j < b.Count - 1; j++)
            {
                if (SegmentLengthSquared(b[j], b[j + 1]) <= (tolerance * tolerance))
                {
                    continue;
                }

                if (SegmentsIntersect(a[i], a[i + 1], b[j], b[j + 1], tolerance))
                {
                    intersections++;
                }
            }
        }

        return intersections;
    }

    private static bool SegmentsIntersect(UvPoint a0, UvPoint a1, UvPoint b0, UvPoint b1, double tolerance)
    {
        var o1 = Orientation(a0, a1, b0);
        var o2 = Orientation(a0, a1, b1);
        var o3 = Orientation(b0, b1, a0);
        var o4 = Orientation(b0, b1, a1);

        if (IsProperStraddle(o1, o2, tolerance) && IsProperStraddle(o3, o4, tolerance))
        {
            return true;
        }

        return (double.Abs(o1) <= tolerance && IsPointOnSegment(b0, a0, a1, tolerance))
            || (double.Abs(o2) <= tolerance && IsPointOnSegment(b1, a0, a1, tolerance))
            || (double.Abs(o3) <= tolerance && IsPointOnSegment(a0, b0, b1, tolerance))
            || (double.Abs(o4) <= tolerance && IsPointOnSegment(a1, b0, b1, tolerance));
    }

    private static bool IsProperStraddle(double a, double b, double tolerance)
        => (a > tolerance && b < -tolerance) || (a < -tolerance && b > tolerance);

    private static bool IsPointOnSegment(UvPoint p, UvPoint a, UvPoint b, double tolerance)
    {
        var minX = double.Min(a.X, b.X) - tolerance;
        var maxX = double.Max(a.X, b.X) + tolerance;
        var minY = double.Min(a.Y, b.Y) - tolerance;
        var maxY = double.Max(a.Y, b.Y) + tolerance;
        return p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;
    }

    private static double SegmentLengthSquared(UvPoint a, UvPoint b)
    {
        var delta = b - a;
        return delta.Dot(delta);
    }

    private static double Orientation(UvPoint a, UvPoint b, UvPoint c)
        => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static KernelResult<UvPoint> ChooseContainmentPoint(IReadOnlyList<UvPoint> polygon, double containmentTolerance)
    {
        if (polygon.Count < 4)
        {
            return LoopRoleFailure<UvPoint>("Unable to choose a containment sample point.", "Importer.LoopRole.DegenerateLoop");
        }

        var centroid = ComputePolygonCentroid(polygon);
        if (!IsPointNearPolygonEdge(centroid, polygon, containmentTolerance) && IsPointInPolygon(centroid, polygon, containmentTolerance))
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
            if (!IsPointNearPolygonEdge(candidate, polygon, containmentTolerance) && IsPointInPolygon(candidate, polygon, containmentTolerance))
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

    private static bool IsPointNearPolygonEdge(UvPoint point, IReadOnlyList<UvPoint> polygon, double containmentTolerance)
    {
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            if (DistancePointToSegment(point, polygon[i], polygon[i + 1]) <= containmentTolerance)
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

    private static bool IsPointInPolygon(UvPoint point, IReadOnlyList<UvPoint> polygon, double containmentTolerance)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count - 1; i++)
        {
            var a = polygon[i];
            var b = polygon[i + 1];

            if (DistancePointToSegment(point, a, b) <= containmentTolerance)
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

    private static int CountUniquePoints(IReadOnlyList<UvPoint> points, double tolerance)
    {
        var unique = new List<UvPoint>();
        foreach (var point in points)
        {
            if (unique.Any(u => (u - point).Length <= tolerance))
            {
                continue;
            }

            unique.Add(point);
        }

        return unique.Count;
    }


    private static List<UvPoint> SimplifyClosedPolygon(IReadOnlyList<UvPoint> polygon, double tolerance)
    {
        var simplified = new List<UvPoint>(polygon.Count + 1);
        foreach (var point in polygon)
        {
            if (simplified.Count > 0 && (simplified[^1] - point).Length <= tolerance)
            {
                continue;
            }

            simplified.Add(point);
        }

        if (simplified.Count == 0)
        {
            return simplified;
        }

        if ((simplified[0] - simplified[^1]).Length > tolerance)
        {
            simplified.Add(simplified[0]);
        }
        else
        {
            simplified[^1] = simplified[0];
        }

        return simplified;
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

    private static List<UvPoint> ProjectLoopToCylinder(IReadOnlyList<Point3D> samples, CylinderSurface cylinder)
    {
        var axis = cylinder.Axis.ToVector();
        var xAxis = cylinder.XAxis.ToVector();
        var yAxis = cylinder.YAxis.ToVector();

        var uv = new List<UvPoint>(samples.Count + 1);
        double? previous = null;
        var revolutions = 0d;
        foreach (var sample in samples)
        {
            var offset = sample - cylinder.Origin;
            var axial = offset.Dot(axis);
            var radial = offset - (axis * axial);
            var angle = NormalizeToZeroTwoPi(double.Atan2(radial.Dot(yAxis), radial.Dot(xAxis)));
            if (previous.HasValue)
            {
                var delta = angle - previous.Value;
                if (delta > double.Pi)
                {
                    revolutions -= 2d * double.Pi;
                }
                else if (delta < -double.Pi)
                {
                    revolutions += 2d * double.Pi;
                }
            }

            uv.Add(new UvPoint(angle + revolutions, axial));
            previous = angle;
        }

        if (uv.Count > 0 && (uv[0] - uv[^1]).Length > ContainmentEps)
        {
            uv.Add(uv[0]);
        }

        return uv;
    }

    private static IReadOnlyList<UvPoint> AlignPolygonToReference(IReadOnlyList<UvPoint> polygon, double currentX, double referenceX)
    {
        var twoPi = 2d * double.Pi;
        var delta = referenceX - currentX;
        var turns = double.Round(delta / twoPi);
        var shift = turns * twoPi;
        if (double.Abs(shift) <= AngleUnwrapEps)
        {
            return polygon;
        }

        return polygon.Select(p => new UvPoint(p.X + shift, p.Y)).ToArray();
    }

    private static double NormalizeToZeroTwoPi(double angle)
    {
        var twoPi = 2d * double.Pi;
        var normalized = angle % twoPi;
        if (normalized < 0d)
        {
            normalized += twoPi;
        }

        return normalized;
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

    private static double ComputeContainmentTolerance(IEnumerable<UvPoint> points)
    {
        var array = points.ToArray();
        if (array.Length == 0)
        {
            return ContainmentEps;
        }

        var minX = array.Min(p => p.X);
        var maxX = array.Max(p => p.X);
        var minY = array.Min(p => p.Y);
        var maxY = array.Max(p => p.Y);
        var diagonal = double.Sqrt(((maxX - minX) * (maxX - minX)) + ((maxY - minY) * (maxY - minY)));
        return System.Math.Max(ContainmentEps, diagonal * 1e-6d);
    }

    private static double ComputeAreaTolerance(IEnumerable<UvPoint> points)
    {
        var array = points.ToArray();
        if (array.Length == 0)
        {
            return AreaEps;
        }

        var minX = array.Min(p => p.X);
        var maxX = array.Max(p => p.X);
        var minY = array.Min(p => p.Y);
        var maxY = array.Max(p => p.Y);
        var extentArea = (maxX - minX) * (maxY - minY);
        return System.Math.Max(AreaEps, extentArea * 1e-8d);
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

    private static KernelResult<IReadOnlyList<Point3D>> SampleCoedgePoints(
        EdgeGeometryBinding edgeBinding,
        BrepGeometryStore geometry,
        bool isReversed,
        int faceEntityId,
        int loopId,
        int coedgeId,
        int edgeId)
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
                var span = trim.End - trim.Start;
                var isFullCircle = double.Abs(span - (2d * double.Pi)) <= AngleUnwrapEps;
                var legacyPointCount = isFullCircle ? 5 : 3;
                var segmentCount = ComputeAdaptiveCircleSegmentCount(span);
                points = SampleCircularTrim(circle, trim, segmentCount);
                ReportCircularSamplingDiagnostic(new LoopRoleCircularSamplingDiagnostic(
                    FaceEntityId: faceEntityId,
                    LoopId: loopId,
                    CoedgeId: coedgeId,
                    EdgeId: edgeId,
                    TrimStart: trim.Start,
                    TrimEnd: trim.End,
                    TrimSpan: span,
                    IsFullCircle: isFullCircle,
                    LegacyPointCount: legacyPointCount,
                    AdaptivePointCount: points.Count));
                break;
            case CurveGeometryKind.BSpline3:
                var spline = curve.BSpline3!.Value;
                var splineTrim = edgeBinding.TrimInterval ?? new ParameterInterval(spline.DomainStart, spline.DomainEnd);
                var splineMid = splineTrim.Start + ((splineTrim.End - splineTrim.Start) * 0.5d);
                points = [spline.Evaluate(splineTrim.Start), spline.Evaluate(splineMid), spline.Evaluate(splineTrim.End)];
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

    private static int ComputeAdaptiveCircleSegmentCount(double span)
    {
        const double maxSegmentAngle = double.Pi / 4d;
        var normalizedSpan = double.Abs(span);
        var segmentCount = (int)double.Ceiling(normalizedSpan / maxSegmentAngle);
        return System.Math.Max(2, segmentCount);
    }

    private static List<Point3D> SampleCircularTrim(Circle3Curve circle, ParameterInterval trim, int segmentCount)
    {
        var points = new List<Point3D>(segmentCount + 1);
        var step = (trim.End - trim.Start) / segmentCount;
        for (var i = 0; i <= segmentCount; i++)
        {
            points.Add(circle.Evaluate(trim.Start + (step * i)));
        }

        return points;
    }

    private static void ReportCircularSamplingDiagnostic(LoopRoleCircularSamplingDiagnostic diagnostic)
    {
        var sink = CircularSamplingDiagnosticsSink.Value;
        sink?.Add(diagnostic);
    }

    public sealed record LoopRoleCircularSamplingDiagnostic(
        int FaceEntityId,
        int LoopId,
        int CoedgeId,
        int EdgeId,
        double TrimStart,
        double TrimEnd,
        double TrimSpan,
        bool IsFullCircle,
        int LegacyPointCount,
        int AdaptivePointCount);

    private sealed class CircularSamplingDiagnosticsScope(ICollection<LoopRoleCircularSamplingDiagnostic>? previous) : IDisposable
    {
        public void Dispose()
        {
            CircularSamplingDiagnosticsSink.Value = previous;
        }
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

    private sealed record PlanarLoopInfoWithSample(PlanarLoopInfo Info, UvPoint SamplePoint);

    private sealed record PlanarLoopOuterCandidate(PlanarLoopInfo Info, UvPoint SamplePoint, int ContainmentCount);

    private sealed record CylindricalLoopInfo(LoopBuildData Loop, IReadOnlyList<UvPoint> ProjectedPoints, double SignedArea, UvPoint Centroid);

    private sealed record ContainmentEvaluation(int OutsideCount, int VertexCount, double MinDistanceToOuter);

    private sealed record ContainmentFailure(string Message, string Source);

    private readonly record struct UvPoint(double X, double Y)
    {
        public static UvPoint operator +(UvPoint a, UvPoint b) => new(a.X + b.X, a.Y + b.Y);

        public static UvPoint operator -(UvPoint a, UvPoint b) => new(a.X - b.X, a.Y - b.Y);

        public static UvPoint operator *(UvPoint a, double s) => new(a.X * s, a.Y * s);

        public double Dot(UvPoint other) => (X * other.X) + (Y * other.Y);

        public double Length => double.Sqrt((X * X) + (Y * Y));
    }
}
