using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Boundary-conforming tessellation of a trimmed parametric face.
/// <para>
/// The mask tessellator in <see cref="TrimmedSurfaceTessellator"/> samples a uniform UV grid and keeps only the
/// cells that lie (almost) entirely inside the trim loops. That leaves a staircase along every trim curve and a gap
/// of up to one cell width between adjacent faces, which is very visible on narrow B-spline patches such as gear
/// flanks. This tessellator instead triangulates the trim loops themselves (so every triangle edge along the
/// boundary is a real boundary sample) and then refines the interior by conforming longest-edge bisection until the
/// triangles satisfy the chord and angular tolerances of the display options (edge-midpoint sag, triangle-centre
/// sag and normal deviation), with a coarse cell-size cap as a safety net.
/// </para>
/// It is deliberately conservative: any input it cannot triangulate as a simple polygon with holes returns null so
/// the caller can fall back to the mask tessellator.
/// </summary>
internal static class BoundaryConformingTrimTessellator
{
    // Soft cap: refinement stops (leaving a valid, conforming, slightly coarser mesh) once a face reaches this size,
    // so dense B-spline faces cannot consume the whole display budget.
    private const int MaxTriangles = 12_000;
    // Never split edges shorter than this fraction of a nominal grid cell (guarantees termination).
    private const double MinimumSplitLength = 0.125d;
    // Safety net: triangles with an edge longer than this many nominal grid cells are always split.
    private const double MaximumCellLength = 8d;
    private const double TieTolerance = 1e-12d;

    public static DisplayFaceMeshPatch? TryTessellate(
        FaceId faceId,
        IReadOnlyList<List<(double U, double V)>> loops,
        int outerLoopIndex,
        Func<double, double, Point3D> evaluate,
        Func<double, double, Vector3D> evaluateNormal,
        double cellU,
        double cellV,
        DisplayTessellationOptions options,
        Action? checkBudget,
        double maximumCellLength = MaximumCellLength)
    {
        if (!(cellU > 0d) || !(cellV > 0d) || !double.IsFinite(cellU) || !double.IsFinite(cellV))
        {
            return null;
        }

        var outer = loops[outerLoopIndex];
        if (loops.Count == 1 && IsAxisAlignedRectangle(outer))
        {
            // The uniform grid is built from the loop bounds, so a lone axis-aligned rectangle is already
            // reproduced exactly by the mask tessellator; there is no staircase to remove.
            return null;
        }

        var holes = new List<List<(double U, double V)>>();
        for (var i = 0; i < loops.Count; i++)
        {
            if (i != outerLoopIndex)
            {
                holes.Add(loops[i]);
            }
        }

        // Every hole must lie inside the outer loop and not inside another hole; otherwise the loop set is not an
        // "outer minus holes" region (e.g. periodic wrap-around ring) and the mask tessellator must decide.
        foreach (var hole in holes)
        {
            if (!IsStrictlyInside(outer, hole[0]))
            {
                return null;
            }
        }

        for (var i = 0; i < holes.Count; i++)
        {
            for (var j = 0; j < holes.Count; j++)
            {
                if (i != j && IsStrictlyInside(holes[j], holes[i][0]))
                {
                    return null;
                }
            }
        }

        // Triangulate in "cell units" so the polygon triangulator's absolute epsilons are well conditioned even for
        // parameter domains that are tiny (a 0.04-wide B-spline patch) or anisotropic (radians x millimetres).
        var outerScaled = ToScaled(outer, cellU, cellV);
        var holesScaled = holes.Select(hole => (IReadOnlyList<(double X, double Y)>)ToScaled(hole, cellU, cellV)).ToList();

        // Preferred: the shared planar polygon triangulator, which also validates that every loop is simple.
        // It occasionally gives up on long, finely sampled loops; a simple polygon it rejects for that reason
        // ("TriangulationFailed") is retried with a robust earcut port. Non-simple input is left to the fallback.
        List<(double X, double Y)> seedPoints;
        List<int> seedIndices;
        if (PlanarPolygonTriangulator.TryTriangulateWithHoles(
                outerScaled.Select(static p => new Point3D(p.X, p.Y, 0d)).ToList(),
                holesScaled.Select(static hole => (IReadOnlyList<Point3D>)hole.Select(static p => new Point3D(p.X, p.Y, 0d)).ToList()).ToList(),
                new Vector3D(0d, 0d, 1d),
                out var triangulationPoints,
                out var triangulationIndices,
                out var triangulationFailure)
            && triangulationIndices.Count >= 3)
        {
            seedPoints = triangulationPoints.Select(static p => (p.X, p.Y)).ToList();
            seedIndices = triangulationIndices.ToList();
        }
        else if (triangulationFailure != PlanarPolygonTriangulationFailure.TriangulationFailed
            || !EarCutTriangulator.TryTriangulate(outerScaled, holesScaled, out seedPoints, out seedIndices))
        {
            return null;
        }

        var mesh = new RefinableMesh(cellU, cellV, evaluate, evaluateNormal, options.ChordTolerance, options.AngularToleranceRadians, maximumCellLength);
        var pointMap = new int[seedPoints.Count];
        for (var i = 0; i < seedPoints.Count; i++)
        {
            pointMap[i] = mesh.AddVertex(seedPoints[i].X * cellU, seedPoints[i].Y * cellV);
        }

        for (var i = 0; i + 2 < seedIndices.Count; i += 3)
        {
            var a = pointMap[seedIndices[i]];
            var b = pointMap[seedIndices[i + 1]];
            var c = pointMap[seedIndices[i + 2]];
            if (a == b || b == c || a == c)
            {
                continue;
            }

            mesh.AddTriangle(a, b, c);
        }

        if (!mesh.Refine(checkBudget))
        {
            return null;
        }

        var positions = new List<Point3D>(mesh.VertexCount);
        var normals = new List<Vector3D>(mesh.VertexCount);
        for (var i = 0; i < mesh.VertexCount; i++)
        {
            positions.Add(mesh.Position(i));
            normals.Add(mesh.Normal(i));
        }

        // The mask tessellator emits triangles clockwise in (u, v); keep that convention.
        var indices = new List<int>(mesh.AliveTriangleCount * 3);
        foreach (var (a, b, c) in mesh.AliveTriangles())
        {
            var pa = mesh.Vertex(a);
            var pb = mesh.Vertex(b);
            var pc = mesh.Vertex(c);
            var cross = ((pb.U - pa.U) * (pc.V - pa.V)) - ((pb.V - pa.V) * (pc.U - pa.U));
            if (cross == 0d)
            {
                continue;
            }

            if (cross > 0d)
            {
                indices.Add(a);
                indices.Add(c);
                indices.Add(b);
            }
            else
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
            }
        }

