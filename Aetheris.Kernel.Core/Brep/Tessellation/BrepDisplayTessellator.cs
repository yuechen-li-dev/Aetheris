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
    private const string PlanarCurveFlatteningUnsupportedSource = "Viewer.Tessellation.PlanarCurveFlatteningUnsupported";
    private const string PlanarCurveFlatteningFailedSource = "Viewer.Tessellation.PlanarCurveFlatteningFailed";
    private const string CircleTrimResolveFailedSource = "Viewer.Tessellation.CircleTrimResolveFailed";
    private const string CircleTrimAmbiguousUsedShorterArcSource = "Viewer.Tessellation.CircleTrimAmbiguousUsedShorterArc";
    private const string CylinderTrimUnsupportedSource = "Viewer.Tessellation.CylinderTrimUnsupported";
    private const string CylinderTrimDegenerateSource = "Viewer.Tessellation.CylinderTrimDegenerate";
    private const string CylinderTrimAxialSpanDegenerateSource = "Viewer.Tessellation.CylinderTrimAxialSpanDegenerate";
    private const string CylinderTrimAxialSpanDegenerateSingleCoedgeNearFullWrapSource = "Viewer.Tessellation.CylinderTrimAxialSpanDegenerate.SingleCoedgeNearFullWrap";
    private const string CylinderTrimAxialSpanDegenerateSingleCoedgeSource = "Viewer.Tessellation.CylinderTrimAxialSpanDegenerate.SingleCoedge";
    private const string CylinderTrimAxialSpanDegenerateMultiCoedgeSource = "Viewer.Tessellation.CylinderTrimAxialSpanDegenerate.MultiCoedge";
    private const string CylinderTrimAxialSpanDegenerateSingleCoedgeNearFullWrapBridgeSource = "Viewer.Tessellation.CylinderTrimAxialSpanDegenerate.SingleCoedgeNearFullWrapBridge";
    private const string CylinderTrimAmbiguousUsedShorterSpanSource = "Viewer.Tessellation.CylinderTrimAmbiguousUsedShorterSpan";
    private const string CurvedTopologyUnsupportedSource = "Viewer.Tessellation.CurvedTopologyUnsupported";
    private const string BSplineSurfaceTrimUnsupportedSource = "Viewer.Tessellation.BSplineSurfaceTrimUnsupported";

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

        var accumulatedDiagnostics = new List<KernelDiagnostic>();

        var facePatches = new List<DisplayFaceMeshPatch>();
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            var faceResult = TessellateFace(body, face.Id, effectiveOptions);
            if (!faceResult.IsSuccess)
            {
                return KernelResult<DisplayTessellationResult>.Failure(faceResult.Diagnostics);
            }

            facePatches.Add(faceResult.Value);
            accumulatedDiagnostics.AddRange(faceResult.Diagnostics);
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
            accumulatedDiagnostics.AddRange(edgeResult.Diagnostics);
        }

        return KernelResult<DisplayTessellationResult>.Success(new DisplayTessellationResult(facePatches, edgePolylines), accumulatedDiagnostics);
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
            SurfaceGeometryKind.Torus => TessellateTorusFace(body, faceId, surface.Torus!.Value, options),
            SurfaceGeometryKind.BSplineSurfaceWithKnots => TessellateBSplineSurfaceFace(body, faceId, surface.BSplineSurfaceWithKnots!, options),
            _ => KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} has unsupported surface kind '{surface.Kind}'.")]),
        };
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellatePlanarFace(BrepBody body, FaceId faceId, PlaneSurface plane, DisplayTessellationOptions options)
    {
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count == 0)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure([CreateNotImplemented($"Face {faceId.Value} planar tessellation requires at least one loop.")]);
        }

        var selectedLoopId = SelectPlanarPrimaryLoop(body, faceId, plane, loopIds);
        var ignoredLoopCount = System.Math.Max(0, loopIds.Count - 1);
        var successDiagnostics = ignoredLoopCount > 0
            ? new[] { CreateValidationWarning($"Face {faceId.Value} planar tessellation ignored {ignoredLoopCount} inner loop(s).", "Viewer.Tessellation.PlanarHolesIgnored") }
            : Array.Empty<KernelDiagnostic>();

        var coedges = body.GetCoedgeIds(selectedLoopId)
            .Select(id => body.Topology.GetCoedge(id))
            .ToArray();

        var polygonPointsResult = BuildFlattenedPlanarLoopPoints(body, coedges, faceId, plane, options);
        if (!polygonPointsResult.IsSuccess)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure(polygonPointsResult.Diagnostics);
        }

        var polygonPoints = polygonPointsResult.Value;

        if (polygonPoints.Count < 3)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure([CreateInvalidArgument($"Face {faceId.Value} planar loop flattening produced fewer than three unique points.", PlanarCurveFlatteningFailedSource)]);
        }

        if (!PlanarPolygonTriangulator.TryTriangulate(polygonPoints, plane.Normal.ToVector(), out var indices, out var failure))
        {
            return failure switch
            {
                PlanarPolygonTriangulationFailure.Degenerate => KernelResult<DisplayFaceMeshPatch>.Failure([
                    CreateInvalidArgument($"Face {faceId.Value} planar loop is degenerate and cannot be triangulated.", "Viewer.Tessellation.PlanarPolygonDegenerate")]),
                PlanarPolygonTriangulationFailure.NonSimple => KernelResult<DisplayFaceMeshPatch>.Failure([
                    CreateNotImplemented($"Face {faceId.Value} planar loop triangulation failed because the polygon is not simple.", "Viewer.Tessellation.PlanarNonConvexTriangulationFailed")]),
                _ => KernelResult<DisplayFaceMeshPatch>.Failure([
                    CreateNotImplemented($"Face {faceId.Value} planar loop triangulation failed.", "Viewer.Tessellation.PlanarNonConvexTriangulationFailed")]),
            };
        }

        return KernelResult<DisplayFaceMeshPatch>.Success(CreatePlanarPatch(faceId, polygonPoints, plane.Normal.ToVector(), indices), successDiagnostics);
    }

    private static LoopId SelectPlanarPrimaryLoop(BrepBody body, FaceId faceId, PlaneSurface plane, IReadOnlyList<LoopId> loopIds)
    {
        if (loopIds.Count <= 1)
        {
            return loopIds[0];
        }

        var bestLoop = loopIds[0];
        var bestArea = double.NegativeInfinity;

        foreach (var loopId in loopIds)
        {
            var coedges = body.GetCoedgeIds(loopId)
                .Select(id => body.Topology.GetCoedge(id))
                .ToArray();

            var pointsResult = BuildFlattenedPlanarLoopPoints(body, coedges, faceId, plane, DisplayTessellationOptions.Default);
            if (!pointsResult.IsSuccess)
            {
                continue;
            }

            var area = double.Abs(ComputeSignedPlanarArea(pointsResult.Value, plane));
            if (area > bestArea)
            {
                bestArea = area;
                bestLoop = loopId;
            }
        }

        return bestLoop;
    }

    private static double ComputeSignedPlanarArea(IReadOnlyList<Point3D> polygonPoints, PlaneSurface plane)
    {
        var uAxis = plane.UAxis.ToVector();
        var vAxis = plane.VAxis.ToVector();
        var area = 0d;

        for (var i = 0; i < polygonPoints.Count; i++)
        {
            var current = polygonPoints[i] - plane.Origin;
            var next = polygonPoints[(i + 1) % polygonPoints.Count] - plane.Origin;
            var currentU = current.Dot(uAxis);
            var currentV = current.Dot(vAxis);
            var nextU = next.Dot(uAxis);
            var nextV = next.Dot(vAxis);
            area += (currentU * nextV) - (nextU * currentV);
        }

        return area * 0.5d;
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
        var trimPatch = TryResolveCylinderTrimPatch(body, faceId, cylinder);
        if (!trimPatch.IsSuccess)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure(trimPatch.Diagnostics);
        }

        var angularSpan = trimPatch.Value.UEnd - trimPatch.Value.UStart;
        var axialSpan = trimPatch.Value.VEnd - trimPatch.Value.VStart;
        var nearFullPeriodicSpan = (2d * double.Pi) - 0.25d;
        if (angularSpan >= nearFullPeriodicSpan)
        {
            var periodicParameters = GetRevolvedFaceParameters(
                body,
                faceId,
                options,
                radiusHint: cylinder.Radius,
                allowThreeCoedgeConeTopology: false,
                axialParameterFromPoint: point => (point - cylinder.Origin).Dot(cylinder.Axis.ToVector()));
            if (periodicParameters.IsSuccess)
            {
                return KernelResult<DisplayFaceMeshPatch>.Success(CreatePeriodicGridPatch(
                    faceId,
                    periodicParameters.Value.AngularSegments,
                    periodicParameters.Value.AxialSegments,
                    (u, v) => cylinder.Evaluate(u, v),
                    (u, _) => cylinder.Normal(u).ToVector(),
                    periodicParameters.Value.VStart,
                    periodicParameters.Value.VEnd),
                    periodicParameters.Diagnostics);
            }
        }

        var angularSegments = CalculateSegmentCount(angularSpan, System.Math.Max(1e-6d, cylinder.Radius), options);
        var axialSegments = System.Math.Max(1, System.Math.Clamp((int)double.Ceiling(axialSpan / options.ChordTolerance), 1, options.MaximumSegments));

        return KernelResult<DisplayFaceMeshPatch>.Success(CreateBoundedGridPatch(
            faceId,
            angularSegments,
            axialSegments,
            (u, v) => cylinder.Evaluate(u, v),
            (u, _) => cylinder.Normal(u).ToVector(),
            trimPatch.Value.UStart,
            trimPatch.Value.UEnd,
            trimPatch.Value.VStart,
            trimPatch.Value.VEnd),
            trimPatch.Diagnostics);
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellateConeFace(BrepBody body, FaceId faceId, ConeSurface cone, DisplayTessellationOptions options)
    {
        var parameters = GetRevolvedFaceParameters(
            body,
            faceId,
            options,
            radiusHint: 1d,
            allowThreeCoedgeConeTopology: true,
            axialParameterFromPoint: cone.AxialParameterFromPoint);
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


    private static KernelResult<DisplayFaceMeshPatch> TessellateBSplineSurfaceFace(BrepBody body, FaceId faceId, BSplineSurfaceWithKnots surface, DisplayTessellationOptions options)
    {
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count > 1)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure([
                CreateNotImplemented($"Face {faceId.Value} BSpline surface tessellation currently supports zero or one trim loop. Observed {loopIds.Count} loops.", BSplineSurfaceTrimUnsupportedSource)]);
        }

        var uStart = surface.DomainStartU;
        var uEnd = surface.DomainEndU;
        var vStart = surface.DomainStartV;
        var vEnd = surface.DomainEndV;

        if (loopIds.Count == 1)
        {
            var trimPatch = TryResolveBSplineTrimPatch(body, faceId, surface, loopIds[0]);
            if (!trimPatch.IsSuccess)
            {
                return KernelResult<DisplayFaceMeshPatch>.Failure(trimPatch.Diagnostics);
            }

            uStart = trimPatch.Value.UStart;
            uEnd = trimPatch.Value.UEnd;
            vStart = trimPatch.Value.VStart;
            vEnd = trimPatch.Value.VEnd;
        }

        var uSpan = uEnd - uStart;
        var vSpan = vEnd - vStart;
        var uSegments = System.Math.Max(options.MinimumSegments, System.Math.Clamp((int)double.Ceiling(double.Abs(uSpan) / options.AngularToleranceRadians), options.MinimumSegments, options.MaximumSegments));
        var vSegments = System.Math.Max(1, System.Math.Clamp((int)double.Ceiling(double.Abs(vSpan) / options.ChordTolerance), 1, options.MaximumSegments));

        return KernelResult<DisplayFaceMeshPatch>.Success(CreateBoundedGridPatch(
            faceId,
            uSegments,
            vSegments,
            (u, v) => surface.Evaluate(u, v),
            (u, v) => EvaluateBSplineNormal(surface, u, v),
            uStart,
            uEnd,
            vStart,
            vEnd));
    }

    private static KernelResult<(double UStart, double UEnd, double VStart, double VEnd)> TryResolveBSplineTrimPatch(
        BrepBody body,
        FaceId faceId,
        BSplineSurfaceWithKnots surface,
        LoopId loopId)
    {
        const double minSpan = 1e-8d;

        var coedges = body.GetCoedgeIds(loopId).Select(id => body.Topology.GetCoedge(id)).ToArray();
        if (coedges.Length < 3)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} BSpline trim loop must contain at least three coedges. Observed {coedges.Length}.", BSplineSurfaceTrimUnsupportedSource)]);
        }

        var vertexPointsResult = BuildLoopVertexPointLookup(body, coedges, faceId);
        if (!vertexPointsResult.IsSuccess)
        {
            return KernelResult<(double, double, double, double)>.Failure(vertexPointsResult.Diagnostics);
        }

        var samples = new List<Point3D>();
        foreach (var coedge in coedges)
        {
            var endpoints = GetEdgeEndpoints(body, coedge.EdgeId, coedge.IsReversed, vertexPointsResult.Value);
            if (!endpoints.IsSuccess)
            {
                return KernelResult<(double, double, double, double)>.Failure(endpoints.Diagnostics);
            }

            samples.Add(endpoints.Value.Start);
            samples.Add(endpoints.Value.End);

            var edgePolyline = TessellateEdge(body, coedge.EdgeId, DisplayTessellationOptions.Default);
            if (!edgePolyline.IsSuccess)
            {
                continue;
            }

            var edgePoints = coedge.IsReversed
                ? edgePolyline.Value.Points.Reverse().ToArray()
                : edgePolyline.Value.Points;
            samples.AddRange(edgePoints);
        }

        if (samples.Count == 0)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} BSpline trim tessellation could not derive loop samples.", BSplineSurfaceTrimUnsupportedSource)]);
        }

        var uvSamples = samples
            .Select(point => TryProjectPointToBSplineUv(surface, point))
            .Where(result => result.HasValue)
            .Select(result => result!.Value)
            .ToArray();

        if (uvSamples.Length == 0)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} BSpline trim tessellation could not project loop samples into the BSpline parametric domain.", BSplineSurfaceTrimUnsupportedSource)]);
        }

        var uStart = System.Math.Max(surface.DomainStartU, uvSamples.Min(s => s.U));
        var uEnd = System.Math.Min(surface.DomainEndU, uvSamples.Max(s => s.U));
        var vStart = System.Math.Max(surface.DomainStartV, uvSamples.Min(s => s.V));
        var vEnd = System.Math.Min(surface.DomainEndV, uvSamples.Max(s => s.V));

        if ((uEnd - uStart) <= minSpan || (vEnd - vStart) <= minSpan)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} BSpline trim tessellation derived a degenerate UV span.", BSplineSurfaceTrimUnsupportedSource)]);
        }

        return KernelResult<(double, double, double, double)>.Success((uStart, uEnd, vStart, vEnd));
    }

    private static (double U, double V)? TryProjectPointToBSplineUv(BSplineSurfaceWithKnots surface, Point3D point)
    {
        var uStart = surface.DomainStartU;
        var uEnd = surface.DomainEndU;
        var vStart = surface.DomainStartV;
        var vEnd = surface.DomainEndV;

        const int coarseSegments = 6;
        var bestU = uStart;
        var bestV = vStart;
        var bestDistanceSquared = double.PositiveInfinity;

        for (var iu = 0; iu <= coarseSegments; iu++)
        {
            var tu = (double)iu / coarseSegments;
            var u = uStart + ((uEnd - uStart) * tu);
            for (var iv = 0; iv <= coarseSegments; iv++)
            {
                var tv = (double)iv / coarseSegments;
                var v = vStart + ((vEnd - vStart) * tv);
                var sample = surface.Evaluate(u, v);
                var delta = sample - point;
                var distanceSquared = delta.Dot(delta);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestU = u;
                    bestV = v;
                }
            }
        }

        var uStep = (uEnd - uStart) / coarseSegments;
        var vStep = (vEnd - vStart) / coarseSegments;

        for (var iteration = 0; iteration < 3; iteration++)
        {
            var nextUStep = uStep * 0.5d;
            var nextVStep = vStep * 0.5d;
            var localBestU = bestU;
            var localBestV = bestV;
            var localBestDistance = bestDistanceSquared;

            for (var du = -1; du <= 1; du++)
            {
                var u = System.Math.Clamp(bestU + (du * uStep), uStart, uEnd);
                for (var dv = -1; dv <= 1; dv++)
                {
                    var v = System.Math.Clamp(bestV + (dv * vStep), vStart, vEnd);
                    var sample = surface.Evaluate(u, v);
                    var delta = sample - point;
                    var distanceSquared = delta.Dot(delta);
                    if (distanceSquared < localBestDistance)
                    {
                        localBestDistance = distanceSquared;
                        localBestU = u;
                        localBestV = v;
                    }
                }
            }

            bestU = localBestU;
            bestV = localBestV;
            bestDistanceSquared = localBestDistance;
            uStep = nextUStep;
            vStep = nextVStep;
        }

        return (bestU, bestV);
    }

    private static Vector3D EvaluateBSplineNormal(BSplineSurfaceWithKnots surface, double u, double v)
    {
        var du = System.Math.Max((surface.DomainEndU - surface.DomainStartU) * 1e-4d, 1e-6d);
        var dv = System.Math.Max((surface.DomainEndV - surface.DomainStartV) * 1e-4d, 1e-6d);

        var uMinus = System.Math.Clamp(u - du, surface.DomainStartU, surface.DomainEndU);
        var uPlus = System.Math.Clamp(u + du, surface.DomainStartU, surface.DomainEndU);
        var vMinus = System.Math.Clamp(v - dv, surface.DomainStartV, surface.DomainEndV);
        var vPlus = System.Math.Clamp(v + dv, surface.DomainStartV, surface.DomainEndV);

        var tangentU = surface.Evaluate(uPlus, v) - surface.Evaluate(uMinus, v);
        var tangentV = surface.Evaluate(u, vPlus) - surface.Evaluate(u, vMinus);
        var normal = tangentU.Cross(tangentV);
        if (!normal.TryNormalize(out var normalized))
        {
            return new Vector3D(0d, 0d, 1d);
        }

        return normalized;
    }

    private static KernelResult<DisplayFaceMeshPatch> TessellateSphereFace(BrepBody body, FaceId faceId, SphereSurface sphere, DisplayTessellationOptions options)
    {
        var trimPatchResult = TryResolveSphereTrimPatch(body, faceId, sphere);
        if (!trimPatchResult.IsSuccess)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure(trimPatchResult.Diagnostics);
        }

        var trimPatch = trimPatchResult.Value;
        var angularSegments = CalculateSegmentCount(trimPatch.USpan, sphere.Radius, options);
        var elevationSegments = System.Math.Max(2, System.Math.Clamp(angularSegments / 2, options.MinimumSegments / 2, options.MaximumSegments));

        return KernelResult<DisplayFaceMeshPatch>.Success(CreateBoundedGridPatch(
            faceId,
            angularSegments,
            elevationSegments,
            (u, v) => sphere.Evaluate(u, v),
            (u, v) => sphere.Normal(u, v).ToVector(),
            trimPatch.UStart,
            trimPatch.UStart + trimPatch.USpan,
            trimPatch.VStart,
            trimPatch.VEnd),
            trimPatchResult.Diagnostics);
    }


    private static KernelResult<DisplayFaceMeshPatch> TessellateTorusFace(BrepBody body, FaceId faceId, TorusSurface torus, DisplayTessellationOptions options)
    {
        var parameters = GetRevolvedFaceParameters(body, faceId, options, radiusHint: torus.MajorRadius + torus.MinorRadius, allowThreeCoedgeConeTopology: false, axialParameterFromPoint: point => TorusMinorAngleOf(torus, point));
        if (!parameters.IsSuccess)
        {
            return KernelResult<DisplayFaceMeshPatch>.Failure(parameters.Diagnostics);
        }

        return KernelResult<DisplayFaceMeshPatch>.Success(CreatePeriodicGridPatch(
            faceId,
            parameters.Value.AngularSegments,
            parameters.Value.AxialSegments,
            (u, v) => torus.Evaluate(u, v),
            (u, v) => torus.Normal(u, v).ToVector(),
            parameters.Value.VStart,
            parameters.Value.VEnd));
    }

    private static KernelResult<(double VStart, double VEnd, int AngularSegments, int AxialSegments)> GetRevolvedFaceParameters(
        BrepBody body,
        FaceId faceId,
        DisplayTessellationOptions options,
        double radiusHint,
        bool allowThreeCoedgeConeTopology,
        Func<Point3D, double>? axialParameterFromPoint = null)
    {
        var coedgesResult = TryGetSupportedRevolvedCoedges(body, faceId);
        if (!coedgesResult.IsSuccess)
        {
            return KernelResult<(double, double, int, int)>.Failure(coedgesResult.Diagnostics);
        }

        var coedges = coedgesResult.Value;

        var lineCoedges = coedges.Where(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.Line3).ToArray();
        var circleCoedges = coedges.Where(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.Circle3).ToArray();
        var bSplineCoedges = coedges.Where(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.BSpline3).ToArray();

        if (TryResolveFourUseMixedRevolvedLoop(body, coedges, lineCoedges, circleCoedges, axialParameterFromPoint, faceId, out var mixedRevolvedParameters, out var mixedRevolvedDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                mixedRevolvedParameters.VStart,
                mixedRevolvedParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(mixedRevolvedParameters.VStart, mixedRevolvedParameters.VEnd, options)), mixedRevolvedDiagnostics);
        }

        if (TryResolveFourUseCircleBsplineRevolvedLoop(body, coedges, circleCoedges, bSplineCoedges, axialParameterFromPoint, faceId, out var circleBsplineParameters, out var circleBsplineDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                circleBsplineParameters.VStart,
                circleBsplineParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(circleBsplineParameters.VStart, circleBsplineParameters.VEnd, options)), circleBsplineDiagnostics);
        }

        if (TryResolveFourCoedgeSingleCircleThreeBsplineRevolvedLoop(body, coedges, circleCoedges, bSplineCoedges, axialParameterFromPoint, faceId, out var fourCoedgeSingleCircleParameters, out var fourCoedgeSingleCircleDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                fourCoedgeSingleCircleParameters.VStart,
                fourCoedgeSingleCircleParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(fourCoedgeSingleCircleParameters.VStart, fourCoedgeSingleCircleParameters.VEnd, options)), fourCoedgeSingleCircleDiagnostics);
        }

        if (TryResolveFourCoedgeThreeCircleSingleBsplineRevolvedLoop(body, coedges, circleCoedges, bSplineCoedges, axialParameterFromPoint, faceId, out var fourCoedgeThreeCircleParameters, out var fourCoedgeThreeCircleDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                fourCoedgeThreeCircleParameters.VStart,
                fourCoedgeThreeCircleParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(fourCoedgeThreeCircleParameters.VStart, fourCoedgeThreeCircleParameters.VEnd, options)), fourCoedgeThreeCircleDiagnostics);
        }

        if (TryResolveFourUseBsplineOnlyRevolvedLoop(body, coedges, bSplineCoedges, axialParameterFromPoint, faceId, out var bsplineOnlyParameters, out var bsplineOnlyDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                bsplineOnlyParameters.VStart,
                bsplineOnlyParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(bsplineOnlyParameters.VStart, bsplineOnlyParameters.VEnd, options)), bsplineOnlyDiagnostics);
        }

        if (TryResolveRepeatedCircleBsplineRevolvedLoop(body, coedges, circleCoedges, bSplineCoedges, axialParameterFromPoint, faceId, out var repeatedCircleBsplineParameters, out var repeatedCircleBsplineDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                repeatedCircleBsplineParameters.VStart,
                repeatedCircleBsplineParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(repeatedCircleBsplineParameters.VStart, repeatedCircleBsplineParameters.VEnd, options)), repeatedCircleBsplineDiagnostics);
        }

        if (TryResolveSixCoedgeSingleCircleFiveBsplineRevolvedLoop(body, coedges, circleCoedges, bSplineCoedges, axialParameterFromPoint, faceId, out var sixCoedgeSingleCircleParameters, out var sixCoedgeSingleCircleDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                sixCoedgeSingleCircleParameters.VStart,
                sixCoedgeSingleCircleParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(sixCoedgeSingleCircleParameters.VStart, sixCoedgeSingleCircleParameters.VEnd, options)), sixCoedgeSingleCircleDiagnostics);
        }

        if (TryResolveSixUseBsplineOnlyRevolvedLoop(body, coedges, bSplineCoedges, axialParameterFromPoint, faceId, out var sixUseBsplineOnlyParameters, out var sixUseBsplineOnlyDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                sixUseBsplineOnlyParameters.VStart,
                sixUseBsplineOnlyParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(sixUseBsplineOnlyParameters.VStart, sixUseBsplineOnlyParameters.VEnd, options)), sixUseBsplineOnlyDiagnostics);
        }

        if (TryResolveDualSeamCircleRevolvedLoop(body, coedges, lineCoedges, circleCoedges, axialParameterFromPoint, faceId, out var dualSeamCircleParameters, out var dualSeamCircleDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                dualSeamCircleParameters.VStart,
                dualSeamCircleParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(dualSeamCircleParameters.VStart, dualSeamCircleParameters.VEnd, options)), dualSeamCircleDiagnostics);
        }

        if (TryResolveRepeatedMixedRevolvedLoop(body, coedges, lineCoedges, circleCoedges, axialParameterFromPoint, faceId, out var repeatedMixedParameters, out var repeatedMixedDiagnostics))
        {
            return KernelResult<(double, double, int, int)>.Success((
                repeatedMixedParameters.VStart,
                repeatedMixedParameters.VEnd,
                CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options),
                CalculateAxialSegments(repeatedMixedParameters.VStart, repeatedMixedParameters.VEnd, options)), repeatedMixedDiagnostics);
        }

        if (allowThreeCoedgeConeTopology && TryResolveThreeCoedgeBsplineConeLoop(body, coedges, bSplineCoedges, axialParameterFromPoint, faceId, out var threeCoedgeBsplineParameters, out var threeCoedgeBsplineDiagnostics))
        {
            var angularSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
            var axialSegments = CalculateAxialSegments(threeCoedgeBsplineParameters.VStart, threeCoedgeBsplineParameters.VEnd, options);
            return KernelResult<(double, double, int, int)>.Success((threeCoedgeBsplineParameters.VStart, threeCoedgeBsplineParameters.VEnd, angularSegments, axialSegments), threeCoedgeBsplineDiagnostics);
        }

        if (allowThreeCoedgeConeTopology && TryResolveThreeCoedgeMixedCircleBsplineConeLoop(body, coedges, circleCoedges, bSplineCoedges, axialParameterFromPoint, faceId, out var threeCoedgeMixedCircleBsplineParameters, out var threeCoedgeMixedCircleBsplineDiagnostics))
        {
            var angularSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
            var axialSegments = CalculateAxialSegments(threeCoedgeMixedCircleBsplineParameters.VStart, threeCoedgeMixedCircleBsplineParameters.VEnd, options);
            return KernelResult<(double, double, int, int)>.Success((threeCoedgeMixedCircleBsplineParameters.VStart, threeCoedgeMixedCircleBsplineParameters.VEnd, angularSegments, axialSegments), threeCoedgeMixedCircleBsplineDiagnostics);
        }

        if (allowThreeCoedgeConeTopology && TryResolveConeRevolvedLoop(body, coedges, lineCoedges, circleCoedges, axialParameterFromPoint, faceId, out var coneRevolvedParameters, out var coneRevolvedDiagnostics))
        {
            var angularSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
            var axialSegments = CalculateAxialSegments(coneRevolvedParameters.VStart, coneRevolvedParameters.VEnd, options);
            return KernelResult<(double, double, int, int)>.Success((coneRevolvedParameters.VStart, coneRevolvedParameters.VEnd, angularSegments, axialSegments), coneRevolvedDiagnostics);
        }

        if (allowThreeCoedgeConeTopology && TryResolveSingleCoedgeCircleSeamReusedConeLoop(body, coedges, lineCoedges, circleCoedges, axialParameterFromPoint, faceId, out var singleCoedgeCircleSeamReusedConeParameters, out var singleCoedgeCircleSeamReusedConeDiagnostics))
        {
            var angularSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
            var axialSegments = CalculateAxialSegments(singleCoedgeCircleSeamReusedConeParameters.VStart, singleCoedgeCircleSeamReusedConeParameters.VEnd, options);
            return KernelResult<(double, double, int, int)>.Success((singleCoedgeCircleSeamReusedConeParameters.VStart, singleCoedgeCircleSeamReusedConeParameters.VEnd, angularSegments, axialSegments), singleCoedgeCircleSeamReusedConeDiagnostics);
        }

        if (TryResolveFourUseCircleOnlyNonSeamRevolvedLoop(body, coedges, lineCoedges, circleCoedges, axialParameterFromPoint, faceId, out var fourUseCircleOnlyNonSeamParameters, out var fourUseCircleOnlyNonSeamDiagnostics))
        {
            var angularSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
            var axialSegments = CalculateAxialSegments(fourUseCircleOnlyNonSeamParameters.VStart, fourUseCircleOnlyNonSeamParameters.VEnd, options);
            return KernelResult<(double, double, int, int)>.Success((fourUseCircleOnlyNonSeamParameters.VStart, fourUseCircleOnlyNonSeamParameters.VEnd, angularSegments, axialSegments), fourUseCircleOnlyNonSeamDiagnostics);
        }

        var uniqueEdgeIds = coedges
            .Select(c => c.EdgeId)
            .Distinct()
            .ToArray();
        var allEdgeCurvesAreCircles = uniqueEdgeIds
            .Select(id => body.GetEdgeCurve(id).Kind)
            .All(kind => kind == CurveGeometryKind.Circle3);

        if (lineCoedges.Length == 0 && circleCoedges.Length >= 4 && uniqueEdgeIds.Length == 2 && allEdgeCurvesAreCircles)
        {
            var angularSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
            var axialSegments = CalculateSegmentCount(2d * double.Pi, System.Math.Max(1e-6d, radiusHint), options);
            return KernelResult<(double, double, int, int)>.Success((0d, 2d * double.Pi, angularSegments, axialSegments));
        }

        var topologyFamily = allowThreeCoedgeConeTopology ? "cone/revolved" : "torus/revolved";
        var observedFamily = ClassifyRevolvedTopologyFamily(body, coedges, lineCoedges, circleCoedges, bSplineCoedges);
        return KernelResult<(double, double, int, int)>.Failure([CreateNotImplemented($"Face {faceId.Value} curved tessellation supports selected repeated {topologyFamily} boundary subfamilies; unsupported subfamily '{observedFamily}'. Observed: {DescribeRevolvedLoopTopology(body, coedges)}")]);
    }

    private static KernelResult<IReadOnlyList<Coedge>> TryGetSupportedRevolvedCoedges(BrepBody body, FaceId faceId)
    {
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count == 1)
        {
            var singleLoopCoedges = body.GetCoedgeIds(loopIds[0])
                .Select(id => body.Topology.GetCoedge(id))
                .ToArray();
            return KernelResult<IReadOnlyList<Coedge>>.Success(singleLoopCoedges);
        }

        if (TryResolvePreservedPeriodicDualSeamCircleLoopFamily(body, loopIds, out var preservedFamilyCoedges))
        {
            return KernelResult<IReadOnlyList<Coedge>>.Success(preservedFamilyCoedges);
        }

        return KernelResult<IReadOnlyList<Coedge>>.Failure([
            CreateNotImplemented($"Face {faceId.Value} curved tessellation supports exactly one loop or the preserved periodic dual single-coedge seam-circle loop family. Observed: {DescribeRevolvedFaceTopology(body, loopIds)}", CurvedTopologyUnsupportedSource)
        ]);
    }

    private static bool TryResolvePreservedPeriodicDualSeamCircleLoopFamily(
        BrepBody body,
        IReadOnlyList<LoopId> loopIds,
        out IReadOnlyList<Coedge> coedges)
    {
        coedges = [];
        if (loopIds.Count != 2)
        {
            return false;
        }

        var resolved = new List<Coedge>(2);
        foreach (var loopId in loopIds)
        {
            var coedgeIds = body.GetCoedgeIds(loopId);
            if (coedgeIds.Count != 1)
            {
                return false;
            }

            var coedge = body.Topology.GetCoedge(coedgeIds[0]);
            if (body.GetEdgeCurve(coedge.EdgeId).Kind != CurveGeometryKind.Circle3)
            {
                return false;
            }

            var edge = body.Topology.GetEdge(coedge.EdgeId);
            if (edge.StartVertexId != edge.EndVertexId)
            {
                return false;
            }

            resolved.Add(coedge);
        }

        coedges = resolved;
        return true;
    }


    private static bool TryResolveRepeatedCircleBsplineRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> circleCoedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count < 5 || circleCoedges.Count < 2 || bSplineCoedges.Count < 1 || axialParameterFromPoint is null)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveFourUseCircleOnlyNonSeamRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> lineCoedges,
        IReadOnlyList<Coedge> circleCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (axialParameterFromPoint is null || coedges.Count != 4 || lineCoedges.Count != 0 || circleCoedges.Count != 4)
        {
            return false;
        }

        var uniqueEdgeIds = coedges.Select(c => c.EdgeId).Distinct().ToArray();
        if (uniqueEdgeIds.Length != 4)
        {
            return false;
        }

        var seamUses = coedges.Count(c =>
        {
            var edge = body.Topology.GetEdge(c.EdgeId);
            return edge.StartVertexId == edge.EndVertexId;
        });
        if (seamUses != 0)
        {
            return false;
        }

        var allEdgeCurvesAreCircles = uniqueEdgeIds
            .Select(id => body.GetEdgeCurve(id).Kind)
            .All(kind => kind == CurveGeometryKind.Circle3);
        if (!allEdgeCurvesAreCircles)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }


    private static bool TryResolveDualSeamCircleRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> lineCoedges,
        IReadOnlyList<Coedge> circleCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];

        if (axialParameterFromPoint is null || coedges.Count != 2 || lineCoedges.Count != 0 || circleCoedges.Count != 2)
        {
            return false;
        }

        var seamUses = coedges.Count(c =>
        {
            var edge = body.Topology.GetEdge(c.EdgeId);
            return edge.StartVertexId == edge.EndVertexId;
        });

        if (seamUses != 2 || coedges.Select(c => c.EdgeId).Distinct().Count() != 2)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveSingleCoedgeCircleSeamReusedConeLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> lineCoedges,
        IReadOnlyList<Coedge> circleCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];

        if (axialParameterFromPoint is null || coedges.Count != 1 || lineCoedges.Count != 0 || circleCoedges.Count != 1)
        {
            return false;
        }

        var coedge = coedges[0];
        var edge = body.Topology.GetEdge(coedge.EdgeId);
        if (edge.StartVertexId != edge.EndVertexId)
        {
            return false;
        }

        if (body.GetEdgeCurve(coedge.EdgeId).Kind != CurveGeometryKind.Circle3)
        {
            return false;
        }

        var vertexPoint = GetVertexPoint(body, edge.StartVertexId);
        if (!vertexPoint.IsSuccess)
        {
            diagnostics = vertexPoint.Diagnostics;
            return false;
        }

        var trimV = axialParameterFromPoint(vertexPoint.Value);
        if (!double.IsFinite(trimV) || trimV <= 1e-8d)
        {
            diagnostics =
            [
                CreateNotImplemented($"Face {faceId.Value} curved tessellation could not derive finite non-degenerate axial bounds for single-coedge seam-reused circle cone topology. Observed: {DescribeRevolvedLoopTopology(body, coedges)}", CurvedTopologyUnsupportedSource)
            ];
            return false;
        }

        parameters = (0d, trimV);
        return true;
    }

    private static bool TryResolveRepeatedMixedRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> lineCoedges,
        IReadOnlyList<Coedge> circleCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count < 5 || lineCoedges.Count < 2 || circleCoedges.Count < 2)
        {
            return false;
        }

        var lineEdgeIds = lineCoedges.Select(c => c.EdgeId).Distinct().ToArray();
        if (lineEdgeIds.Length == 0)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromLines(body, coedges, lineEdgeIds, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveSixCoedgeSingleCircleFiveBsplineRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> circleCoedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        var uniqueEdgeCount = coedges.Select(c => c.EdgeId).Distinct().Count();
        if (coedges.Count != 6 || uniqueEdgeCount != 6 || circleCoedges.Count != 1 || bSplineCoedges.Count != 5 || axialParameterFromPoint is null)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveFourUseMixedRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> lineCoedges,
        IReadOnlyList<Coedge> circleCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 4 || lineCoedges.Count != 2 || circleCoedges.Count != 2)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromLines(body, coedges, lineCoedges.Select(c => c.EdgeId).Distinct().ToArray(), axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveFourUseCircleBsplineRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> circleCoedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 4 || circleCoedges.Count != 2 || bSplineCoedges.Count != 2 || axialParameterFromPoint is null)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveFourUseBsplineOnlyRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 4 || bSplineCoedges.Count != 4 || axialParameterFromPoint is null)
        {
            return false;
        }

        var uniqueEdgeCount = coedges
            .Select(c => c.EdgeId)
            .Distinct()
            .Count();
        if (uniqueEdgeCount != 4)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveSixUseBsplineOnlyRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 6 || bSplineCoedges.Count != 6 || axialParameterFromPoint is null)
        {
            return false;
        }

        var uniqueEdgeCount = coedges
            .Select(c => c.EdgeId)
            .Distinct()
            .Count();
        if (uniqueEdgeCount != 6)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveFourCoedgeSingleCircleThreeBsplineRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> circleCoedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 4 || circleCoedges.Count != 1 || bSplineCoedges.Count != 3 || axialParameterFromPoint is null)
        {
            return false;
        }

        var uniqueEdgeCount = coedges
            .Select(c => c.EdgeId)
            .Distinct()
            .Count();
        if (uniqueEdgeCount < 3)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveFourCoedgeThreeCircleSingleBsplineRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> circleCoedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 4 || circleCoedges.Count != 3 || bSplineCoedges.Count != 1 || axialParameterFromPoint is null)
        {
            return false;
        }

        var uniqueEdgeCount = coedges
            .Select(c => c.EdgeId)
            .Distinct()
            .Count();
        if (uniqueEdgeCount != 4)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveConeRevolvedLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> lineCoedges,
        IReadOnlyList<Coedge> circleCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count < 3 || lineCoedges.Count < 2 || circleCoedges.Count < 1)
        {
            return false;
        }

        var lineEdgeIds = lineCoedges.Select(c => c.EdgeId).Distinct().ToArray();
        var result = TryResolveAxialBoundsFromLines(body, coedges, lineEdgeIds, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveThreeCoedgeMixedCircleBsplineConeLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> circleCoedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 3 || circleCoedges.Count != 1 || bSplineCoedges.Count != 2 || axialParameterFromPoint is null)
        {
            return false;
        }

        var uniqueEdgeCount = coedges
            .Select(c => c.EdgeId)
            .Distinct()
            .Count();
        if (uniqueEdgeCount != 3)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static bool TryResolveThreeCoedgeBsplineConeLoop(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> bSplineCoedges,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId,
        out (double VStart, double VEnd) parameters,
        out IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        parameters = default;
        diagnostics = [];
        if (coedges.Count != 3 || bSplineCoedges.Count != 3 || axialParameterFromPoint is null)
        {
            return false;
        }

        var uniqueEdgeCount = coedges
            .Select(c => c.EdgeId)
            .Distinct()
            .Count();
        if (uniqueEdgeCount != 3)
        {
            return false;
        }

        var result = TryResolveAxialBoundsFromProjectedLoopVertices(body, coedges, axialParameterFromPoint, faceId);
        if (!result.IsSuccess)
        {
            diagnostics = result.Diagnostics;
            return false;
        }

        parameters = result.Value;
        diagnostics = result.Diagnostics;
        return true;
    }

    private static KernelResult<(double VStart, double VEnd)> TryResolveAxialBoundsFromLines(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<EdgeId> lineEdgeIds,
        Func<Point3D, double>? axialParameterFromPoint,
        FaceId faceId)
    {
        if (lineEdgeIds.Count == 0)
        {
            return KernelResult<(double VStart, double VEnd)>.Failure([CreateNotImplemented($"Face {faceId.Value} curved tessellation expected line trim edges for revolved topology. Observed: {DescribeRevolvedLoopTopology(body, coedges)}", CurvedTopologyUnsupportedSource)]);
        }

        var intervals = new List<ParameterInterval>(lineEdgeIds.Count);
        foreach (var lineEdgeId in lineEdgeIds)
        {
            if (!body.Bindings.TryGetEdgeBinding(lineEdgeId, out var lineTrimBinding))
            {
                return KernelResult<(double VStart, double VEnd)>.Failure([CreateNotImplemented($"Face {faceId.Value} line trim edge is missing curve trim binding.")]);
            }

            intervals.Add(lineTrimBinding.TrimInterval ?? new ParameterInterval(0d, 1d));
        }

        var vMin = intervals.Min(i => System.Math.Min(i.Start, i.End));
        var vMax = intervals.Max(i => System.Math.Max(i.Start, i.End));
        var increasing = intervals[0].End >= intervals[0].Start;
        var vStart = increasing ? vMin : vMax;
        var vEnd = increasing ? vMax : vMin;

        if (axialParameterFromPoint is not null)
        {
            var vertexPointsResult = BuildLoopVertexPointLookup(body, coedges, faceId);
            if (!vertexPointsResult.IsSuccess)
            {
                return KernelResult<(double VStart, double VEnd)>.Failure(vertexPointsResult.Diagnostics);
            }

            var projected = vertexPointsResult.Value.Values
                .Select(axialParameterFromPoint)
                .Where(double.IsFinite)
                .ToArray();

            if (projected.Length != 0)
            {
                var projectedMin = projected.Min();
                var projectedMax = projected.Max();
                vStart = increasing ? projectedMin : projectedMax;
                vEnd = increasing ? projectedMax : projectedMin;
            }
        }

        return KernelResult<(double VStart, double VEnd)>.Success((vStart, vEnd));
    }


    private static KernelResult<(double VStart, double VEnd)> TryResolveAxialBoundsFromProjectedLoopVertices(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        Func<Point3D, double> axialParameterFromPoint,
        FaceId faceId)
    {
        var vertexPointsResult = BuildLoopVertexPointLookup(body, coedges, faceId);
        if (!vertexPointsResult.IsSuccess)
        {
            return KernelResult<(double VStart, double VEnd)>.Failure(vertexPointsResult.Diagnostics);
        }

        var projected = vertexPointsResult.Value.Values
            .Select(axialParameterFromPoint)
            .Where(double.IsFinite)
            .ToArray();

        if (projected.Length < 2)
        {
            return KernelResult<(double VStart, double VEnd)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} curved tessellation could not derive axial bounds from projected loop vertices. Observed: {DescribeRevolvedLoopTopology(body, coedges)}", CurvedTopologyUnsupportedSource)]);
        }

        var vStart = projected.Min();
        var vEnd = projected.Max();
        if (double.Abs(vEnd - vStart) <= 1e-9d)
        {
            return KernelResult<(double VStart, double VEnd)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} curved tessellation derived a degenerate projected axial span. Observed: {DescribeRevolvedLoopTopology(body, coedges)}", CurvedTopologyUnsupportedSource)]);
        }

        return KernelResult<(double VStart, double VEnd)>.Success((vStart, vEnd));
    }

    private static int CalculateAxialSegments(double vStart, double vEnd, DisplayTessellationOptions options)
    {
        var axialSpan = double.Abs(vEnd - vStart);
        return System.Math.Max(1, System.Math.Clamp((int)double.Ceiling(axialSpan / options.ChordTolerance), 1, options.MaximumSegments));
    }

    private static string DescribeRevolvedLoopTopology(BrepBody body, IReadOnlyList<Coedge> coedges, int loopCount = 1)
    {
        var lineUseCount = 0;
        var circleUseCount = 0;
        var ellipseUseCount = 0;
        var bSplineUseCount = 0;
        var seamUseCount = 0;

        foreach (var coedge in coedges)
        {
            var curve = body.GetEdgeCurve(coedge.EdgeId);
            if (curve.Kind == CurveGeometryKind.Line3)
            {
                lineUseCount++;
            }
            else if (curve.Kind == CurveGeometryKind.Circle3)
            {
                circleUseCount++;
            }
            else if (curve.Kind == CurveGeometryKind.Ellipse3)
            {
                ellipseUseCount++;
            }
            else if (curve.Kind == CurveGeometryKind.BSpline3)
            {
                bSplineUseCount++;
            }

            var edge = body.Topology.GetEdge(coedge.EdgeId);
            if (edge.StartVertexId == edge.EndVertexId)
            {
                seamUseCount++;
            }
        }

        var edgeFamilies = coedges
            .Select(c => $"{c.EdgeId.Value}:{body.GetEdgeCurve(c.EdgeId).Kind}")
            .ToArray();

        return $"loops={loopCount} coedges={coedges.Count} lineUses={lineUseCount} circleUses={circleUseCount} ellipseUses={ellipseUseCount} bSplineUses={bSplineUseCount} seamEdgeUses={seamUseCount} edgeFamilies=[{string.Join(", ", edgeFamilies)}]";
    }

    private static string DescribeRevolvedFaceTopology(BrepBody body, IReadOnlyList<LoopId> loopIds)
    {
        var loopDescriptions = new List<string>(loopIds.Count);
        var aggregateCoedges = new List<Coedge>();

        foreach (var loopId in loopIds)
        {
            var coedges = body.GetCoedgeIds(loopId)
                .Select(id => body.Topology.GetCoedge(id))
                .ToArray();
            aggregateCoedges.AddRange(coedges);

            var uniqueEdges = coedges.Select(c => c.EdgeId).Distinct().Count();
            var circleUses = coedges.Count(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.Circle3);
            var seamUses = coedges.Count(c =>
            {
                var edge = body.Topology.GetEdge(c.EdgeId);
                return edge.StartVertexId == edge.EndVertexId;
            });

            loopDescriptions.Add($"{loopId.Value}:coedges={coedges.Length}:uniqueEdges={uniqueEdges}:circleUses={circleUses}:seamUses={seamUses}");
        }

        return $"{DescribeRevolvedLoopTopology(body, aggregateCoedges, loopIds.Count)} loopFamilies=[{string.Join(", ", loopDescriptions)}]";
    }

    private static string ClassifyRevolvedTopologyFamily(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        IReadOnlyList<Coedge> lineCoedges,
        IReadOnlyList<Coedge> circleCoedges,
        IReadOnlyList<Coedge> bSplineCoedges)
    {
        var uniqueEdgeIds = coedges.Select(c => c.EdgeId).Distinct().ToArray();
        var seamUses = coedges.Count(c =>
        {
            var edge = body.Topology.GetEdge(c.EdgeId);
            return edge.StartVertexId == edge.EndVertexId;
        });

        if (lineCoedges.Count == 0 && circleCoedges.Count == coedges.Count)
        {
            if (coedges.Count == 1 && uniqueEdgeIds.Length == 1 && seamUses == 1)
            {
                return "single-coedge circle-only seam-reused revolved loop";
            }

            if (coedges.Count == 4 && uniqueEdgeIds.Length == 4 && seamUses == 0)
            {
                return "four-coedge circle-only non-seam revolved loop";
            }

            return seamUses > 0 ? "circle-only seam reused loop" : "circle-only non-seam loop";
        }

        if (lineCoedges.Count >= 2 && circleCoedges.Count >= 2 && coedges.Count >= 5)
        {
            return "repeated mixed line/circle revolved loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count >= 2 && bSplineCoedges.Count >= 1 && coedges.Count >= 5)
        {
            return "repeated mixed circle/bspline revolved loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count == 1 && bSplineCoedges.Count == 5 && coedges.Count == 6 && uniqueEdgeIds.Length == 6)
        {
            return "six-coedge single-circle/five-bspline revolved loop";
        }

        if (lineCoedges.Count >= 2 && circleCoedges.Count >= 1 && coedges.Count == 3)
        {
            return "three-coedge cone/revolved loop";
        }

        if (bSplineCoedges.Count == 3 && coedges.Count == 3 && uniqueEdgeIds.Length == 3)
        {
            return "three-coedge cone/revolved bspline loop";
        }

        if (circleCoedges.Count == 1 && bSplineCoedges.Count == 2 && coedges.Count == 3 && uniqueEdgeIds.Length == 3)
        {
            return "three-coedge cone/revolved mixed circle/bspline loop";
        }

        if (lineCoedges.Count == 2 && circleCoedges.Count == 2 && coedges.Count == 4)
        {
            return "four-coedge mixed line/circle loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count == 2 && bSplineCoedges.Count == 2 && coedges.Count == 4)
        {
            return "four-coedge mixed circle/bspline loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count == 1 && bSplineCoedges.Count == 3 && coedges.Count == 4 && uniqueEdgeIds.Length >= 3)
        {
            return "four-coedge single-circle/three-bspline revolved loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count == 3 && bSplineCoedges.Count == 1 && coedges.Count == 4 && uniqueEdgeIds.Length == 4)
        {
            return "four-coedge three-circle/single-bspline revolved loop";
        }

        if (lineCoedges.Count == 2 && circleCoedges.Count == 0 && bSplineCoedges.Count == 2 && coedges.Count == 4 && uniqueEdgeIds.Length == 4)
        {
            return "four-coedge mixed line/bspline revolved loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count == 0 && bSplineCoedges.Count == 4 && coedges.Count == 4)
        {
            return "four-coedge bspline-only revolved loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count == 0 && bSplineCoedges.Count == 6 && coedges.Count == 6 && uniqueEdgeIds.Length == 6 && seamUses > 0)
        {
            return "six-coedge bspline-only seam-reused revolved loop";
        }

        if (lineCoedges.Count == 0 && circleCoedges.Count == 0 && bSplineCoedges.Count == 6 && coedges.Count == 6 && uniqueEdgeIds.Length == 6)
        {
            return "six-coedge bspline-only revolved loop";
        }

        return $"other (coedges={coedges.Count}, uniqueEdges={uniqueEdgeIds.Length})";
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

    private static DisplayFaceMeshPatch CreateBoundedGridPatch(
        FaceId faceId,
        int angularSegments,
        int axialSegments,
        Func<double, double, Point3D> evaluate,
        Func<double, double, Vector3D> evaluateNormal,
        double uStart,
        double uEnd,
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
                var uu = uStart + ((uEnd - uStart) * tu);
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

    private static KernelResult<(double UStart, double USpan, double VStart, double VEnd)> TryResolveSphereTrimPatch(
        BrepBody body,
        FaceId faceId,
        SphereSurface sphere)
    {
        const double minAngularSpan = 1e-6d;
        const double minElevationSpan = 1e-6d;

        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count == 0)
        {
            return KernelResult<(double, double, double, double)>.Success((0d, 2d * double.Pi, -double.Pi / 2d, double.Pi / 2d));
        }

        if (loopIds.Count != 1)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} sphere tessellation currently supports exactly one trim loop. Observed {loopIds.Count} loops.")]);
        }

        var coedges = body.GetCoedgeIds(loopIds[0]).Select(id => body.Topology.GetCoedge(id)).ToArray();
        if (coedges.Length < 3)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} spherical trim loop must contain at least three coedges. Observed {coedges.Length}.")]);
        }

        var vertexPointsResult = BuildLoopVertexPointLookup(body, coedges, faceId);
        if (!vertexPointsResult.IsSuccess)
        {
            return KernelResult<(double, double, double, double)>.Failure(vertexPointsResult.Diagnostics);
        }

        var allAzimuths = new List<double>();
        var allElevations = new List<double>();

        foreach (var coedge in coedges)
        {
            var curve = body.GetEdgeCurve(coedge.EdgeId);
            if (curve.Kind != CurveGeometryKind.Circle3
                && curve.Kind != CurveGeometryKind.BSpline3
                && curve.Kind != CurveGeometryKind.Ellipse3
                && curve.Kind != CurveGeometryKind.Line3)
            {
                return KernelResult<(double, double, double, double)>.Failure([
                    CreateNotImplemented($"Face {faceId.Value} spherical trim tessellation supports only line/circle/ellipse/bspline loop edges in this milestone. Observed curve kind '{curve.UnsupportedKind ?? curve.Kind.ToString()}'.")]);
            }

            var endpoints = GetEdgeEndpoints(body, coedge.EdgeId, coedge.IsReversed, vertexPointsResult.Value);
            if (!endpoints.IsSuccess)
            {
                return KernelResult<(double, double, double, double)>.Failure(endpoints.Diagnostics);
            }

            AppendSphereUv(allAzimuths, allElevations, sphere, endpoints.Value.Start);
            AppendSphereUv(allAzimuths, allElevations, sphere, endpoints.Value.End);

            var edgePolyline = TessellateEdge(body, coedge.EdgeId, options: DisplayTessellationOptions.Default);
            if (edgePolyline.IsSuccess)
            {
                var edgePoints = coedge.IsReversed
                    ? edgePolyline.Value.Points.Reverse().ToArray()
                    : edgePolyline.Value.Points;

                foreach (var point in edgePoints)
                {
                    AppendSphereUv(allAzimuths, allElevations, sphere, point);
                }
            }
        }

        if (allAzimuths.Count == 0 || allElevations.Count == 0)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} spherical trim tessellation could not derive loop parameter samples.")]);
        }

        var angularBounds = ResolveAngularBounds(allAzimuths);
        if (!angularBounds.IsSuccess || angularBounds.Value.Span <= minAngularSpan)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} spherical trim tessellation derived a degenerate azimuth span.")]);
        }

        var vStart = allElevations.Min();
        var vEnd = allElevations.Max();
        if ((vEnd - vStart) <= minElevationSpan)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateNotImplemented($"Face {faceId.Value} spherical trim tessellation derived a degenerate elevation span.")]);
        }

        vStart = System.Math.Clamp(vStart, -double.Pi / 2d, double.Pi / 2d);
        vEnd = System.Math.Clamp(vEnd, -double.Pi / 2d, double.Pi / 2d);

        return KernelResult<(double, double, double, double)>.Success((angularBounds.Value.Start, angularBounds.Value.Span, vStart, vEnd));
    }

    private static KernelResult<(double UStart, double UEnd, double VStart, double VEnd)> TryResolveCylinderTrimPatch(
        BrepBody body,
        FaceId faceId,
        CylinderSurface cylinder)
    {
        const double minSpan = 1e-8d;
        const double nearFullWrapThreshold = 2d * double.Pi * 0.95d;
        const double bridgeSeamGapMin = 8d * (double.Pi / 180d);
        const double bridgeSeamGapMax = 12d * (double.Pi / 180d);
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count == 0)
        {
            return KernelResult<(double, double, double, double)>.Failure([
                CreateInvalidArgument("Cylindrical face tessellation derived a degenerate trim patch.", CylinderTrimDegenerateSource)]);
        }

        var axis = cylinder.Axis.ToVector();
        var xAxis = cylinder.XAxis.ToVector();
        var yAxis = cylinder.YAxis.ToVector();

        (double UStart, double USpan, double VStart, double VEnd, bool UsedAmbiguousShorterSpanResolution)? best = null;
        (LoopId LoopId, int CoedgeCount, double AngularStart, double AngularSpan, double AxialSpan, bool IsNearFullWrap, bool AngularResolved, bool IsSingleCoedgeClosed, double VStart, bool HasBridgeTwin, SurfaceGeometryKind? TwinSurfaceKind)? bestAxialSpanDegenerate = null;

        foreach (var loopId in loopIds)
        {
            var coedges = body.GetCoedgeIds(loopId).Select(id => body.Topology.GetCoedge(id)).ToArray();

            var vertexPointsResult = BuildLoopVertexPointLookup(body, coedges, faceId);
            if (!vertexPointsResult.IsSuccess)
            {
                continue;
            }

            var allAngles = new List<double>();
            var allAxials = new List<double>();
            foreach (var coedge in coedges)
            {
                var endpoints = GetEdgeEndpoints(body, coedge.EdgeId, coedge.IsReversed, vertexPointsResult.Value);
                if (!endpoints.IsSuccess)
                {
                    allAngles.Clear();
                    break;
                }

                var start = endpoints.Value.Start;
                var end = endpoints.Value.End;
                allAngles.Add(CylinderAngleOf(cylinder, start, xAxis, yAxis, axis));
                allAngles.Add(CylinderAngleOf(cylinder, end, xAxis, yAxis, axis));
                allAxials.Add((start - cylinder.Origin).Dot(axis));
                allAxials.Add((end - cylinder.Origin).Dot(axis));

                var edgePolyline = TessellateEdge(body, coedge.EdgeId, DisplayTessellationOptions.Default);
                if (!edgePolyline.IsSuccess)
                {
                    continue;
                }

                var edgePoints = coedge.IsReversed
                    ? edgePolyline.Value.Points.Reverse().ToArray()
                    : edgePolyline.Value.Points;

                foreach (var point in edgePoints)
                {
                    allAngles.Add(CylinderAngleOf(cylinder, point, xAxis, yAxis, axis));
                    allAxials.Add((point - cylinder.Origin).Dot(axis));
                }
            }

            if (allAxials.Count == 0 || allAngles.Count == 0)
            {
                continue;
            }

            var vStart = allAxials.Min();
            var vEnd = allAxials.Max();
            var axialSpan = vEnd - vStart;
            var angularBounds = ResolveAngularBounds(allAngles);
            var angularSpan = (angularBounds.IsSuccess && angularBounds.Value.Span > minSpan) ? angularBounds.Value.Span : 0d;

            if (axialSpan <= minSpan)
            {
                var isSingleCoedgeClosed = false;
                var hasBridgeTwin = false;
                SurfaceGeometryKind? twinSurfaceKind = null;
                if (coedges.Length == 1 && body.TryGetEdgeVertices(coedges[0].EdgeId, out var edgeStart, out var edgeEnd))
                {
                    isSingleCoedgeClosed = edgeStart == edgeEnd;
                    hasBridgeTwin = TryResolveBridgeTwinSurfaceKind(body, faceId, coedges[0].EdgeId, out twinSurfaceKind);
                }

                var degenerateCandidate = (
                    LoopId: loopId,
                    CoedgeCount: coedges.Length,
                    AngularStart: angularBounds.IsSuccess ? angularBounds.Value.Start : 0d,
                    AngularSpan: angularSpan,
                    AxialSpan: axialSpan,
                    IsNearFullWrap: angularBounds.IsSuccess && angularBounds.Value.Span >= nearFullWrapThreshold,
                    AngularResolved: angularBounds.IsSuccess && angularBounds.Value.Span > minSpan,
                    IsSingleCoedgeClosed: isSingleCoedgeClosed,
                    VStart: vStart,
                    HasBridgeTwin: hasBridgeTwin,
                    TwinSurfaceKind: twinSurfaceKind);
                if (!bestAxialSpanDegenerate.HasValue ||
                    degenerateCandidate.AngularSpan > bestAxialSpanDegenerate.Value.AngularSpan ||
                    (System.Math.Abs(degenerateCandidate.AngularSpan - bestAxialSpanDegenerate.Value.AngularSpan) <= minSpan &&
                        degenerateCandidate.CoedgeCount > bestAxialSpanDegenerate.Value.CoedgeCount))
                {
                    bestAxialSpanDegenerate = degenerateCandidate;
                }

                continue;
            }

            if (!angularBounds.IsSuccess || angularBounds.Value.Span <= minSpan)
            {
                continue;
            }

            var candidate = (UStart: angularBounds.Value.Start, USpan: angularBounds.Value.Span, VStart: vStart, VEnd: vEnd, angularBounds.Value.UsedAmbiguousShorterSpanResolution);
            var candidateScore = candidate.USpan * (candidate.VEnd - candidate.VStart);
            var bestScore = best.HasValue ? best.Value.USpan * (best.Value.VEnd - best.Value.VStart) : double.NegativeInfinity;
            if (!best.HasValue || candidateScore > bestScore)
            {
                best = candidate;
            }
        }

        if (!best.HasValue)
        {
            if (bestAxialSpanDegenerate.HasValue && loopIds.Count == 1)
            {
                var isSingleCoedge = bestAxialSpanDegenerate.Value.CoedgeCount == 1;
                var seamGap = (2d * double.Pi) - bestAxialSpanDegenerate.Value.AngularSpan;
                var isBridgeFamily =
                    isSingleCoedge
                    && bestAxialSpanDegenerate.Value.IsNearFullWrap
                    && bestAxialSpanDegenerate.Value.AngularResolved
                    && bestAxialSpanDegenerate.Value.AxialSpan <= minSpan
                    && bestAxialSpanDegenerate.Value.IsSingleCoedgeClosed
                    && seamGap >= bridgeSeamGapMin
                    && seamGap <= bridgeSeamGapMax
                    && bestAxialSpanDegenerate.Value.HasBridgeTwin
                    && bestAxialSpanDegenerate.Value.TwinSurfaceKind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cone or SurfaceGeometryKind.Torus;

                if (isBridgeFamily)
                {
                    return KernelResult<(double, double, double, double)>.Success(
                        (
                            bestAxialSpanDegenerate.Value.AngularStart,
                            bestAxialSpanDegenerate.Value.AngularStart + bestAxialSpanDegenerate.Value.AngularSpan,
                            bestAxialSpanDegenerate.Value.VStart,
                            bestAxialSpanDegenerate.Value.VStart),
                        [CreateValidationWarning(
                            $"Face {faceId.Value} cylindrical trim treated as zero-width bridge limit (loop {bestAxialSpanDegenerate.Value.LoopId.Value}, seam gap {seamGap * (180d / double.Pi):F3} deg, twin {bestAxialSpanDegenerate.Value.TwinSurfaceKind}).",
                            CylinderTrimAxialSpanDegenerateSingleCoedgeNearFullWrapBridgeSource)]);
                }

                var classification = bestAxialSpanDegenerate.Value.IsNearFullWrap
                    ? isSingleCoedge
                        ? bestAxialSpanDegenerate.Value.IsSingleCoedgeClosed
                            ? "single-coedge closed ring-like near-full-wrap boundary with collapsed axial span"
                            : "single-coedge near-full-wrap boundary with collapsed axial span"
                        : "multi-coedge ring-like near-full-wrap boundary with collapsed axial span"
                    : bestAxialSpanDegenerate.Value.AngularResolved
                        ? isSingleCoedge
                            ? "single-coedge trimmed cylindrical loop with collapsed axial span"
                            : "multi-coedge trimmed cylindrical loop with collapsed axial span"
                        : "axial-span-collapsed loop without resolvable angular extent";

                var diagnosticSource = bestAxialSpanDegenerate.Value.IsNearFullWrap
                    ? isSingleCoedge
                        ? CylinderTrimAxialSpanDegenerateSingleCoedgeNearFullWrapSource
                        : CylinderTrimAxialSpanDegenerateMultiCoedgeSource
                    : isSingleCoedge
                        ? CylinderTrimAxialSpanDegenerateSingleCoedgeSource
                        : CylinderTrimAxialSpanDegenerateMultiCoedgeSource;
                return KernelResult<(double, double, double, double)>.Failure([
                    CreateInvalidArgument(
                        $"Face {faceId.Value} cylindrical trim loop {bestAxialSpanDegenerate.Value.LoopId.Value} has {bestAxialSpanDegenerate.Value.CoedgeCount} coedges, angular span {bestAxialSpanDegenerate.Value.AngularSpan:R}, axial span {bestAxialSpanDegenerate.Value.AxialSpan:R}, loop count {loopIds.Count}; classified as {classification} and treated as degenerate.",
                        diagnosticSource)]);
            }

            return KernelResult<(double, double, double, double)>.Failure([
                CreateInvalidArgument("Cylindrical face tessellation derived a degenerate trim patch.", CylinderTrimDegenerateSource)]);
        }

        var diagnostics = new List<KernelDiagnostic>();
        if (best.Value.UsedAmbiguousShorterSpanResolution)
        {
            diagnostics.Add(CreateValidationWarning(
                "Cylindrical face tessellation resolved ambiguous trim by choosing the shorter angular span.",
                CylinderTrimAmbiguousUsedShorterSpanSource));
        }

        return KernelResult<(double, double, double, double)>.Success(
            (best.Value.UStart, best.Value.UStart + best.Value.USpan, best.Value.VStart, best.Value.VEnd),
            diagnostics);
    }

    private static bool TryResolveBridgeTwinSurfaceKind(BrepBody body, FaceId sourceFaceId, EdgeId edgeId, out SurfaceGeometryKind? twinSurfaceKind)
    {
        twinSurfaceKind = null;
        var uses = new HashSet<FaceId>();

        foreach (var face in body.Topology.Faces)
        {
            foreach (var loopId in face.LoopIds)
            {
                var coedgeIds = body.GetCoedgeIds(loopId);
                foreach (var coedgeId in coedgeIds)
                {
                    var coedge = body.Topology.GetCoedge(coedgeId);
                    if (coedge.EdgeId == edgeId)
                    {
                        uses.Add(face.Id);
                        break;
                    }
                }
            }
        }

        if (uses.Count != 2)
        {
            return false;
        }

        var useArray = uses.ToArray();
        var twinFaceId = useArray[0] == sourceFaceId ? useArray[1] : useArray[0];
        if (twinFaceId == sourceFaceId)
        {
            return false;
        }

        if (!body.TryGetFaceSurfaceGeometry(twinFaceId, out var twinSurface) || twinSurface is null)
        {
            return false;
        }

        twinSurfaceKind = twinSurface.Kind;
        return true;
    }

    private static double CylinderAngleOf(CylinderSurface cylinder, Point3D point, Vector3D xAxis, Vector3D yAxis, Vector3D axis)
    {
        var offset = point - cylinder.Origin;
        var axial = axis * offset.Dot(axis);
        var radial = offset - axial;
        return NormalizeToZeroTwoPi(double.Atan2(radial.Dot(yAxis), radial.Dot(xAxis)));
    }

    private static KernelResult<(double Start, double Span, bool UsedAmbiguousShorterSpanResolution)> ResolveAngularBounds(IReadOnlyList<double> rawAngles)
    {
        const double epsilon = 1e-8d;

        var angles = rawAngles
            .Select(NormalizeToZeroTwoPi)
            .OrderBy(a => a)
            .ToArray();

        if (angles.Length == 0)
        {
            return KernelResult<(double, double, bool)>.Failure([CreateInvalidArgument("Cylindrical face tessellation derived a degenerate trim patch.", CylinderTrimDegenerateSource)]);
        }

        var largestGap = -1d;
        var largestGapIndex = -1;
        for (var i = 0; i < angles.Length; i++)
        {
            var current = angles[i];
            var next = i == angles.Length - 1 ? angles[0] + (2d * double.Pi) : angles[i + 1];
            var gap = next - current;
            if (gap > largestGap + epsilon)
            {
                largestGap = gap;
                largestGapIndex = i;
            }
        }

        if (largestGapIndex < 0)
        {
            return KernelResult<(double, double, bool)>.Failure([CreateInvalidArgument("Cylindrical face tessellation derived a degenerate trim patch.", CylinderTrimDegenerateSource)]);
        }

        var usedAmbiguous = largestGap <= epsilon;
        var start = angles[(largestGapIndex + 1) % angles.Length];
        var span = (2d * double.Pi) - largestGap;
        return KernelResult<(double, double, bool)>.Success((start, span, usedAmbiguous));
    }

    private static DisplayFaceMeshPatch CreatePlanarPatch(FaceId faceId, IReadOnlyList<Point3D> polygonPoints, Vector3D normal, IReadOnlyList<int> triangleIndices)
    {
        var normals = Enumerable.Repeat(normal, polygonPoints.Count).ToArray();
        return new DisplayFaceMeshPatch(faceId, polygonPoints.ToArray(), normals, triangleIndices.ToArray());
    }

    private static KernelResult<IReadOnlyList<Point3D>> BuildFlattenedPlanarLoopPoints(
        BrepBody body,
        IReadOnlyList<Coedge> coedges,
        FaceId faceId,
        PlaneSurface plane,
        DisplayTessellationOptions options)
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

        var orderedIndices = new List<int>(coedges.Count) { 0 };
        var used = new bool[coedges.Count];
        used[0] = true;
        var currentEnd = orientedEndpoints[0].End;

        for (var step = 1; step < coedges.Count; step++)
        {
            var nextIndex = FindNextCoedgeIndex(orientedEndpoints, used, currentEnd, out _, out var nextEnd);
            if (nextIndex < 0)
            {
                return KernelResult<IReadOnlyList<Point3D>>.Failure([CreateInvalidArgument($"Face {faceId.Value} planar loop coedges do not form a contiguous chain.", PlanarCurveFlatteningFailedSource)]);
            }

            used[nextIndex] = true;
            orderedIndices.Add(nextIndex);
            currentEnd = nextEnd;
        }

        if (!PointsAlmostEqual(orientedEndpoints[orderedIndices[0]].Start, currentEnd))
        {
            return KernelResult<IReadOnlyList<Point3D>>.Failure([CreateInvalidArgument($"Face {faceId.Value} planar loop coedges did not close after chaining.", PlanarCurveFlatteningFailedSource)]);
        }

        var flattened = new List<Point3D>();
        AppendUniquePoint(flattened, orientedEndpoints[orderedIndices[0]].Start);

        foreach (var index in orderedIndices)
        {
            var coedge = coedges[index];
            var (segmentStart, segmentEnd) = orientedEndpoints[index];
            var curve = body.GetEdgeCurve(coedge.EdgeId);

            switch (curve.Kind)
            {
                case CurveGeometryKind.Line3:
                    AppendUniquePoint(flattened, segmentEnd);
                    break;
                case CurveGeometryKind.Circle3:
                    var arcPointsResult = SampleCircleArc(body, coedge, curve.Circle3!.Value, segmentStart, segmentEnd, plane, faceId);
                    if (!arcPointsResult.IsSuccess)
                    {
                        return KernelResult<IReadOnlyList<Point3D>>.Failure(arcPointsResult.Diagnostics);
                    }

                    foreach (var point in arcPointsResult.Value)
                    {
                        AppendUniquePoint(flattened, point);
                    }

                    break;
                case CurveGeometryKind.BSpline3:
                    var splinePointsResult = SamplePlanarBSpline(body, coedge, curve.BSpline3!.Value, segmentStart, segmentEnd);
                    if (!splinePointsResult.IsSuccess)
                    {
                        return KernelResult<IReadOnlyList<Point3D>>.Failure(splinePointsResult.Diagnostics);
                    }

                    foreach (var point in splinePointsResult.Value)
                    {
                        AppendUniquePoint(flattened, point);
                    }

                    break;
                case CurveGeometryKind.Ellipse3:
                    var ellipsePointsResult = SamplePlanarEllipse(body, coedge, curve.Ellipse3!.Value, segmentStart, segmentEnd, plane, faceId);
                    if (!ellipsePointsResult.IsSuccess)
                    {
                        return KernelResult<IReadOnlyList<Point3D>>.Failure(ellipsePointsResult.Diagnostics);
                    }

                    foreach (var point in ellipsePointsResult.Value)
                    {
                        AppendUniquePoint(flattened, point);
                    }

                    break;
                default:
                    var curveKind = curve.UnsupportedKind ?? curve.Kind.ToString();
                    return KernelResult<IReadOnlyList<Point3D>>.Failure([
                        CreateNotImplemented($"Face {faceId.Value} planar curve flattening does not support curve kind '{curveKind}'.", PlanarCurveFlatteningUnsupportedSource)]);
            }
        }

        if (flattened.Count > 1 && PointsAlmostEqual(flattened[0], flattened[^1]))
        {
            flattened.RemoveAt(flattened.Count - 1);
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(flattened);
    }

    private static KernelResult<IReadOnlyList<Point3D>> SampleCircleArc(
        BrepBody body,
        Coedge coedge,
        Circle3Curve circle,
        Point3D start,
        Point3D end,
        PlaneSurface plane,
        FaceId faceId)
    {
        var deltaResult = ResolveArcDelta(body, coedge, circle, start, end, plane, faceId);
        if (!deltaResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<Point3D>>.Failure(deltaResult.Diagnostics);
        }

        var (startAngle, delta) = deltaResult.Value;
        var samples = CurveSampler.SampleCircleArc(circle, startAngle, delta);
        var points = new List<Point3D>(samples.Count - 1);
        for (var i = 1; i < samples.Count; i++)
        {
            points.Add(samples[i]);
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(points);
    }

    private static KernelResult<IReadOnlyList<Point3D>> SamplePlanarBSpline(
        BrepBody body,
        Coedge coedge,
        BSpline3Curve spline,
        Point3D start,
        Point3D end)
    {
        var interval = new ParameterInterval(spline.DomainStart, spline.DomainEnd);
        var reverseSampleOrder = false;
        if (body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var binding) && binding.TrimInterval is ParameterInterval trim)
        {
            interval = trim;
            reverseSampleOrder = coedge.IsReversed;
        }
        else
        {
            reverseSampleOrder = coedge.IsReversed;
        }

        var sampled = CurveSampler.SampleBSpline(spline, interval).ToArray();
        if (reverseSampleOrder)
        {
            Array.Reverse(sampled);
        }

        if (sampled.Length < 2)
        {
            return KernelResult<IReadOnlyList<Point3D>>.Failure([
                CreateInvalidArgument($"Edge {coedge.EdgeId.Value} planar BSpline flattening produced an invalid sample set.", PlanarCurveFlatteningFailedSource)]);
        }

        sampled[0] = start;
        sampled[^1] = end;

        var points = new List<Point3D>(sampled.Length - 1);
        for (var i = 1; i < sampled.Length; i++)
        {
            points.Add(sampled[i]);
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(points);
    }

    private static KernelResult<IReadOnlyList<Point3D>> SamplePlanarEllipse(
        BrepBody body,
        Coedge coedge,
        Ellipse3Curve ellipse,
        Point3D start,
        Point3D end,
        PlaneSurface plane,
        FaceId faceId)
    {
        const double planeContainmentTolerance = 1e-7d;
        var normalAlignment = double.Abs(ellipse.Normal.ToVector().Dot(plane.Normal.ToVector()));
        var centerOffset = double.Abs((ellipse.Center - plane.Origin).Dot(plane.Normal.ToVector()));

        if (normalAlignment < 1d - 1e-7d || centerOffset > planeContainmentTolerance)
        {
            return KernelResult<IReadOnlyList<Point3D>>.Failure([
                CreateNotImplemented($"Face {faceId.Value} edge {coedge.EdgeId.Value} planar ellipse flattening requires the ellipse plane to match the face plane.", PlanarCurveFlatteningUnsupportedSource)]);
        }

        var trimResult = ResolvePlanarEllipseTrim(body, coedge, ellipse, start, end, plane, faceId);
        if (!trimResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<Point3D>>.Failure(trimResult.Diagnostics);
        }

        var trim = trimResult.Value;
        var segmentCount = ComputeEllipseSegmentCount(trim.Start, trim.End);
        var sampled = SampleEllipseCurve(ellipse, trim, segmentCount).ToArray();
        sampled[0] = start;
        sampled[^1] = end;

        var points = new List<Point3D>(sampled.Length - 1);
        for (var i = 1; i < sampled.Length; i++)
        {
            points.Add(sampled[i]);
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(points);
    }

    private static KernelResult<ParameterInterval> ResolvePlanarEllipseTrim(
        BrepBody body,
        Coedge coedge,
        Ellipse3Curve ellipse,
        Point3D start,
        Point3D end,
        PlaneSurface plane,
        FaceId faceId)
    {
        if (body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var binding) && binding.TrimInterval is ParameterInterval trim)
        {
            return KernelResult<ParameterInterval>.Success(coedge.IsReversed
                ? new ParameterInterval(trim.End, trim.Start)
                : trim);
        }

        var startAngle = AngleOnEllipse(ellipse, start);
        var endAngle = AngleOnEllipse(ellipse, end);
        var delta = NormalizeToSignedPi(endAngle - startAngle);
        if (double.Abs(delta) < 1e-9d)
        {
            return KernelResult<ParameterInterval>.Failure([
                CreateInvalidArgument($"Face {faceId.Value} planar ellipse flattening detected a degenerate ellipse trim.", PlanarCurveFlatteningFailedSource)]);
        }

        var orientation = ellipse.Normal.ToVector().Dot(plane.Normal.ToVector());
        if (orientation < 0d)
        {
            delta = -delta;
        }

        return KernelResult<ParameterInterval>.Success(new ParameterInterval(startAngle, startAngle + delta));
    }

    private static KernelResult<(double StartAngle, double Delta)> ResolveArcDelta(
        BrepBody body,
        Coedge coedge,
        Circle3Curve circle,
        Point3D start,
        Point3D end,
        PlaneSurface plane,
        FaceId faceId)
    {
        var startAngle = AngleOnCircle(circle, start);
        var endAngle = AngleOnCircle(circle, end);

        if (body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var binding) && binding.TrimInterval is ParameterInterval trim)
        {
            var trimStart = coedge.IsReversed ? trim.End : trim.Start;
            var trimEnd = coedge.IsReversed ? trim.Start : trim.End;
            var trimDelta = trimEnd - trimStart;
            if (double.IsFinite(trimDelta) && double.Abs(trimDelta) > 1e-9d)
            {
                return KernelResult<(double, double)>.Success((trimStart, trimDelta));
            }
        }

        var delta = NormalizeToSignedPi(endAngle - startAngle);
        if (double.Abs(delta) < 1e-9d)
        {
            return KernelResult<(double, double)>.Failure([
                CreateInvalidArgument($"Face {faceId.Value} planar arc flattening detected a degenerate circle arc.", PlanarCurveFlatteningFailedSource)]);
        }

        var orientation = circle.Normal.ToVector().Dot(plane.Normal.ToVector());
        if (orientation < 0d)
        {
            delta = -delta;
        }

        return KernelResult<(double, double)>.Success((startAngle, delta));
    }

    private static double AngleOnCircle(Circle3Curve circle, Point3D point)
    {
        var offset = point - circle.Center;
        var x = offset.Dot(circle.XAxis.ToVector());
        var y = offset.Dot(circle.YAxis.ToVector());
        return double.Atan2(y, x);
    }

    private static double AngleOnEllipse(Ellipse3Curve ellipse, Point3D point)
    {
        var offset = point - ellipse.Center;
        var x = offset.Dot(ellipse.XAxis.ToVector()) / ellipse.MajorRadius;
        var y = offset.Dot(ellipse.YAxis.ToVector()) / ellipse.MinorRadius;
        return double.Atan2(y, x);
    }

    private static double NormalizeToSignedPi(double angle)
    {
        while (angle <= -double.Pi)
        {
            angle += 2d * double.Pi;
        }

        while (angle > double.Pi)
        {
            angle -= 2d * double.Pi;
        }

        return angle;
    }


    private static double TorusMinorAngleOf(TorusSurface torus, Point3D point)
    {
        var offset = point - torus.Center;
        var x = offset.Dot(torus.XAxis.ToVector());
        var y = offset.Dot(torus.YAxis.ToVector());
        var u = NormalizeToZeroTwoPi(double.Atan2(y, x));

        var majorDirection = (torus.XAxis.ToVector() * double.Cos(u)) + (torus.YAxis.ToVector() * double.Sin(u));
        var radialFromMajorCircle = offset.Dot(majorDirection) - torus.MajorRadius;
        var axial = offset.Dot(torus.Axis.ToVector());
        return NormalizeToZeroTwoPi(double.Atan2(axial, radialFromMajorCircle));
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

    private static void AppendSphereUv(List<double> azimuths, List<double> elevations, SphereSurface sphere, Point3D point)
    {
        var fromCenter = point - sphere.Center;
        var radial = fromCenter.Length;
        if (!double.IsFinite(radial) || radial <= 1e-9d)
        {
            return;
        }

        var normalized = fromCenter / radial;
        var x = normalized.Dot(sphere.XAxis.ToVector());
        var y = normalized.Dot(sphere.YAxis.ToVector());
        var z = normalized.Dot(sphere.Axis.ToVector());
        azimuths.Add(double.Atan2(y, x));
        elevations.Add(System.Math.Asin(System.Math.Clamp(z, -1d, 1d)));
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

    private static void AppendUniquePoint(List<Point3D> points, Point3D point)
    {
        if (points.Count == 0)
        {
            points.Add(point);
            return;
        }

        if (!PointsAlmostEqual(points[^1], point))
        {
            points.Add(point);
        }
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
                    CurveSampler.SampleLine(line, interval),
                    IsClosed: false));

            case CurveGeometryKind.Circle3:
                var circle = curve.Circle3!.Value;
                var startPointResult = EvaluateEdgeEndpoint(body, edgeId, useStart: true);
                if (!startPointResult.IsSuccess)
                {
                    return KernelResult<DisplayEdgePolyline>.Failure(startPointResult.Diagnostics);
                }

                var endPointResult = EvaluateEdgeEndpoint(body, edgeId, useStart: false);
                if (!endPointResult.IsSuccess)
                {
                    return KernelResult<DisplayEdgePolyline>.Failure(endPointResult.Diagnostics);
                }

                if (!CurveSampler.TrySampleTrimmedCircleArc(
                        circle,
                        startPointResult.Value,
                        endPointResult.Value,
                        binding.OrientedEdgeSense,
                        out var points,
                        out var isClosed,
                        out var usedShorterArcFallback))
                {
                    return KernelResult<DisplayEdgePolyline>.Failure([
                        CreateInvalidArgument($"Edge {edgeId.Value} failed to resolve circle trim from edge endpoints.", CircleTrimResolveFailedSource)]);
                }

                if (usedShorterArcFallback)
                {
                    return KernelResult<DisplayEdgePolyline>.Success(
                        new DisplayEdgePolyline(edgeId, points, isClosed),
                        [CreateValidationWarning($"Edge {edgeId.Value} circular trim was ambiguous; using shorter arc.", CircleTrimAmbiguousUsedShorterArcSource)]);
                }

                return KernelResult<DisplayEdgePolyline>.Success(new DisplayEdgePolyline(edgeId, points, isClosed));

            case CurveGeometryKind.BSpline3:
                var spline = curve.BSpline3!.Value;
                return KernelResult<DisplayEdgePolyline>.Success(new DisplayEdgePolyline(
                    edgeId,
                    CurveSampler.SampleBSpline(spline, interval),
                    IsClosed: false));

            case CurveGeometryKind.Ellipse3:
                var ellipse = curve.Ellipse3!.Value;
                var segments = ComputeEllipseSegmentCount(interval.Start, interval.End);
                return KernelResult<DisplayEdgePolyline>.Success(new DisplayEdgePolyline(
                    edgeId,
                    SampleEllipseCurve(ellipse, interval, segments),
                    IsClosed: false));

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
        if (!body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null)
        {
            return KernelResult<Point3D>.Failure([CreateNotImplemented($"Edge {edgeId.Value} is missing bound curve geometry.")]);
        }

        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding))
        {
            return KernelResult<Point3D>.Failure([CreateNotImplemented($"Edge {edgeId.Value} is missing curve binding.")]);
        }

        var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d);
        var parameter = useStart ? interval.Start : interval.End;
        return curve.Kind switch
        {
            CurveGeometryKind.Line3 => KernelResult<Point3D>.Success(curve.Line3!.Value.Evaluate(parameter)),
            CurveGeometryKind.Circle3 => KernelResult<Point3D>.Success(curve.Circle3!.Value.Evaluate(parameter)),
            CurveGeometryKind.BSpline3 => KernelResult<Point3D>.Success(curve.BSpline3!.Value.Evaluate(parameter)),
            CurveGeometryKind.Ellipse3 => KernelResult<Point3D>.Success(curve.Ellipse3!.Value.Evaluate(parameter)),
            _ => KernelResult<Point3D>.Failure([CreateNotImplemented($"Edge {edgeId.Value} endpoint evaluation does not support curve kind '{curve.UnsupportedKind ?? curve.Kind.ToString()}'.", PlanarCurveFlatteningUnsupportedSource)]),
        };
    }

    private static KernelResult<(Point3D Start, Point3D End)> GetEdgeEndpoints(
        BrepBody body,
        EdgeId edgeId,
        bool reversed,
        IReadOnlyDictionary<VertexId, Point3D> vertexPoints)
    {
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

    private static IReadOnlyList<Point3D> SampleEllipseCurve(Ellipse3Curve ellipse, ParameterInterval trim, int segmentCount)
    {
        var points = new List<Point3D>(segmentCount + 1);
        var step = (trim.End - trim.Start) / segmentCount;
        for (var i = 0; i <= segmentCount; i++)
        {
            points.Add(ellipse.Evaluate(trim.Start + (step * i)));
        }

        return points;
    }

    private static int ComputeEllipseSegmentCount(double start, double end)
    {
        const double maxSegmentAngle = double.Pi / 4d;
        var span = double.Abs(end - start);
        var segmentCount = (int)double.Ceiling(span / maxSegmentAngle);
        return System.Math.Max(2, segmentCount);
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

    private static KernelDiagnostic CreateInvalidArgument(string message, string? source = null)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source);

    private static KernelDiagnostic CreateNotImplemented(string message, string? source = null)
        => new(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source);

    private static KernelDiagnostic CreateValidationWarning(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Warning, message, source);
}
