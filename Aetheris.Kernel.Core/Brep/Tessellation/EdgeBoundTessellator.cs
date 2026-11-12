using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public static partial class BrepDisplayTessellator
{
    /// <summary>
    /// Presentation meshing of a curved face from its edges inward. The face's boundary is the edges' own canonical
    /// samples, kept at their exact 3D positions and never split, so every neighbour that samples the same edges meets
    /// it vertex for vertex. The boundary is mapped into a plane chart of the surface (developed for cylinders and
    /// cones, conformal for faces that go once around a periodic direction, metric-affine otherwise; cylinders are
    /// compressed along their straight generators so triangles run along them), triangulated,
    /// made Delaunay, then refined by splitting interior edges until the surface between vertices is within the chord
    /// and angle tolerances, and finally relaxed. Interior vertices are evaluated on the surface. Faces whose
    /// boundary does not fit one chart (pole or apex rings, faces wrapping two ways, folds) decline, and the general
    /// paths mesh them.
    /// </summary>
    private static bool TryTessellateEdgeBound(BrepBody body, FaceId faceId, Func<IReadOnlyList<IReadOnlyList<Point3D>>, EdgeBoundSurface> surfaceFor,
        DisplayTessellationOptions options, DisplayTessellationExecutionBudget? budget, out DisplayFaceMeshPatch patch)
    {
        patch = null!;
        try
        {
            budget?.ThrowIfExpired("EdgeBound.Start", faceId, null);
            if (!TryCollectEdgeBoundLoops(body, faceId, options, budget, out var loops)) return false;
            if (loops.Sum(l => l.Count) > 20000) return false;
            // The face lies left of its loops seen along its normal; the mesh must agree, or it is the complement.
            var aligned = !body.Bindings.TryGetFaceBinding(faceId, out var binding) || binding.Orientation.IsAlignedWithSurface;
            var mesh = EdgeBoundMesh.TryBuild(surfaceFor(loops), loops, options, aligned);
            if (mesh is null) return false;
            patch = new DisplayFaceMeshPatch(faceId, mesh.Value.Positions, mesh.Value.Normals, mesh.Value.Indices);
            return true;
        }
        catch (ArithmeticException) { return false; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>The face's boundary as closed 3D polylines of canonical edge samples. Seam edges (used twice) are cut out.</summary>
    private static bool TryCollectEdgeBoundLoops(BrepBody body, FaceId faceId, DisplayTessellationOptions options,
        DisplayTessellationExecutionBudget? budget, out List<List<Point3D>> loops)
    {
        loops = [];
        var loopIds = body.GetLoopIds(faceId).ToArray();
        if (loopIds.Length == 0 || loopIds.Any(id => body.Topology.GetLoop(id).Kind != LoopKind.Edge)) return false;
        var coedges = loopIds.SelectMany(body.GetCoedgeIds).Select(id => body.Topology.GetCoedge(id)).ToArray();
        var uses = coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.Count());
        var vertexPoints = BuildLoopVertexPointLookup(body, coedges, faceId);
        if (!vertexPoints.IsSuccess) return false;

        var chains = new List<List<Point3D>>();
        var scale = 0d;
        foreach (var loopId in loopIds)
        {
            List<Point3D>? chain = null;
            foreach (var coedge in body.GetCoedgeIds(loopId).Select(id => body.Topology.GetCoedge(id)))
            {
                if (uses[coedge.EdgeId] != 1) { if (chain is { Count: > 0 }) chains.Add(chain); chain = null; continue; }
                var sampled = TrySampleCoedgeForTrimEvaluation(body, faceId, coedge, vertexPoints.Value, options, budget, null);
                if (!sampled.IsSuccess || sampled.Value.Count < 2) return false;
                chain ??= [];
                foreach (var p in sampled.Value)
                {
                    if (chain.Count == 0 || (chain[^1] - p).Length > 1e-9) chain.Add(p);
                    scale = double.Max(scale, double.Max(double.Abs(p.X), double.Max(double.Abs(p.Y), double.Abs(p.Z))));
                }
            }
            if (chain is { Count: > 0 }) chains.Add(chain);
        }

        // Join chains end to start into closed rings (a loop cut by a seam leaves open pieces).
        var weld = 1e-6 * double.Max(1d, scale);
        while (chains.Count > 0)
        {
            var ring = chains[0];
            chains.RemoveAt(0);
            while ((ring[0] - ring[^1]).Length > weld)
            {
                var next = chains.FindIndex(c => (c[0] - ring[^1]).Length <= weld);
                if (next < 0) return false;
                ring.AddRange(chains[next].Skip(1));
                chains.RemoveAt(next);
            }
            ring.RemoveAt(ring.Count - 1);
            if (ring.Count < 2) return false;
            loops.Add(ring);
        }
        return loops.Count > 0;
    }

    /// <summary>A surface as the edge-bound mesher sees it: projection, evaluation and periods.</summary>
    private sealed record EdgeBoundSurface(
        Func<Point3D, (double U, double V)?> Project,
        Func<double, double, Point3D> Evaluate,
        Func<double, double, Vector3D> Normal,
        double PeriodU,
        double PeriodV,
        double? ConeSemiAngle = null,
        bool Closed = false,
        double StretchV = 1d)
    {
        public EdgeBoundSurface Swapped() => new(
            p => Project(p) is { } uv ? (uv.V, uv.U) : null,
            (u, v) => Evaluate(v, u), (u, v) => Normal(v, u), PeriodV, PeriodU, null, Closed);

        // Boundary samples keep their exact positions; the projection only places them in the chart, so edges that
        // approximate the surface loosely (vendor B-spline trims) are accepted up to a fraction of the radius.
        public static EdgeBoundSurface Of(CylinderSurface s) => new(
            p => TryProjectPointToCylinderUv(s, p, 0.02 * s.Radius), s.Evaluate, (u, _) => s.Normal(u).ToVector(), 2d * double.Pi, 0d, null, false, 0.25);

        public static EdgeBoundSurface Of(ConeSurface s)
        {
            var axis = s.Axis.ToVector();
            var x = s.ReferenceAxis.ToVector() - (axis * s.ReferenceAxis.ToVector().Dot(axis));
            x = x * (1d / x.Length);
            var y = axis.Cross(x);
            return new(p =>
            {
                var d = p - s.Apex;
                var v = d.Dot(axis);
                if (v < -1e-6) return null;
                var radial = d - (axis * v);
                if (double.Abs(radial.Length - (v * double.Tan(s.SemiAngleRadians))) > double.Max(AnalyticProjectionDeviationFloor, v * double.Tan(s.SemiAngleRadians) * 0.02)) return null;
                // The apex has no angle: the chart places it by its slant distance alone.
                return (radial.Length <= 1e-9 ? double.NaN : double.Atan2(radial.Dot(y), radial.Dot(x)), double.Max(v, 0d));
            }, s.Evaluate, (u, _) => s.Normal(u).ToVector(), 2d * double.Pi, 0d, s.SemiAngleRadians);
        }

        /// <summary>
        /// A sphere parameterized about an axis at right angles to the face's mean direction, so its poles, where the
        /// angle is undefined, are a quarter turn away from the face.
        /// </summary>
        public static EdgeBoundSurface Of(SphereSurface s, IReadOnlyList<IReadOnlyList<Point3D>> loops)
        {
            var mean = new Vector3D(0, 0, 0);
            foreach (var p in loops.SelectMany(l => l)) { var d = p - s.Center; if (d.Length > 0) mean += d * (1d / d.Length); }
            Vector3D x, z;
            if (mean.Length > 1e-6 * loops.Sum(l => l.Count))
            {
                x = mean * (1d / mean.Length);
                var helper = double.Abs(x.Dot(s.Axis.ToVector())) < 0.9 ? s.Axis.ToVector() : s.XAxis.ToVector();
                z = x.Cross(helper); z = z * (1d / z.Length);
            }
            else (x, z) = (s.XAxis.ToVector(), s.Axis.ToVector());
            var y = z.Cross(x);
            var radius = s.Radius;
            Point3D Evaluate(double u, double v) => s.Center + (((x * (double.Cos(u) * double.Cos(v))) + (y * (double.Sin(u) * double.Cos(v))) + (z * double.Sin(v))) * radius);
            return new(p =>
            {
                var d = p - s.Center;
                if (double.Abs(d.Length - radius) > double.Max(AnalyticProjectionDeviationFloor, radius * 0.02)) return null;
                var height = d.Dot(z);
                var planar = d - (z * height);
                if (planar.Length <= 1e-9 * radius) return null;
                return (double.Atan2(planar.Dot(y), planar.Dot(x)), double.Atan2(height, planar.Length));
            }, Evaluate, (u, v) => Evaluate(u, v) - s.Center, 2d * double.Pi, 0d, null, true);
        }

        public static EdgeBoundSurface Of(TorusSurface s) => new(
            p => TryProjectPointToTorusUv(s, p), s.Evaluate, (u, v) => s.Normal(u, v).ToVector(), 2d * double.Pi, 2d * double.Pi, null, true);

        public static EdgeBoundSurface Of(BSplineSurfaceWithKnots s)
        {
            var projector = new BSplineUvProjector(s);
            return new(projector.Project, s.Evaluate, (u, v) => EvaluateBSplineNormal(s, u, v), 0d, 0d);
        }
    }

    private readonly record struct EdgeBoundResult(List<Point3D> Positions, List<Vector3D> Normals, List<int> Indices);

    /// <summary>The plane chart, the triangulation in it, and its refinement.</summary>
    private sealed class EdgeBoundMesh
    {
        private readonly EdgeBoundSurface surface;
        private readonly Func<double, double, (double X, double Y)> toPlane;
        private readonly Func<double, double, (double U, double V)> fromPlane;
        private readonly List<(double X, double Y)> plane = [];
        private readonly List<Point3D> positions = [];
        private readonly List<Vector3D> normals = [];
        private readonly List<bool> singular = [];
        private int boundaryCount;
        private readonly List<int[]> triangles = [];
        private readonly Dictionary<(int, int), int> owner = [];

        private EdgeBoundMesh(EdgeBoundSurface surface, Func<double, double, (double X, double Y)> toPlane, Func<double, double, (double U, double V)> fromPlane)
        { this.surface = surface; this.toPlane = toPlane; this.fromPlane = fromPlane; }

        public static EdgeBoundResult? TryBuild(EdgeBoundSurface surface, List<List<Point3D>> loops, DisplayTessellationOptions options, bool aligned)
        {
            // Project, and unwrap each loop along its periodic directions.
            var uv = new List<(double U, double V)[]>(loops.Count);
            var winding = new List<(int U, int V)>(loops.Count);
            foreach (var loop in loops)
            {
                var projected = new (double U, double V)[loop.Count];
                for (var i = 0; i < loop.Count; i++) { if (surface.Project(loop[i]) is not { } p) return null; projected[i] = p; }
                var (wu, ou) = Unwrap(projected, surface.PeriodU, true);
                var (wv, ov) = Unwrap(projected, surface.PeriodV, false);
                if (!ou || !ov) return null;
                uv.Add(projected);
                winding.Add((wu, wv));
            }

            var ringsU = winding.Count(w => w.U != 0);
            var ringsV = winding.Count(w => w.V != 0);
            if (ringsU == 0 && ringsV == 0) return Open(surface, loops, uv, options, aligned);
            if (ringsV == 0 && ringsU == 2 && winding.All(w => double.Abs(w.U) <= 1)) return Ring(surface, loops, uv, winding.Select(w => w.U != 0).ToArray(), options, aligned);
            if (ringsU == 0 && ringsV == 2 && winding.All(w => double.Abs(w.V) <= 1))
                return Ring(surface.Swapped(), loops, uv.Select(l => l.Select(p => (p.V, p.U)).ToArray()).ToList(), winding.Select(w => w.V != 0).ToArray(), options, aligned);
            return null;
        }

        /// <summary>Makes a loop continuous across the period; returns its winding number. NaN coordinates are skipped.</summary>
        private static (int Winding, bool Ok) Unwrap((double U, double V)[] loop, double period, bool alongU)
        {
            if (period <= 0) return (0, true);
            double Get(int i) => alongU ? loop[i].U : loop[i].V;
            void Set(int i, double x) { if (alongU) loop[i] = (x, loop[i].V); else loop[i] = (loop[i].U, x); }
            // A loop through a singular point (a cone apex) has no defined step across it: walk from just after it,
            // so the crossing is the unmeasured closing step, and such a loop does not go around.
            var singular = Enumerable.Range(0, loop.Length).Where(i => double.IsNaN(Get(i))).ToArray();
            if (singular.Length == loop.Length) return (0, true);
            var start = 0;
            if (singular.Length > 0)
            {
                // Exactly one run of singular samples.
                var runs = singular.Count(i => !double.IsNaN(Get((i + loop.Length - 1) % loop.Length)));
                if (runs != 1) return (0, false);
                start = singular.First(i => !double.IsNaN(Get((i + 1) % loop.Length)));
                start = (start + 1) % loop.Length;
            }
            double previous = Get(start), total = 0;
            for (var k = 1; k < loop.Length; k++)
            {
                var i = (start + k) % loop.Length;
                var x = Get(i);
                if (double.IsNaN(x)) continue;
                var step = System.Math.IEEERemainder(x - previous, period);
                if (double.Abs(step) > period * 0.45) return (0, false); // samples too sparse to unwrap
                total += step;
                previous += step;
                Set(i, previous);
            }
            if (singular.Length > 0) return (0, true);
            total += System.Math.IEEERemainder(Get(start) - previous, period);
            return ((int)double.Round(total / period), true);
        }

        /// <summary>A face that does not go around: one chart over its parameter range, centred on it.</summary>
        private static EdgeBoundResult? Open(EdgeBoundSurface surface, List<List<Point3D>> loops, List<(double U, double V)[]> uv, DisplayTessellationOptions options, bool aligned)
        {
            // Align every loop to the first along the periodic directions, then centre the range.
            double Center(bool alongU, double period)
            {
                double Of((double U, double V) p) => alongU ? p.U : p.V;
                var reference = uv[0].Select(Of).Where(double.IsFinite).DefaultIfEmpty(0).Average();
                double lo = double.MaxValue, hi = double.MinValue;
                foreach (var loop in uv)
                {
                    var values = loop.Select(Of).Where(double.IsFinite).ToArray();
                    if (values.Length == 0) continue;
                    var shift = period > 0 ? period * double.Round((reference - values.Average()) / period) : 0;
                    lo = double.Min(lo, values.Min() + shift); hi = double.Max(hi, values.Max() + shift);
                }
                if (period > 0 && hi - lo >= period * 0.98) return double.NaN;
                return (lo + hi) / 2;
            }
            var (uc, vc) = (Center(true, surface.PeriodU), Center(false, surface.PeriodV));
            if (double.IsNaN(uc) || double.IsNaN(vc)) return null;
            double Wrap(double x, double c, double period) => period > 0 ? c + System.Math.IEEERemainder(x - c, period) : x;

            Func<double, double, (double X, double Y)> toPlane;
            Func<double, double, (double U, double V)> fromPlane;
            if (surface.ConeSemiAngle is { } alpha)
            {
                // Developed: slant distance and angle scaled by sin(alpha). Exact, and the apex is one point.
                var (sin, cos) = (double.Sin(alpha), double.Cos(alpha));
                toPlane = (u, v) =>
                {
                    var s = v / cos;
                    if (double.IsNaN(u)) return (0, 0);
                    var phi = (Wrap(u, uc, surface.PeriodU) - uc) * sin;
                    return (s * double.Cos(phi), s * double.Sin(phi));
                };
                fromPlane = (x, y) => (uc + (double.Atan2(y, x) / sin), double.Sqrt((x * x) + (y * y)) * cos);
            }
            else
            {
                // Metric-affine at the centre: the first fundamental form there, factored.
                var (pu, pv) = Partials(surface, uc, vc);
                var (e, f, g) = (pu.Dot(pu), pu.Dot(pv), pv.Dot(pv));
                var det = (e * g) - (f * f);
                if (e <= 1e-24 || det <= 1e-12 * e * g) return null;
                // Along a straight generator (StretchV < 1) the chart is compressed, so Delaunay triangles run along it.
                var (a, b, d) = (double.Sqrt(e), f / double.Sqrt(e) * surface.StretchV, double.Sqrt(det / e) * surface.StretchV);
                toPlane = (u, v) =>
                {
                    var (du, dv) = (Wrap(u, uc, surface.PeriodU) - uc, Wrap(v, vc, surface.PeriodV) - vc);
                    return ((a * du) + (b * dv), d * dv);
                };
                fromPlane = (x, y) => { var dv = y / d; return (uc + ((x - (b * dv)) / a), vc + dv); };
            }
            var mesh = new EdgeBoundMesh(surface, toPlane, fromPlane);
            return mesh.Run(loops, uv, null, options, aligned);
        }

        /// <summary>A face going once around u between two rings: a conformal annulus, r = exp(t(v)), angle = u.</summary>
        private static EdgeBoundResult? Ring(EdgeBoundSurface surface, List<List<Point3D>> loops, List<(double U, double V)[]> uv, bool[] isRing, DisplayTessellationOptions options, bool aligned)
        {
            var vs = uv.SelectMany(l => l).Select(p => p.V).ToArray();
            var (lo, hi) = (vs.Min(), vs.Max());
            var margin = double.Max((hi - lo) * 0.05, 1e-9);
            (lo, hi) = (lo - margin, hi + margin);
            const int n = 256;
            var table = new double[n + 1];
            double Rate(double v) { var (pu, pv) = Partials(surface, 0, v); var e = pu.Length; return e <= 1e-12 ? double.NaN : pv.Length / e; }
            var previous = Rate(lo);
            for (var k = 1; k <= n; k++)
            {
                var r = Rate(lo + ((hi - lo) * k / n));
                if (!double.IsFinite(r) || !double.IsFinite(previous) || r <= 0) return null;
                table[k] = table[k - 1] + ((previous + r) / 2 * (hi - lo) / n);
                previous = r;
            }
            if (table[n] > 14) return null; // too long for the exponential to stay well conditioned
            var mid = table[n] / 2;
            double T(double v)
            {
                var x = double.Clamp((v - lo) / (hi - lo) * n, 0, n);
                var k = int.Min((int)x, n - 1);
                return table[k] + ((table[k + 1] - table[k]) * (x - k)) - mid;
            }
            double V(double t)
            {
                t += mid;
                var k = Array.BinarySearch(table, t);
                if (k < 0) k = ~k - 1;
                k = int.Clamp(k, 0, n - 1);
                var span = table[k + 1] - table[k];
                return lo + ((hi - lo) * (k + (span > 0 ? (t - table[k]) / span : 0)) / n);
            }
            var mesh = new EdgeBoundMesh(surface,
                (u, v) => { var r = double.Exp(T(v)); return (r * double.Cos(u), r * double.Sin(u)); },
                (x, y) => (double.Atan2(y, x), V(double.Log(double.Sqrt((x * x) + (y * y))))));
            return mesh.Run(loops, uv, isRing, options, aligned);
        }

        private static (Vector3D U, Vector3D V) Partials(EdgeBoundSurface surface, double u, double v)
        {
            const double h = 1e-5;
            var du = (surface.Evaluate(u + h, v) - surface.Evaluate(u - h, v)) * (1d / (2 * h));
            var dv = (surface.Evaluate(u, v + h) - surface.Evaluate(u, v - h)) * (1d / (2 * h));
            return (du, dv);
        }

        private int AddVertex((double X, double Y) p, Point3D position, Vector3D normal, bool isSingular)
        {
            plane.Add(p); positions.Add(position);
            normals.Add(normal.Length > 1e-12 ? normal * (1d / normal.Length) : normal);
            singular.Add(isSingular);
            return plane.Count - 1;
        }

        private (double X, double Y) shift; private double unit = 1;

        private EdgeBoundResult? Run(List<List<Point3D>> loops, List<(double U, double V)[]> uv, bool[]? isRing, DisplayTessellationOptions options, bool aligned)
        {
            // Boundary vertices: the edge samples, exact; their normals from the surface at their parameters.
            var rings = new List<List<int>>();
            var centerNormal = (Vector3D?)null;
            for (var l = 0; l < loops.Count; l++)
            {
                var ring = new List<int>();
                for (var i = 0; i < loops[l].Count; i++)
                {
                    var (u, v) = uv[l][i];
                    var p = toPlane(u, v);
                    if (!double.IsFinite(p.X) || !double.IsFinite(p.Y)) return null;
                    var isSingular = double.IsNaN(u);
                    if (isSingular)
                    {
                        // The apex: its normal is the face's mean direction, taken from the plane centroid below.
                        ring.Add(AddVertex(p, loops[l][i], new Vector3D(0, 0, 0), true));
                        continue;
                    }
                    ring.Add(AddVertex(p, loops[l][i], surface.Normal(u, v), false));
                }
                // Consecutive samples that meet in the chart would be degenerate.
                for (var i = 0; i < ring.Count; i++)
                    if (Distance(plane[ring[i]], plane[ring[(i + 1) % ring.Count]]) <= 1e-12 * double.Max(1, Magnitude(plane[ring[i]]))) return null;
                rings.Add(ring);
            }
            boundaryCount = plane.Count;

            // Normalize the chart to a unit box, so tolerances below are relative.
            var (minX, maxX, minY, maxY) = (plane.Min(p => p.X), plane.Max(p => p.X), plane.Min(p => p.Y), plane.Max(p => p.Y));
            unit = double.Max(maxX - minX, maxY - minY);
            if (!(unit > 0)) return null;
            shift = (minX, minY);
            for (var i = 0; i < plane.Count; i++) plane[i] = ((plane[i].X - shift.X) / unit, (plane[i].Y - shift.Y) / unit);

            // Outer loop: the ring farther out in a ring chart, otherwise the largest.
            var areas = rings.Select(r => Area(r.Select(i => plane[i]).ToArray())).ToArray();
            int outer;
            if (isRing is not null)
            {
                var ringIndices = Enumerable.Range(0, rings.Count).Where(i => isRing[i]).ToArray();
                outer = ringIndices.OrderByDescending(i => double.Abs(areas[i])).First();
            }
            else outer = Enumerable.Range(0, rings.Count).OrderByDescending(i => double.Abs(areas[i])).First();
            var expected = double.Abs(areas[outer]) - areas.Where((_, i) => i != outer).Sum(double.Abs);
            if (expected <= 0) return null;
            if (SegmentsCross(rings)) return null;

            var holes = rings.Where((_, i) => i != outer).ToArray();
            if (!EarCutTriangulator.TryTriangulate(rings[outer].Select(i => plane[i]).ToArray(),
                    holes.Select(h => (IReadOnlyList<(double X, double Y)>)h.Select(i => plane[i]).ToArray()).ToArray(), out _, out var earIndices)) return null;
            var map = rings[outer].Concat(holes.SelectMany(h => h)).ToArray();
            var total = 0d;
            for (var t = 0; t < earIndices.Count; t += 3)
            {
                int a = map[earIndices[t]], b = map[earIndices[t + 1]], c = map[earIndices[t + 2]];
                var o = Orient(plane[a], plane[b], plane[c]);
                if (double.Abs(o) <= 1e-15) continue;
                total += double.Abs(o) / 2;
                AddTriangle(o > 0 ? [a, b, c] : [a, c, b]);
            }
            if (triangles.Count == 0 || double.Abs(total - expected) > 1e-6 * double.Max(1e-12, double.Abs(areas[outer]))) return null;
            if (!RestoreDroppedBoundaryVertices()) return null;

            if (singular.Any(s => s))
            {
                var c = fromPlane((0.5 * unit) + shift.X, (0.5 * unit) + shift.Y);
                centerNormal = surface.Normal(c.U, c.V);
                for (var i = 0; i < boundaryCount; i++) if (singular[i]) normals[i] = centerNormal.Value * (1d / centerNormal.Value.Length);
            }
            Legalize(AllInteriorEdges());
            Refine(options);
            Relax(2);
            Legalize(AllInteriorEdges());
            return Emit(rings, aligned, isRing is null);
        }

        /// <summary>
        /// Ear clipping drops collinear vertices (an isoparametric arc is straight in the chart), which would leave
        /// T-junctions on the face boundary. Each dropped vertex lies on a one-sided edge: split that triangle there.
        /// </summary>
        private bool RestoreDroppedBoundaryVertices()
        {
            var used = new bool[boundaryCount];
            foreach (var t in triangles) foreach (var v in t) used[v] = true;
            for (var v = 0; v < boundaryCount; v++)
            {
                if (used[v]) continue;
                (int A, int B)? host = null; var best = double.MaxValue;
                foreach (var (a, b) in owner.Keys.Where(k => !owner.ContainsKey((k.Item2, k.Item1))).ToArray())
                {
                    var (pa, pb, pv) = (plane[a], plane[b], plane[v]);
                    var (dx, dy) = (pb.X - pa.X, pb.Y - pa.Y);
                    var l2 = (dx * dx) + (dy * dy);
                    if (l2 <= 0) continue;
                    var tt = (((pv.X - pa.X) * dx) + ((pv.Y - pa.Y) * dy)) / l2;
                    if (tt <= 0 || tt >= 1) continue;
                    var distance = double.Abs(Orient(pa, pb, pv)) / double.Sqrt(l2);
                    if (distance < best) { best = distance; host = (a, b); }
                }
                if (host is not { } h || best > 1e-9) return false;
                var t = owner[h];
                var c = Opposite(triangles[t], h.A, h.B);
                owner.Remove(h);
                SetTriangle(t, [h.A, v, c]);
                AddTriangle([v, h.B, c]);
                used[v] = true;
            }
            return true;
        }

        private void AddTriangle(int[] t)
        {
            var index = triangles.Count;
            triangles.Add(t);
            for (var s = 0; s < 3; s++) owner[(t[s], t[(s + 1) % 3])] = index;
        }

        private void SetTriangle(int index, int[] t)
        {
            var old = triangles[index];
            for (var s = 0; s < 3; s++) { var key = (old[s], old[(s + 1) % 3]); if (owner.TryGetValue(key, out var o) && o == index) owner.Remove(key); }
            triangles[index] = t;
            for (var s = 0; s < 3; s++) owner[(t[s], t[(s + 1) % 3])] = index;
        }

        private IEnumerable<(int, int)> AllInteriorEdges() =>
            owner.Keys.Where(k => k.Item1 < k.Item2 && owner.ContainsKey((k.Item2, k.Item1))).ToArray();

        private static int Opposite(int[] t, int a, int b) => t.First(x => x != a && x != b);

        /// <summary>Lawson flips to the Delaunay triangulation; boundary edges have one side and are never flipped.</summary>
        private void Legalize(IEnumerable<(int A, int B)> seed)
        {
            var stack = new Stack<(int A, int B)>(seed);
            var budget = 64 * (triangles.Count + 16);
            while (stack.Count > 0 && budget-- > 0)
            {
                var (a, b) = stack.Pop();
                if (!owner.TryGetValue((a, b), out var t) || !owner.TryGetValue((b, a), out var u)) continue;
                var c = Opposite(triangles[t], a, b);
                var d = Opposite(triangles[u], a, b);
                // Orient so (a, b, c) is counter-clockwise.
                if (Orient(plane[a], plane[b], plane[c]) < 0) { (a, b) = (b, a); (t, u) = (u, t); (c, d) = (d, c); }
                if (InCircle(plane[a], plane[b], plane[c], plane[d]) <= 1e-13) continue;
                if (Orient(plane[a], plane[d], plane[c]) <= 1e-15 || Orient(plane[d], plane[b], plane[c]) <= 1e-15) continue;
                owner.Remove((a, b)); owner.Remove((b, a));
                SetTriangle(t, [a, d, c]);
                SetTriangle(u, [d, b, c]);
                stack.Push((a, d)); stack.Push((d, b)); stack.Push((b, c)); stack.Push((c, a));
            }
        }

        /// <summary>
        /// Splits interior edges at their chart midpoints until every interior edge is within the chord and angle
        /// tolerances and no interior triangle bulges past the chord tolerance. Longest first, so the mesh grades
        /// smoothly. Boundary edges are the face's edges and are never split; a triangle on the boundary sags with its
        /// boundary edge, which is that edge's own sampling, so only interior triangles are tested for bulge.
        /// </summary>
        private void Refine(DisplayTessellationOptions options)
        {
            var chord = options.ChordTolerance;
            var cosAngle = double.Cos(options.AngularToleranceRadians);
            // Nothing is split below a quarter of the shortest boundary segment: finer than the edges is invisible.
            var shortest = owner.Keys.Where(k => !owner.ContainsKey((k.Item2, k.Item1)))
                .Select(k => (positions[k.Item1] - positions[k.Item2]).Length).DefaultIfEmpty(0).Min();
            var floor = double.Max(shortest * 0.25, chord * 0.05);
            var limit = int.Min(60000, 4000 + (5 * boundaryCount));
            for (var pass = 0; pass < 40 && plane.Count < limit; pass++)
            {
                var bad = new Dictionary<(int, int), double>();
                void Mark(int a, int b)
                {
                    var length = (positions[a] - positions[b]).Length;
                    if (length >= floor) bad[a < b ? (a, b) : (b, a)] = length;
                }
                foreach (var (a, b) in AllInteriorEdges())
                {
                    if (!singular[a] && !singular[b] && normals[a].Dot(normals[b]) < cosAngle) { Mark(a, b); continue; }
                    var mid = Lift(Mid(plane[a], plane[b]));
                    if ((mid.P - (positions[a] + ((positions[b] - positions[a]) * 0.5))).Length > chord) Mark(a, b);
                }
                foreach (var tri in triangles)
                {
                    if (!owner.ContainsKey((tri[1], tri[0])) || !owner.ContainsKey((tri[2], tri[1])) || !owner.ContainsKey((tri[0], tri[2]))) continue;
                    var (pa, pb, pc) = (positions[tri[0]], positions[tri[1]], positions[tri[2]]);
                    var centroid = Lift(((plane[tri[0]].X + plane[tri[1]].X + plane[tri[2]].X) / 3, (plane[tri[0]].Y + plane[tri[1]].Y + plane[tri[2]].Y) / 3));
                    var flat = new Point3D((pa.X + pb.X + pc.X) / 3, (pa.Y + pb.Y + pc.Y) / 3, (pa.Z + pb.Z + pc.Z) / 3);
                    if ((centroid.P - flat).Length > chord && LongestEdge(tri) is var (la, lb)) Mark(la, lb);
                }
                if (bad.Count == 0) break;
                foreach (var ((a, b), _) in bad.OrderByDescending(kv => kv.Value))
                {
                    if (plane.Count >= limit) break;
                    if (owner.ContainsKey((a, b)) && owner.ContainsKey((b, a))) Split(a, b);
                }
            }
        }

        private (int, int) LongestEdge(int[] tri)
        {
            var best = (tri[0], tri[1]); var length = -1d;
            for (var s = 0; s < 3; s++)
            {
                var l = (positions[tri[s]] - positions[tri[(s + 1) % 3]]).Length;
                if (l > length) { length = l; best = (tri[s], tri[(s + 1) % 3]); }
            }
            return best;
        }

        private (Point3D P, Vector3D N) Lift((double X, double Y) p)
        {
            var (u, v) = fromPlane((p.X * unit) + shift.X, (p.Y * unit) + shift.Y);
            return (surface.Evaluate(u, v), surface.Normal(u, v));
        }

        private void Split(int a, int b)
        {
            var t = owner[(a, b)]; var u = owner[(b, a)];
            var c = Opposite(triangles[t], a, b); var d = Opposite(triangles[u], a, b);
            var mid = Mid(plane[a], plane[b]);
            var lifted = Lift(mid);
            var m = AddVertex(mid, lifted.P, lifted.N, false);
            owner.Remove((a, b)); owner.Remove((b, a));
            // t = (a, b, c) and u = (b, a, d), both counter-clockwise.
            SetTriangle(t, [a, m, c]); AddTriangle([m, b, c]);
            SetTriangle(u, [b, m, d]); AddTriangle([m, a, d]);
            Legalize([(a, c), (c, b), (b, d), (d, a)]);
        }

        /// <summary>Moves interior vertices toward the mean of their neighbours, where no triangle turns over.</summary>
        private void Relax(int passes)
        {
            if (plane.Count == boundaryCount) return;
            for (var pass = 0; pass < passes; pass++)
            {
                var ring = new Dictionary<int, List<int>>();
                var incident = new Dictionary<int, List<int>>();
                for (var t = 0; t < triangles.Count; t++)
                    foreach (var v in triangles[t])
                    {
                        if (v < boundaryCount) continue;
                        (incident.TryGetValue(v, out var list) ? list : incident[v] = []).Add(t);
                    }
                foreach (var (v, list) in incident)
                {
                    var neighbours = list.SelectMany(t => triangles[t]).Where(x => x != v).Distinct().ToArray();
                    var target = (neighbours.Average(x => plane[x].X), neighbours.Average(x => plane[x].Y));
                    var original = plane[v];
                    plane[v] = target;
                    if (list.Any(t => Orient(plane[triangles[t][0]], plane[triangles[t][1]], plane[triangles[t][2]]) <= 1e-14)) { plane[v] = original; continue; }
                    var lifted = Lift(target);
                    positions[v] = lifted.P;
                    normals[v] = lifted.N.Length > 1e-12 ? lifted.N * (1d / lifted.N.Length) : lifted.N;
                }
            }
        }

        private EdgeBoundResult? Emit(List<List<int>> rings, bool aligned, bool openChart)
        {
            // Wind to the surface normal; a triangle against it is a fold the chart introduced.
            var indices = new List<int>(triangles.Count * 3);
            var agree = 0d;
            foreach (var t in triangles)
            {
                var cross = (positions[t[1]] - positions[t[0]]).Cross(positions[t[2]] - positions[t[0]]);
                agree += cross.Dot(normals[t[0]] + normals[t[1]] + normals[t[2]]);
            }
            var flip = agree < 0;
            foreach (var t in triangles)
            {
                var (a, b, c) = flip ? (t[0], t[2], t[1]) : (t[0], t[1], t[2]);
                var cross = (positions[b] - positions[a]).Cross(positions[c] - positions[a]);
                var n = normals[a] + normals[b] + normals[c];
                if (cross.Length > 1e-14 && n.Length > 1e-12 && cross.Dot(n) < -0.05 * cross.Length * n.Length) return null;
                indices.Add(a); indices.Add(b); indices.Add(c);
            }
            // On a closed surface an open chart could have meshed the complement of the face. There, every loop must
            // run along the mesh boundary the way the face sense says. (Elsewhere loop direction is not relied on:
            // imported loops are not always oriented consistently with their face sense.)
            if (!surface.Closed || !openChart) return new EdgeBoundResult(positions, normals, indices);
            var directed = new HashSet<(int, int)>();
            for (var i = 0; i < indices.Count; i += 3) for (var k = 0; k < 3; k++) directed.Add((indices[i + k], indices[i + ((k + 1) % 3)]));
            foreach (var ring in rings)
            {
                var along = 0;
                for (var i = 0; i < ring.Count; i++)
                {
                    var (a, b) = (ring[i], ring[(i + 1) % ring.Count]);
                    if (directed.Contains((a, b))) along++; else if (directed.Contains((b, a))) along--;
                }
                if (along == 0 || (along > 0) != aligned) return null;
            }
            return new EdgeBoundResult(positions, normals, indices);
        }

        private bool SegmentsCross(List<List<int>> rings)
        {
            var segments = rings.SelectMany(r => r.Select((v, i) => (A: v, B: r[(i + 1) % r.Count]))).ToArray();
            var order = segments.OrderBy(s => double.Min(plane[s.A].X, plane[s.B].X)).ToArray();
            for (var i = 0; i < order.Length; i++)
            {
                var (a, b) = order[i];
                var maxX = double.Max(plane[a].X, plane[b].X);
                for (var j = i + 1; j < order.Length && double.Min(plane[order[j].A].X, plane[order[j].B].X) <= maxX; j++)
                {
                    var (c, d) = order[j];
                    if (a == c || a == d || b == c || b == d) continue;
                    if (Cross(plane[a], plane[b], plane[c], plane[d])) return true;
                }
            }
            return false;

            static bool Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
            {
                var (o1, o2, o3, o4) = (Orient(a, b, c), Orient(a, b, d), Orient(c, d, a), Orient(c, d, b));
                return ((o1 > 0 && o2 < 0) || (o1 < 0 && o2 > 0)) && ((o3 > 0 && o4 < 0) || (o3 < 0 && o4 > 0));
            }
        }

        private static (double X, double Y) Mid((double X, double Y) a, (double X, double Y) b) => ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        private static double Distance((double X, double Y) a, (double X, double Y) b) => double.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
        private static double Magnitude((double X, double Y) a) => double.Sqrt((a.X * a.X) + (a.Y * a.Y));
        private static double Orient((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

        private static double Area((double X, double Y)[] ring)
        {
            var sum = 0d;
            for (var i = 0; i < ring.Length; i++) { var (p, q) = (ring[i], ring[(i + 1) % ring.Length]); sum += (p.X * q.Y) - (q.X * p.Y); }
            return sum / 2;
        }

        private static double InCircle((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
        {
            var (adx, ady, bdx, bdy, cdx, cdy) = (a.X - d.X, a.Y - d.Y, b.X - d.X, b.Y - d.Y, c.X - d.X, c.Y - d.Y);
            var (ad, bd, cd) = ((adx * adx) + (ady * ady), (bdx * bdx) + (bdy * bdy), (cdx * cdx) + (cdy * cdy));
            return (adx * ((bdy * cd) - (bd * cdy))) - (ady * ((bdx * cd) - (bd * cdx))) + (ad * ((bdx * cdy) - (bdy * cdx)));
        }
    }
}
