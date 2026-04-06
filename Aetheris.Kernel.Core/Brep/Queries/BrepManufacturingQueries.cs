using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

/// <summary>
/// Bounded schema-free geometric query layer for manufacturability-oriented facts.
///
/// Supported v1 subset:
/// - First-hit ray query on bodies supported by <see cref="BrepSpatialQueries.Raycast"/> (primitive bodies only).
/// - Local thickness probe for planar faces, measuring along inward face normal only.
/// - Internal concave manufacturing edge facts for axis-aligned occupied-cell compositions
///   produced by supported safe boolean additive-root flows.
///
/// Unsupported requests return explicit diagnostics instead of approximate results.
/// </summary>
public static class BrepManufacturingQueries
{
    public static KernelResult<RayHit?> FirstHit(
        BrepBody body,
        Ray3D ray,
        RayQueryOptions? options = null,
        ToleranceContext? tolerance = null)
    {
        var cast = BrepSpatialQueries.Raycast(body, ray, options, tolerance);
        if (!cast.IsSuccess)
        {
            return KernelResult<RayHit?>.Failure(cast.Diagnostics);
        }

        return KernelResult<RayHit?>.Success(cast.Value.FirstOrDefault(), cast.Diagnostics);
    }

    public static KernelResult<LocalThicknessMeasurement> ProbeLocalThickness(
        BrepBody body,
        LocalThicknessProbe probe,
        ToleranceContext? tolerance = null)
    {
        var context = tolerance ?? ToleranceContext.Default;

        if (!body.Topology.TryGetFace(probe.FaceId, out var face) || face is null)
        {
            return KernelResult<LocalThicknessMeasurement>.Failure([
                NotImplemented($"Local thickness probe face '{probe.FaceId}' was not found on the body.")
            ]);
        }

        if (!body.TryGetFaceSurfaceGeometry(probe.FaceId, out var surface) || surface is null || surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is null)
        {
            return KernelResult<LocalThicknessMeasurement>.Failure([
                NotImplemented("Local thickness v1 supports planar faces only.")
            ]);
        }

        if (probe.DirectionMode != LocalThicknessDirectionMode.FaceNormalInward)
        {
            return KernelResult<LocalThicknessMeasurement>.Failure([
                NotImplemented("Local thickness v1 supports FaceNormalInward direction mode only.")
            ]);
        }

        var plane = surface.Plane.Value;
        var inward = Direction3D.Create(-plane.Normal.ToVector());
        var epsilon = context.Linear * 10d;
        var launch = probe.SamplePoint + (inward.ToVector() * epsilon);
        var ray = new Ray3D(launch, inward);
        var hits = BrepSpatialQueries.Raycast(body, ray, RayQueryOptions.Default with { IncludeBackfaces = true }, context);
        if (!hits.IsSuccess)
        {
            return KernelResult<LocalThicknessMeasurement>.Failure(hits.Diagnostics);
        }

        var oppositeHit = hits.Value.FirstOrDefault(hit => hit.FaceId != probe.FaceId && hit.T > context.Linear);
        if (oppositeHit.FaceId is null)
        {
            return KernelResult<LocalThicknessMeasurement>.Failure([
                NotImplemented("Local thickness v1 could not find an opposite boundary hit for this planar-face inward probe.")
            ]);
        }

        var thickness = oppositeHit.T + epsilon;
        return KernelResult<LocalThicknessMeasurement>.Success(new LocalThicknessMeasurement(
            probe.FaceId,
            oppositeHit.FaceId.Value,
            probe.SamplePoint,
            inward,
            thickness,
            oppositeHit.Point));
    }

