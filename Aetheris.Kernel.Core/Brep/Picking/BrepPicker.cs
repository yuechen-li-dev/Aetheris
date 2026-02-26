using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Picking;

public static class BrepPicker
{
    public static KernelResult<IReadOnlyList<PickHit>> Pick(
        BrepBody body,
        Ray3D ray,
        DisplayTessellationOptions? tessellationOptions = null,
        PickQueryOptions? pickOptions = null,
        ToleranceContext? tolerance = null)
    {
        if (body is null)
        {
            return KernelResult<IReadOnlyList<PickHit>>.Failure([CreateInvalidArgument("body must be provided.")]);
        }

        var tessellationResult = BrepDisplayTessellator.Tessellate(body, tessellationOptions);
        if (!tessellationResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<PickHit>>.Failure(tessellationResult.Diagnostics);
        }

        return Pick(body, tessellationResult.Value, ray, pickOptions, tolerance);
    }

    public static KernelResult<IReadOnlyList<PickHit>> Pick(
        BrepBody body,
        DisplayTessellationResult tessellation,
        Ray3D ray,
        PickQueryOptions? options = null,
        ToleranceContext? tolerance = null)
    {
        if (body is null)
        {
            return KernelResult<IReadOnlyList<PickHit>>.Failure([CreateInvalidArgument("body must be provided.")]);
        }

        if (tessellation is null)
        {
            return KernelResult<IReadOnlyList<PickHit>>.Failure([CreateInvalidArgument("tessellation must be provided.")]);
        }

        if (tessellation.FacePatches.Count == 0 && tessellation.EdgePolylines.Count == 0)
        {
            return KernelResult<IReadOnlyList<PickHit>>.Failure([CreateInvalidArgument("tessellation must contain at least one face patch or edge polyline.")]);
        }

        var context = tolerance ?? ToleranceContext.Default;
        var effectiveOptions = options ?? PickQueryOptions.Default;
        var optionDiagnostics = effectiveOptions.Validate();
        if (optionDiagnostics.Count > 0)
        {
            return KernelResult<IReadOnlyList<PickHit>>.Failure(optionDiagnostics);
        }

        var bodyId = body.Topology.Bodies.OrderBy(b => b.Id.Value).Select(b => (BodyId?)b.Id).FirstOrDefault();
        var hits = new List<PickHit>();

        hits.AddRange(CollectFaceHits(tessellation.FacePatches, ray, effectiveOptions, context, bodyId));
        hits.AddRange(CollectEdgeHits(tessellation.EdgePolylines, ray, effectiveOptions, context, bodyId));

        var ordered = SortHits(hits, effectiveOptions.SortTieTolerance);

        if (effectiveOptions.NearestOnly)
        {
            return KernelResult<IReadOnlyList<PickHit>>.Success(ordered.Take(1).ToList());
        }

        return KernelResult<IReadOnlyList<PickHit>>.Success(ordered);
    }

    private static IEnumerable<PickHit> CollectFaceHits(
        IReadOnlyList<DisplayFaceMeshPatch> patches,
        Ray3D ray,
        PickQueryOptions options,
        ToleranceContext tolerance,
        BodyId? bodyId)
    {
        var results = new List<PickHit>();

        for (var patchIndex = 0; patchIndex < patches.Count; patchIndex++)
        {
            var patch = patches[patchIndex];
            if (patch.Positions.Count == 0 || patch.TriangleIndices.Count == 0)
            {
                continue;
            }

            PickHit? bestPatchHit = null;

            for (var triangleIndex = 0; triangleIndex + 2 < patch.TriangleIndices.Count; triangleIndex += 3)
            {
                var i0 = patch.TriangleIndices[triangleIndex];
                var i1 = patch.TriangleIndices[triangleIndex + 1];
                var i2 = patch.TriangleIndices[triangleIndex + 2];

                if (!TryGetTriangle(patch, i0, i1, i2, out var p0, out var p1, out var p2))
                {
                    continue;
                }

                if (!TryIntersectTriangle(ray, p0, p1, p2, tolerance, out var t, out var u, out var v))
                {
                    continue;
                }

                var clampedT = ToleranceMath.ClampToZero(t, tolerance.Linear);
                if (options.MaxDistance.HasValue && clampedT > options.MaxDistance.Value + tolerance.Linear)
                {
                    continue;
                }

                var hitPoint = ray.PointAt(clampedT);
                var normal = TryInterpolateNormal(patch, i0, i1, i2, u, v) ?? TryCreateTriangleNormal(p0, p1, p2);
                if (!options.IncludeBackfaces && normal.HasValue)
                {
                    var facing = normal.Value.ToVector().Dot(ray.Direction.ToVector());
                    if (facing > tolerance.Linear)
                    {
                        continue;
                    }
                }

                var hit = new PickHit(
                    clampedT,
                    hitPoint,
                    normal,
                    SelectionEntityKind.Face,
                    patch.FaceId,
                    EdgeId: null,
                    bodyId,
                    SourcePatchIndex: patchIndex,
                    SourcePrimitiveIndex: triangleIndex / 3);

                if (bestPatchHit is null || IsBetterCandidate(hit, bestPatchHit, options.SortTieTolerance))
                {
                    bestPatchHit = hit;
                }
            }

            if (bestPatchHit is not null)
            {
                results.Add(bestPatchHit);
            }
        }

        return results;
    }

