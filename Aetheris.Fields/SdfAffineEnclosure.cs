using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Backends.Sdf;

/// <summary>For x in the queried box, f(x) lies between
/// Value + Gradient.(x-center) - LowerError and the same affine term + UpperError.
/// Analytic curvature bounds, not fitted samples, supply the remainder.</summary>
public readonly record struct SdfAffineEnclosure(double Value, Vector3D Gradient, double LowerError, double UpperError);
public readonly record struct SdfRegionEnclosure(FieldInterval Interval, SdfAffineEnclosure? Affine);

public sealed partial class SdfTape
{
    public SdfRegionEnclosure EvaluateRegionEnclosure(SdfBounds region)
    {
        var ranges = new FieldInterval[SlotCount];
        EvaluateIntervalSlots(region, ranges);
        var root = ranges[OutputSlot];
        if (root.MinValue > 0 || root.MaxValue < 0) return new(root, null);
        var affine = new SdfAffineEnclosure?[SlotCount];
        foreach (var instruction in Instructions)
        {
            var a = instruction.InputA; var b = instruction.InputB;
            affine[instruction.DestSlot] = instruction.OpCode switch
            {
                SdfTapeOpCode.EvalSphere => SphereEnclosure(region, SpherePayloads[instruction.PayloadIndex]),
                SdfTapeOpCode.EvalCylinder => CylinderEnclosure(region, CylinderPayloads[instruction.PayloadIndex]),
                SdfTapeOpCode.EvalBox => BoxEnclosure(region, BoxPayloads[instruction.PayloadIndex]),
                SdfTapeOpCode.EvalTorus => TorusEnclosure(region, TorusPayloads[instruction.PayloadIndex]),
                SdfTapeOpCode.EvalCone => ConeEnclosure(region, ConePayloads[instruction.PayloadIndex]),
                SdfTapeOpCode.Min => ranges[a].MaxValue <= ranges[b].MinValue ? affine[a]
                    : ranges[b].MaxValue <= ranges[a].MinValue ? affine[b] : null,
                SdfTapeOpCode.Max => ranges[a].MinValue >= ranges[b].MaxValue ? affine[a]
                    : ranges[b].MinValue >= ranges[a].MaxValue ? affine[b] : null,
                SdfTapeOpCode.Neg when affine[a] is { } value => new(-value.Value, -value.Gradient, value.UpperError, value.LowerError),
                _ => null // Unsupported/singular branches retain the interval fallback.
            };
        }
        return new(root, affine[OutputSlot]);
    }

    private static Point3D Center(SdfBounds box) => box.Min + (box.Max - box.Min) * .5;
    private static double HalfDiagonalSquared(SdfBounds box) => (box.SizeX * box.SizeX + box.SizeY * box.SizeY + box.SizeZ * box.SizeZ) * .25;
    private static Vector3D PullGradient(Transform3D inverse, Vector3D local) => new(
        local.Dot(inverse.Apply(new Vector3D(1, 0, 0))), local.Dot(inverse.Apply(new Vector3D(0, 1, 0))),
        local.Dot(inverse.Apply(new Vector3D(0, 0, 1))));

    private static double NormRemainder(SdfBounds region, Transform3D inverse, Vector3D unit,
        double length, double fallback, bool radial = false)
    {
        var x = inverse.Apply(new Vector3D(region.SizeX / 2, 0, 0));
        var y = inverse.Apply(new Vector3D(0, region.SizeY / 2, 0));
        var z = inverse.Apply(new Vector3D(0, 0, region.SizeZ / 2));
        if (radial) { x = new(x.X, x.Y, 0); y = new(y.X, y.Y, 0); z = new(z.X, z.Y, 0); }
        var minProjection = length - double.Abs(unit.Dot(x)) - double.Abs(unit.Dot(y)) - double.Abs(unit.Dot(z));
        if (minProjection <= 0) return fallback;
        double perpendicularSquared = 0;
        // A convex quadratic reaches its maximum at a box corner. Opposite
        // corners have the same value, so four transformed deltas suffice.
        for (var corner = 0; corner < 4; corner++)
        {
            var delta = x + ((corner & 1) == 0 ? y : -y) + ((corner & 2) == 0 ? z : -z);
            var parallel = unit.Dot(delta);
            perpendicularSquared = double.Max(perpendicularSquared, delta.LengthSquared - parallel * parallel);
        }
        // ||q+d|| - (||q|| + unit.d) = |d_perp|^2 /
        // (||q+d|| + ||q|| + unit.d). The denominator is >= 2*minProjection.
        return double.Min(fallback, perpendicularSquared / (2 * minProjection));
    }