    public static KernelResult<IReadOnlyList<ConcaveManufacturingEdgeFact>> EnumerateInternalConcaveEdges(
        BrepBody body,
        ToleranceContext? tolerance = null)
    {
        var context = tolerance ?? ToleranceContext.Default;
        var composition = body.SafeBooleanComposition;
        if (composition?.OccupiedCells is null || composition.OccupiedCells.Count == 0)
        {
            return KernelResult<IReadOnlyList<ConcaveManufacturingEdgeFact>>.Failure([
                NotImplemented("Concave manufacturing-edge query v1 supports occupied-cell additive-root compositions only.")
            ]);
        }

        var faces = body.Bindings.FaceBindings
            .Select(binding => (binding.FaceId, Surface: body.Geometry.GetSurface(binding.SurfaceGeometryId)))
            .Where(entry => entry.Surface.Kind == SurfaceGeometryKind.Plane && entry.Surface.Plane is not null)
            .Select(entry => (entry.FaceId, Plane: entry.Surface.Plane!.Value))
            .ToArray();

        var xNegative = faces.Where(f => IsAxisNormal(f.Plane.Normal, AxisKind.X, false, context)).ToArray();
        var xPositive = faces.Where(f => IsAxisNormal(f.Plane.Normal, AxisKind.X, true, context)).ToArray();
        var yNegative = faces.Where(f => IsAxisNormal(f.Plane.Normal, AxisKind.Y, false, context)).ToArray();
        var yPositive = faces.Where(f => IsAxisNormal(f.Plane.Normal, AxisKind.Y, true, context)).ToArray();

        var cornerCandidates = EnumerateInternalCorners(composition.OccupiedCells, context.Linear);
        var edgeFacts = new List<ConcaveManufacturingEdgeFact>();

        foreach (var corner in cornerCandidates)
        {
            var xFace = ResolveCornerFace(corner.X, corner.EmptyToPositiveX ? xPositive : xNegative, AxisKind.X, context);
            var yFace = ResolveCornerFace(corner.Y, corner.EmptyToPositiveY ? yPositive : yNegative, AxisKind.Y, context);
            if (!xFace.HasValue || !yFace.HasValue)
            {
                continue;
            }

            var sharedEdgeId = TryResolveSharedLinearEdge(body, xFace.Value.FaceId, yFace.Value.FaceId);
            if (!sharedEdgeId.HasValue)
            {
                continue;
            }

            edgeFacts.Add(new ConcaveManufacturingEdgeFact(
                sharedEdgeId.Value,
                xFace.Value.FaceId,
                yFace.Value.FaceId,
                ConcaveManufacturingEdgeGeometryClass.PlanarPlanarLinearSharp,
                RequiresFiniteToolRadius: true,
                MinimumToolRadiusLowerBound: 0d));
        }

        return KernelResult<IReadOnlyList<ConcaveManufacturingEdgeFact>>.Success(edgeFacts
            .DistinctBy(fact => fact.EdgeId)
            .OrderBy(fact => fact.EdgeId.Value)
            .ToArray());
    }

    private static EdgeId? TryResolveSharedLinearEdge(BrepBody body, FaceId firstFaceId, FaceId secondFaceId)
    {
        var firstEdges = body.GetEdges(firstFaceId);
        var secondEdges = new HashSet<EdgeId>(body.GetEdges(secondFaceId));

        foreach (var edgeId in firstEdges)
        {
            if (!secondEdges.Contains(edgeId))
            {
                continue;
            }

            if (!body.TryGetEdgeCurve(edgeId, out var curve) || curve is null || curve.Kind != CurveGeometryKind.Line3)
            {
                continue;
            }

            return edgeId;
        }

        return null;
    }

    private static (FaceId FaceId, PlaneSurface Plane)? ResolveCornerFace(
        double coordinate,
        IReadOnlyList<(FaceId FaceId, PlaneSurface Plane)> faces,
        AxisKind axis,
        ToleranceContext tolerance)
    {
        foreach (var face in faces)
        {
            var value = axis == AxisKind.X ? face.Plane.Origin.X : face.Plane.Origin.Y;
            if (ToleranceMath.AlmostEqual(value, coordinate, tolerance))
            {
                return face;
            }
        }

        return null;
    }

    private static bool IsAxisNormal(Direction3D direction, AxisKind axis, bool positive, ToleranceContext tolerance)
    {
        var expected = axis switch
        {
            AxisKind.X => positive ? new Vector3D(1d, 0d, 0d) : new Vector3D(-1d, 0d, 0d),
            AxisKind.Y => positive ? new Vector3D(0d, 1d, 0d) : new Vector3D(0d, -1d, 0d),
            _ => Vector3D.Zero,
        };

        var vector = direction.ToVector();
        return ToleranceMath.AlmostEqual(vector.X, expected.X, tolerance)
            && ToleranceMath.AlmostEqual(vector.Y, expected.Y, tolerance)
            && ToleranceMath.AlmostEqual(vector.Z, expected.Z, tolerance);
    }