    private static IEnumerable<PickHit> CollectEdgeHits(
        IReadOnlyList<DisplayEdgePolyline> polylines,
        Ray3D ray,
        PickQueryOptions options,
        ToleranceContext tolerance,
        BodyId? bodyId)
    {
        var results = new List<PickHit>();

        for (var polylineIndex = 0; polylineIndex < polylines.Count; polylineIndex++)
        {
            var polyline = polylines[polylineIndex];
            if (polyline.Points.Count < 2)
            {
                continue;
            }

            PickHit? bestEdgeHit = null;
            for (var segmentIndex = 0; segmentIndex < polyline.Points.Count - 1; segmentIndex++)
            {
                var start = polyline.Points[segmentIndex];
                var end = polyline.Points[segmentIndex + 1];

                if (!TryIntersectRayWithSegment(ray, start, end, tolerance, out var t, out var hitPoint, out var distance))
                {
                    continue;
                }

                if (distance > options.EdgeTolerance + tolerance.Linear)
                {
                    continue;
                }

                if (options.MaxDistance.HasValue && t > options.MaxDistance.Value + tolerance.Linear)
                {
                    continue;
                }

                var hit = new PickHit(
                    t,
                    hitPoint,
                    Normal: null,
                    SelectionEntityKind.Edge,
                    FaceId: null,
                    polyline.EdgeId,
                    bodyId,
                    SourcePatchIndex: polylineIndex,
                    SourcePrimitiveIndex: segmentIndex);

                if (bestEdgeHit is null || IsBetterCandidate(hit, bestEdgeHit, options.SortTieTolerance))
                {
                    bestEdgeHit = hit;
                }
            }

            if (bestEdgeHit is not null)
            {
                results.Add(bestEdgeHit);
            }
        }

        return results;
    }

    private static bool TryIntersectTriangle(
        Ray3D ray,
        Point3D p0,
        Point3D p1,
        Point3D p2,
        ToleranceContext tolerance,
        out double t,
        out double u,
        out double v)
    {
        t = 0d;
        u = 0d;
        v = 0d;

        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        var pvec = ray.Direction.ToVector().Cross(edge2);
        var determinant = edge1.Dot(pvec);

        if (ToleranceMath.AlmostZero(determinant, tolerance))
        {
            return false;
        }

        var invDet = 1d / determinant;
        var tvec = ray.Origin - p0;
        u = tvec.Dot(pvec) * invDet;
        if (u < -tolerance.Linear || u > 1d + tolerance.Linear)
        {
            return false;
        }

        var qvec = tvec.Cross(edge1);
        v = ray.Direction.ToVector().Dot(qvec) * invDet;
        if (v < -tolerance.Linear || u + v > 1d + tolerance.Linear)
        {
            return false;
        }

        t = edge2.Dot(qvec) * invDet;
        return t >= -tolerance.Linear;
    }

    private static Direction3D? TryInterpolateNormal(DisplayFaceMeshPatch patch, int i0, int i1, int i2, double u, double v)
    {
        if (!TryGetNormal(patch, i0, out var n0)
            || !TryGetNormal(patch, i1, out var n1)
            || !TryGetNormal(patch, i2, out var n2))
        {
            return null;
        }

        var w = 1d - u - v;
        var normal = (n0 * w) + (n1 * u) + (n2 * v);
        return Direction3D.TryCreate(normal, out var direction) ? direction : null;
    }

    private static Direction3D? TryCreateTriangleNormal(Point3D p0, Point3D p1, Point3D p2)
    {
        var normal = (p1 - p0).Cross(p2 - p0);
        return Direction3D.TryCreate(normal, out var direction) ? direction : null;
    }

