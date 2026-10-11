using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record GarmentMeshPanel(string Name, string Identity, int VertexStart, int VertexCount,
    int IndexStart, int IndexCount, IReadOnlyDictionary<string, ImmutableArray<int>> Edges);

internal sealed record PanelMesh(List<Vector2> Points, List<int> Indices, Dictionary<string, List<int>> Edges);

/// <summary>Projection of resolved Profile boundaries through the kernel's polygon triangulator.
/// Conforming edge refinement preserves directed source spans; no garment-specific sketch kernel.</summary>
internal static class GarmentPanelMesher
{
    public static PanelMesh Mesh(GarmentPanel panel, float meshSize)
    {
        try
        {
            return MeshCore(panel, meshSize);
        }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("planar-mesh-", StringComparison.Ordinal))
        {
            throw new GarmentAuthoringException("garment-mesh-budget", panel.Name + ": " + exception.Message);
        }
    }

    private static PanelMesh MeshCore(GarmentPanel panel, float meshSize)
    {
        if (panel.Profile.Loops.Count != 1)
        {
            GarmentAuthoring.Fail("garment-profile-holes-unsupported", "This panel mesher currently requires one outer loop; cutouts must meet the outer boundary.");
        }
        var points = new List<Vector2>();
        var edges = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var segment in panel.Profile.Loops[0].Segments)
        {
            var sampled = Sample(segment.Geometry, meshSize);
            var boundary = new List<int>();
            for (int sample = 0; sample < sampled.Count - 1; sample++)
            {
                if (points.Count >= 8192)
                {
                    GarmentAuthoring.Fail("garment-mesh-budget", panel.Name + " exceeds 8192 boundary vertices; increase meshSize.");
                }
                boundary.Add(points.Count);
                points.Add(sampled[sample]);
            }
            // The endpoint is the next segment's first vertex (wrapped below).
            boundary.Add(points.Count);
            edges.Add(segment.Name, boundary);
        }
        foreach (var boundary in edges.Values)
        {
            if (boundary[^1] == points.Count)
            {
                boundary[^1] = 0;
            }
        }
        var polygon = points.Select(point => new Point3D(point.X * 1000d, point.Y * 1000d, 0)).ToArray();
        PlanarMeshQuality.ValidateFillBudget(polygon, meshSize * 800d);
        if (!PlanarPolygonTriangulator.TryTriangulate(polygon, new Vector3D(0, 0, 1), out var seed, out var failure))
        {
            GarmentAuthoring.Fail("garment-triangulation-failed", panel.Name + ": " + failure);
        }
        var indices = seed.ToList();
        // The kernel may elide collinear boundary samples. Reinsert them into their
        // containing triangle edge before refinement, retaining seam correspondence.
        for (int point = 0; point < points.Count; point++)
        {
            if (!indices.Contains(point))
            {
                InsertBoundaryPoint(point, points, indices);
            }
        }
        var filled = PlanarMeshQuality.FillInterior(polygon, indices, meshSize * 800d);
        points = filled.Points.Select(point => new Vector2((float)(point.X * .001), (float)(point.Y * .001))).ToList();
        indices = filled.Indices.ToList();
        for (int pass = 0; pass < 12; pass++)
        {
            indices = PlanarMeshQuality.Improve(points.Select(point =>
                new Point3D(point.X * 1000d, point.Y * 1000d, 0)).ToArray(), indices).ToList();
            var split = new Dictionary<(int, int), int>();
            for (int face = 0; face < indices.Count; face += 3)
            {
                Mark(indices[face], indices[face + 1]);
                Mark(indices[face + 1], indices[face + 2]);
                Mark(indices[face + 2], indices[face]);
            }
            if (split.Count == 0)
            {
                foreach (var edit in panel.Profile.BoundaryEdits ?? [])
                {
                    var boundary = new List<int>();
                    foreach (string descendant in edit.Descendants)
                    {
                        var next = edges[descendant];
                        if (boundary.Count > 0 && boundary[^1] != next[0])
                        {
                            GarmentAuthoring.Fail("garment-boundary-disconnected", panel.Identity + ".edges." + edit.Name);
                        }
                        boundary.AddRange(boundary.Count == 0 ? next : next.Skip(1));
                    }
                    if (!edges.TryAdd(edit.Name, boundary) && !edges[edit.Name].SequenceEqual(boundary))
                    {
                        GarmentAuthoring.Fail("garment-boundary-name-ambiguous", panel.Identity + ".edges." + edit.Name);
                    }
                }
                return new(points, indices, edges);
            }
            if (points.Count > 8192)
            {
                GarmentAuthoring.Fail("garment-mesh-budget", panel.Name + " exceeds 8192 vertices; increase meshSize.");
            }
            var refined = new List<int>();
            for (int face = 0; face < indices.Count; face += 3)
            {
                int a = indices[face];
                int b = indices[face + 1];
                int c = indices[face + 2];
                bool ab = split.TryGetValue(Key(a, b), out int x);
                bool bc = split.TryGetValue(Key(b, c), out int y);
                bool ca = split.TryGetValue(Key(c, a), out int z);
                int mask = (ab ? 1 : 0) | (bc ? 2 : 0) | (ca ? 4 : 0);
                switch (mask)
                {
                    case 0:
                        Triangle(a, b, c);
                        break;
                    case 1:
                        Triangle(a, x, c);
                        Triangle(x, b, c);
                        break;
                    case 2:
                        Triangle(b, y, a);
                        Triangle(y, c, a);
                        break;
                    case 4:
                        Triangle(c, z, b);
                        Triangle(z, a, b);
                        break;
                    case 3:
                        Triangle(b, y, x);
                        Triangle(a, x, c);
                        Triangle(x, y, c);
                        break;
                    case 5:
                        Triangle(a, x, z);
                        Triangle(x, b, c);
                        Triangle(x, c, z);
                        break;
                    case 6:
                        Triangle(c, z, y);
                        Triangle(b, y, a);
                        Triangle(y, z, a);
                        break;
                    case 7:
                        Triangle(a, x, z);
                        Triangle(x, b, y);
                        Triangle(z, y, c);
                        Triangle(x, y, z);
                        break;
                }
            }
            foreach (string name in edges.Keys.ToArray())
            {
                var boundary = edges[name];
                var refinedBoundary = new List<int>();
                for (int i = 0; i < boundary.Count - 1; i++)
                {
                    refinedBoundary.Add(boundary[i]);
                    if (split.TryGetValue(Key(boundary[i], boundary[i + 1]), out int midpoint))
                    {
                        refinedBoundary.Add(midpoint);
                    }
                }
                refinedBoundary.Add(boundary[^1]);
                edges[name] = refinedBoundary;
            }
            indices = refined;

            void Mark(int a, int b)
            {
                var key = Key(a, b);
                if (!split.ContainsKey(key) && Vector2.Distance(points[a], points[b]) > meshSize * 1.001f)
                {
                    if (points.Count >= 8192)
                    {
                        GarmentAuthoring.Fail("garment-mesh-budget", panel.Name + " exceeds 8192 vertices; increase meshSize.");
                    }
                    split.Add(key, points.Count);
                    points.Add((points[a] + points[b]) * .5f);
                }
            }
            void Triangle(int a, int b, int c) => refined.AddRange([a, b, c]);
        }
        GarmentAuthoring.Fail("garment-mesh-budget", "Panel refinement did not converge within 12 passes.");
        return null!;
    }

    private static List<Vector2> Sample(LineArcProfileCurve2D curve, float meshSize)
    {
        switch (curve)
        {
            case LineArcLineSegment2D line:
            {
                int count = SegmentCount(GarmentPatternInspection.CurveLength(line) / (meshSize * 800d));
                return Enumerable.Range(0, count + 1).Select(i => Vector2.Lerp(Point(line.Start), Point(line.End), (float)i / count)).ToList();
            }
            case LineArcCircularArc2D arc:
            {
                double tolerance = Math.Min(1, meshSize * 20d);
                double maximumAngle = 2 * Math.Acos(Math.Clamp(1 - tolerance / arc.Radius, -1, 1));
                int count = SegmentCount(Math.Max(Math.Abs(arc.SweepAngleRadians) * arc.Radius / (meshSize * 800),
                    Math.Abs(arc.SweepAngleRadians) / maximumAngle));
                return Enumerable.Range(0, count + 1).Select(i =>
                {
                    double angle = arc.StartAngleRadians + arc.SweepAngleRadians * i / count;
                    return Point((arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle)));
                }).ToList();
            }
            case LineArcCubicBezier2D cubic:
            {
                var sampled = new List<Vector2> { Point(cubic.Start) };
                Subdivide(cubic, 0);
                return sampled;

                void Subdivide(LineArcCubicBezier2D section, int depth)
                {
                    Vector2 start = Point(section.Start);
                    Vector2 end = Point(section.End);
                    Vector2 direction = end - start;
                    float length = direction.Length();
                    float deviation = Math.Max(DistanceToChord(Point(section.Control1)), DistanceToChord(Point(section.Control2)));
                    if (length <= meshSize * .8f && deviation <= Math.Min(.001f, meshSize * .02f))
                    {
                        if (sampled.Count >= 4097)
                        {
                            GarmentAuthoring.Fail("garment-curve-sampling-budget", "Cubic sampling exceeds 4096 segments.");
                        }
                        sampled.Add(end);
                        return;
                    }
                    if (depth == 16)
                    {
                        GarmentAuthoring.Fail("garment-curve-sampling-budget", "Cubic sampling did not converge within its bounded error/size budget.");
                    }
                    Subdivide(BezierSectionCurve2D.Trim(section, 0, .5), depth + 1);
                    Subdivide(BezierSectionCurve2D.Trim(section, .5, 1), depth + 1);

                    float DistanceToChord(Vector2 point)
                    {
                        if (length < 1e-10f)
                        {
                            return Vector2.Distance(point, start);
                        }
                        float t = Math.Clamp(Vector2.Dot(point - start, direction) / (length * length), 0, 1);
                        return Vector2.Distance(point, start + direction * t);
                    }
                }
            }
            default:
                GarmentAuthoring.Fail("garment-curve-unsupported", "Panels currently admit directed lines, circular arcs and cubic Beziers.");
                return null!;
        }
    }

    private static int SegmentCount(double required)
    {
        double count = Math.Max(1, Math.Ceiling(required));
        if (!double.IsFinite(count) || count > 4096)
        {
            GarmentAuthoring.Fail("garment-curve-sampling-budget", "Boundary sampling exceeds 4096 segments; increase meshSize.");
        }
        return (int)count;
    }

    private static void InsertBoundaryPoint(int vertex, List<Vector2> points, List<int> indices)
    {
        Vector2 point = points[vertex];
        for (int face = 0; face < indices.Count; face += 3)
        {
            for (int edge = 0; edge < 3; edge++)
            {
                int a = indices[face + edge];
                int b = indices[face + (edge + 1) % 3];
                int c = indices[face + (edge + 2) % 3];
                Vector2 direction = points[b] - points[a];
                float t = Vector2.Dot(point - points[a], direction) / direction.LengthSquared();
                if (t <= 0 || t >= 1 || Vector2.DistanceSquared(point, points[a] + direction * t) > 1e-12f)
                {
                    continue;
                }
                indices[face] = a;
                indices[face + 1] = vertex;
                indices[face + 2] = c;
                indices.AddRange([vertex, b, c]);
                return;
            }
        }
        GarmentAuthoring.Fail("garment-boundary-lost", "Kernel triangulation did not retain an addressable boundary point.");
    }

    private static Vector2 Point((double X, double Y) value) => new((float)value.X * .001f, (float)value.Y * .001f);
    private static (int, int) Key(int a, int b) => (Math.Min(a, b), Math.Max(a, b));
}
