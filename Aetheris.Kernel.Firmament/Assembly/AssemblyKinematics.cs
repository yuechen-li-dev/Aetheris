using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Pure occurrence pose evaluation. State keys are Interface names; values are
/// degrees for Revolute and millimetres for Prismatic. Geometry definitions are reused.</summary>
public static class AssemblyKinematics
{
    public static AssemblyPoseResult Evaluate(AssemblyIr assembly, IReadOnlyDictionary<string, double> state)
    {
        var joints = assembly.Joints ?? [];
        var diagnostics = new List<AssemblyDiagnostic>();
        var movable = joints.Where(joint => joint.DegreesOfFreedom == 1)
            .ToDictionary(joint => joint.Name, StringComparer.Ordinal);
        foreach (var (name, value) in state)
            if (!movable.ContainsKey(name) || !double.IsFinite(value))
                diagnostics.Add(new("assembly-kinematic-invalid-state", $"State '{name}' must name a movable Interface and have a finite value."));
        if (diagnostics.Count > 0) return new([], diagnostics, new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
            state.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));
        var snapshot = new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
            movable.Values.ToDictionary(joint => joint.Name, joint => state.GetValueOrDefault(joint.Name, joint.DefaultState), StringComparer.Ordinal));

        var baseline = assembly.Instances.Where(instance => instance.ResolvedTransform is not null)
            .ToDictionary(instance => instance.StableId, instance => instance.ResolvedTransform!, StringComparer.Ordinal);
        var world = new Dictionary<string, AssemblyTransform>(baseline, StringComparer.Ordinal);
        var driven = joints.Select(joint => joint.ChildOccurrenceId).ToHashSet(StringComparer.Ordinal);
        var pending = joints.ToList();
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var progress = false;
            foreach (var joint in pending.ToArray())
            {
                if (driven.Contains(joint.ParentOccurrenceId) && !resolved.Contains(joint.ParentOccurrenceId)) continue;
                if (!world.TryGetValue(joint.ParentOccurrenceId, out var parentWorld))
                {
                    diagnostics.Add(new("assembly-kinematic-unresolved-parent", $"Interface '{joint.Name}' parent occurrence has no compiled zero pose."));
                    pending.Remove(joint); progress = true; continue;
                }
                var value = snapshot.GetValueOrDefault(joint.Name, joint.DefaultState);
                var motion = Motion(joint.Family, value);
                var child = Transform3D.FromRowMajor(joint.ChildLocalFrame.Matrix).Inverse()
                    * motion * Transform3D.FromRowMajor(joint.ParentLocalFrame.Matrix)
                    * Transform3D.FromRowMajor(parentWorld.Matrix);
                world[joint.ChildOccurrenceId] = new(child.ToRowMajor());
                resolved.Add(joint.ChildOccurrenceId);
                pending.Remove(joint); progress = true;
            }
            if (progress) continue;
            diagnostics.Add(new("assembly-kinematic-loop-unsupported", "Kinematic relationships contain an unresolved cycle."));
            break;
        }
        // Descendants without a joint retain their immutable local placement.
        foreach (var instance in assembly.Instances.OrderBy(instance => instance.Path.Segments.Count))
        {
            if (instance.ParentStableId is null || driven.Contains(instance.StableId)
                || !baseline.TryGetValue(instance.StableId, out var zero)
                || !baseline.TryGetValue(instance.ParentStableId, out var zeroParent)
                || !world.TryGetValue(instance.ParentStableId, out var currentParent)) continue;
            var relative = Transform3D.FromRowMajor(zero.Matrix) * Transform3D.FromRowMajor(zeroParent.Matrix).Inverse();
            world[instance.StableId] = new((relative * Transform3D.FromRowMajor(currentParent.Matrix)).ToRowMajor());
        }
        return new(assembly.Instances.Select(instance => instance with
        {
            ResolvedTransform = world.GetValueOrDefault(instance.StableId)
        }).ToArray(), diagnostics, snapshot);
    }

    private static Transform3D Motion(MechanicalInterfaceFamily family, double state)
    {
        var angle = family == MechanicalInterfaceFamily.Revolute ? state * Math.PI / 180d : 0;
        var cosine = Math.Cos(angle); var sine = Math.Sin(angle);
        return Transform3D.FromRowMajor([
            cosine, sine, 0, 0, -sine, cosine, 0, 0, 0, 0, 1, 0,
            0, 0, family == MechanicalInterfaceFamily.Prismatic ? state : 0, 1]);
    }
}

public sealed record AssemblyPoseResult(IReadOnlyList<AssemblyInstanceIr> Instances, IReadOnlyList<AssemblyDiagnostic> Diagnostics,
    IReadOnlyDictionary<string, double> State)
{
    public bool IsSuccess => Diagnostics.Count == 0;
}
