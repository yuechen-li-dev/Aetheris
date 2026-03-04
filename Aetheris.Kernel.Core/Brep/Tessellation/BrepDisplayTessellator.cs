using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public static class BrepDisplayTessellator
{
    private const double LoopEndpointTolerance = 1e-6d;

    public static KernelResult<DisplayTessellationResult> Tessellate(BrepBody body, DisplayTessellationOptions? options = null)
    {
        if (body is null)
        {
            return KernelResult<DisplayTessellationResult>.Failure([CreateInvalidArgument("body must be provided.")]);
        }

        var effectiveOptions = options ?? DisplayTessellationOptions.Default;
        var optionDiagnostics = effectiveOptions.Validate();
        if (optionDiagnostics.Count > 0)
        {
            return KernelResult<DisplayTessellationResult>.Failure(optionDiagnostics);
        }

        var facePatches = new List<DisplayFaceMeshPatch>();
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            var faceResult = TessellateFace(body, face.Id, effectiveOptions);
            if (!faceResult.IsSuccess)
            {
                return KernelResult<DisplayTessellationResult>.Failure(faceResult.Diagnostics);
            }

            facePatches.Add(faceResult.Value);
        }

        var edgePolylines = new List<DisplayEdgePolyline>();
        foreach (var edge in body.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            var edgeResult = TessellateEdge(body, edge.Id, effectiveOptions);
            if (!edgeResult.IsSuccess)
            {
                return KernelResult<DisplayTessellationResult>.Failure(edgeResult.Diagnostics);
            }

            edgePolylines.Add(edgeResult.Value);
        }

        return KernelResult<DisplayTessellationResult>.Success(new DisplayTessellationResult(facePatches, edgePolylines));
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellateFace(BrepBody body, FaceId faceId, DisplayTessellationOptions options)
    {
        if (!body.TryGetFaceSurfaceGeometry(faceId, out var surface) || surface is null)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} is missing bound surface geometry.")]);
        }

        return surface.Kind switch
        {
            SurfaceGeometryKind.Plane => TessellatePlanarFace(body, faceId, surface.Plane!.Value, options),
            SurfaceGeometryKind.Cylinder => TessellateCylinderFace(body, faceId, surface.Cylinder!.Value, options),
            SurfaceGeometryKind.Cone => TessellateConeFace(body, faceId, surface.Cone!.Value, options),
            SurfaceGeometryKind.Sphere => TessellateSphereFace(body, faceId, surface.Sphere!.Value, options),
            _ => KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} has unsupported surface kind '{surface.Kind}'.")]),
        };
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellatePlanarFace(BrepBody body, FaceId faceId, PlaneSurface plane, DisplayTessellationOptions options)
    {
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count != 1)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} planar tessellation requires exactly one loop.")]);
        }

        var coedges = body.GetCoedgeIds(loopIds[0])
            .Select(id => body.Topology.GetCoedge(id))
            .ToArray();

        if (coedges.All(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.Line3))
        {
            var polygonPointsResult = BuildOrderedPlanarLoopPoints(body, coedges, faceId);
            if (!polygonPointsResult.IsSuccess)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure(polygonPointsResult.Diagnostics);
            }

            var polygonPoints = polygonPointsResult.Value;

            if (polygonPoints.Count < 3)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} planar loop must contain at least three line coedges.")]);
            }

            var indices = BuildPlanarTriangleIndices(polygonPoints, plane.Normal.ToVector());
            if (indices is null)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} planar loop triangulation requires a convex polygon.")]);
            }

            return KernelResult<DisplayFaceMeshPatch>.Success(CreatePlanarPatch(faceId, polygonPoints, plane.Normal.ToVector(), indices));
        }

        if (coedges.Length == 1 && body.GetEdgeCurve(coedges[0].EdgeId).Kind == CurveGeometryKind.Circle3)
        {
            var circlePatch = TessellatePlanarCircleFace(body, faceId, coedges[0], plane, options);
            return circlePatch;
        }

        return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} planar tessellation supports only all-line loops or a single circle loop.")]);
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellatePlanarCircleFace(BrepBody body, FaceId faceId, Coedge coedge, PlaneSurface plane, DisplayTessellationOptions options)
    {
        var edge = TessellateEdge(body, coedge.EdgeId, options);
        if (!edge.IsSuccess)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure(edge.Diagnostics);
        }

        var edgePoints = edge.Value.Points;
        if (edgePoints.Count < 4)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} circular planar tessellation requires at least three perimeter samples.")]);
        }

        var center = new Point3D(
            edgePoints.Take(edgePoints.Count - 1).Average(p => p.X),
            edgePoints.Take(edgePoints.Count - 1).Average(p => p.Y),
            edgePoints.Take(edgePoints.Count - 1).Average(p => p.Z));

        var positions = new List<Point3D> { center };
        positions.AddRange(edgePoints.Take(edgePoints.Count - 1));

        var normal = plane.Normal.ToVector();
        if (coedge.IsReversed)
        {
            normal = -normal;
        }

        var normals = Enumerable.Repeat(normal, positions.Count).ToArray();
        var indices = new List<int>((positions.Count - 2) * 3);
        for (var i = 1; i < positions.Count - 1; i++)
        {
            indices.Add(0);
            indices.Add(i);
            indices.Add(i + 1);
        }

        indices.Add(0);
        indices.Add(positions.Count - 1);
        indices.Add(1);

        return KernelResult<DisplayFaceMeshPatch>.Success(new DisplayFaceMeshPatch(faceId, positions, normals, indices));
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellateCylinderFace(BrepBody body, FaceId faceId, CylinderSurface cylinder, DisplayTessellationOptions options)
    {
        var parameters = GetRevolvedFaceParameters(body, faceId, options, cylinder.Radius);
        if (!parameters.IsSuccess)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure(parameters.Diagnostics);
        }

        return KernelResult<DisplayFaceMeshPatch>.Success(CreatePeriodicGridPatch(
            faceId,
            parameters.Value.AngularSegments,
            parameters.Value.AxialSegments,
            (u, v) => cylinder.Evaluate(u, v),
            (u, _) => cylinder.Normal(u).ToVector(),
            parameters.Value.VStart,
            parameters.Value.VEnd));
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellateConeFace(BrepBody body, FaceId faceId, ConeSurface cone, DisplayTessellationOptions options)
    {
        var parameters = GetRevolvedFaceParameters(body, faceId, options, radiusHint: 1d);
        if (!parameters.IsSuccess)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure(parameters.Diagnostics);
        }

        return KernelResult<DisplayFaceMeshPatch>.Success(CreatePeriodicGridPatch(
            faceId,
            parameters.Value.AngularSegments,
            parameters.Value.AxialSegments,
            (u, v) => cone.Evaluate(u, v),
            (u, _) => cone.Normal(u).ToVector(),
            parameters.Value.VStart,
            parameters.Value.VEnd));
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellateSphereFace(BrepBody body, FaceId faceId, SphereSurface sphere, DisplayTessellationOptions options)
    {
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count != 0)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} sphere tessellation supports only untrimmed sphere faces with zero loops.")]);
        }

        var angularSegments = CalculateSegmentCount(2d * double.Pi, sphere.Radius, options);
        var elevationSegments = System.Math.Max(2, System.Math.Clamp(angularSegments / 2, options.MinimumSegments / 2, options.MaximumSegments));

        var positions = new List<Point3D>((angularSegments + 1) * (elevationSegments + 1));
        var normals = new List<Vector3D>((angularSegments + 1) * (elevationSegments + 1));
        var indices = new List<int>(angularSegments * elevationSegments * 6);

        for (var v = 0; v <= elevationSegments; v++)
        {
            var t = (double)v / elevationSegments;
            var elevation = (-double.Pi / 2d) + (t * double.Pi);

            for (var u = 0; u <= angularSegments; u++)
            {
                var s = (double)u / angularSegments;
                var azimuth = s * 2d * double.Pi;
                positions.Add(sphere.Evaluate(azimuth, elevation));
                normals.Add(sphere.Normal(azimuth, elevation).ToVector());
            }
        }

        var rowWidth = angularSegments + 1;
        for (var v = 0; v < elevationSegments; v++)
        {
            for (var u = 0; u < angularSegments; u++)
            {
                var i0 = (v * rowWidth) + u;
                var i1 = i0 + 1;
                var i2 = i0 + rowWidth;
                var i3 = i2 + 1;

                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);

                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i3);
            }
        }

        return KernelResult<DisplayFaceMeshPatch>.Success(new DisplayFaceMeshPatch(faceId, positions, normals, indices));
    }

    private static KernelResult<(double VStart, double VEnd, int AngularSegments, int AxialSegments)> GetRevolvedFaceParameters(
        BrepBody body,
        FaceId faceId,
        DisplayTessellationOptions options,
        double radiusHint)
    {
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count != 1)
        {
            return KernelResult<(double, double, int, int)>.Failure([CreateNotImplemented($"Face {faceId.Value} curved tessellation requires exactly one loop.")]);
        }

        var coedges = body.GetCoedgeIds(loopIds[0])
            .Select(id => body.Topology.GetCoedge(id))
            .ToArray();

        if (coedges.Length != 4)
        {
            return KernelResult<(double, double, int, int)>.Failure([CreateNotImplemented($"Face {faceId.Value} curved tessellation supports the M11 seam loop layout with four coedges.")]);
        }

        var lineCoedges = coedges.Where(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.Line3).ToArray();
        var circleCoedges = coedges.Where(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.Circle3).ToArray();

        if (lineCoedges.Length != 2 || circleCoedges.Length != 2)
        {
            return KernelResult<(double, double, int, int)>.Failure([CreateNotImplemented($"Face {faceId.Value} curved tessellation requires two line seam uses and two circular trim uses.")]);
        }

        if (!body.Bindings.TryGetEdgeBinding(lineCoedges[0].EdgeId, out var seamBinding))
        {
            return KernelResult<(double, double, int, int)>.Failure([CreateNotImplemented($"Face {faceId.Value} seam edge is missing curve trim binding.")]);
        }

        var seamInterval = seamBinding.TrimInterval ?? new ParameterInterval(0d, 1d);
        var angularSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
        var axialSegments = System.Math.Max(1, System.Math.Clamp((int)double.Ceiling((seamInterval.End - seamInterval.Start) / options.ChordTolerance), 1, options.MaximumSegments));

        return KernelResult<(double, double, int, int)>.Success((seamInterval.Start, seamInterval.End, angularSegments, axialSegments));
    }

    private static DisplayFaceMeshPatch CreatePeriodicGridPatch(
        FaceId faceId,
        int angularSegments,
        int axialSegments,
        Func<double, double, Point3D> evaluate,
        Func<double, double, Vector3D> evaluateNormal,
        double vStart,
        double vEnd)
    {
        var positions = new List<Point3D>((angularSegments + 1) * (axialSegments + 1));
        var normals = new List<Vector3D>((angularSegments + 1) * (axialSegments + 1));
        var indices = new List<int>(angularSegments * axialSegments * 6);

        for (var v = 0; v <= axialSegments; v++)
        {
            var tv = (double)v / axialSegments;
            var vv = vStart + ((vEnd - vStart) * tv);

            for (var u = 0; u <= angularSegments; u++)
            {
                var tu = (double)u / angularSegments;
                var uu = tu * 2d * double.Pi;
                positions.Add(evaluate(uu, vv));
                normals.Add(evaluateNormal(uu, vv));
            }
        }

        var rowWidth = angularSegments + 1;
        for (var v = 0; v < axialSegments; v++)
        {
            for (var u = 0; u < angularSegments; u++)
            {
                var i0 = (v * rowWidth) + u;
                var i1 = i0 + 1;
                var i2 = i0 + rowWidth;
                var i3 = i2 + 1;

                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);

                indices.Add(i1);
                indices.Add(i2);
                indices.Add(i3);
            }
        }

        return new DisplayFaceMeshPatch(faceId, positions, normals, indices);
    }

    private static DisplayFaceMeshPatch CreatePlanarPatch(FaceId faceId, IReadOnlyList<Point3D> polygonPoints, Vector3D normal, IReadOnlyList<int> triangleIndices)
    {
        var normals = Enumerable.Repeat(normal, polygonPoints.Count).ToArray();
        return new DisplayFaceMeshPatch(faceId, polygonPoints.ToArray(), normals, triangleIndices.ToArray());
    }

    private static KernelResult<IReadOnlyList<Point3D>> BuildOrderedPlanarLoopPoints(BrepBody body, IReadOnlyList<Coedge> coedges, FaceId faceId)
    {
        var vertexPointsResult = BuildLoopVertexPointLookup(body, coedges, faceId);
        if (!vertexPointsResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<Point3D>>.Failure(vertexPointsResult.Diagnostics);
        }

        var vertexPoints = vertexPointsResult.Value;
        var orientedEndpoints = new List<(Point3D Start, Point3D End)>(coedges.Count);
        foreach (var coedge in coedges)
        {
            var endpoints = GetEdgeEndpoints(body, coedge.EdgeId, coedge.IsReversed, vertexPoints);
            if (!endpoints.IsSuccess)
            {
                return KernelResult<IReadOnlyList<Point3D>>.Failure(endpoints.Diagnostics);
            }

            orientedEndpoints.Add(endpoints.Value);
        }

        var orderedVertices = new List<Point3D>(coedges.Count);
        var used = new bool[coedges.Count];
        var currentIndex = 0;
        used[currentIndex] = true;

        var currentEdge = orientedEndpoints[currentIndex];
        orderedVertices.Add(currentEdge.Start);
        var currentEnd = currentEdge.End;

        for (var step = 1; step < coedges.Count; step++)
        {
            var nextIndex = FindNextCoedgeIndex(orientedEndpoints, used, currentEnd, out var nextStart, out var nextEnd);
            if (nextIndex < 0)
            {
                return KernelResult<IReadOnlyList<Point3D>>.Failure([CreateNotImplemented($"Face {faceId.Value} planar loop line coedges do not form a contiguous chain.")]);
            }

            used[nextIndex] = true;
            if (!PointsAlmostEqual(orderedVertices[^1], nextStart))
            {
                orderedVertices.Add(nextStart);
            }

            currentEnd = nextEnd;
        }

        if (PointsAlmostEqual(orderedVertices[0], currentEnd))
        {
            return KernelResult<IReadOnlyList<Point3D>>.Success(orderedVertices);
        }

        return KernelResult<IReadOnlyList<Point3D>>.Failure([CreateNotImplemented($"Face {faceId.Value} planar loop line coedges did not close after chaining.")]);
    }

    private static int FindNextCoedgeIndex(
        IReadOnlyList<(Point3D Start, Point3D End)> endpoints,
        IReadOnlyList<bool> used,
        Point3D currentEnd,
        out Point3D nextStart,
        out Point3D nextEnd)
    {
        var matchIndex = -1;
        nextStart = default;
        nextEnd = default;

        for (var i = 0; i < endpoints.Count; i++)
        {
            if (used[i])
            {
                continue;
            }

            var (start, end) = endpoints[i];
            if (PointsAlmostEqual(start, currentEnd))
            {
                matchIndex = i;
                nextStart = start;
                nextEnd = end;
                break;
            }

            if (PointsAlmostEqual(end, currentEnd))
            {
                matchIndex = i;
                nextStart = end;
                nextEnd = start;
                break;
            }
        }

        return matchIndex;
    }

    private static IReadOnlyList<int>? BuildPlanarTriangleIndices(IReadOnlyList<Point3D> polygonPoints, Vector3D expectedNormal)
    {
        if (!IsConvexPolygon(polygonPoints, expectedNormal))
        {
            return null;
        }

        var indices = new List<int>((polygonPoints.Count - 2) * 3);
        for (var i = 1; i < polygonPoints.Count - 1; i++)
        {
            indices.Add(0);
            indices.Add(i);
            indices.Add(i + 1);
        }

        if (indices.Count >= 3)
        {
            var firstNormal = (polygonPoints[indices[1]] - polygonPoints[indices[0]])
                .Cross(polygonPoints[indices[2]] - polygonPoints[indices[0]]);
            if (firstNormal.Dot(expectedNormal) < 0d)
            {
                for (var i = 0; i < indices.Count; i += 3)
                {
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                }
            }
        }

        return indices;
    }

    private static bool IsConvexPolygon(IReadOnlyList<Point3D> polygonPoints, Vector3D expectedNormal)
    {
        var referenceSign = 0;
        for (var i = 0; i < polygonPoints.Count; i++)
        {
            var a = polygonPoints[i];
            var b = polygonPoints[(i + 1) % polygonPoints.Count];
            var c = polygonPoints[(i + 2) % polygonPoints.Count];
            var cross = (b - a).Cross(c - b);
            var dot = cross.Dot(expectedNormal);
            if (double.Abs(dot) <= LoopEndpointTolerance)
            {
                continue;
            }

            var sign = dot > 0d ? 1 : -1;
            if (referenceSign == 0)
            {
                referenceSign = sign;
                continue;
            }

            if (referenceSign != sign)
            {
                return false;
            }
        }

        return referenceSign != 0;
    }

    private static bool PointsAlmostEqual(Point3D left, Point3D right)
        => (left - right).LengthSquared <= (LoopEndpointTolerance * LoopEndpointTolerance);

    private static KernelResult<DisplayEdgePolyline> TessellateEdge(BrepBody body, EdgeId edgeId, DisplayTessellationOptions options)
    {
        if (!body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null)
        {
            return KernelResult<DisplayEdgePolyline>.Failure([CreateNotImplemented($"Edge {edgeId.Value} is missing bound curve geometry.")]);
        }

        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding))
        {
            return KernelResult<DisplayEdgePolyline>.Failure([CreateNotImplemented($"Edge {edgeId.Value} is missing curve binding.")]);
        }

        var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d);

        switch (curve.Kind)
        {
            case CurveGeometryKind.Line3:
                var line = curve.Line3!.Value;
                return KernelResult<DisplayEdgePolyline>.Success(new DisplayEdgePolyline(
                    edgeId,
                    [line.Evaluate(interval.Start), line.Evaluate(interval.End)],
                    IsClosed: false));

            case CurveGeometryKind.Circle3:
                var circle = curve.Circle3!.Value;
                var delta = interval.End - interval.Start;
                var fullCircleSegments = CalculateSegmentCount(2d * double.Pi, circle.Radius, options);
                var segmentCount = System.Math.Max(1, (int)double.Ceiling(fullCircleSegments * (delta / (2d * double.Pi))));
                segmentCount = System.Math.Clamp(segmentCount, 1, options.MaximumSegments);

                var points = new List<Point3D>(segmentCount + 1);
                for (var i = 0; i <= segmentCount; i++)
                {
                    var t = (double)i / segmentCount;
                    points.Add(circle.Evaluate(interval.Start + (delta * t)));
                }

                return KernelResult<DisplayEdgePolyline>.Success(new DisplayEdgePolyline(edgeId, points, IsClosed: delta >= (2d * double.Pi)));

            default:
                return KernelResult<DisplayEdgePolyline>.Failure([CreateNotImplemented($"Edge {edgeId.Value} has unsupported curve kind '{curve.Kind}'.")]);
        }
    }

    private static KernelResult<Dictionary<VertexId, Point3D>> BuildLoopVertexPointLookup(BrepBody body, IReadOnlyList<Coedge> coedges, FaceId faceId)
    {
        var requiredVertexIds = new HashSet<VertexId>();
        foreach (var coedge in coedges)
        {
            if (!body.TryGetEdgeVertices(coedge.EdgeId, out var startVertexId, out var endVertexId))
            {
                return KernelResult<Dictionary<VertexId, Point3D>>.Failure([CreateNotImplemented($"Face {faceId.Value} references missing edge topology for edge {coedge.EdgeId.Value}.")]);
            }

            requiredVertexIds.Add(startVertexId);
            requiredVertexIds.Add(endVertexId);
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertexId in requiredVertexIds.OrderBy(v => v.Value))
        {
            var pointResult = GetVertexPoint(body, vertexId);
            if (!pointResult.IsSuccess)
            {
                return KernelResult<Dictionary<VertexId, Point3D>>.Failure(pointResult.Diagnostics);
            }

            vertexPoints[vertexId] = pointResult.Value;
        }

        return KernelResult<Dictionary<VertexId, Point3D>>.Success(vertexPoints);
    }

    private static KernelResult<Point3D> GetVertexPoint(BrepBody body, VertexId vertexId)
    {
        foreach (var edge in body.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            if (edge.StartVertexId != vertexId)
            {
                continue;
            }

            var edgePoint = EvaluateEdgeEndpoint(body, edge.Id, useStart: true);
            if (edgePoint.IsSuccess)
            {
                return KernelResult<Point3D>.Success(edgePoint.Value);
            }
        }

        foreach (var edge in body.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            if (edge.EndVertexId != vertexId)
            {
                continue;
            }

            var edgePoint = EvaluateEdgeEndpoint(body, edge.Id, useStart: false);
            if (edgePoint.IsSuccess)
            {
                return KernelResult<Point3D>.Success(edgePoint.Value);
            }
        }

        return KernelResult<Point3D>.Failure([CreateNotImplemented($"Vertex {vertexId.Value} cannot be resolved to a geometric point for tessellation.")]);
    }

    private static KernelResult<Point3D> EvaluateEdgeEndpoint(BrepBody body, EdgeId edgeId, bool useStart)
    {
        if (!body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null || curve.Kind != CurveGeometryKind.Line3)
        {
            return KernelResult<Point3D>.Failure([CreateNotImplemented($"Edge {edgeId.Value} planar polygon tessellation requires line geometry.")]);
        }

        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding))
        {
            return KernelResult<Point3D>.Failure([CreateNotImplemented($"Edge {edgeId.Value} is missing curve binding.")]);
        }

        var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d);
        var parameter = useStart ? interval.Start : interval.End;
        return KernelResult<Point3D>.Success(curve.Line3!.Value.Evaluate(parameter));
    }

    private static KernelResult<(Point3D Start, Point3D End)> GetEdgeEndpoints(
        BrepBody body,
        EdgeId edgeId,
        bool reversed,
        IReadOnlyDictionary<VertexId, Point3D> vertexPoints)
    {
        if (!body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null || curve.Kind != CurveGeometryKind.Line3)
        {
            return KernelResult<(Point3D, Point3D)>.Failure([CreateNotImplemented($"Edge {edgeId.Value} planar polygon tessellation requires line geometry.")]);
        }

        if (!body.TryGetEdgeVertices(edgeId, out var startVertexId, out var endVertexId))
        {
            return KernelResult<(Point3D, Point3D)>.Failure([CreateNotImplemented($"Edge {edgeId.Value} is missing topology endpoints.")]);
        }

        if (!vertexPoints.TryGetValue(startVertexId, out var start) || !vertexPoints.TryGetValue(endVertexId, out var end))
        {
            return KernelResult<(Point3D, Point3D)>.Failure([CreateNotImplemented($"Edge {edgeId.Value} references unresolved vertex points.")]);
        }

        return KernelResult<(Point3D, Point3D)>.Success(reversed ? (end, start) : (start, end));
    }

    private static int CalculateSegmentCount(double angleSpan, double radius, DisplayTessellationOptions options)
    {
        var byAngle = (int)double.Ceiling(angleSpan / options.AngularToleranceRadians);

        var byChord = 1;
        if (radius > 0d)
        {
            var ratio = 1d - (options.ChordTolerance / radius);
            ratio = System.Math.Clamp(ratio, -1d, 1d);
            var halfStep = double.Acos(ratio);
            if (halfStep > 0d)
            {
                byChord = (int)double.Ceiling(angleSpan / (2d * halfStep));
            }
        }

        var segments = System.Math.Max(options.MinimumSegments, System.Math.Max(byAngle, byChord));
        return System.Math.Clamp(segments, options.MinimumSegments, options.MaximumSegments);
    }

    private static KernelDiagnostic CreateInvalidArgument(string message)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message);

    private static KernelDiagnostic CreateNotImplemented(string message)
        => new(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message);
}
