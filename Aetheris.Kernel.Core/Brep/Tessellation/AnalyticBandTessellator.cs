using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public static partial class BrepDisplayTessellator
{
    /// <summary>
    /// A cylinder or cone face bounded by two coaxial circular rims (whole circles, or arcs closed by straight
    /// generators) is meshed as one row of quads between the rims. Its columns are the union of the two rims' samples,
    /// and those samples are the edges' own canonical samples, so the planar caps and neighbouring faces that sample the
    /// same edges meet it vertex for vertex. Generators are straight on both surfaces, so one row is exact along them.
    /// Anything else (elliptic or spline trims, apex rims, partial coverage) is left to the general paths.
    /// </summary>
    private static bool TryTessellateRevolvedBand(BrepBody body, FaceId faceId, Point3D origin, Vector3D axis, Vector3D xAxis,
        double semiAngle, DisplayTessellationOptions options, out DisplayFaceMeshPatch patch)
    {
        patch = null!;
        axis = axis * (1d / axis.Length);
        xAxis = xAxis - (axis * xAxis.Dot(axis));
        if (xAxis.Length < 1e-9) return false;
        xAxis = xAxis * (1d / xAxis.Length);
        var yAxis = axis.Cross(xAxis);
        double U(Point3D p) { var d = p - origin; return double.Atan2(d.Dot(yAxis), d.Dot(xAxis)); }
        double V(Point3D p) => (p - origin).Dot(axis);
        double R(Point3D p) { var d = p - origin; return (d - (axis * d.Dot(axis))).Length; }

        var coedges = body.GetLoopIds(faceId).Where(id => body.Topology.GetLoop(id).Kind == LoopKind.Edge)
            .SelectMany(body.GetCoedgeIds).Select(id => body.Topology.GetCoedge(id)).ToArray();
        if (coedges.Length == 0) return false;
        var uses = coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.Count());
        var scale = 0d;
        var arcs = new List<IReadOnlyList<Point3D>>();
        var generators = new List<double>();
        var seams = new List<double>();
        foreach (var edgeId in uses.Keys)
        {
            if (!body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null) return false;
            if (curve.Kind == CurveGeometryKind.Circle3)
            {
                var circle = curve.Circle3!.Value;
                if (double.Abs(double.Abs(circle.Normal.ToVector().Dot(axis)) - 1d) > 1e-9) return false;
                var offset = circle.Center - origin;
                if ((offset - (axis * offset.Dot(axis))).Length > 1e-7 * double.Max(1d, circle.Radius)) return false;
                if (circle.Radius < 1e-9) return false; // an apex rim: the general path closes it
                if (uses[edgeId] != 1) return false;
                var sampled = TessellateEdge(body, edgeId, options);
                if (!sampled.IsSuccess || sampled.Value.Points.Count < 2) return false;
                arcs.Add(sampled.Value.Points);
                scale = double.Max(scale, circle.Radius);
            }
            else if (curve.Kind == CurveGeometryKind.Line3)
            {
                var start = EvaluateEdgeEndpoint(body, edgeId, useStart: true);
                var end = EvaluateEdgeEndpoint(body, edgeId, useStart: false);
                if (!start.IsSuccess || !end.IsSuccess) return false;
                var (us, ue) = (U(start.Value), U(end.Value));
                if (double.Abs(Wrap(us - ue)) > 1e-7 && double.Min(R(start.Value), R(end.Value)) > 1e-9) return false; // not a generator
                (uses[edgeId] == 2 ? seams : generators).Add(R(start.Value) > 1e-9 ? us : ue);
            }
            else return false;
        }
        if (arcs.Count == 0 || generators.Count is not (0 or 2) || seams.Count > 1) return false;

        // Two rims, at two heights.
        var tolerance = 1e-7 * double.Max(1d, scale);
        var rims = new List<(double V, List<Point3D> Points)>();
        foreach (var arc in arcs)
        {
            var v = V(arc[0]);
            if (arc.Any(p => double.Abs(V(p) - v) > tolerance)) return false;
            var rim = rims.FindIndex(r => double.Abs(r.V - v) <= tolerance);
            if (rim < 0) rims.Add((v, [.. arc])); else rims[rim].Points.AddRange(arc);
        }
        if (rims.Count != 2) return false;

        // The angular domain: the whole turn, or from one generator to the other on the side the arcs occupy.
        var periodic = generators.Count == 0;
        double u0, span;
        if (periodic) { u0 = seams.Count == 1 ? seams[0] : U(rims[0].Points[0]); span = 2d * double.Pi; }
        else
        {
            u0 = generators[0];
            span = Mod(generators[1] - generators[0]);
            var interior = arcs.Select(a => a[a.Count / 2]).Select(p => Mod(U(p) - u0)).ToArray();
            if (interior.Any(d => d > span + 1e-9)) { u0 = generators[1]; span = Mod(generators[0] - generators[1]); }
        }
        double Along(Point3D p)
        {
            var d = Mod(U(p) - u0);
            if (!periodic && d > span + 1e-9) d = d > (span + (2d * double.Pi)) / 2d ? 0d : span; // endpoints a hair outside
            return periodic && d > (2d * double.Pi) - 1e-10 ? 0d : d;
        }

        // Columns: every rim sample, from both rims.
        var columns = new List<double>();
        foreach (var d in rims.SelectMany(r => r.Points).Select(Along).Order())
            if (columns.Count == 0 || d - columns[^1] > 1e-9) columns.Add(d);
        if (!periodic)
        {
            if (columns[0] > 1e-9) columns.Insert(0, 0d);
            if (span - columns[^1] > 1e-9) columns.Add(span); else columns[^1] = span;
        }
        if (columns.Count < (periodic ? 3 : 2)) return false;
        // Both rims must cover the whole domain: each column within a rim sample's reach.
        foreach (var rim in rims)
        {
            var at = rim.Points.Select(Along).Order().ToArray();
            if (!periodic && (at[0] > 1e-7 || at[^1] < span - 1e-7)) return false;
            var gaps = at.Zip(at.Skip(1), (a, b) => b - a).DefaultIfEmpty(0d).Max();
            if (periodic) gaps = double.Max(gaps, (2d * double.Pi) - at[^1] + at[0]);
            if (gaps > double.Pi / 2d + 1e-9) return false; // a rim with an uncovered stretch is not a closed rim
        }

        var ordered = rims.OrderBy(r => r.V).ToArray();
        var positions = new List<Point3D>(columns.Count * 2);
        var normals = new List<Vector3D>(columns.Count * 2);
        foreach (var rim in ordered)
        {
            var radius = rim.Points.Average(R);
            var center = origin + (axis * rim.V);
            foreach (var d in columns)
            {
                // Use the edge's own sample where it has one, so the neighbour that sampled the same edge matches exactly.
                var exact = rim.Points.FirstOrDefault(p => double.Abs(Along(p) - d) <= 1e-9 || periodic && double.Abs(Along(p) - d) >= (2d * double.Pi) - 1e-9);
                var u = u0 + d;
                var radial = (xAxis * double.Cos(u)) + (yAxis * double.Sin(u));
                positions.Add(rim.Points.Any(p => double.Abs(Along(p) - d) <= 1e-9) ? exact : center + (radial * radius));
                // On the nappe behind the apex the generators slope the other way.
                var normal = radial - (axis * (double.Tan(semiAngle) * double.Sign(rim.V == 0 ? 1 : rim.V)));
                normals.Add(normal * (1d / normal.Length));
            }
        }

        var n = columns.Count;
        var quads = periodic ? n : n - 1;
        var indices = new List<int>(quads * 6);
        for (var i = 0; i < quads; i++)
        {
            var (a, b) = (i, (i + 1) % n);
            var (c, d) = (n + i, n + ((i + 1) % n));
            indices.AddRange([a, c, b, b, c, d]);
        }
        // Wind to the surface normal emitted, as the other analytic paths do.
        var (p0, p1, p2) = (positions[indices[0]], positions[indices[1]], positions[indices[2]]);
        if ((p1 - p0).Cross(p2 - p0).Dot(normals[indices[0]]) < 0)
            for (var t = 0; t < indices.Count; t += 3) (indices[t + 1], indices[t + 2]) = (indices[t + 2], indices[t + 1]);
        patch = new DisplayFaceMeshPatch(faceId, positions, normals, indices);
        return true;

        static double Wrap(double a) => System.Math.IEEERemainder(a, 2d * double.Pi);
        static double Mod(double a) { var m = a % (2d * double.Pi); return m < 0 ? m + (2d * double.Pi) : m; }
    }
}
