using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Bounded display lane for natural rectangular spline patches and simple planar caps.
/// Samples each source edge once; every face reuses those boundary positions. Arbitrary
/// trimmed splines and holes remain outside this lane. Never alters BRep.
/// </summary>
public static class RectangularSplineDisplayTessellator
{
    private const double BoundaryTolerance = 1e-5;

    public static bool TryTessellate(BrepBody body, DisplayTessellationOptions options, out DisplayTessellationResult result)
    {
        result = default!;
        if (options.Validate().Count != 0 || options.MaximumSegments > 256
            || !body.Geometry.Surfaces.Any(s => s.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots)
            || body.Geometry.Surfaces.Any(s => s.Value.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.BSplineSurfaceWithKnots))) return false;
        for (var segments = options.MinimumSegments; ; segments = int.Min(segments * 2, options.MaximumSegments))
        {
            if (!TryBuild(body, options, segments, out result, out var withinTolerance)) return false;
            if (withinTolerance) return true;
            if (segments == options.MaximumSegments) { result = default!; return false; }
        }
    }

    private static bool TryBuild(BrepBody body, DisplayTessellationOptions options, int segments,
        out DisplayTessellationResult result, out bool withinTolerance)
    {
        result = default!;
        withinTolerance = true;
        var edges = new Dictionary<EdgeId, Point3D[]>();
        foreach (var edge in body.Topology.Edges)
        {
            if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var binding) || binding.TrimInterval is not { } interval
                || !body.TryGetEdgeCurveGeometry(edge.Id, out var curve)) return false;
            Func<double, Point3D>? evaluate = curve?.Kind switch
            {
                CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate,
                CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate,
                _ => null
            };
            if (evaluate is null) return false;
            var points = Enumerable.Range(0, segments + 1).Select(i => evaluate(interval.Start + (interval.End - interval.Start) * i / segments)).ToArray();
            if (!body.TryGetVertexPoint(edge.StartVertexId, out var start) || !body.TryGetVertexPoint(edge.EndVertexId, out var end)
                || (points[0] - start).Length > BoundaryTolerance || (points[^1] - end).Length > BoundaryTolerance) return false;
            points[0] = start; points[^1] = end;
            edges.Add(edge.Id, points);
        }
        var patches = new List<DisplayFaceMeshPatch>();
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (!body.Bindings.TryGetFaceBinding(face.Id, out var binding) || !body.TryGetFaceSurfaceGeometry(face.Id, out var support)) return false;
            var loops = body.GetLoopIds(face.Id).ToArray();
            if (loops.Length != 1) return false;
            var coedges = body.GetCoedgeIds(loops[0]).Select(body.Topology.GetCoedge).ToArray();
            var sign = binding.Orientation.IsAlignedWithSurface ? 1d : -1d;
            if (support?.Plane is { } plane)
            {
                var boundary = coedges.SelectMany(c => (c.IsReversed ? edges[c.EdgeId].Reverse() : edges[c.EdgeId]).Take(segments)).ToArray();
                if (boundary.Length < 3) return false;
                var center = new Point3D(boundary.Average(p => p.X), boundary.Average(p => p.Y), boundary.Average(p => p.Z));
                var normal = plane.Normal.ToVector() * sign;
                // Keep the existing convex fan. Concave caps reuse the ordinary
                // simple-polygon triangulator over these same shared edge samples.
                var winding = 0;
                var convex = true;
                for (var i = 0; i < boundary.Length; i++)
                {
                    if (double.Abs((boundary[i] - plane.Origin).Dot(plane.Normal.ToVector())) > BoundaryTolerance) return false;
                    var cross = (boundary[i] - center).Cross(boundary[(i + 1) % boundary.Length] - center).Dot(normal);
                    if (double.Abs(cross) < 1e-12) { convex = false; continue; }
                    var direction = System.Math.Sign(cross);
                    if (winding != 0 && winding != direction) convex = false;
                    winding = direction;
                    var a = boundary[(i + 1) % boundary.Length] - boundary[i];
                    var b = boundary[(i + 2) % boundary.Length] - boundary[(i + 1) % boundary.Length];
                    if (a.Cross(b).Dot(normal) * winding < -1e-8) convex = false;
                }
                if (!convex)
                {
                    if (!PlanarPolygonTriangulator.TryTriangulate(boundary, normal, out var capIndices, out _)) return false;
                    patches.Add(new(face.Id, boundary, Enumerable.Repeat(normal, boundary.Length).ToArray(), capIndices));
                    continue;
                }
                var positions = boundary.Append(center).ToArray();
                var indices = new List<int>();
                for (var i = 0; i < boundary.Length; i++) AddTriangle(indices, boundary.Length, i, (i + 1) % boundary.Length, positions, normal);
                patches.Add(new(face.Id, positions, Enumerable.Repeat(normal, positions.Length).ToArray(), indices));
                continue;
            }
            if (support?.BSplineSurfaceWithKnots is not { } spline || coedges.Length != 4) return false;
            Point3D Evaluate(double u, double v) => spline.Evaluate(spline.DomainStartU + (spline.DomainEndU - spline.DomainStartU) * u,
                spline.DomainStartV + (spline.DomainEndV - spline.DomainStartV) * v);
            Vector3D Normal(double u, double v)
            {
                const double h = 1e-5;
                var du = Evaluate(double.Min(1, u + h), v) - Evaluate(double.Max(0, u - h), v);
                var dv = Evaluate(u, double.Min(1, v + h)) - Evaluate(u, double.Max(0, v - h));
                if (!du.TryNormalize(out du) || !dv.TryNormalize(out dv) || !du.Cross(dv).TryNormalize(out var n)) return default;
                return n * sign;
            }
            var pointsGrid = new Point3D[(segments + 1) * (segments + 1)];
            var normals = new Vector3D[pointsGrid.Length];
            int Id(int u, int v) => v * (segments + 1) + u;
            for (var v = 0; v <= segments; v++) for (var u = 0; u <= segments; u++)
            {
                pointsGrid[Id(u, v)] = Evaluate((double)u / segments, (double)v / segments);
                normals[Id(u, v)] = Normal((double)u / segments, (double)v / segments);
                if (normals[Id(u, v)].Length < .5) return false;
            }
            var sides = new[] {
                Enumerable.Range(0,segments+1).Select(i=>Id(i,0)).ToArray(),
                Enumerable.Range(0,segments+1).Select(i=>Id(segments,i)).ToArray(),
                Enumerable.Range(0,segments+1).Select(i=>Id(i,segments)).ToArray(),
                Enumerable.Range(0,segments+1).Select(i=>Id(0,i)).ToArray() };
            var used = new HashSet<EdgeId>();
            foreach (var side in sides)
            {
                var matched = false;
                foreach (var coedge in coedges.Where(c => !used.Contains(c.EdgeId)))
                {
                    var edge = edges[coedge.EdgeId];
                    var reverse = (pointsGrid[side[0]] - edge[^1]).Length < BoundaryTolerance;
                    if (side.Where((id, i) => (pointsGrid[id] - edge[reverse ? segments - i : i]).Length > BoundaryTolerance).Any()) continue;
                    for (var i = 0; i <= segments; i++) pointsGrid[side[i]] = edge[reverse ? segments - i : i];
                    used.Add(coedge.EdgeId); matched = true; break;
                }
                if (!matched) return false;
            }
            var triangles = new List<int>();
            for (var v = 0; v < segments; v++) for (var u = 0; u < segments; u++)
            {
                var a = Id(u, v); var b = Id(u + 1, v); var c = Id(u + 1, v + 1); var d = Id(u, v + 1);
                var midpoint = Evaluate((u + .5) / segments, (v + .5) / segments);
                var linear = pointsGrid[a] + (pointsGrid[c] - pointsGrid[a]) * .5;
                var normal = Normal((u + .5) / segments, (v + .5) / segments);
                if ((midpoint - linear).Length > options.ChordTolerance || new[] { a, b, c, d }.Any(i => normals[i].Dot(normal) < double.Cos(options.AngularToleranceRadians))) withinTolerance = false;
                AddTriangle(triangles, a, b, c, pointsGrid, normal); AddTriangle(triangles, a, c, d, pointsGrid, normal);
            }
            patches.Add(new(face.Id, pointsGrid, normals, triangles));
        }
        result = new(patches, edges.Select(e => new DisplayEdgePolyline(e.Key, e.Value, false)).ToArray(), MeshPipeline: DisplayMeshPipeline.StructuredSpline);
        return true;
    }

    private static void AddTriangle(List<int> indices, int a, int b, int c, IReadOnlyList<Point3D> points, Vector3D normal)
    {
        if ((points[b] - points[a]).Cross(points[c] - points[a]).Dot(normal) < 0) (b, c) = (c, b);
        indices.Add(a); indices.Add(b); indices.Add(c);
    }
}
