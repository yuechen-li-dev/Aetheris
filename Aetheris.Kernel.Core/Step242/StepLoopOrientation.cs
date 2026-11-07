using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

/// <summary>
/// STEP reads every face loop as running counter-clockwise about the face's outward normal (the surface normal,
/// negated when same_sense is false), outer loops counter-clockwise and holes clockwise, so that each edge of a closed
/// shell is used once in each direction. Kernel builders do not share one convention for coedge direction, and the
/// kernel's own consumers (tessellation, mass properties, the importer) never rely on it, so the writer derives it.
/// <para>
/// Two loops that share an edge must use it in opposite directions; that fixes every loop relative to its neighbours,
/// leaving one choice per connected set of loops. Planar faces decide it: their loops' signed areas about the outward
/// normal, the largest loop of each face counted as its outer boundary. A set with no planar loop keeps its authored
/// direction, as does any set whose edge uses cannot be made consistent.
/// </para>
/// </summary>
internal static class StepLoopOrientation
{
    /// <summary>The loops whose authored direction is reversed on export.</summary>
    public static IReadOnlySet<LoopId> ReversedLoops(BrepBody body, IReadOnlyDictionary<LoopId, IReadOnlyList<(Coedge Coedge, bool IsReversed)>> uses)
    {
        var byEdge = new Dictionary<EdgeId, List<(LoopId Loop, bool Reversed)>>();
        foreach (var (loop, loopUses) in uses)
            foreach (var use in loopUses)
            {
                if (!byEdge.TryGetValue(use.Coedge.EdgeId, out var list)) byEdge[use.Coedge.EdgeId] = list = [];
                list.Add((loop, use.IsReversed));
            }

        // flip[a] ^ flip[b] must equal (reversedA == reversedB) for every edge used once by a and once by b.
        var neighbours = uses.Keys.ToDictionary(loop => loop, _ => new List<(LoopId Other, bool Differ)>());
        foreach (var pair in byEdge.Values)
        {
            if (pair.Count != 2) continue;
            var (a, b) = (pair[0], pair[1]);
            if (a.Loop == b.Loop) continue; // a seam: its two uses do not depend on the loop's direction
            var differ = a.Reversed == b.Reversed;
            neighbours[a.Loop].Add((b.Loop, differ));
            neighbours[b.Loop].Add((a.Loop, differ));
        }

        var faceOfLoop = new Dictionary<LoopId, FaceId>();
        foreach (var face in body.Topology.Faces)
            foreach (var loop in face.LoopIds) faceOfLoop[loop] = face.Id;
        var reversed = new HashSet<LoopId>();
        var flip = new Dictionary<LoopId, bool>();
        foreach (var seed in uses.Keys.OrderBy(loop => loop.Value))
        {
            if (flip.ContainsKey(seed)) continue;
            var component = new List<LoopId>();
            var consistent = true;
            flip[seed] = false;
            var queue = new Queue<LoopId>([seed]);
            while (queue.Count > 0)
            {
                var loop = queue.Dequeue();
                component.Add(loop);
                foreach (var (other, differ) in neighbours[loop])
                {
                    var want = flip[loop] ^ differ;
                    if (flip.TryGetValue(other, out var have)) { if (have != want) consistent = false; continue; }
                    flip[other] = want;
                    queue.Enqueue(other);
                }
            }
            if (!consistent) continue;

            // Score the component's relative solution by its planar loops: positive when they wind as STEP reads them.
            var score = 0d;
            foreach (var group in component.GroupBy(loop => faceOfLoop.TryGetValue(loop, out var f) ? f : (FaceId?)null))
            {
                var face = group.Key;
                if (face is null || !body.Bindings.TryGetFaceBinding(face.Value, out var binding)
                    || !body.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface) || surface?.Plane is not { } plane) continue;
                var normal = plane.Normal.ToVector() * (binding.Orientation.IsAlignedWithSurface ? 1d : -1d);
                var areas = group.Select(loop => (Loop: loop, Area: SignedArea(body, uses[loop], normal) * (flip[loop] ? -1d : 1d))).ToArray();
                if (areas.Length == 0) continue;
                // The face's outer boundary is its largest loop, which may lie in another component (a bore's rim
                // is linked to the bore, not to the face's outline).
                var outer = body.Topology.GetFace(face.Value).LoopIds.Where(uses.ContainsKey)
                    .MaxBy(loop => double.Abs(SignedArea(body, uses[loop], normal)));
                foreach (var (loop, area) in areas) score += loop == outer ? area : -area;
            }
            var flipAll = score < 0d;
            foreach (var loop in component)
                if (flip[loop] ^ flipAll) reversed.Add(loop);
        }
        return reversed;
    }

    /// <summary>
    /// Planar faces whose outer loop, as written, runs clockwise about the outward normal the face binding declares.
    /// With every edge used once each way the loops agree with each other, so such a face's binding is the one that is
    /// wrong: its normal points into the material.
    /// </summary>
    public static IReadOnlyList<FaceId> FacesAgainstTheirLoops(BrepBody body, IReadOnlyDictionary<LoopId, IReadOnlyList<(Coedge Coedge, bool IsReversed)>> uses, IReadOnlySet<LoopId> reversed)
    {
        var result = new List<FaceId>();
        foreach (var face in body.Topology.Faces)
        {
            if (!body.Bindings.TryGetFaceBinding(face.Id, out var binding)
                || !body.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface) || surface?.Plane is not { } plane) continue;
            var normal = plane.Normal.ToVector() * (binding.Orientation.IsAlignedWithSurface ? 1d : -1d);
            var areas = face.LoopIds.Where(uses.ContainsKey)
                .Select(loop => SignedArea(body, uses[loop], normal) * (reversed.Contains(loop) ? -1d : 1d)).ToArray();
            if (areas.Length > 0 && areas.MaxBy(double.Abs) < 0d) result.Add(face.Id);
        }
        return result;
    }

    private static double SignedArea(BrepBody body, IReadOnlyList<(Coedge Coedge, bool IsReversed)> loop, Vector3D normal)
    {
        var points = new List<Point3D>();
        foreach (var (coedge, isReversed) in loop)
        {
            var samples = Sample(body, coedge.EdgeId);
            if (samples is null) return 0d;
            if (isReversed) samples.Reverse();
            points.AddRange(samples.Take(samples.Count - 1));
        }
        var sum = Vector3D.Zero;
        for (var i = 0; i < points.Count; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Count]);
            sum += new Vector3D(a.X, a.Y, a.Z).Cross(new Vector3D(b.X, b.Y, b.Z));
        }
        return sum.Dot(normal) / 2d;
    }

    /// <summary>Points along an edge from its start vertex to its end vertex.</summary>
    private static List<Point3D>? Sample(BrepBody body, EdgeId edgeId)
    {
        var edge = body.Topology.GetEdge(edgeId);
        if (!body.TryGetVertexPoint(edge.StartVertexId, out var start) || !body.TryGetVertexPoint(edge.EndVertexId, out var end)) return null;
        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding) || !body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null)
            return [start, end];
        Func<double, Point3D>? evaluate = curve.Kind switch
        {
            CurveGeometryKind.Circle3 when curve.Circle3 is { } circle => circle.Evaluate,
            CurveGeometryKind.Ellipse3 when curve.Ellipse3 is { } ellipse => ellipse.Evaluate,
            CurveGeometryKind.BSpline3 when curve.BSpline3 is { } spline => spline.Evaluate,
            CurveGeometryKind.Hyperbola3 when curve.Hyperbola3 is { } hyperbola => hyperbola.Evaluate,
            _ => null,
        };
        if (evaluate is null) return [start, end];
        var interval = binding.TrimInterval
            ?? (curve.BSpline3 is { } s ? new ParameterInterval(s.DomainStart, s.DomainEnd)
                : curve.Circle3 is { } c ? Arc(c.Center, c.XAxis.ToVector() * c.Radius, c.YAxis.ToVector() * c.Radius)
                : curve.Ellipse3 is { } e ? Arc(e.Center, e.XAxis.ToVector() * e.MajorRadius, e.YAxis.ToVector() * e.MinorRadius)
                : (ParameterInterval?)null);
        if (interval is not { } range) return [start, end];
        const int count = 48;
        var samples = Enumerable.Range(0, count + 1).Select(i => evaluate(range.Start + (range.End - range.Start) * i / count)).ToList();
        // The curve's parameter runs with the edge unless the binding says otherwise; an open edge's endpoints settle it.
        var forward = edge.StartVertexId == edge.EndVertexId
            ? binding.OrientedEdgeSense
            : (samples[0] - start).Length + (samples[^1] - end).Length <= (samples[0] - end).Length + (samples[^1] - start).Length;
        if (!forward) samples.Reverse();
        return samples;

        // An untrimmed arc runs between its vertices' angles, the way the binding's sense says (all the way round when
        // the edge is closed).
        ParameterInterval Arc(Point3D center, Vector3D x, Vector3D y)
        {
            double Angle(Point3D p) { var d = p - center; return double.Atan2(d.Dot(y) / y.Dot(y), d.Dot(x) / x.Dot(x)); }
            var a0 = Angle(start);
            var turn = 2d * double.Pi;
            var sweep = edge.StartVertexId == edge.EndVertexId ? turn : ((Angle(end) - a0) % turn + turn) % turn;
            if (!binding.OrientedEdgeSense) sweep -= turn;
            if (edge.StartVertexId == edge.EndVertexId && !binding.OrientedEdgeSense) sweep = -turn;
            return new ParameterInterval(double.Min(a0, a0 + sweep), double.Max(a0, a0 + sweep));
        }
    }
}
