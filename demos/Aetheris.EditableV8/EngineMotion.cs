using Aetheris.Kernel.Core.Mechanisms;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.EditableV8;

public sealed record EngineCylinder(int Number, double BankRadians, double CrankpinRadians, double Z, double IgnitionDegrees);
public sealed record EngineBinding(string Name, string Kind, int Cylinder, string Category, double[] Rest, string? OccurrenceId = null,
    IReadOnlyList<int>? CylinderMembership = null, EngineSpringAxis? SpringAxis = null);
public sealed record EngineSpringAxis(double[] LocalOrigin, double[] LocalDirection, string StemOccurrenceId);
public sealed record EngineMotionManifest(string Schema, V8Spec Spec, int CycleDegrees, double CamRatio,
    int[] FiringOrder, IReadOnlyList<EngineCylinder> Cylinders, IReadOnlyList<EngineBinding> Bindings);

public static class EngineMotion
{
    public static EngineMotionManifest BindAssembly(EngineMotionManifest motion, AssemblyM1CompilationResult assembly)
    {
        if (!assembly.IsSuccess) throw new ArgumentException("engine-motion-unqualified-assembly");
        if (assembly.Ir!.FitResults.Any(f => !f.Compatible)) throw new ArgumentException("engine-motion-incompatible-interface-fit");
        var instances = assembly.Ir!.Instances.ToDictionary(i => i.Path.Segments.Last(), StringComparer.Ordinal);
        var zShift = instances["MainJournal1"].ResolvedTransform!.Matrix[14] - motion.Bindings.Single(b => b.Name == "MainJournal1").Rest[14];
        return motion with {
            Cylinders = motion.Cylinders.Select(c => c with { Z = c.Z + zShift }).ToArray(),
            Bindings = motion.Bindings.Select(b => {
                var instance = instances[b.Name]; EngineSpringAxis? springAxis = null;
                if (b.Kind.EndsWith("Spring", StringComparison.Ordinal))
                {
                    if (!instance.SemanticRoot.ExposedMembers["Winding"].ExposedMembers["Axis"].TryBinding<ExactAxisBinding>(out var axis))
                        throw new InvalidOperationException("engine-spring-axis-missing:" + b.Name);
                    springAxis = new([axis.OriginX, axis.OriginY, axis.OriginZ], [axis.DirectionX, axis.DirectionY, axis.DirectionZ],
                        instances[b.Name.Replace("Spring", "Stem", StringComparison.Ordinal)].StableId);
                }
                return b with { OccurrenceId = instance.StableId, Rest = instance.ResolvedTransform!.Matrix.ToArray(), SpringAxis = springAxis };
            }).ToArray()
        };
    }

