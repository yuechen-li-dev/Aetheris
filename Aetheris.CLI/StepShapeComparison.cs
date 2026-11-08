using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using System.Globalization;

namespace Aetheris.CLI;

/// <summary>
/// Geometric comparison of a candidate body against a reference body, for reconstruction work: which analytic
/// carriers (planes, cylinders, cones, spheres, tori) each side has that the other lacks, and where material
/// differs. Carriers are compared by canonical parameters, not face ids, so a reconstruction that splits or
/// merges faces differently still matches; material is compared along parallel Z rays through both
/// tessellations, so the overlap figures are approximate to the display chord tolerance.
/// </summary>
public static class StepShapeComparison
{
    public sealed record CarrierGroup(string Kind, string Description, IReadOnlyList<int> FaceIds, double[]? BoundsMin, double[]? BoundsMax);

    public sealed record CarrierComparison(
        int ReferenceCarrierCount,
        int CandidateCarrierCount,
        int MatchedCarrierCount,
        IReadOnlyList<CarrierGroup> MissingFromCandidate,
        IReadOnlyList<CarrierGroup> ExtraInCandidate);

    public sealed record DiscrepancyRegion(string Side, double Volume, int ColumnCount, double[] BoundsMin, double[] BoundsMax);

    public sealed record OverlapComparison(
        int ColumnsX,
        int ColumnsY,
        double CellSize,
        double ReferenceVolume,
        double CandidateVolume,
        double ReferenceOnlyVolume,
        double CandidateOnlyVolume,
        double IntersectionOverUnion,
        int UnpairedReferenceColumns,
        int UnpairedCandidateColumns,
        IReadOnlyList<DiscrepancyRegion> Regions,
        int SuppressedSingleColumnRegions,
        string Basis);

    public sealed record ShapeComparisonResult(CarrierComparison Carriers, OverlapComparison Overlap);

    public static ShapeComparisonResult Compare(string referencePath, string candidatePath, int resolution = 400)
    {
        var (_, reference) = StepAnalyzer.ImportStepBody(referencePath);
        var (_, candidate) = StepAnalyzer.ImportStepBody(candidatePath);
        return Compare(reference, candidate, resolution);
    }

    public static ShapeComparisonResult Compare(BrepBody reference, BrepBody candidate, int resolution = 400)
    {
        var referenceMesh = Tessellate(reference);
        var candidateMesh = Tessellate(candidate);
        var (min, max) = UnionBounds(referenceMesh, candidateMesh);
        var diagonal = (max - min).Length;
        var linear = double.Max(1e-4, diagonal * 1e-6);
        var carriers = CompareCarriers(reference, referenceMesh, candidate, candidateMesh, linear);
        var overlap = CompareOverlap(referenceMesh, candidateMesh, min, max, resolution);
        return new ShapeComparisonResult(carriers, overlap);
    }

    private static DisplayTessellationResult Tessellate(BrepBody body)
        => BrepDisplayTessellator.TessellateBoundedPartial(body, DisplayTessellationOptions.Default with { ChordTolerance = 0.02d }, TimeSpan.FromSeconds(30));

    private static (Point3D Min, Point3D Max) UnionBounds(params DisplayTessellationResult[] meshes)
    {
        double x0 = double.PositiveInfinity, y0 = x0, z0 = x0, x1 = double.NegativeInfinity, y1 = x1, z1 = x1;
        foreach (var p in meshes.SelectMany(m => m.FacePatches).SelectMany(p => p.Positions))
        {
            x0 = double.Min(x0, p.X); y0 = double.Min(y0, p.Y); z0 = double.Min(z0, p.Z);
            x1 = double.Max(x1, p.X); y1 = double.Max(y1, p.Y); z1 = double.Max(z1, p.Z);
        }

        if (!double.IsFinite(x0)) throw new InvalidOperationException("Shape comparison requires tessellated faces on both sides.");
        return (new Point3D(x0, y0, z0), new Point3D(x1, y1, z1));
    }

