using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

public enum AssemblyMotionDimension { Time, Angle, Length }
public enum AssemblyMotionInterpolation { Linear, Hold, Cosine }
public enum AssemblyMotionRepeat { Once, Loop }
public sealed record AssemblyMotionKeyIr(string Name, double At, double Value, SemanticSourceSpan? SourceSpan);
public sealed record AssemblyMotionCurveIr(string Name, AssemblyMotionDimension Domain, AssemblyMotionDimension ValueDimension,
    AssemblyMotionInterpolation Interpolation, double? Period, IReadOnlyList<AssemblyMotionKeyIr> Keys);
public sealed record AssemblyMotionTrackIr(string Name, string MateStableId, string JointName, AssemblyMotionCurveIr Curve,
    ExpressionSyntax? Input, IReadOnlyList<string> Dependencies, SemanticSourceSpan? SourceSpan);
public sealed record AssemblyMotionIr(string Name, double DurationSeconds, AssemblyMotionRepeat Repeat,
    IReadOnlyList<AssemblyMotionTrackIr> Tracks, SemanticSourceSpan? SourceSpan);

/// <summary>Pure sampling of a compiled finite clip. There is no playback state,
/// source parsing, geometry rebuild or accumulated matrix update here.</summary>
public static class AssemblyMotion
{
    public static AssemblyPoseResult Evaluate(AssemblyIr assembly, string motion, double seconds, bool preserveEndpoint = false)
    {
        var clip = assembly.Motions.SingleOrDefault(m => m.Name == motion);
        if (clip is null || !double.IsFinite(seconds))
            return new([], [new("assembly-motion-invalid-query", $"Motion '{motion}' requires an admitted clip and finite time in seconds.")], new Dictionary<string, double>());
        var time = clip.Repeat == AssemblyMotionRepeat.Loop && !(preserveEndpoint && seconds == clip.DurationSeconds)
            ? Wrap(seconds, clip.DurationSeconds) : Math.Clamp(seconds, 0, clip.DurationSeconds);
        var state = (assembly.Joints ?? []).Where(j => j.DegreesOfFreedom == 1).ToDictionary(j => j.Name, j => j.DefaultState, StringComparer.Ordinal);
        var variables = Variables(assembly, state);
        var joints = (assembly.Joints ?? []).ToDictionary(j => j.MateStableId, StringComparer.Ordinal);
        var writers = clip.Tracks.Select(t => t.MateStableId).ToHashSet(StringComparer.Ordinal);
        var complete = new HashSet<string>(StringComparer.Ordinal);
        var diagnostics = new List<AssemblyDiagnostic>();
        bool Track(AssemblyMotionTrackIr track)
        {
            if (complete.Contains(track.MateStableId)) return true;
            if (track.Dependencies.Any(dependency => writers.Contains(dependency) && !complete.Contains(dependency)))
            { diagnostics.Add(new("assembly-motion-driver-cycle", $"Motion '{motion}' has an invalid compiled driver order at '{track.Name}'.") { SourceSpan = track.SourceSpan }); return false; }
            var input = time;
            if (track.Input is not null)
            {
                if (!FirmamentNumericEvaluation.TryEvaluate(track.Input, out var value, variables) || value.Unit != Unit(track.Curve.Domain))
                { diagnostics.Add(new("assembly-motion-input-invalid", $"Track '{track.Name}' input did not produce finite {track.Curve.Domain}.") { SourceSpan = track.SourceSpan }); return false; }
                input = value.Number;
            }
            var sampled = Sample(track.Curve, input);
            if (!double.IsFinite(sampled))
            { diagnostics.Add(new("assembly-motion-value-invalid", $"Track '{track.Name}' produced a nonfinite value.") { SourceSpan = track.SourceSpan }); return false; }
            state[track.JointName] = sampled;
            foreach (var name in CoordinateNames(assembly.Name, joints[track.MateStableId], track.Curve.ValueDimension))
                variables[name] = new(sampled, Unit(track.Curve.ValueDimension));
            complete.Add(track.MateStableId); return true;
        }
        foreach (var track in clip.Tracks) if (!Track(track)) return new([], diagnostics, state);
        return AssemblyKinematics.Evaluate(assembly, state);
    }

    public static double Sample(AssemblyMotionCurveIr curve, double input)
    {
        if (!double.IsFinite(input)) throw new ArgumentOutOfRangeException(nameof(input));
        if (curve.Period is { } period) input = Wrap(input, period);
        if (input <= curve.Keys[0].At) return curve.Keys[0].Value;
        for (var i = 1; i < curve.Keys.Count; i++)
        {
            var end = curve.Keys[i]; if (input > end.At) continue;
            if (input == end.At) return end.Value;
            var start = curve.Keys[i - 1]; var t = (input - start.At) / (end.At - start.At);
            var weight = curve.Interpolation switch
            { AssemblyMotionInterpolation.Hold => 0, AssemblyMotionInterpolation.Cosine => (1 - Math.Cos(Math.PI * t)) / 2, _ => t };
            return start.Value + (end.Value - start.Value) * weight;
        }
        return curve.Keys[^1].Value;
    }

    internal static Dictionary<string, FirmamentNumericEvaluation.Value> Variables(AssemblyIr assembly, IReadOnlyDictionary<string, double> state)
    {
        var result = new Dictionary<string, FirmamentNumericEvaluation.Value>(StringComparer.Ordinal);
        foreach (var joint in assembly.Joints ?? [])
        {
            var dimension = joint.Family == MechanicalInterfaceFamily.Prismatic ? AssemblyMotionDimension.Length : AssemblyMotionDimension.Angle;
            foreach (var name in CoordinateNames(assembly.Name, joint, dimension))
                result[name] = new(state.GetValueOrDefault(joint.Name, joint.DefaultState), Unit(dimension));
        }
        return result;
    }
    internal static IEnumerable<string> CoordinateNames(string assembly, AssemblyJointIr joint, AssemblyMotionDimension dimension)
    {
        var suffix = dimension == AssemblyMotionDimension.Angle ? ".angle" : ".translation";
        foreach (var name in joint.PublicNames)
        {
            yield return name + suffix;
            if (!name.StartsWith(assembly + ".", StringComparison.Ordinal)) yield return assembly + "." + name + suffix;
        }
    }
    internal static string Unit(AssemblyMotionDimension dimension) => dimension switch { AssemblyMotionDimension.Time => "s", AssemblyMotionDimension.Angle => "deg", _ => "mm" };
    private static double Wrap(double value, double period) => (value % period + period) % period;
}