    public static double[] Evaluate(EngineMotionManifest manifest, EngineBinding binding, double degrees)
    {
        if (!double.IsFinite(degrees) || binding.Rest.Length != 16 || binding.Rest.Any(v => !double.IsFinite(v)))
            throw new ArgumentException("engine-motion-invalid-transform-or-angle");
        if (binding.Kind is not ("Crank" or "Cam" or "Fixed" or "Rod" or "Piston" or "Intake" or "Exhaust" or "IntakeSpring" or "ExhaustSpring"))
            throw new ArgumentException("engine-motion-unknown-kind:" + binding.Kind);
        var transform = binding.Rest.ToArray();
        var angle = degrees * double.Pi / 180;
        if (binding.Kind is "Crank" or "Cam")
        {
            var turn = binding.Kind == "Cam" ? PrescribedMotion.AngularRatio(angle, manifest.CamRatio) : angle;
            var s = double.Sin(turn); var c = double.Cos(turn);
            for (var i = 0; i < 12; i += 4)
            {
                transform[i] = c * binding.Rest[i] + s * binding.Rest[i + 1];
                transform[i + 1] = -s * binding.Rest[i] + c * binding.Rest[i + 1];
            }
            if (binding.Kind == "Crank")
            {
                transform[12] = c * binding.Rest[12] + s * binding.Rest[13];
                transform[13] = -s * binding.Rest[12] + c * binding.Rest[13];
            }
            return transform;
        }
        if (binding.Kind == "Fixed") return transform;
        var cylinder = manifest.Cylinders.Single(c => c.Number == binding.Cylinder);
        var mechanism = new SliderCrank(manifest.Spec.CrankRadius, manifest.Spec.RodLength);
        var pose = mechanism.Evaluate(angle, cylinder.BankRadians, cylinder.CrankpinRadians);
        if (binding.Kind == "Rod")
        {
            var s = double.Sin(pose.RodAngleRadians); var c = double.Cos(pose.RodAngleRadians);
            return [c,-s,0,0, s,c,0,0, 0,0,1,0, pose.CrankPin.X,pose.CrankPin.Y,cylinder.Z,1];
        }
        double displacement;
        if (binding.Kind == "Piston")
            displacement = pose.PistonPositionMm - mechanism.Evaluate(0, cylinder.BankRadians, cylinder.CrankpinRadians).PistonPositionMm;
        else
        {
            var intake = binding.Kind.StartsWith("Intake", StringComparison.Ordinal);
            var opening = intake ? manifest.Spec.IntakeOpeningDegrees : manifest.Spec.ExhaustOpeningDegrees;
            var lift = PrescribedMotion.ValveLift(degrees - cylinder.IgnitionDegrees, opening, manifest.Spec.ValveDurationDegrees, manifest.Spec.ValveLift, manifest.CycleDegrees);
            var initial = PrescribedMotion.ValveLift(-cylinder.IgnitionDegrees, opening, manifest.Spec.ValveDurationDegrees, manifest.Spec.ValveLift, manifest.CycleDegrees);
            displacement = -(lift - initial);
            if (binding.Kind.EndsWith("Spring", StringComparison.Ordinal))
            {
                // Compress along the solved winding axis, about its actual seating origin.
                // The coil's source-local Z axis need not coincide with its winding axis.
                var datum = binding.SpringAxis ?? throw new InvalidOperationException("engine-spring-axis-missing:" + binding.Name);
                var rest = Transform3D.FromRowMajor(binding.Rest);
                var origin = rest.Apply(new Point3D(datum.LocalOrigin[0], datum.LocalOrigin[1], datum.LocalOrigin[2]));
                var direction = rest.Apply(new Vector3D(datum.LocalDirection[0], datum.LocalDirection[1], datum.LocalDirection[2]));
                direction /= direction.Length;
                var scale = (manifest.Spec.SpringFreeHeight - lift) / (manifest.Spec.SpringFreeHeight - initial);
                for (var i = 0; i < 16; i += 4)
                {
                    var vector = new Vector3D(transform[i], transform[i + 1], transform[i + 2]);
                    var relative = i == 12 ? vector - new Vector3D(origin.X, origin.Y, origin.Z) : vector;
                    vector += direction * ((scale - 1) * relative.Dot(direction));
                    transform[i] = vector.X; transform[i + 1] = vector.Y; transform[i + 2] = vector.Z;
                }
                return transform;
            }
        }
        transform[12] += double.Sin(cylinder.BankRadians) * displacement;
        transform[13] += double.Cos(cylinder.BankRadians) * displacement;
        return transform;
    }

    public static object ValidateSpringAlignment(EngineMotionManifest manifest)
    {
        double distance = 0, angle = 0, seatDrift = 0; var count = 0;
        static Vector3D Apply(double[] m, double[] v, bool point) => new(
            v[0]*m[0]+v[1]*m[4]+v[2]*m[8]+(point?m[12]:0),
            v[0]*m[1]+v[1]*m[5]+v[2]*m[9]+(point?m[13]:0),
            v[0]*m[2]+v[1]*m[6]+v[2]*m[10]+(point?m[14]:0));
        foreach (var spring in manifest.Bindings.Where(b => b.SpringAxis != null))
        {
            var datum = spring.SpringAxis!;
            var stem = manifest.Bindings.Single(b => b.OccurrenceId == datum.StemOccurrenceId);
            var seat = Apply(spring.Rest, datum.LocalOrigin, true);
            for (var degree = 0; degree <= 720; degree++)
            {
                var springMatrix = Evaluate(manifest, spring, degree); var stemMatrix = Evaluate(manifest, stem, degree);
                var origin = Apply(springMatrix, datum.LocalOrigin, true);
                var a = Apply(springMatrix, datum.LocalDirection, false); a /= a.Length;
                var b = Apply(stemMatrix, [0,0,1], false); b /= b.Length;
                var stemOrigin = Apply(stemMatrix, [0,0,0], true);
                distance = double.Max(distance, (origin - stemOrigin).Cross(b).Length);
                angle = double.Max(angle, double.Atan2(a.Cross(b).Length, a.Dot(b)));
                seatDrift = double.Max(seatDrift, (origin - seat).Length); count++;
            }
        }
        if (count != manifest.Cylinders.Count * 2 * 721 || distance > 1e-9 || angle > 1e-9 || seatDrift > 1e-9)
            throw new InvalidOperationException($"engine-spring-axis-sweep-failed:distance={distance};angle={angle};seat={seatDrift};samples={count}");
        return new { status = "pass", samples = count, maximumAxisDistanceMm = distance, maximumAxisAngleRadians = angle, maximumSeatDriftMm = seatDrift };
    }