    // ---- Carriers ---------------------------------------------------------------------------------------------

    private sealed record Carrier(string Kind, double[] Key, bool[] Angular, string Description);

    private static CarrierComparison CompareCarriers(BrepBody reference, DisplayTessellationResult referenceMesh, BrepBody candidate, DisplayTessellationResult candidateMesh, double linear)
    {
        var referenceGroups = GroupCarriers(reference, referenceMesh, linear);
        var candidateGroups = GroupCarriers(candidate, candidateMesh, linear);
        var matchedReference = new bool[referenceGroups.Count];
        var matchedCandidate = new bool[candidateGroups.Count];
        for (var i = 0; i < referenceGroups.Count; i++)
        {
            for (var j = 0; j < candidateGroups.Count; j++)
            {
                if (!Same(referenceGroups[i].Carrier, candidateGroups[j].Carrier, linear)) continue;
                matchedReference[i] = true;
                matchedCandidate[j] = true;
            }
        }

        return new CarrierComparison(
            referenceGroups.Count,
            candidateGroups.Count,
            matchedReference.Count(m => m),
            referenceGroups.Where((_, i) => !matchedReference[i]).Select(g => g.Group).ToArray(),
            candidateGroups.Where((_, j) => !matchedCandidate[j]).Select(g => g.Group).ToArray());
    }

    private static List<(Carrier Carrier, CarrierGroup Group)> GroupCarriers(BrepBody body, DisplayTessellationResult mesh, double linear)
    {
        var patches = mesh.FacePatches.GroupBy(p => p.FaceId.Value).ToDictionary(g => g.Key, g => g.SelectMany(p => p.Positions).ToArray());
        var groups = new List<(Carrier Carrier, List<int> Faces)>();
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            var carrier = body.TryGetFaceSurface(face.Id, out var surface) && surface is not null
                ? Canonical(surface)
                : new Carrier("Unknown", [], [], "unresolved surface");
            var existing = groups.FindIndex(g => Same(g.Carrier, carrier, linear));
            if (existing >= 0) groups[existing].Faces.Add(face.Id.Value);
            else groups.Add((carrier, [face.Id.Value]));
        }