    private static SdfAffineEnclosure? SphereEnclosure(SdfBounds region, SdfTapeSpherePayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var minRadius = MinDistanceToAabbOrigin(local);
        if (minRadius <= 0) return null; // Norm Hessian is singular at its center.
        var p = payload.InverseTransform.Apply(Center(region));
        var v = new Vector3D(p.X, p.Y, p.Z); var length = v.Length;
        var unit = v / length;
        return new(length - payload.Radius, PullGradient(payload.InverseTransform, unit),
            0, NormRemainder(region, payload.InverseTransform, unit, length, HalfDiagonalSquared(local) / (2 * minRadius)));
    }

    private static SdfAffineEnclosure? CylinderEnclosure(SdfBounds region, SdfTapeCylinderPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var radial = RadiusInterval(local, payload.Radius);
        var axial = AxisAbsDistanceInterval(local.Min.Z, local.Max.Z, payload.Height / 2);
        var p = payload.InverseTransform.Apply(Center(region));
        // With the other extrusion component inside, the finite-cylinder SDF
        // equals its dominant radial or planar-cap distance over this whole box.
        if (axial.MaxValue <= 0 && radial.MinValue >= axial.MaxValue)
        {
            var minRadius = radial.MinValue + payload.Radius;
            if (minRadius <= 0) return null;
            var length = double.Sqrt(p.X * p.X + p.Y * p.Y);
            var unit = new Vector3D(p.X / length, p.Y / length, 0);
            return new(length - payload.Radius, PullGradient(payload.InverseTransform, unit),
                0, NormRemainder(region, payload.InverseTransform, unit, length,
                    (local.SizeX * local.SizeX + local.SizeY * local.SizeY) / (8 * minRadius), radial: true));
        }
        if (radial.MaxValue <= 0 && axial.MinValue >= radial.MaxValue && (local.Min.Z >= 0 || local.Max.Z <= 0))
        {
            var sign = p.Z >= 0 ? 1 : -1;
            return new(double.Abs(p.Z) - payload.Height / 2, PullGradient(payload.InverseTransform, new(0, 0, sign)), 0, 0);
        }
        return null;
    }

    private static SdfAffineEnclosure? BoxEnclosure(SdfBounds region, SdfTapeBoxPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var p = payload.InverseTransform.Apply(Center(region));
        double[] min = [local.Min.X, local.Min.Y, local.Min.Z];
        double[] max = [local.Max.X, local.Max.Y, local.Max.Z];
        double[] half = [payload.Width / 2, payload.Height / 2, payload.Depth / 2];
        double[] center = [p.X, p.Y, p.Z];
        var ranges = Enumerable.Range(0, 3).Select(i => AxisAbsDistanceInterval(min[i], max[i], half[i])).ToArray();
        for (var axis = 0; axis < 3; axis++)
        {
            if (min[axis] < 0 && max[axis] > 0) continue;
            if (Enumerable.Range(0, 3).Any(i => i != axis && (ranges[i].MaxValue > 0 || ranges[axis].MinValue < ranges[i].MaxValue))) continue;
            var sign = center[axis] >= 0 ? 1 : -1;
            var normal = axis == 0 ? new Vector3D(sign, 0, 0) : axis == 1 ? new Vector3D(0, sign, 0) : new Vector3D(0, 0, sign);
            return new(double.Abs(center[axis]) - half[axis], PullGradient(payload.InverseTransform, normal), 0, 0);
        }
        return null;
    }

