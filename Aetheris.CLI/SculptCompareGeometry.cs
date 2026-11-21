using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI;

internal readonly record struct SculptSectionSegment(Point3D A, Point3D B);

internal sealed record SectionWheel(Point3D Center, double RadiusMm, double WidthMm)
{
    private double SteerCos { get; init; } = 1;
    private double SteerSin { get; init; }

    internal SectionWheel AtPose(double steerDegrees, double bumpMm)
    {
        var angle = steerDegrees * Math.PI / 180;
        return this with { Center = Center + new Vector3D(0, 0, bumpMm), SteerCos = Math.Cos(angle), SteerSin = Math.Sin(angle) };
    }

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
        var offset = ToWheelFrame(p);
        return SignedCylinderDistance(Math.Abs(offset.X), Math.Abs(offset.Y), Math.Abs(offset.Z));
    }

    internal double TriangleLowerBound(Point3D a, Point3D b, Point3D c)
    {
        var u = ToWheelFrame(a); var v = ToWheelFrame(b); var w = ToWheelFrame(c);
        return SignedCylinderDistance(Nearest(u.X, v.X, w.X), Nearest(u.Y, v.Y, w.Y), Nearest(u.Z, v.Z, w.Z));

        static double Nearest(double a, double b, double c) =>
            Math.Max(0, Math.Max(Math.Min(a, Math.Min(b, c)), -Math.Max(a, Math.Max(b, c))));
    }

    private Vector3D ToWheelFrame(Point3D p)
    {
        var offset = p - Center;
        return new(offset.X * SteerCos + offset.Y * SteerSin, -offset.X * SteerSin + offset.Y * SteerCos, offset.Z);
    }

    private double SignedCylinderDistance(double x, double y, double z)
    {
        var r = Math.Sqrt(x * x + z * z) - RadiusMm;
        var h = y - WidthMm / 2;
        return Math.Sqrt(Math.Max(0, r) * Math.Max(0, r) + Math.Max(0, h) * Math.Max(0, h)) + Math.Min(0, Math.Max(r, h));
    }
}

internal sealed record WheelClearance(double LowerBoundMm, double SampledUpperBoundMm, double IntervalWidthMm,
    bool CenterOutsideShell, int SubtriangleSamples);

internal sealed record WheelPoseClearance(double SteerDegrees, double BumpMm, WheelClearance Clearance);
internal sealed record WheelSweepClearance(double SteeringLockDegrees, double SuspensionBumpMm,
    double MaximumDistanceFromNearestPoseMm, double LowerBoundMm, IReadOnlyList<WheelPoseClearance> Poses);

internal static class SculptCompareGeometry
{
    internal static WheelSweepClearance BoundWheelSweep(TriangleMesh mesh, SectionWheel wheel, double steerDegrees, double bumpMm)
    {
        const int stepsToLock = 4;
        var angles = steerDegrees > 0 ? Enumerable.Range(-stepsToLock, 2 * stepsToLock + 1)
            .Select(step => steerDegrees * step / stepsToLock).ToArray() : [0d];
        double[] bumps = bumpMm > 0 ? [0, bumpMm / 2, bumpMm] : [0];
        var poses = (from bump in bumps
                     from angle in angles
                     select new WheelPoseClearance(angle, bump, Clearance(mesh, wheel.AtPose(angle, bump), tolerance: 0.25))).ToArray();

        // Every point on the cylinder sits within this horizontal radius of
        // the steering pivot. Within one grid cell, yaw moves points by at
        // most the nearest-pose chord and bump moves them vertically.
        var halfYawStep = steerDegrees * (Math.PI / 180) / (2 * stepsToLock);
        var yawRadius = Math.Sqrt(wheel.RadiusMm * wheel.RadiusMm + wheel.WidthMm * wheel.WidthMm / 4);
        var horizontal = 2 * yawRadius * Math.Sin(halfYawStep / 2);
        var vertical = bumpMm / 4;
        var allowance = Math.Sqrt(horizontal * horizontal + vertical * vertical);
        return new(steerDegrees, bumpMm, allowance, poses.Min(pose => pose.Clearance.LowerBoundMm) - allowance, poses);
    }

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

    internal static WheelClearance Clearance(TriangleMesh mesh, SectionWheel wheel, double tolerance = 0.025)
    {
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
                // The signed distance is also monotone in the radius and the
                // absolute axial coordinate. The wheel-frame triangle bounds
                // can exclude a broad face parallel to an end cap at once.
                if (distance - radius >= upper - tolerance || wheel.TriangleLowerBound(a, b, c) >= upper - tolerance) continue;
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