        if (indices.Count == 0)
        {
            return null;
        }

        return new DisplayFaceMeshPatch(faceId, positions, normals, indices);
    }

    private static List<(double X, double Y)> ToScaled(IReadOnlyList<(double U, double V)> loop, double cellU, double cellV)
    {
        var points = new List<(double X, double Y)>(loop.Count);
        foreach (var (u, v) in loop)
        {
            points.Add((u / cellU, v / cellV));
        }

        return points;
    }

    private static bool IsAxisAlignedRectangle(IReadOnlyList<(double U, double V)> loop)
    {
        if (loop.Count != 4)
        {
            return false;
        }

        const double tolerance = 1e-12d;
        for (var i = 0; i < 4; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % 4];
            var horizontal = System.Math.Abs(a.V - b.V) <= tolerance;
            var vertical = System.Math.Abs(a.U - b.U) <= tolerance;
            if (horizontal == vertical)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStrictlyInside(IReadOnlyList<(double U, double V)> polygon, (double U, double V) point)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if ((a.V > point.V) != (b.V > point.V)
                && point.U < ((b.U - a.U) * (point.V - a.V) / (b.V - a.V)) + a.U)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private sealed class RefinableMesh
    {
        private readonly double _cellU;
        private readonly double _cellV;
        private readonly Func<double, double, Point3D> _evaluate;
        private readonly Func<double, double, Vector3D> _evaluateNormal;
        private readonly double _chordTolerance;
        private readonly double _angularTolerance;
        private readonly double _maximumCellLength;
        private readonly List<Point3D?> _positions = new();
        private readonly List<Vector3D?> _normals = new();
        private readonly List<(double U, double V)> _vertices = new();
        private readonly Dictionary<(ulong, ulong), int> _vertexLookup = new();
        private readonly List<int> _triangles = new();
        private readonly List<bool> _alive = new();
        private readonly Dictionary<long, List<int>> _edgeTriangles = new();
        private readonly Stack<int> _work = new();
        private int _aliveCount;

        public RefinableMesh(
            double cellU,
            double cellV,
            Func<double, double, Point3D> evaluate,
            Func<double, double, Vector3D> evaluateNormal,
            double chordTolerance,
            double angularTolerance,
            double maximumCellLength)
        {
            _cellU = cellU;
            _cellV = cellV;
            _evaluate = evaluate;
            _evaluateNormal = evaluateNormal;
            _chordTolerance = chordTolerance;
            _angularTolerance = angularTolerance;
            _maximumCellLength = maximumCellLength;
        }

        public Point3D Position(int index)
        {
            if (_positions[index] is { } cached)
            {
                return cached;
            }

            var (u, v) = _vertices[index];
            var position = _evaluate(u, v);
            _positions[index] = position;
            return position;
        }

        public Vector3D Normal(int index)
        {
            if (_normals[index] is { } cached)
            {
                return cached;
            }

            var (u, v) = _vertices[index];
            var normal = _evaluateNormal(u, v);
            _normals[index] = normal;
            return normal;
        }

        public int VertexCount => _vertices.Count;

        public int AliveTriangleCount => _aliveCount;

        public (double U, double V) Vertex(int index) => _vertices[index];

        public int AddVertex(double u, double v)
        {
            var key = (BitConverter.DoubleToUInt64Bits(u), BitConverter.DoubleToUInt64Bits(v));
            if (_vertexLookup.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var index = _vertices.Count;
            _vertices.Add((u, v));
            _positions.Add(null);
            _normals.Add(null);
            _vertexLookup[key] = index;
            return index;
        }

        public int AddTriangle(int a, int b, int c)
        {
            var id = _alive.Count;
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
            _alive.Add(true);
            _aliveCount++;
            LinkEdge(a, b, id);
            LinkEdge(b, c, id);
            LinkEdge(c, a, id);
            _work.Push(id);
            return id;
        }

        public IEnumerable<(int A, int B, int C)> AliveTriangles()
        {
            for (var t = 0; t < _alive.Count; t++)
            {
                if (_alive[t])
                {
                    yield return (_triangles[3 * t], _triangles[(3 * t) + 1], _triangles[(3 * t) + 2]);
                }
            }
        }

        public bool Refine(Action? checkBudget)
        {
            var iterations = 0;
            while (_work.Count > 0)
            {
                if ((++iterations & 0xFF) == 0)
                {
                    checkBudget?.Invoke();
                }

                if (_alive.Count > MaxTriangles)
                {
                    return true;
                }

                var t = _work.Pop();
                if (!_alive[t] || !NeedsSplit(t))
                {
                    continue;
                }

                // Longest-edge propagation path: walk to the neighbour across the longest edge until both
                // triangles agree the shared edge is their longest, then bisect it. This keeps the mesh conforming.
                var current = t;
                var guard = 0;
                while (true)
                {
                    var slot = LongestEdgeSlot(current);
                    var key = EdgeKey(current, slot);
                    var neighbour = OtherTriangle(key, current);
                    if (neighbour < 0 || EdgeKey(neighbour, LongestEdgeSlot(neighbour)) == key)
                    {
                        SplitEdge(current, slot);
                        break;
                    }

                    current = neighbour;
                    if (++guard > 100_000)
                    {
                        return false;
                    }
                }

                if (_alive[t])
                {
                    _work.Push(t);
                }
            }

            return true;
        }

        private bool NeedsSplit(int triangle)
        {
            var longest = LongestEdgeLength(triangle);
            if (longest <= MinimumSplitLength)
            {
                return false;
            }

            if (longest > _maximumCellLength)
            {
                return true;
            }

            var a = _triangles[3 * triangle];
            var b = _triangles[(3 * triangle) + 1];
            var c = _triangles[(3 * triangle) + 2];
            for (var slot = 0; slot < 3; slot++)
            {
                if (EdgeLength(triangle, slot) <= MinimumSplitLength)
                {
                    continue;
                }

                var p = _triangles[(3 * triangle) + slot];
                var q = _triangles[(3 * triangle) + ((slot + 1) % 3)];
                var (pu, pv) = _vertices[p];
                var (qu, qv) = _vertices[q];
                var midpoint = _evaluate((pu + qu) * 0.5d, (pv + qv) * 0.5d);
                var pp = Position(p);
                var pq = Position(q);
                var chordMidpoint = new Point3D((pp.X + pq.X) * 0.5d, (pp.Y + pq.Y) * 0.5d, (pp.Z + pq.Z) * 0.5d);
                if ((midpoint - chordMidpoint).Length > _chordTolerance)
                {
                    return true;
                }

                var np = Normal(p);
                var nq = Normal(q);
                var lp = np.Length;
                var lq = nq.Length;
                if (lp > 1e-12d && lq > 1e-12d)
                {
                    var cosine = double.Clamp(np.Dot(nq) / (lp * lq), -1d, 1d);
                    if (double.Acos(cosine) > _angularTolerance)
                    {
                        return true;
                    }
                }
            }

            var (au, av) = _vertices[a];
            var (bu, bv) = _vertices[b];
            var (cu, cv) = _vertices[c];
            var centre = _evaluate((au + bu + cu) / 3d, (av + bv + cv) / 3d);
            var pa = Position(a);
            var pb = Position(b);
            var pc = Position(c);
            var average = new Point3D((pa.X + pb.X + pc.X) / 3d, (pa.Y + pb.Y + pc.Y) / 3d, (pa.Z + pb.Z + pc.Z) / 3d);
            return (centre - average).Length > _chordTolerance;
        }

        private void SplitEdge(int triangle, int slot)
        {
            var a = _triangles[(3 * triangle) + slot];
            var b = _triangles[(3 * triangle) + ((slot + 1) % 3)];
            var key = MakeKey(a, b);
            var sharing = _edgeTriangles[key].ToArray();
            var (au, av) = _vertices[a];
            var (bu, bv) = _vertices[b];
            var mid = AddVertex((au + bu) * 0.5d, (av + bv) * 0.5d);

            foreach (var id in sharing)
            {
                var s = 0;
                for (; s < 3; s++)
                {
                    var p = _triangles[(3 * id) + s];
                    var q = _triangles[(3 * id) + ((s + 1) % 3)];
                    if (MakeKey(p, q) == key)
                    {
                        break;
                    }
                }

                var x = _triangles[(3 * id) + s];
                var y = _triangles[(3 * id) + ((s + 1) % 3)];
                var z = _triangles[(3 * id) + ((s + 2) % 3)];
                Kill(id);
                AddTriangle(x, mid, z);
                AddTriangle(mid, y, z);
            }
        }

        private void Kill(int id)
        {
            _alive[id] = false;
            _aliveCount--;
            for (var s = 0; s < 3; s++)
            {
                var key = EdgeKey(id, s);
                if (_edgeTriangles.TryGetValue(key, out var list))
                {
                    list.Remove(id);
                }
            }
        }

        private int OtherTriangle(long key, int self)
        {
            if (!_edgeTriangles.TryGetValue(key, out var list))
            {
                return -1;
            }

            foreach (var id in list)
            {
                if (id != self && _alive[id])
                {
                    return id;
                }
            }

            return -1;
        }

        private void LinkEdge(int a, int b, int triangle)
        {
            var key = MakeKey(a, b);
            if (!_edgeTriangles.TryGetValue(key, out var list))
            {
                list = new List<int>(2);
                _edgeTriangles[key] = list;
            }

            list.Add(triangle);
        }

        private static long MakeKey(int a, int b)
            => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        private long EdgeKey(int triangle, int slot)
            => MakeKey(_triangles[(3 * triangle) + slot], _triangles[(3 * triangle) + ((slot + 1) % 3)]);

        private double EdgeLength(int triangle, int slot)
        {
            var (au, av) = _vertices[_triangles[(3 * triangle) + slot]];
            var (bu, bv) = _vertices[_triangles[(3 * triangle) + ((slot + 1) % 3)]];
            var du = (bu - au) / _cellU;
            var dv = (bv - av) / _cellV;
            return double.Sqrt((du * du) + (dv * dv));
        }

        private double LongestEdgeLength(int triangle)
            => System.Math.Max(EdgeLength(triangle, 0), System.Math.Max(EdgeLength(triangle, 1), EdgeLength(triangle, 2)));

        private int LongestEdgeSlot(int triangle)
        {
            var best = 0;
            var bestLength = EdgeLength(triangle, 0);
            var bestKey = EdgeKey(triangle, 0);
            for (var s = 1; s < 3; s++)
            {
                var length = EdgeLength(triangle, s);
                var key = EdgeKey(triangle, s);
                if (length > bestLength + TieTolerance
                    || (System.Math.Abs(length - bestLength) <= TieTolerance && key < bestKey))
                {
                    best = s;
                    bestLength = length;
                    bestKey = key;
                }
            }

            return best;
        }
    }
}