    private static bool TryIntersectRayWithSegment(
        Ray3D ray,
        Point3D segmentStart,
        Point3D segmentEnd,
        ToleranceContext tolerance,
        out double rayT,
        out Point3D rayPoint,
        out double distance)
    {
        rayT = 0d;
        rayPoint = Point3D.Origin;
        distance = 0d;

        var d = ray.Direction.ToVector();
        var v = segmentEnd - segmentStart;
        var w0 = ray.Origin - segmentStart;

        var a = d.Dot(d);
        var b = d.Dot(v);
        var c = v.Dot(v);
        var d0 = d.Dot(w0);
        var e = v.Dot(w0);

        if (ToleranceMath.AlmostZero(c, tolerance))
        {
            return false;
        }

        var denominator = (a * c) - (b * b);
        double u;
        double s;

        if (ToleranceMath.AlmostZero(denominator, tolerance))
        {
            u = System.Math.Clamp(e / c, 0d, 1d);
            s = 0d;
        }
        else
        {
            s = ((b * e) - (c * d0)) / denominator;
            u = ((a * e) - (b * d0)) / denominator;

            if (u < 0d || u > 1d)
            {
                u = System.Math.Clamp(u, 0d, 1d);
                s = -(d0 + (b * u)) / a;
            }
        }

        if (s < -tolerance.Linear)
        {
            return false;
        }

        rayT = ToleranceMath.ClampToZero(s, tolerance.Linear);
        var segmentPoint = segmentStart + (v * u);
        rayPoint = ray.PointAt(rayT);
        distance = (rayPoint - segmentPoint).Length;
        return true;
    }

    private static bool TryGetTriangle(DisplayFaceMeshPatch patch, int i0, int i1, int i2, out Point3D p0, out Point3D p1, out Point3D p2)
    {
        p0 = Point3D.Origin;
        p1 = Point3D.Origin;
        p2 = Point3D.Origin;

        if (i0 < 0 || i0 >= patch.Positions.Count
            || i1 < 0 || i1 >= patch.Positions.Count
            || i2 < 0 || i2 >= patch.Positions.Count)
        {
            return false;
        }

        p0 = patch.Positions[i0];
        p1 = patch.Positions[i1];
        p2 = patch.Positions[i2];
        return true;
    }

    private static bool TryGetNormal(DisplayFaceMeshPatch patch, int index, out Vector3D normal)
    {
        normal = Vector3D.Zero;
        if (index < 0 || index >= patch.Normals.Count)
        {
            return false;
        }

        normal = patch.Normals[index];
        return true;
    }


    private static bool IsBetterCandidate(PickHit candidate, PickHit current, double tieTolerance)
    {
        if (candidate.T < current.T - tieTolerance)
        {
            return true;
        }

        if (System.Math.Abs(candidate.T - current.T) > tieTolerance)
        {
            return false;
        }

        if (EntityRank(candidate.EntityKind) != EntityRank(current.EntityKind))
        {
            return EntityRank(candidate.EntityKind) < EntityRank(current.EntityKind);
        }

        var candidateFace = candidate.FaceId?.Value ?? int.MaxValue;
        var currentFace = current.FaceId?.Value ?? int.MaxValue;
        if (candidateFace != currentFace)
        {
            return candidateFace < currentFace;
        }

        var candidateEdge = candidate.EdgeId?.Value ?? int.MaxValue;
        var currentEdge = current.EdgeId?.Value ?? int.MaxValue;
        if (candidateEdge != currentEdge)
        {
            return candidateEdge < currentEdge;
        }

        return (candidate.SourcePrimitiveIndex ?? int.MaxValue) < (current.SourcePrimitiveIndex ?? int.MaxValue);
    }

    private static List<PickHit> SortHits(IEnumerable<PickHit> hits, double tieTolerance)
    {
        var byT = hits
            .OrderBy(hit => hit.T)
            .ThenBy(hit => hit.FaceId?.Value ?? int.MaxValue)
            .ThenBy(hit => hit.EdgeId?.Value ?? int.MaxValue)
            .ThenBy(hit => hit.SourcePrimitiveIndex ?? int.MaxValue)
            .ToList();

        if (byT.Count <= 1 || tieTolerance <= 0d)
        {
            return byT;
        }

        var result = new List<PickHit>(byT.Count);
        var index = 0;
        while (index < byT.Count)
        {
            var groupEndExclusive = index + 1;
            var groupStartT = byT[index].T;
            while (groupEndExclusive < byT.Count
                && System.Math.Abs(byT[groupEndExclusive].T - groupStartT) <= tieTolerance)
            {
                groupEndExclusive++;
            }

            if (groupEndExclusive - index == 1)
            {
                result.Add(byT[index]);
                index = groupEndExclusive;
                continue;
            }

            var tieGroup = byT
                .GetRange(index, groupEndExclusive - index)
                .OrderBy(hit => EntityRank(hit.EntityKind))
                .ThenBy(hit => hit.FaceId?.Value ?? int.MaxValue)
                .ThenBy(hit => hit.EdgeId?.Value ?? int.MaxValue)
                .ThenBy(hit => hit.SourcePrimitiveIndex ?? int.MaxValue);

            result.AddRange(tieGroup);
            index = groupEndExclusive;
        }

        return result;
    }

    private static int EntityRank(SelectionEntityKind kind)
        => kind switch
        {
            SelectionEntityKind.Edge => 0,
            SelectionEntityKind.Face => 1,
            SelectionEntityKind.Body => 2,
            _ => 3,
        };

    private static KernelDiagnostic CreateInvalidArgument(string message)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, Source: nameof(BrepPicker));
}