    private static SdfAffineEnclosure? TorusEnclosure(SdfBounds region, SdfTapeTorusPayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var radial = RadiusInterval(local, payload.MajorRadius);
        var minRadius = radial.MinValue + payload.MajorRadius;
        var minTube = MinDistanceToRectOrigin(radial.MinValue, radial.MaxValue, local.Min.Z, local.Max.Z);
        if (minRadius <= 0 || minTube <= 0) return null;
        var p = payload.InverseTransform.Apply(Center(region));
        var radius = double.Sqrt(p.X * p.X + p.Y * p.Y);
        var d = radius - payload.MajorRadius;
        var tube = double.Sqrt(d * d + p.Z * p.Z);
        var gradient = new Vector3D(d * p.X / (tube * radius), d * p.Y / (tube * radius), p.Z / tube);
        // H(norm(rho-R,z)) <= 1/minTube + 1/minRadius. This bound
        // holds across both convex and concave tube quadrants.
        var error = .5 * HalfDiagonalSquared(local) * (1 / minTube + 1 / minRadius);
        return new(tube - payload.MinorRadius, PullGradient(payload.InverseTransform, gradient), error, error);
    }

    private static SdfAffineEnclosure? ConeEnclosure(SdfBounds region, SdfTapeConePayload payload)
    {
        var local = TransformBounds(region, payload.InverseTransform);
        var radial = RadiusInterval(local, 0);
        var p = payload.InverseTransform.Apply(Center(region));
        var slope = (payload.TopRadius - payload.BottomRadius) / payload.Height;
        var intercept = (payload.TopRadius + payload.BottomRadius) / 2;
        var norm = double.Sqrt(1 + slope * slope);
        var z0 = slope * local.Min.Z; var z1 = slope * local.Max.Z;
        var wall = new FieldInterval((radial.MinValue - double.Max(z0, z1) - intercept) / norm,
            (radial.MaxValue - double.Min(z0, z1) - intercept) / norm);
        var capGap = payload.Height / 2 - MaxAbsInInterval(local.Min.Z, local.Max.Z);
        var deltaRadius = payload.TopRadius - payload.BottomRadius;
        var denominator = deltaRadius * deltaRadius + payload.Height * payload.Height;
        if (denominator <= 1e-18) return null; // Scalar evaluator uses a degenerate projection, not this wall distance.
        var projections = new[]
        {
            ((payload.TopRadius - radial.MinValue) * deltaRadius + (payload.Height / 2 - local.Min.Z) * payload.Height) / denominator,
            ((payload.TopRadius - radial.MinValue) * deltaRadius + (payload.Height / 2 - local.Max.Z) * payload.Height) / denominator,
            ((payload.TopRadius - radial.MaxValue) * deltaRadius + (payload.Height / 2 - local.Min.Z) * payload.Height) / denominator,
            ((payload.TopRadius - radial.MaxValue) * deltaRadius + (payload.Height / 2 - local.Max.Z) * payload.Height) / denominator
        };
        // The nearest point lies inside the finite generator, and is closer
        // than either cap throughout the box: the SDF equals the wall distance.
        if (capGap > 0 && MaxAbsInInterval(wall.MinValue, wall.MaxValue) <= capGap
            && projections.Min() >= 0 && projections.Max() <= 1 && radial.MinValue > 0)
        {
            var radius = double.Sqrt(p.X * p.X + p.Y * p.Y);
            var unit = new Vector3D(p.X / radius, p.Y / radius, 0);
            return new((radius - slope * p.Z - intercept) / norm,
                PullGradient(payload.InverseTransform, new(p.X / (radius * norm), p.Y / (radius * norm), -slope / norm)),
                0, NormRemainder(region, payload.InverseTransform, unit, radius,
                    (local.SizeX * local.SizeX + local.SizeY * local.SizeY) / (8 * radial.MinValue), radial: true) / norm);
        }
        if (local.Min.Z >= 0 || local.Max.Z <= 0)
        {
            var sign = p.Z >= 0 ? 1 : -1;
            var cap = AxisAbsDistanceInterval(local.Min.Z, local.Max.Z, payload.Height / 2);
            var endRadius = sign > 0 ? payload.TopRadius : payload.BottomRadius;
            if (radial.MaxValue <= endRadius && (wall.MaxValue <= 0 || cap.MinValue >= 0)
                && MinAbsInInterval(wall.MinValue, wall.MaxValue) >= MaxAbsInInterval(cap.MinValue, cap.MaxValue))
                return new(double.Abs(p.Z) - payload.Height / 2, PullGradient(payload.InverseTransform, new(0, 0, sign)), 0, 0);
        }
        return null;
    }
}
