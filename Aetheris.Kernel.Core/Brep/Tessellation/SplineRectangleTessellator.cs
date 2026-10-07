using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>Bounded display sampling for rectangular spline trims; native knots prevent periodic aliasing.</summary>
internal static class SplineRectangleTessellator
{
    public static DisplayFaceMeshPatch? Tessellate(FaceId faceId, BSplineSurfaceWithKnots support,
        double uStart, double uEnd, double vStart, double vEnd,
        Func<double, double, Vector3D> normal, DisplayTessellationOptions options,
        Action? checkBudget, int maximumTriangles, out bool incomplete)
    {
        var grid = PlanGrid(support, uStart, uEnd, vStart, vEnd, options, checkBudget, maximumTriangles, out incomplete);
        if (grid is null) return null;
        var us = grid.Value.U; var vs = grid.Value.V;
        var positions = new List<Point3D>(us.Count * vs.Count);
        var normals = new List<Vector3D>(us.Count * vs.Count);
        var indices = new List<int>((us.Count - 1) * (vs.Count - 1) * 6);
        var cells = new List<SurfaceMeshCell>((us.Count - 1) * (vs.Count - 1));
        foreach (var v in vs)
        {
            checkBudget?.Invoke();
            foreach (var u in us) { positions.Add(support.Evaluate(u, v)); normals.Add(normal(u, v)); }
        }
        for (var j = 0; j < vs.Count - 1; j++) for (var i = 0; i < us.Count - 1; i++)
        {
            var a = j * us.Count + i; var b = a + 1; var c = a + us.Count; var d = c + 1;
            cells.Add(new QuadCell([a, c, d, b]));
        }
        foreach (var cell in cells)
            SurfaceMeshIrTessellator.AppendQuadTriangles(positions, cell.VertexIds, indices);
        return new(faceId, positions, normals, indices, Cells: cells);
    }