        return groups.Select(g =>
        {
            var points = g.Faces.Where(patches.ContainsKey).SelectMany(f => patches[f]).ToArray();
            double[]? lo = points.Length == 0 ? null : [Round(points.Min(p => p.X)), Round(points.Min(p => p.Y)), Round(points.Min(p => p.Z))];
            double[]? hi = points.Length == 0 ? null : [Round(points.Max(p => p.X)), Round(points.Max(p => p.Y)), Round(points.Max(p => p.Z))];
            return (g.Carrier, new CarrierGroup(g.Carrier.Kind, g.Carrier.Description, g.Faces, lo, hi));
        }).ToList();
    }

    private static bool Same(Carrier a, Carrier b, double linear)
    {
        if (a.Kind != b.Kind || a.Key.Length != b.Key.Length) return false;
        if (a.Key.Length == 0) return false; // unknown/freeform carriers never match by parameters
        for (var i = 0; i < a.Key.Length; i++)
        {
            var tolerance = a.Angular[i] ? 1e-6d : linear;
            if (double.Abs(a.Key[i] - b.Key[i]) > tolerance) return false;
        }

        return true;
    }

    private static Carrier Canonical(SurfaceGeometry surface)
    {
        switch (surface.Kind)
        {
            case SurfaceGeometryKind.Plane when surface.Plane is { } plane:
            {
                var n = CanonicalDirection(plane.Normal.ToVector());
                var d = n.Dot(plane.Origin - Point3D.Origin);
                return new Carrier("Plane", [n.X, n.Y, n.Z, d], [true, true, true, false],
                    $"plane n=({F(n.X)}, {F(n.Y)}, {F(n.Z)}) d={F(d)}");
            }
            case SurfaceGeometryKind.Cylinder when surface.Cylinder is { } cylinder:
            {
                var a = CanonicalDirection(cylinder.Axis.ToVector());
                var p = AxisFoot(cylinder.Origin, a);
                return new Carrier("Cylinder", [a.X, a.Y, a.Z, p.X, p.Y, p.Z, cylinder.Radius], [true, true, true, false, false, false, false],
                    $"cylinder r={F(cylinder.Radius)} axis=({F(a.X)}, {F(a.Y)}, {F(a.Z)}) through ({F(p.X)}, {F(p.Y)}, {F(p.Z)})");
            }
            case SurfaceGeometryKind.Cone when surface.Cone is { } cone:
            {
                var a = CanonicalDirection(cone.Axis.ToVector());
                var half = double.Abs(cone.SemiAngleRadians);
                return new Carrier("Cone", [a.X, a.Y, a.Z, cone.Apex.X, cone.Apex.Y, cone.Apex.Z, half], [true, true, true, false, false, false, true],
                    $"cone half-angle={F(half * 180d / double.Pi)}deg apex=({F(cone.Apex.X)}, {F(cone.Apex.Y)}, {F(cone.Apex.Z)}) axis=({F(a.X)}, {F(a.Y)}, {F(a.Z)})");
            }
            case SurfaceGeometryKind.Sphere when surface.Sphere is { } sphere:
                return new Carrier("Sphere", [sphere.Center.X, sphere.Center.Y, sphere.Center.Z, sphere.Radius], [false, false, false, false],
                    $"sphere r={F(sphere.Radius)} center=({F(sphere.Center.X)}, {F(sphere.Center.Y)}, {F(sphere.Center.Z)})");
            case SurfaceGeometryKind.Torus when surface.Torus is { } torus:
            {
                var a = CanonicalDirection(torus.Axis.ToVector());
                return new Carrier("Torus", [a.X, a.Y, a.Z, torus.Center.X, torus.Center.Y, torus.Center.Z, torus.MajorRadius, torus.MinorRadius],
                    [true, true, true, false, false, false, false, false],
                    $"torus R={F(torus.MajorRadius)} r={F(torus.MinorRadius)} center=({F(torus.Center.X)}, {F(torus.Center.Y)}, {F(torus.Center.Z)}) axis=({F(a.X)}, {F(a.Y)}, {F(a.Z)})");
            }
            default:
                return new Carrier(surface.Kind.ToString(), [], [], $"{surface.Kind} (compared by count only)");
        }
    }

    private static Vector3D CanonicalDirection(Vector3D v)
    {
        var u = v / v.Length;
        var flip = double.Abs(u.X) > 1e-9 ? u.X < 0 : double.Abs(u.Y) > 1e-9 ? u.Y < 0 : u.Z < 0;
        return flip ? -u : u;
    }

    private static Point3D AxisFoot(Point3D origin, Vector3D axis)
    {
        var o = origin - Point3D.Origin;
        var foot = o - axis * o.Dot(axis);
        return Point3D.Origin + foot;
    }

    // ---- Overlap ----------------------------------------------------------------------------------------------

    private readonly record struct Triangle(double Ax, double Ay, double Az, double Bx, double By, double Bz, double Cx, double Cy, double Cz);

    private static OverlapComparison CompareOverlap(DisplayTessellationResult referenceMesh, DisplayTessellationResult candidateMesh, Point3D min, Point3D max, int resolution)
    {
        resolution = int.Clamp(resolution, 16, 4096);
        var spanX = max.X - min.X;
        var spanY = max.Y - min.Y;
        var cell = double.Max(spanX, spanY) / resolution;
        var nx = int.Max(1, (int)double.Ceiling(spanX / cell));
        var ny = int.Max(1, (int)double.Ceiling(spanY / cell));
        var referenceColumns = CastColumns(referenceMesh, min, cell, nx, ny, out var unpairedReference);
        var candidateColumns = CastColumns(candidateMesh, min, cell, nx, ny, out var unpairedCandidate);
        var area = cell * cell;

        double referenceVolume = 0, candidateVolume = 0, referenceOnly = 0, candidateOnly = 0, union = 0;
        var referenceOnlyColumns = new (double Length, double ZMin, double ZMax)[nx * ny];
        var candidateOnlyColumns = new (double Length, double ZMin, double ZMax)[nx * ny];
        for (var k = 0; k < nx * ny; k++)
        {
            if (referenceColumns[k] is not { } r || candidateColumns[k] is not { } c) continue;
            referenceVolume += Length(r) * area;
            candidateVolume += Length(c) * area;
            union += Length(Combine(r, c, (a, b) => a || b)) * area;
            var rOnly = Combine(r, c, (a, b) => a && !b);
            var cOnly = Combine(r, c, (a, b) => b && !a);
            referenceOnly += Length(rOnly) * area;
            candidateOnly += Length(cOnly) * area;
            referenceOnlyColumns[k] = Summarize(rOnly);
            candidateOnlyColumns[k] = Summarize(cOnly);
        }

        // Thin slivers along curved walls come from the two tessellations' chords disagreeing, not from the shapes.
        var noise = 4d * 0.02d;
        var regions = new List<DiscrepancyRegion>();
        var suppressed = 0;
        foreach (var (side, columns) in new[] { ("reference-only", referenceOnlyColumns), ("candidate-only", candidateOnlyColumns) })
        {
            foreach (var region in Cluster(columns, nx, ny, noise))
            {
                if (region.Count <= 1) { suppressed++; continue; }
                var volume = region.Sum(k => columns[k].Length) * area;
                var xs = region.Select(k => min.X + (k % nx) * cell).ToArray();
                var ys = region.Select(k => min.Y + (k / nx) * cell).ToArray();
                regions.Add(new DiscrepancyRegion(side, Round(volume), region.Count,
                    [Round(xs.Min()), Round(ys.Min()), Round(region.Min(k => columns[k].ZMin))],
                    [Round(xs.Max() + cell), Round(ys.Max() + cell), Round(region.Max(k => columns[k].ZMax))]));
            }
        }

        return new OverlapComparison(nx, ny, Round(cell), Round(referenceVolume), Round(candidateVolume), Round(referenceOnly), Round(candidateOnly),
            union > 0 ? double.Round((union - referenceOnly - candidateOnly) / union, 6) : 1d,
            unpairedReference, unpairedCandidate,
            regions.OrderByDescending(r => r.Volume).Take(40).ToArray(), suppressed,
            "Parallel +Z rays at cell centres through each side's display tessellation (chord 0.02); inside intervals by crossing parity.");
    }

    private static List<double>?[] CastColumns(DisplayTessellationResult mesh, Point3D min, double cell, int nx, int ny, out int unpaired)
    {
        var bins = new List<Triangle>[nx * ny];
        foreach (var patch in mesh.FacePatches)
        {
            for (var i = 0; i + 2 < patch.TriangleIndices.Count; i += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[i]];
                var b = patch.Positions[patch.TriangleIndices[i + 1]];
                var c = patch.Positions[patch.TriangleIndices[i + 2]];
                var t = new Triangle(a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z);
                var i0 = int.Clamp((int)double.Floor((double.Min(a.X, double.Min(b.X, c.X)) - min.X) / cell), 0, nx - 1);
                var i1 = int.Clamp((int)double.Floor((double.Max(a.X, double.Max(b.X, c.X)) - min.X) / cell), 0, nx - 1);
                var j0 = int.Clamp((int)double.Floor((double.Min(a.Y, double.Min(b.Y, c.Y)) - min.Y) / cell), 0, ny - 1);
                var j1 = int.Clamp((int)double.Floor((double.Max(a.Y, double.Max(b.Y, c.Y)) - min.Y) / cell), 0, ny - 1);
                for (var j = j0; j <= j1; j++)
                for (var k = i0; k <= i1; k++)
                    (bins[j * nx + k] ??= []).Add(t);
            }
        }

        // An irrational offset keeps ray centres off mesh vertices and edges, which sit on round numbers.
        const double jitterX = 0.5d + 0.0012345678d;
        const double jitterY = 0.5d - 0.0008765432d;
        var columns = new List<double>?[nx * ny];
        var odd = 0;
        Parallel.For(0, nx * ny, k =>
        {
            var x = min.X + ((k % nx) + jitterX) * cell;
            var y = min.Y + ((k / nx) + jitterY) * cell;
            var hits = new List<double>();
            foreach (var t in bins[k] ?? [])
            {
                if (TryHitZ(t, x, y, out var z)) hits.Add(z);
            }

            hits.Sort();
            // An odd crossing count (a ray grazing a seam or apex) cannot be classified; it is skipped on
            // both sides rather than read as empty, which would show as a false difference.
            if (hits.Count % 2 != 0)
            {
                Interlocked.Increment(ref odd);
                columns[k] = null;
                return;
            }

            columns[k] = hits;
        });
        unpaired = odd;
        return columns;
    }

    private static bool TryHitZ(Triangle t, double x, double y, out double z)
    {
        z = 0;
        var d = (t.By - t.Cy) * (t.Ax - t.Cx) + (t.Cx - t.Bx) * (t.Ay - t.Cy);
        if (double.Abs(d) < 1e-14) return false;
        var l1 = ((t.By - t.Cy) * (x - t.Cx) + (t.Cx - t.Bx) * (y - t.Cy)) / d;
        var l2 = ((t.Cy - t.Ay) * (x - t.Cx) + (t.Ax - t.Cx) * (y - t.Cy)) / d;
        var l3 = 1 - l1 - l2;
        if (l1 < 0 || l2 < 0 || l3 < 0) return false;
        z = l1 * t.Az + l2 * t.Bz + l3 * t.Cz;
        return true;
    }

    private static double Length(List<double> intervals)
    {
        var sum = 0d;
        for (var i = 0; i + 1 < intervals.Count; i += 2) sum += intervals[i + 1] - intervals[i];
        return sum;
    }

    private static List<double> Combine(List<double> a, List<double> b, Func<bool, bool, bool> op)
    {
        var events = a.Select(z => (z, 0)).Concat(b.Select(z => (z, 1))).OrderBy(e => e.z).ToArray();
        var inside = new bool[2];
        var result = new List<double>();
        var was = false;
        foreach (var (z, side) in events)
        {
            inside[side] = !inside[side];
            var now = op(inside[0], inside[1]);
            if (now != was) result.Add(z);
            was = now;
        }

        if (result.Count % 2 != 0) result.RemoveAt(result.Count - 1);
        // Coincident faces on both sides (a shared bottom plane) leave zero-length slivers; they are not material.
        var kept = new List<double>(result.Count);
        for (var i = 0; i + 1 < result.Count; i += 2)
        {
            if (result[i + 1] - result[i] <= 1e-6) continue;
            kept.Add(result[i]);
            kept.Add(result[i + 1]);
        }

        return kept;
    }

    private static (double Length, double ZMin, double ZMax) Summarize(List<double> intervals)
        => intervals.Count == 0 ? (0, 0, 0) : (Length(intervals), intervals[0], intervals[^1]);

    private static List<List<int>> Cluster((double Length, double ZMin, double ZMax)[] columns, int nx, int ny, double noise)
    {
        var seen = new bool[columns.Length];
        var regions = new List<List<int>>();
        for (var start = 0; start < columns.Length; start++)
        {
            if (seen[start] || columns[start].Length <= noise) continue;
            var region = new List<int>();
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                var k = stack.Pop();
                region.Add(k);
                var (i, j) = (k % nx, k / nx);
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var (ii, jj) = (i + di, j + dj);
                    if (ii < 0 || jj < 0 || ii >= nx || jj >= ny) continue;
                    var n = jj * nx + ii;
                    if (seen[n] || columns[n].Length <= noise) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }

            regions.Add(region);
        }

        return regions;
    }

    private static double Round(double value) => double.Round(value, 4);

    private static string F(double value) => double.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