    private static IReadOnlyList<InternalCornerCandidate> EnumerateInternalCorners(
        IReadOnlyList<Boolean.AxisAlignedBoxExtents> cells,
        double tolerance)
    {
        var xCoords = cells.SelectMany(cell => new[] { cell.MinX, cell.MaxX }).Distinct().OrderBy(v => v).ToArray();
        var yCoords = cells.SelectMany(cell => new[] { cell.MinY, cell.MaxY }).Distinct().OrderBy(v => v).ToArray();

        if (xCoords.Length < 3 || yCoords.Length < 3)
        {
            return [];
        }

        var occupied = new bool[xCoords.Length - 1, yCoords.Length - 1];
        for (var xi = 0; xi < xCoords.Length - 1; xi++)
        {
            for (var yi = 0; yi < yCoords.Length - 1; yi++)
            {
                var cx = 0.5d * (xCoords[xi] + xCoords[xi + 1]);
                var cy = 0.5d * (yCoords[yi] + yCoords[yi + 1]);
                occupied[xi, yi] = cells.Any(cell =>
                    cx > cell.MinX + tolerance
                    && cx < cell.MaxX - tolerance
                    && cy > cell.MinY + tolerance
                    && cy < cell.MaxY - tolerance);
            }
        }

        var result = new List<InternalCornerCandidate>();
        for (var xi = 1; xi < xCoords.Length - 1; xi++)
        {
            for (var yi = 1; yi < yCoords.Length - 1; yi++)
            {
                var sw = occupied[xi - 1, yi - 1];
                var se = occupied[xi, yi - 1];
                var nw = occupied[xi - 1, yi];
                var ne = occupied[xi, yi];
                var count = (sw ? 1 : 0) + (se ? 1 : 0) + (nw ? 1 : 0) + (ne ? 1 : 0);
                if (count != 3)
                {
                    continue;
                }

                // Empty quadrant direction decides which pair of boundary normals meet at the re-entrant corner.
                // Coordinate points to the corner line x=xCoords[xi], y=yCoords[yi].
                var emptyToPositiveX = !se && !ne;
                var emptyToPositiveY = !nw && !ne;

                if (!sw)
                {
                    emptyToPositiveX = false;
                    emptyToPositiveY = false;
                }
                else if (!se)
                {
                    emptyToPositiveX = true;
                    emptyToPositiveY = false;
                }
                else if (!nw)
                {
                    emptyToPositiveX = false;
                    emptyToPositiveY = true;
                }
                else if (!ne)
                {
                    emptyToPositiveX = true;
                    emptyToPositiveY = true;
                }

                result.Add(new InternalCornerCandidate(xCoords[xi], yCoords[yi], emptyToPositiveX, emptyToPositiveY));
            }
        }

        return result;
    }

    private static KernelDiagnostic NotImplemented(string message)
        => new(
            KernelDiagnosticCode.NotImplemented,
            KernelDiagnosticSeverity.Error,
            message,
            Source: nameof(BrepManufacturingQueries));

    private enum AxisKind
    {
        X,
        Y,
    }

    private readonly record struct InternalCornerCandidate(double X, double Y, bool EmptyToPositiveX, bool EmptyToPositiveY);
}

public enum LocalThicknessDirectionMode
{
    FaceNormalInward,
}

public readonly record struct LocalThicknessProbe(
    FaceId FaceId,
    Point3D SamplePoint,
    LocalThicknessDirectionMode DirectionMode = LocalThicknessDirectionMode.FaceNormalInward);

public readonly record struct LocalThicknessMeasurement(
    FaceId SourceFaceId,
    FaceId OppositeFaceId,
    Point3D ProbePoint,
    Direction3D ProbeDirection,
    double Thickness,
    Point3D OppositePoint);

public enum ConcaveManufacturingEdgeGeometryClass
{
    PlanarPlanarLinearSharp,
}

public readonly record struct ConcaveManufacturingEdgeFact(
    EdgeId EdgeId,
    FaceId FirstFaceId,
    FaceId SecondFaceId,
    ConcaveManufacturingEdgeGeometryClass GeometryClass,
    bool RequiresFiniteToolRadius,
    double MinimumToolRadiusLowerBound);