    // Shared by rectangles and local decomposition of nonrectangular trims.
    internal static (List<double> U, List<double> V)? PlanGrid(BSplineSurfaceWithKnots support,
        double uStart, double uEnd, double vStart, double vEnd,
        DisplayTessellationOptions options, Action? checkBudget, int maximumTriangles, out bool incomplete)
    {
        var us = Seeds(support.KnotValuesU, uStart, uEnd, true);
        var vs = Seeds(support.KnotValuesV, vStart, vEnd, false);
        if (us.Count - 1 > options.MaximumSegments || vs.Count - 1 > options.MaximumSegments
            || (long)(us.Count - 1) * (vs.Count - 1) * 2 > maximumTriangles)
        { incomplete = true; return null; }
        // Inspect across every original knot span, not just the opposite axis midpoint.
        // A long helicoidal support can return to the same point between coarse samples.
        var crossU = CrossSamples(us); var crossV = CrossSamples(vs);
        var limited = false;
        Refine(us, crossV, true);
        Refine(vs, crossU, false);
        // UV units can be arbitrarily stretched. Balance physical cell edges after
        // chord refinement, preserving native knot lines and the same work limits.
        for (var pass = 0; pass < 8; pass++)
        {
            var uLengths = SpanLengths(us, true); var vLengths = SpanLengths(vs, false);
            var changed = Balance(us, uLengths, Median(vLengths), true);
            changed |= Balance(vs, vLengths, Median(uLengths), false);
            if (!changed) break;
        }
        incomplete = limited;
        return (us, vs);

        double[] SpanLengths(List<double> values, bool alongU)
        {
            var otherStart = alongU ? vStart : uStart; var otherEnd = alongU ? vEnd : uEnd;
            return values.Zip(values.Skip(1), (start, end) =>
            {
                checkBudget?.Invoke();
                return new[] { .25d, .5d, .75d }.Max(fraction =>
                {
                    var other = otherStart + (otherEnd - otherStart) * fraction;
                    return (alongU ? support.Evaluate(end, other) - support.Evaluate(start, other)
                        : support.Evaluate(other, end) - support.Evaluate(other, start)).Length;
                });
            }).ToArray();
        }

        double Median(double[] lengths)
        {
            var valid = lengths.Where(length => length > options.ChordTolerance * .01d).Order().ToArray();
            return valid.Length == 0 ? 0d : valid[valid.Length / 2];
        }

        bool Balance(List<double> values, double[] lengths, double transverse, bool alongU)
        {
            if (transverse <= 0d) return false;
            var changed = false;
            for (var i = lengths.Length - 1; i >= 0; i--)
            {
                if (lengths[i] <= System.Math.Max(options.ChordTolerance, transverse * 4d)) continue;
                var otherCount = alongU ? vs.Count : us.Count;
                if (values.Count > options.MaximumSegments || (long)values.Count * (otherCount - 1) * 2 > maximumTriangles)
                { limited = true; continue; }
                values.Insert(i + 1, (values[i] + values[i + 1]) * .5d); changed = true;
            }
            return changed;
        }

        List<double> Seeds(IReadOnlyList<double> knots, double start, double end, bool alongU)
        {
            var sorted = knots.Where(k => k > start && k < end)
                .Concat(Enumerable.Range(0, options.MinimumSegments + 1).Select(i => start + (end - start) * i / options.MinimumSegments))
                .Append(start).Append(end).Distinct().Order().ToList();
            // Uniform seeds can land within rounding distance of a native knot.
            // Merge only nearly coincident lines with sampled physical separation
            // below one ten-thousandth of the display chord budget. Kernel knots
            // remain untouched; this avoids vanishing-width display cell rows.
            var merged = new List<double> { start };
            foreach (var value in sorted.Skip(1))
            {
                if (value - merged[^1] <= (end - start) * 1e-6d
                    && new[] { 0d, .5d, 1d }.All(fraction =>
                    {
                        var other = alongU ? vStart + (vEnd - vStart) * fraction : uStart + (uEnd - uStart) * fraction;
                        return (alongU ? support.Evaluate(value, other) - support.Evaluate(merged[^1], other)
                            : support.Evaluate(other, value) - support.Evaluate(other, merged[^1])).Length <= options.ChordTolerance * 1e-4d;
                    }))
                {
                    if (value == end) merged[^1] = end;
                    continue;
                }
                merged.Add(value);
            }
            return merged;
        }

        static double[] CrossSamples(List<double> values) => values.Concat(values.Zip(values.Skip(1), (a, b) => (a + b) * .5d)).Order().ToArray();

        void Refine(List<double> values, double[] cross, bool alongU)
        {
            for (var i = 0; i < values.Count - 1;)
            {
                checkBudget?.Invoke();
                var start = values[i]; var end = values[i + 1];
                var failed = cross.Any(other => NeedsSplit(start, end, other, alongU));
                if (!failed) { i++; continue; }
                var otherCount = alongU ? vs.Count : us.Count;
                if (values.Count > options.MaximumSegments || (long)values.Count * (otherCount - 1) * 2 > maximumTriangles
                    || end - start <= (alongU ? uEnd - uStart : vEnd - vStart) * 1e-9d)
                { limited = true; i++; continue; }
                values.Insert(i + 1, (start + end) * .5d);
            }
        }

        bool NeedsSplit(double start, double end, double other, bool alongU)
        {
            Point3D At(double t) => alongU ? support.Evaluate(t, other) : support.Evaluate(other, t);
            var a = At(start); var b = At(end);
            foreach (var fraction in new[] { .25d, .5d, .75d })
            {
                var p = At(start + (end - start) * fraction);
                var chord = a + (b - a) * fraction;
                // Half the display budget per direction reserves error for mixed-cell diagonals.
                if ((p - chord).Length > options.ChordTolerance * .5d) return true;
            }
            return false;
        }
    }
}
