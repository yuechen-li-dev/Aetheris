using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI;

internal readonly record struct SculptSectionSegment(Point3D A, Point3D B);

internal sealed record SectionWheel(Point3D Center, double RadiusMm, double WidthMm)
{
    internal static SectionWheel Load(string path)
    {
        var imported = Step242Importer.ImportBody(File.ReadAllText(path));
        if (!imported.IsSuccess)
            throw new InvalidOperationException("mesh-compare-wheel-import-failed: The wheel STEP must import as one exact body.");
        var body = imported.Value;
        var faces = body.Topology.Faces.Select(face => body.TryGetFaceSurfaceGeometry(face.Id, out var support) ? support : null).ToArray();
        var cylinders = faces.Where(surface => surface?.Cylinder is not null).Select(surface => surface!.Cylinder!.Value).ToArray();
        var planes = faces.Where(surface => surface?.Plane is not null).Select(surface => surface!.Plane!.Value).ToArray();
        if (cylinders.Length != 1 || planes.Length != 2 || faces.Length != 3)
            throw new InvalidOperationException("mesh-compare-wheel-shape-unsupported: Use a full capped analytic cylinder.");
        var cylinder = cylinders[0];
        var axis = cylinder.Axis.ToVector();
        if (Math.Abs(axis.X) > 1e-12 || Math.Abs(axis.Z) > 1e-12 ||
            planes.Any(plane => plane.Normal.ToVector().Cross(axis).Length > 1e-12))
            throw new InvalidOperationException("mesh-compare-wheel-axis-unsupported: The cylinder and both caps must be transverse along model Y.");
        // Require both circular rims: a trimmed cylinder with the same support
        // inventory cannot be treated as occupying the full analytic cylinder.
        var circles = new List<double>();
        foreach (var edge in body.Topology.Edges)
        {
            if (!body.TryGetEdgeCurveGeometry(edge.Id, out var curve) || curve?.Circle3 is not { } circle) continue;
            if (edge.StartVertexId != edge.EndVertexId ||
                !body.Bindings.TryGetEdgeBinding(edge.Id, out var binding) || binding.TrimInterval is not { } trim ||
                Math.Abs((trim.End - trim.Start) - 2 * Math.PI) > 1e-7 ||
                Math.Abs(circle.Radius - cylinder.Radius) > 1e-7 ||
                Math.Abs(circle.Center.X - cylinder.Origin.X) > 1e-7 || Math.Abs(circle.Center.Z - cylinder.Origin.Z) > 1e-7)
                throw new InvalidOperationException("mesh-compare-wheel-shape-unsupported: The cylinder needs two full circular end rims.");
            circles.Add(circle.Center.Y);
        }
        var y0 = planes[0].Origin.Y;
        var y1 = planes[1].Origin.Y;
        if (circles.Count != 2 || Math.Abs(y0 - y1) <= 1e-7 ||
            Math.Abs(circles.Min() - Math.Min(y0, y1)) > 1e-7 || Math.Abs(circles.Max() - Math.Max(y0, y1)) > 1e-7)
            throw new InvalidOperationException("mesh-compare-wheel-shape-unsupported: Both circular rims must meet their transverse caps.");
        return new(new(cylinder.Origin.X, (y0 + y1) * 0.5, cylinder.Origin.Z), cylinder.Radius, Math.Abs(y1 - y0));
    }

    // Signed point distance to the exact finite cylinder in its imported pose.
    internal double Distance(Point3D p)
    {
        var r = Math.Sqrt((p.X - Center.X) * (p.X - Center.X) + (p.Z - Center.Z) * (p.Z - Center.Z)) - RadiusMm;
        var h = Math.Abs(p.Y - Center.Y) - WidthMm / 2;
        return Math.Sqrt(Math.Max(0, r) * Math.Max(0, r) + Math.Max(0, h) * Math.Max(0, h)) + Math.Min(0, Math.Max(r, h));
    }
}

internal sealed record WheelClearance(double LowerBoundMm, double SampledUpperBoundMm, double IntervalWidthMm,
    bool CenterOutsideShell, int SubtriangleSamples);

internal static class SculptCompareGeometry
{
    internal static IReadOnlyList<SculptSectionSegment> Section(TriangleMesh mesh, double y)
    {
        const double tolerance = 1e-8;
        var lines = new List<SculptSectionSegment>();
        foreach (var (a, b, c) in Triangles(mesh))
        {
            var p = new[] { a, b, c };
            if (p.All(v => v.Y > y + tolerance) || p.All(v => v.Y < y - tolerance)) continue;
            var hits = new List<Point3D>(3);
            for (var i = 0; i < 3; i++)
            {
                var first = p[i];
                var next = p[(i + 1) % 3];
                var u = first.Y - y;
                var v = next.Y - y;
                if (Math.Abs(u) <= tolerance) hits.Add(new(first.X, y, first.Z));
                else if (Math.Abs(v) > tolerance && u * v < 0)
                    hits.Add(first + (next - first) * (u / (u - v)));
            }
            if (hits.Count == 2 && (hits[1] - hits[0]).Length > tolerance)
                lines.Add(new(hits[0], hits[1]));
        }
        return lines;
    }

    internal static WheelClearance Clearance(TriangleMesh mesh, SectionWheel wheel)
    {
        const double tolerance = 0.025;
        const int sampleBudget = 1_000_000;
        var upper = mesh.Positions.Min(wheel.Distance);
        var samples = 0;
        var solidAngle = 0d;
        var pending = new Stack<(Point3D A, Point3D B, Point3D C)>();
        foreach (var triangle in Triangles(mesh))
        {
            var a0 = triangle.A - wheel.Center;
            var b0 = triangle.B - wheel.Center;
            var c0 = triangle.C - wheel.Center;
            var denominator = a0.Length * b0.Length * c0.Length + a0.Dot(b0) * c0.Length
                + b0.Dot(c0) * a0.Length + c0.Dot(a0) * b0.Length;
            solidAngle += 2 * Math.Atan2(a0.Dot(b0.Cross(c0)), denominator);
            pending.Push(triangle);
            while (pending.TryPop(out var current))
            {
                if (++samples > sampleBudget)
                    throw new InvalidOperationException("mesh-compare-clearance-budget-exceeded: The shell-cylinder interval did not converge within the sample budget.");
                var (a, b, c) = current;
                var centroid = a + ((b - a) + (c - a)) * (1d / 3);
                var distance = wheel.Distance(centroid);
                upper = Math.Min(upper, distance);
                // Signed distance is 1-Lipschitz. The farthest triangle point
                // from its centroid is a vertex, giving a valid lower bound.
                var radius = Math.Max((a - centroid).Length, Math.Max((b - centroid).Length, (c - centroid).Length));
                if (distance - radius >= upper - tolerance) continue;
                var ab = a + (b - a) * 0.5;
                var bc = b + (c - b) * 0.5;
                var ca = c + (a - c) * 0.5;
                pending.Push((a, ab, ca)); pending.Push((ab, b, bc)); pending.Push((ca, bc, c)); pending.Push((ab, bc, ca));
            }
        }

        // A closed, oriented triangle mesh winds around an interior point.
        return new(upper - tolerance, upper, tolerance, Math.Abs(solidAngle) < 2 * Math.PI, samples);
    }

    internal static IEnumerable<(Point3D A, Point3D B, Point3D C)> Triangles(TriangleMesh mesh)
    {
        var indices = mesh.TriangleIndices;
        for (var i = 0; i < indices.Count; i += 3)
            yield return (mesh.Positions[indices[i]], mesh.Positions[indices[i + 1]], mesh.Positions[indices[i + 2]]);
    }
}
