using System.Linq;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Validation;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentPlacementResolver
{
    public static KernelResult<Vector3D> ResolvePlacementTranslation(FirmamentLoweredPrimitive primitive, IReadOnlyDictionary<string, BrepBody> featureBodies)
    {
        if (primitive.Placement is null) return KernelResult<Vector3D>.Success(Vector3D.Zero);

        var anchorResult = ResolveAnchorPoint(primitive.Placement, featureBodies);
        if (!anchorResult.IsSuccess) return KernelResult<Vector3D>.Failure(anchorResult.Diagnostics);
        var baseAnchorPoint = anchorResult.Value;
        if (!string.IsNullOrWhiteSpace(primitive.Placement.AroundAxis))
        {
            var axisResult = ResolveAxisFromSelector(primitive.Placement.AroundAxis!, featureBodies);
            if (!axisResult.IsSuccess) return KernelResult<Vector3D>.Failure(axisResult.Diagnostics);
            var radialOffset = primitive.Placement.RadialOffset ?? 0d;
            var angleDegrees = primitive.Placement.AngleDegrees ?? 0d;
            baseAnchorPoint = ResolveAroundAxisPoint(axisResult.Value, baseAnchorPoint, radialOffset, angleDegrees);
        }

        var o = primitive.Placement.Offset;
        return KernelResult<Vector3D>.Success(
            (baseAnchorPoint - Point3D.Origin)
            + ResolvePrimitivePlacementLocalFrameCorrection(primitive)
            + ResolveOffset(o));
    }

    public static KernelResult<Vector3D> ResolvePlacementTranslation(FirmamentLoweredBoolean booleanOp, IReadOnlyDictionary<string, BrepBody> featureBodies)
    {
        if (booleanOp.Placement is null) return KernelResult<Vector3D>.Success(Vector3D.Zero);
        var anchorResult = ResolveAnchorPoint(booleanOp.Placement, featureBodies);
        if (!anchorResult.IsSuccess) return KernelResult<Vector3D>.Failure(anchorResult.Diagnostics);
        var baseAnchorPoint = anchorResult.Value;
        if (!string.IsNullOrWhiteSpace(booleanOp.Placement.AroundAxis))
        {
            var axisResult = ResolveAxisFromSelector(booleanOp.Placement.AroundAxis!, featureBodies);
            if (!axisResult.IsSuccess) return KernelResult<Vector3D>.Failure(axisResult.Diagnostics);
            var radialOffset = booleanOp.Placement.RadialOffset ?? 0d;
            var angleDegrees = booleanOp.Placement.AngleDegrees ?? 0d;
            baseAnchorPoint = ResolveAroundAxisPoint(axisResult.Value, baseAnchorPoint, radialOffset, angleDegrees);
        }

        var o = booleanOp.Placement.Offset;
        return KernelResult<Vector3D>.Success((baseAnchorPoint - Point3D.Origin) + ResolveOffset(o));
    }

    private static KernelResult<Point3D> ResolveAnchorPoint(FirmamentLoweredPlacement placement, IReadOnlyDictionary<string, BrepBody> featureBodies)
    {
        if (placement.On is not null)
        {
            return ResolveAnchorPoint(placement.On, featureBodies);
        }

        if (!string.IsNullOrWhiteSpace(placement.AroundAxis))
        {
            var axisResult = ResolveAxisFromSelector(placement.AroundAxis!, featureBodies);
            if (!axisResult.IsSuccess) return KernelResult<Point3D>.Failure(axisResult.Diagnostics);
            return KernelResult<Point3D>.Success(axisResult.Value.Origin);
        }

        return KernelResult<Point3D>.Success(Point3D.Origin);
    }

    private static Vector3D ResolvePrimitivePlacementLocalFrameCorrection(FirmamentLoweredPrimitive primitive)
    {
        return primitive.Kind switch
        {
            FirmamentLoweredPrimitiveKind.Sphere => new Vector3D(0d, 0d, ((FirmamentLoweredSphereParameters)primitive.Parameters).Radius),
            FirmamentLoweredPrimitiveKind.Torus => new Vector3D(0d, 0d, ((FirmamentLoweredTorusParameters)primitive.Parameters).MinorRadius),
            _ => Vector3D.Zero
        };
    }

    private static KernelResult<Point3D> ResolveAnchorPoint(FirmamentLoweredPlacementAnchor anchor, IReadOnlyDictionary<string, BrepBody> featureBodies)
    {
        if (anchor is FirmamentLoweredPlacementOriginAnchor) return KernelResult<Point3D>.Success(Point3D.Origin);
        if (anchor is not FirmamentLoweredPlacementSelectorAnchor selectorAnchor) return Failure("Placement anchor extraction failed: unsupported placement anchor shape.");

        var selector = selectorAnchor.Selector;
        var i = selector.IndexOf('.', StringComparison.Ordinal);
        if (i <= 0 || i >= selector.Length - 1) return Failure($"Placement selector '{selector}' is not selector-shaped.");

        var featureId = selector[..i];
        var port = selector[(i + 1)..];
        if (!featureBodies.TryGetValue(featureId, out var body)) return Failure($"Placement selector '{selector}' resolved empty at runtime.");

        var featureKind = InferFeatureKind(body);
        if (featureKind is null || !FirmamentSelectorContracts.TryGetPortContract(featureKind.Value, port, out var contract))
            return Failure($"Placement selector '{selector}' uses unsupported selector result for placement anchor extraction.");

        return contract.ResultKind switch
        {
            FirmamentSelectorResultKind.Face or FirmamentSelectorResultKind.FaceSet => ResolveFaceAnchor(body, port),
            FirmamentSelectorResultKind.EdgeSet => ResolveEdgeAnchor(body, port),
            FirmamentSelectorResultKind.VertexSet => ResolveVertexAnchor(body),
            _ => Failure($"Placement selector '{selector}' uses unsupported selector result for placement anchor extraction.")
        };
    }

    private static KernelResult<FirmamentPlacementAxis> ResolveAxisFromSelector(string selector, IReadOnlyDictionary<string, BrepBody> featureBodies)
    {
        var i = selector.IndexOf('.', StringComparison.Ordinal);
        if (i <= 0 || i >= selector.Length - 1) return FailureAxis($"Placement selector '{selector}' is not selector-shaped.");

        var featureId = selector[..i];
        var port = selector[(i + 1)..];
        if (!featureBodies.TryGetValue(featureId, out var body)) return FailureAxis($"Placement selector '{selector}' resolved empty at runtime.");

        var candidateFaces = body.Topology.Faces
            .Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var _) && true)
            .ToList();
        if (candidateFaces.Count == 0)
        {
            return FailureAxis($"Placement selector '{selector}' resolved empty face set at runtime.");
        }

        foreach (var face in candidateFaces)
        {
            body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
            if (surface is null)
            {
                continue;
            }

            if (port == "side_face" && surface.Kind == SurfaceGeometryKind.Cylinder)
            {
                var c = surface.Cylinder!.Value;
                return KernelResult<FirmamentPlacementAxis>.Success(new FirmamentPlacementAxis(c.Origin, c.Axis.ToVector()));
            }

            if (port == "side_face" && surface.Kind == SurfaceGeometryKind.Cone)
            {
                var c = surface.Cone!.Value;
                return KernelResult<FirmamentPlacementAxis>.Success(new FirmamentPlacementAxis(c.Apex, c.Axis.ToVector()));
            }
        }

        return FailureAxis($"Placement selector '{selector}' cannot resolve an axis for P1 around-axis placement; expected cylindrical/conical side_face selector.");
    }

    private static Point3D ResolveAroundAxisPoint(FirmamentPlacementAxis axis, Point3D basePoint, double radialOffset, double angleDegrees)
    {
        if (!axis.Direction.TryNormalize(out var axisDir))
        {
            return basePoint;
        }
        var toBase = basePoint - axis.Origin;
        var projectedLength = toBase.Dot(axisDir);
        var onAxis = axis.Origin + (axisDir * projectedLength);

        var reference = new Vector3D(1d, 0d, 0d);
        if (!reference.TryNormalize(out var normalizedReference))
        {
            normalizedReference = new Vector3D(1d, 0d, 0d);
        }

        if (Math.Abs(normalizedReference.Dot(axisDir)) > 0.99d)
        {
            reference = new Vector3D(0d, 1d, 0d);
        }

        var radialUraw = reference - axisDir * reference.Dot(axisDir);
        if (!radialUraw.TryNormalize(out var radialU))
        {
            radialU = new Vector3D(1d, 0d, 0d);
        }

        var radialVraw = axisDir.Cross(radialU);
        if (!radialVraw.TryNormalize(out var radialV))
        {
            radialV = new Vector3D(0d, 1d, 0d);
        }
        var radians = angleDegrees * (Math.PI / 180d);
        var radial = (radialU * Math.Cos(radians) + radialV * Math.Sin(radians)) * radialOffset;
        return onAxis + radial;
    }

    private static Vector3D ResolveOffset(IReadOnlyList<double> offset)
    {
        if (offset.Count == 3)
        {
            return new Vector3D(offset[0], offset[1], offset[2]);
        }

        return Vector3D.Zero;
    }

    private static KernelResult<Point3D> ResolveFaceAnchor(BrepBody body, string port)
    {
        var facePoints = body.Topology.Faces
            .Select(f => new { Face = f, Surface = body.TryGetFaceSurfaceGeometry(f.Id, out var surface) ? surface : null, Point = GetFaceRepresentativePoint(body, f.Id) })
            .Where(x => x.Surface is not null && x.Point is not null)
            .Select(x => (x.Face, Surface: x.Surface!, Point: x.Point!.Value))
            .ToList();
        if (facePoints.Count == 0) return Failure("Placement selector resolved empty face set at runtime.");

        IEnumerable<Point3D> selected = port switch
        {
            "top_face" => facePoints
                .Where(x => x.Surface.Kind == SurfaceGeometryKind.Plane && x.Surface.Plane!.Value.Normal.ToVector().Z > 0.5d)
                .Select(x => x.Point),
            "bottom_face" => facePoints
                .Where(x => x.Surface.Kind == SurfaceGeometryKind.Plane && x.Surface.Plane!.Value.Normal.ToVector().Z < -0.5d)
                .Select(x => x.Point),
            "side_face" => facePoints
                .Where(x => x.Surface.Kind is SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone)
                .Select(x => x.Point),
            "side_faces" => facePoints
                .Where(x => x.Surface.Kind == SurfaceGeometryKind.Plane && Math.Abs(x.Surface.Plane!.Value.Normal.ToVector().Z) <= 0.5d)
                .Select(x => x.Point),
            "surface" => facePoints
                .Where(x => x.Surface.Kind is SurfaceGeometryKind.Sphere or SurfaceGeometryKind.Torus)
                .Select(x => x.Point),
            _ => facePoints.Select(x => x.Point)
        };

        var pts = selected.ToArray();
        if (pts.Length == 0) return Failure("Placement selector resolved empty face set at runtime.");
        return KernelResult<Point3D>.Success(ComputeCentroid(pts));
    }

    private static Point3D? GetFaceRepresentativePoint(BrepBody body, FaceId faceId)
    {
        if (!body.TryGetFaceSurfaceGeometry(faceId, out var s) || s is null) return null;
        return s.Kind switch
        {
            SurfaceGeometryKind.Plane => s.Plane!.Value.Origin,
            SurfaceGeometryKind.Cylinder => s.Cylinder!.Value.Origin,
            SurfaceGeometryKind.Cone => GetConicalFaceRepresentativePoint(body, faceId, s.Cone!.Value),
            SurfaceGeometryKind.Torus => s.Torus!.Value.Center,
            SurfaceGeometryKind.Sphere => s.Sphere!.Value.Center,
            _ => null
        };
    }

    private static Point3D? GetConicalFaceRepresentativePoint(BrepBody body, FaceId faceId, ConeSurface cone)
    {
        var points = new List<Point3D>();
        foreach (var edgeId in body.GetEdges(faceId))
        {
            var (startVertexId, endVertexId) = body.GetEdgeVertices(edgeId);
            if (TryGetVertexPoint(body, startVertexId, out var startPoint))
            {
                points.Add(startPoint);
            }

            if (TryGetVertexPoint(body, endVertexId, out var endPoint))
            {
                points.Add(endPoint);
            }
        }

        if (points.Count > 0)
        {
            return ComputeCentroid(points);
        }

        return cone.Evaluate(0d, 1d);
    }

    private static KernelResult<Point3D> ResolveEdgeAnchor(BrepBody body, string _)
    {
        var points = new List<Point3D>();
        foreach (var edge in body.Topology.Edges)
        {
            if (TryGetVertexPoint(body, edge.StartVertexId, out var p1)) points.Add(p1);
            if (TryGetVertexPoint(body, edge.EndVertexId, out var p2)) points.Add(p2);
        }

        return points.Count == 0 ? Failure("Placement selector resolved empty edge set at runtime.") : KernelResult<Point3D>.Success(ComputeCentroid(points));
    }

    private static KernelResult<Point3D> ResolveVertexAnchor(BrepBody body)
    {
        var points = new List<Point3D>();
        foreach (var vertex in body.Topology.Vertices)
            if (TryGetVertexPoint(body, vertex.Id, out var point)) points.Add(point);

        return points.Count == 0 ? Failure("Placement selector resolved empty vertex set at runtime.") : KernelResult<Point3D>.Success(ComputeCentroid(points));
    }

    internal static bool TryGetVertexPoint(BrepBody body, VertexId vertexId, out Point3D point)
    {
        if (body.TryGetVertexPoint(vertexId, out point)) return true;

        foreach (var edge in body.Topology.Edges)
        {
            if (edge.StartVertexId != vertexId && edge.EndVertexId != vertexId) continue;
            if (!body.TryGetEdgeCurveGeometry(edge.Id, out var curve) || curve is null) continue;

            if (curve.Kind == CurveGeometryKind.Line3)
            {
                var line = curve.Line3!.Value;
                point = edge.StartVertexId == vertexId ? line.Evaluate(0d) : line.Evaluate(1d);
                return true;
            }

            if (curve.Kind == CurveGeometryKind.Circle3)
            {
                point = curve.Circle3!.Value.Evaluate(0d);
                return true;
            }
        }

        point = Point3D.Origin;
        return false;
    }

    private static Point3D ComputeCentroid(IReadOnlyList<Point3D> pts)
    {
        var sx = 0d; var sy = 0d; var sz = 0d;
        foreach (var p in pts) { sx += p.X; sy += p.Y; sz += p.Z; }
        return new Point3D(sx / pts.Count, sy / pts.Count, sz / pts.Count);
    }

    private static FirmamentKnownOpKind? InferFeatureKind(BrepBody body)
    {
        var f = body.Topology.Faces.Count();
        var e = body.Topology.Edges.Count();
        if (f == 6 && e == 12) return FirmamentKnownOpKind.Box;
        if (f is 2 or 3)
        {
            foreach (var face in body.Topology.Faces)
            {
                if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null)
                {
                    continue;
                }

                if (surface.Kind == SurfaceGeometryKind.Cone)
                {
                    return FirmamentKnownOpKind.Cone;
                }

                if (surface.Kind == SurfaceGeometryKind.Cylinder)
                {
                    return FirmamentKnownOpKind.Cylinder;
                }
            }
        }

        if (f == 1 && e == 2) return FirmamentKnownOpKind.Torus;
        if (f == 1) return FirmamentKnownOpKind.Sphere;
        return FirmamentKnownOpKind.Add;
    }

    private static KernelResult<Point3D> Failure(string message) =>
        KernelResult<Point3D>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"[{FirmamentDiagnosticCodes.ValidationTargetSelectorResolvedEmpty.Value}] {message}", FirmamentDiagnosticConventions.Source)]);

    private static KernelResult<FirmamentPlacementAxis> FailureAxis(string message) =>
        KernelResult<FirmamentPlacementAxis>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, $"[{FirmamentDiagnosticCodes.ValidationTargetSelectorResolvedEmpty.Value}] {message}", FirmamentDiagnosticConventions.Source)]);

    private readonly record struct FirmamentPlacementAxis(Point3D Origin, Vector3D Direction);
}
