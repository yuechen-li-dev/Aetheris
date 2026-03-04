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
    private const double PlanarLoopChainEpsilon = 1e-9;

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
            var segmentResult = BuildPlanarSegments(body, coedges);
            if (!segmentResult.IsSuccess)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure(segmentResult.Diagnostics);
            }

            var mismatches = ValidateLoopEndpointChain(segmentResult.Value);
            var orderedSegmentsResult = ChainPlanarSegments(segmentResult.Value, faceId, mismatches.Count);
            if (!orderedSegmentsResult.IsSuccess)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure(orderedSegmentsResult.Diagnostics);
            }

            var polygonPointsResult = BuildPolygonPointsFromSegments(orderedSegmentsResult.Value, faceId);
            if (!polygonPointsResult.IsSuccess)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure(polygonPointsResult.Diagnostics);
            }

            var polygonPoints = polygonPointsResult.Value;
            if (polygonPoints.Count < 3)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} planar loop must contain at least three line coedges.")]);
            }

            return KernelResult<DisplayFaceMeshPatch>.Success(CreatePlanarPatch(faceId, polygonPoints, plane.Normal.ToVector()));
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

    private static DisplayFaceMeshPatch CreatePlanarPatch(FaceId faceId, IReadOnlyList<Point3D> polygonPoints, Vector3D normal)
    {
        var normals = Enumerable.Repeat(normal, polygonPoints.Count).ToArray();
        var indices = new List<int>((polygonPoints.Count - 2) * 3);
        for (var i = 1; i < polygonPoints.Count - 1; i++)
        {
            indices.Add(0);
            indices.Add(i);
            indices.Add(i + 1);
        }

        return new DisplayFaceMeshPatch(faceId, polygonPoints.ToArray(), normals, indices);
    }

    private static KernelResult<IReadOnlyList<PlanarSegment>> BuildPlanarSegments(BrepBody body, IReadOnlyList<Coedge> coedges)
    {
        var segments = new List<PlanarSegment>(coedges.Count);
        foreach (var coedge in coedges)
        {
            var endpoints = GetEdgeEndpoints(body, coedge.EdgeId, coedge.IsReversed);
            if (!endpoints.IsSuccess)
            {
                return KernelResult<IReadOnlyList<PlanarSegment>>.Failure(endpoints.Diagnostics);
            }

            segments.Add(new PlanarSegment(coedge, endpoints.Value.Start, endpoints.Value.End));
        }

        return KernelResult<IReadOnlyList<PlanarSegment>>.Success(segments);
    }

    private static IReadOnlyList<int> ValidateLoopEndpointChain(IReadOnlyList<PlanarSegment> segments)
    {
        var mismatches = new List<int>();
        for (var i = 0; i < segments.Count; i++)
        {
            var next = (i + 1) % segments.Count;
            if (!PointsNear(segments[i].End, segments[next].Start))
            {
                mismatches.Add(i);
            }
        }

        return mismatches;
    }

    private static KernelResult<IReadOnlyList<PlanarSegment>> ChainPlanarSegments(
        IReadOnlyList<PlanarSegment> segments,
        FaceId faceId,
        int preflightMismatchCount)
    {
        if (segments.Count == 0)
        {
            return KernelResult<IReadOnlyList<PlanarSegment>>.Failure([CreateNotImplemented($"Face {faceId.Value} planar loop must contain at least one coedge.")]);
        }

        var unused = segments
            .OrderBy(s => s.Coedge.EdgeId.Value)
            .ThenBy(s => s.Coedge.Id.Value)
            .ToList();

        var ordered = new List<PlanarSegment>(segments.Count) { unused[0] };
        unused.RemoveAt(0);

        while (unused.Count > 0)
        {
            var currentEnd = ordered[^1].End;
            var next = unused
                .Where(s => PointsNear(s.Start, currentEnd))
                .OrderBy(s => s.Coedge.EdgeId.Value)
                .ThenBy(s => s.Coedge.Id.Value)
                .FirstOrDefault();

            if (next is null)
            {
                return KernelResult<IReadOnlyList<PlanarSegment>>.Failure([
                    CreateNotImplemented($"Face {faceId.Value} planar loop cannot be chained into a continuous cycle (mismatches:{preflightMismatchCount}).")]);
            }

            ordered.Add(next);
            unused.Remove(next);
        }

        if (!PointsNear(ordered[^1].End, ordered[0].Start))
        {
            return KernelResult<IReadOnlyList<PlanarSegment>>.Failure([
                CreateNotImplemented($"Face {faceId.Value} planar loop does not close after deterministic chaining.")]);
        }

        return KernelResult<IReadOnlyList<PlanarSegment>>.Success(ordered);
    }

    private static KernelResult<IReadOnlyList<Point3D>> BuildPolygonPointsFromSegments(IReadOnlyList<PlanarSegment> segments, FaceId faceId)
    {
        var points = new List<Point3D>(segments.Count + 1)
        {
            segments[0].Start,
        };

        foreach (var segment in segments)
        {
            points.Add(segment.End);
        }

        if (!PointsNear(points[^1], points[0]))
        {
            return KernelResult<IReadOnlyList<Point3D>>.Failure([
                CreateNotImplemented($"Face {faceId.Value} planar chained loop failed closure validation.")]);
        }

        points.RemoveAt(points.Count - 1);
        return KernelResult<IReadOnlyList<Point3D>>.Success(points);
    }

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

    private static KernelResult<(Point3D Start, Point3D End)> GetEdgeEndpoints(BrepBody body, EdgeId edgeId, bool reversed)
    {
        if (!body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null || curve.Kind != CurveGeometryKind.Line3)
        {
            return KernelResult<(Point3D, Point3D)>.Failure([CreateNotImplemented($"Edge {edgeId.Value} planar polygon tessellation requires line geometry.")]);
        }

        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding))
        {
            return KernelResult<(Point3D, Point3D)>.Failure([CreateNotImplemented($"Edge {edgeId.Value} is missing curve binding.")]);
        }

        var line = curve.Line3!.Value;
        var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d);
        var start = line.Evaluate(interval.Start);
        var end = line.Evaluate(interval.End);
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

    private static bool PointsNear(Point3D a, Point3D b)
        => (a - b).Length <= PlanarLoopChainEpsilon;

    private sealed record PlanarSegment(Coedge Coedge, Point3D Start, Point3D End);
}
