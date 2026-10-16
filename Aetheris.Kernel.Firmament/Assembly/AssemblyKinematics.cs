using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Pure occurrence pose evaluation. State keys are realized Mate names (qualified by occurrence in reusable definitions); values are
/// degrees for Revolute and millimetres for Prismatic. Geometry definitions are reused.</summary>
public static class AssemblyKinematics
{
    internal static bool IsRigidPort(AssemblyInstanceIr occurrence, Aetheris.Semantics.SemanticValue value,
        IReadOnlyList<AssemblyInstanceIr> instances, IReadOnlyList<AssemblyJointIr> joints)
    {
        var owner = occurrence;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (value.TryBinding<AssemblyExposedSemanticBinding>(out var alias))
        {
            if (!seen.Add(value.StableIdentity)) return false;
            var target = AssemblyWorldQuery.ExposedTarget(instances, owner, alias);
            owner = target.Owner; value = target.Value;
        }
        var drivers = joints.DistinctBy(j => j.ChildOccurrenceId).ToDictionary(j => j.ChildOccurrenceId, StringComparer.Ordinal);
        seen.Clear();
        while (owner.StableId != occurrence.StableId)
        {
            if (!seen.Add(owner.StableId)) return false;
            string? parent;
            if (drivers.TryGetValue(owner.StableId, out var joint))
            {
                if (joint.DegreesOfFreedom > 0) return false;
                parent = joint.ParentOccurrenceId;
            }
            else parent = owner.ParentStableId;
            if (parent is null || instances.FirstOrDefault(i => i.StableId == parent) is not { } next) return false;
            owner = next;
        }
        return true;
    }

    internal static IReadOnlyList<AssemblyDiagnostic> ValidateGraph(IReadOnlyList<AssemblyInstanceIr> instances, IReadOnlyList<AssemblyJointIr> joints)
    {
        var diagnostics = new List<AssemblyDiagnostic>();
        foreach (var group in joints.GroupBy(j => j.ChildOccurrenceId).Where(g => g.Count() > 1))
            diagnostics.Add(new("assembly-kinematic-duplicate-child", $"Occurrence '{group.Key}' has multiple pose drivers: {string.Join(", ", group.Select(j => j.Name))}."));
        foreach (var group in joints.GroupBy(j => j.Name).Where(g => g.Count() > 1))
            diagnostics.Add(new("assembly-kinematic-duplicate-name", $"Realized Mate name '{group.Key}' is ambiguous."));
        var drivers = joints.DistinctBy(j => j.ChildOccurrenceId).ToDictionary(j => j.ChildOccurrenceId, StringComparer.Ordinal);
        var dependencies = instances.ToDictionary(i => i.StableId,
            i => drivers.TryGetValue(i.StableId, out var joint) ? joint.ParentOccurrenceId : i.ParentStableId, StringComparer.Ordinal);
        var complete = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in dependencies.Keys)
        {
            var active = new HashSet<string>(StringComparer.Ordinal);
            string? current = id;
            while (current is not null && !complete.Contains(current))
            {
                if (!active.Add(current))
                {
                    diagnostics.Add(new("assembly-kinematic-loop-unsupported", $"Kinematic dependency cycle at occurrence '{current}'."));
                    break;
                }
                if (!dependencies.TryGetValue(current, out var next))
                {
                    diagnostics.Add(new("assembly-kinematic-unresolved-parent", $"Kinematic dependency '{current}' is outside the assembly."));
                    break;
                }
                current = next;
            }
            complete.UnionWith(active);
        }
        return diagnostics;
    }

    public static AssemblyPoseResult Evaluate(AssemblyIr assembly, IReadOnlyDictionary<string, double> state)
    {
        var joints = (assembly.Joints ?? []).Concat(assembly.FrameDependencies).ToArray();
        var diagnostics = ValidateGraph(assembly.Instances, joints).ToList();
        var movable = joints.Where(joint => joint.DegreesOfFreedom == 1)
            .DistinctBy(joint => joint.Name).ToDictionary(joint => joint.Name, StringComparer.Ordinal);
        foreach (var (name, value) in state)
            if (!movable.ContainsKey(name) || !double.IsFinite(value))
                diagnostics.Add(new("assembly-kinematic-invalid-state", $"State '{name}' must name a movable realized Mate and have a finite value."));
        if (diagnostics.Count > 0) return new([], diagnostics, new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
            state.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));
        var snapshot = new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(
            movable.Values.ToDictionary(joint => joint.Name, joint => state.GetValueOrDefault(joint.Name, joint.DefaultState), StringComparer.Ordinal));

        var baseline = assembly.Instances.Where(instance => instance.ResolvedTransform is not null)
            .ToDictionary(instance => instance.StableId, instance => instance.ResolvedTransform!, StringComparer.Ordinal);
        var world = new Dictionary<string, AssemblyTransform>(StringComparer.Ordinal);
        var instances = assembly.Instances.ToDictionary(i => i.StableId, StringComparer.Ordinal);
        var drivers = joints.ToDictionary(j => j.ChildOccurrenceId, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);
        bool Resolve(string id)
        {
            if (world.ContainsKey(id)) return true;
            if (failed.Contains(id)) return false;
            if (!visiting.Add(id))
            {
                diagnostics.Add(new("assembly-kinematic-loop-unsupported", $"Kinematic dependency cycle at occurrence '{id}'."));
                failed.Add(id); return false;
            }
            try
            {
                if (!instances.TryGetValue(id, out var instance) || !baseline.TryGetValue(id, out var zero))
                {
                    diagnostics.Add(new("assembly-kinematic-unresolved-parent", $"Occurrence '{id}' has no compiled zero pose."));
                    failed.Add(id); return false;
                }
                if (drivers.TryGetValue(id, out var joint))
                {
                    if (!Resolve(joint.ParentOccurrenceId)) { failed.Add(id); return false; }
                    var child = Transform3D.FromRowMajor(joint.ChildLocalFrame.Matrix).Inverse()
                        * Motion(joint.Family, snapshot.GetValueOrDefault(joint.Name, joint.DefaultState))
                        * Transform3D.FromRowMajor(joint.ParentLocalFrame.Matrix)
                        * Transform3D.FromRowMajor(world[joint.ParentOccurrenceId].Matrix);
                    world[id] = new(child.ToRowMajor());
                }
                else if (instance.ParentStableId is { } parent)
                {
                    if (!Resolve(parent)) { failed.Add(id); return false; }
                    var relative = Transform3D.FromRowMajor(zero.Matrix) * Transform3D.FromRowMajor(baseline[parent].Matrix).Inverse();
                    world[id] = new((relative * Transform3D.FromRowMajor(world[parent].Matrix)).ToRowMajor());
                }
                else world[id] = zero;
                return true;
            }
            finally { visiting.Remove(id); }
        }
        // Resolve rigid ancestry and joints together. A joint on a rigid grandchild
        // of a moving parent must read that parent's current pose, never its rest pose.
        foreach (var instance in assembly.Instances) Resolve(instance.StableId);
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