    public static object Validate(EngineMotionManifest manifest)
    {
        double rod = 0, axis = 0, radius = 0, headClearance = double.PositiveInfinity, valveClearance = double.PositiveInfinity;
        double rodSleeve = double.PositiveInfinity, rodFloor = double.PositiveInfinity;
        var mechanism = new SliderCrank(manifest.Spec.CrankRadius, manifest.Spec.RodLength);
        foreach (var cylinder in manifest.Cylinders)
        for (var degree = 0; degree <= 720; degree++)
        {
            var pose = mechanism.Evaluate(degree * double.Pi / 180, cylinder.BankRadians, cylinder.CrankpinRadians);
            rod = double.Max(rod, double.Abs((pose.WristPin - pose.CrankPin).Length - manifest.Spec.RodLength));
            axis = double.Max(axis, pose.WristPin.Cross(pose.BoreAxis).Length);
            radius = double.Max(radius, double.Abs(pose.CrankPin.Length - manifest.Spec.CrankRadius));
            var pistonTop = pose.PistonPositionMm + manifest.Spec.PistonCompressionHeight;
            headClearance = double.Min(headClearance, manifest.Spec.DeckHeight + 8 - pistonTop);
            var lift = double.Max(PrescribedMotion.ValveLift(degree - cylinder.IgnitionDegrees, 360, 180, manifest.Spec.ValveLift),
                PrescribedMotion.ValveLift(degree - cylinder.IgnitionDegrees, 180, 180, manifest.Spec.ValveLift));
            valveClearance = double.Min(valveClearance, manifest.Spec.DeckHeight + 4 - lift - pistonTop);
            var corners = new[] { (-26d, -24d), (26d, -24d), (26d, manifest.Spec.RodLength + 24), (-26d, manifest.Spec.RodLength + 24) }
                .Select(p => (X: pose.CrankPin.X + double.Cos(pose.RodAngleRadians) * p.Item1 + double.Sin(pose.RodAngleRadians) * p.Item2,
                    Y: pose.CrankPin.Y - double.Sin(pose.RodAngleRadians) * p.Item1 + double.Cos(pose.RodAngleRadians) * p.Item2)).ToArray();
            rodFloor = double.Min(rodFloor, corners.Min(p => p.Y) - manifest.Spec.CrankcaseFloor);
            var projected = corners.Select(p => (Axial: p.X * pose.BoreAxis.X + p.Y * pose.BoreAxis.Y,
                Lateral: p.X * pose.BoreAxis.Y - p.Y * pose.BoreAxis.X)).ToList();
            var sleeveBottom = manifest.Spec.DeckHeight - manifest.Spec.Stroke - 29;
            var clipped = new List<(double Axial, double Lateral)>();
            for (var i = 0; i < projected.Count; i++)
            {
                var a = projected[i]; var b = projected[(i + 1) % projected.Count];
                if (a.Axial >= sleeveBottom) clipped.Add(a);
                if ((a.Axial >= sleeveBottom) != (b.Axial >= sleeveBottom))
                {
                    var t = (sleeveBottom - a.Axial) / (b.Axial - a.Axial);
                    clipped.Add((sleeveBottom, a.Lateral + t * (b.Lateral - a.Lateral)));
                }
            }
            if (clipped.Count > 0)
                rodSleeve = double.Min(rodSleeve, manifest.Spec.Bore / 2 - double.Sqrt(clipped.Max(p => p.Lateral * p.Lateral) + 6 * 6));
        }
        if (rod > 1e-9 || axis > 1e-9 || radius > 1e-9 || headClearance < 1 || valveClearance < 1 || rodSleeve < 1 || rodFloor < 1)
            throw new InvalidOperationException($"engine-sweep-failed:rod={rod};axis={axis};radius={radius};head={headClearance};valve={valveClearance};rod-sleeve={rodSleeve};rod-floor={rodFloor}");
        foreach (var cylinder in manifest.Cylinders)
        {
            var ignition = mechanism.Evaluate(cylinder.IgnitionDegrees * double.Pi / 180, cylinder.BankRadians, cylinder.CrankpinRadians);
            if (double.Abs(ignition.PistonPositionMm - manifest.Spec.CrankRadius - manifest.Spec.RodLength) > 1e-9)
                throw new InvalidOperationException("engine-firing-not-at-tdc:" + cylinder.Number);
        }
        return new { status = "pass", samples = 721 * manifest.Cylinders.Count, rodClosureMm = rod, pistonAxisMm = axis, crankRadiusMm = radius,
            headClearanceMm = headClearance, valvePistonClearanceMm = valveClearance, camAdvanceDegrees = 720 * manifest.CamRatio,
            rodSleeveClearanceMm = rodSleeve, rodFloorClearanceMm = rodFloor,
            crankWebFloorClearanceMm = -manifest.Spec.CrankWebEnvelope - manifest.Spec.CrankcaseFloor,
            pairedRodAxialClearanceMm = 4, rodWebAxialClearanceMm = 2,
            rodSupportAxialClearanceMm = manifest.Spec.CylinderSpacing / 2 - 8 - 14,
            clearanceScope = "parallel crown/head/valve planes, clipped conservative rod box against own bore, rod floor, web swept disk vs floor, axial rod/rod/web/support slabs; other interference not certified" };
    }
}
