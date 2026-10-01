using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Bounded frame authoring; lowers layout to the existing frame constraint solver.</summary>
internal static class AssemblyFrameAuthoring
{
    internal static AssemblyFrameTransformSource? Parse(string name, string body, string targetField,
        string sourceIdentity, List<AssemblyDiagnostic> diagnostics)
    {
        var remaining = body;
        string? Field(string pattern, string group = "value")
        {
            var matches = Regex.Matches(remaining, pattern, RegexOptions.CultureInvariant);
            if (matches.Count > 1) diagnostics.Add(new("assembly-frame-duplicate-field", $"Frame '{name}' repeats a field."));
            if (matches.Count == 0) return null;
            remaining = Regex.Replace(remaining, pattern, "", RegexOptions.CultureInvariant);
            return matches[0].Groups[group].Value;
        }
        var from = Field($@"\b{targetField}\s*:\s*(?<value>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;?");
        var translate = Field(@"\bTranslateLocal\s*:\s*\[(?<value>[^]]+)\]\s*;?");
        var normal = Field(@"\bNormal\s*:\s*\[(?<value>[^]]+)\]\s*;?");
        var up = Field(@"\bUp\s*:\s*\[(?<value>[^]]+)\]\s*;?");
        var rotate = Field(@"\bRotateLocal\s*:\s*\{(?<value>[^{}]*)\}\s*;?");
        var axis = "Z"; double angle = 0;
        if (rotate is not null)
        {
            var m = Regex.Match(rotate, @"^\s*Axis\s*:\s*(?<axis>X|Y|Z)\s*;\s*Angle\s*:\s*(?<angle>[-+0-9.eE]+)deg\s*;?\s*$");
            if (!m.Success || !double.TryParse(m.Groups["angle"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out angle) || !double.IsFinite(angle))
                diagnostics.Add(new("assembly-frame-invalid-rotation", $"Frame '{name}' requires Axis X/Y/Z and a finite Angle in deg."));
            axis = m.Success ? m.Groups["axis"].Value : "Z";
        }
        if (from is null || !string.IsNullOrWhiteSpace(remaining.Replace(";", "")))
            diagnostics.Add(new("assembly-frame-invalid-fields", $"Frame '{name}' requires {targetField}; unknown or malformed fields: {remaining.Trim()}."));
        if ((normal is null) != (up is null)) diagnostics.Add(new("assembly-frame-invalid-basis", $"Frame '{name}' requires both Normal and Up."));
        double[]? Vector(string? value, bool lengths)
        {
            if (value is null) return null;
            var values = value.Split(',', StringSplitOptions.TrimEntries);
            var result = new double[3];
            if (values.Length != 3) { diagnostics.Add(new("assembly-frame-invalid-vector", $"Frame '{name}' requires three components.")); return null; }
            for (var i = 0; i < 3; i++)
            {
                var text = values[i];
                if (lengths && !text.EndsWith("mm", StringComparison.Ordinal))
                    diagnostics.Add(new("assembly-frame-invalid-unit", $"Frame '{name}' translation components require mm."));
                if (lengths && text.EndsWith("mm", StringComparison.Ordinal)) text = text[..^2];
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]) || !double.IsFinite(result[i]))
                    diagnostics.Add(new("assembly-frame-invalid-vector", $"Frame '{name}' requires finite numeric components."));
            }
            return result;
        }
        return from is null ? null : new(name, from, Vector(translate, true) ?? [0,0,0], axis, angle,
            Vector(normal, false), Vector(up, false), SemanticSourceSpan.Generated(sourceIdentity));
    }

    internal static (AssemblySource Source, IReadOnlyList<AssemblyInstanceIr> Instances) Lower(
        AssemblySource source, IReadOnlyList<AssemblyInstanceIr> original, List<AssemblyDiagnostic> diagnostics)
    {
        var instances = original.ToDictionary(i => i.Path.ToString(), StringComparer.Ordinal);
        var named = (source.FrameTransforms ?? []).GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var group in (source.FrameTransforms ?? []).GroupBy(f => f.Name).Where(g => g.Count() > 1))
            diagnostics.Add(new("assembly-frame-duplicate-name", $"FrameTransform '{group.Key}' is duplicated."));
        foreach (var name in named.Keys.Where(n => n is "World" or "Origin" or "Parent" || n.StartsWith("__Layout", StringComparison.Ordinal)))
            diagnostics.Add(new("assembly-frame-reserved-name", $"FrameTransform '{name}' uses a reserved name."));
        var interfaces = source.Interfaces.ToList(); var mates = source.Mates.ToList();
        var root = original.Single(i => i.ParentStableId is null);
        (AssemblyInstanceIr Owner, Transform3D Frame)? Resolve(string path, string context, HashSet<string> visiting)
        {
            var key = path.EndsWith(".Frame", StringComparison.Ordinal) ? path[..^6] : path;
            if (named.TryGetValue(key, out var transform))
            {
                if (!visiting.Add(key)) { diagnostics.Add(new("assembly-frame-cycle", $"FrameTransform cycle at '{key}'.")); return null; }
                var resolved = Resolve(transform.From, root.Path.ToString(), visiting);
                visiting.Remove(key);
                return resolved is { } value ? (value.Owner, Compose(value.Frame, transform, diagnostics)) : null;
            }
            if (path == "World") return (instances[root.Path.ToString()], Transform3D.Identity);
            if (path == "Origin") return (instances[context], Transform3D.Identity);
            if (path == "Parent")
            {
                var parent=instances[context].ParentStableId;
                if (parent is not null) return (instances.Values.Single(i=>i.StableId==parent),Transform3D.Identity);
                diagnostics.Add(new("assembly-frame-unresolved",$"'{context}' has no parent frame.")); return null;
            }
            if (!path.StartsWith(root.Path + ".", StringComparison.Ordinal) && path != root.Path.ToString())
            {
                var self = instances[context];
                var candidate = context + "." + path;
                if (!AssemblyM0Compiler.TryResolve(AssemblyPath.Parse(candidate), instances.Values, out _))
                    candidate = self.ParentStableId is null ? root.Path + "." + path
                        : instances.Values.Single(i => i.StableId == self.ParentStableId).Path + "." + path;
                path = candidate;
            }
            if (!AssemblyM0Compiler.TryResolve(AssemblyPath.Parse(path), instances.Values, out var reference)
                || !reference!.Value.TryBinding<ExactDatumFrameBinding>(out var frame))
            { diagnostics.Add(new("assembly-frame-unresolved", $"'{context}' cannot resolve exact published DatumFrame '{path}'.")); return null; }
            var owner = instances.Values.Where(i => path == i.Path.ToString() || path.StartsWith(i.Path + ".", StringComparison.Ordinal))
                .OrderByDescending(i => i.Path.Segments.Count).First();
            return (owner, Matrix(frame));
        }
        AssemblyPath Publish(AssemblyInstanceIr owner, string name, Transform3D frame, string origin)
        {
            owner = instances[owner.Path.ToString()];
            var m = frame.ToRowMajor(); var id = owner.SemanticRoot.StableIdentity + ":" + name;
            var value = new SemanticValue(id, new("AssemblyDatumFrame"), [new DatumFrameCapability()],
                [new ExactDatumFrameBinding(m[12],m[13],m[14],m[0],m[1],m[2],m[4],m[5],m[6],m[8],m[9],m[10],id)],
                provenance: [new("frame-layout", origin, name, SemanticSourceSpan.Generated(source.SourceIdentity))], exposedName: name);
            var semantic = owner.SemanticRoot;
            var updated = new SemanticValue(semantic.StableIdentity, semantic.Type, semantic.Capabilities.Values, semantic.Bindings,
                semantic.ExposedMembers.Values.Append(value), semantic.Provenance, semantic.AuthoredSourceSpan, semantic.GeneratedSourceSpan, semantic.ExposedName);
            instances[owner.Path.ToString()] = owner with { SemanticRoot = updated };
            return owner.Path.Append(name);
        }
        foreach (var (member, path) in Walk(source.Root, source.Root.Name))
        {
            if (member.FramePlacement is not { } layout) continue;
            var owner = instances[path];
            if (owner.ParentStableId is null || source.Anchor.ToString() == path || source.Anchor.ToString().StartsWith(path + ".", StringComparison.Ordinal))
                diagnostics.Add(new("assembly-placement-authority-conflict", $"Anchored occurrence '{path}' cannot also have authored placement."));
            bool DrivesOccurrence(MateSource mate)
            {
                var definition = source.Interfaces.FirstOrDefault(d => d.Name == mate.InterfaceName);
                var movingRole = "B";
                if (definition?.Family == MechanicalInterfaceFamily.Fixed)
                {
                    var b = mate.Roles.FirstOrDefault(r => r.Role == "B");
                    var bOwner = b is null ? null : original.Where(i => b.Participant.ToString() == i.Path.ToString()
                        || b.Participant.ToString().StartsWith(i.Path + ".", StringComparison.Ordinal))
                        .OrderByDescending(i => i.Path.Segments.Count).FirstOrDefault();
                    if (bOwner is not null && (source.Anchor.ToString() == bOwner.Path.ToString()
                        || source.Anchor.ToString().StartsWith(bOwner.Path + ".", StringComparison.Ordinal))) movingRole = "A";
                }
                return mate.Roles.Any(r => (r.Role == movingRole || r.Role == "Moving")
                    && (r.Participant.ToString() == path || r.Participant.ToString().StartsWith(path + ".", StringComparison.Ordinal)));
            }
            if (member.ExplicitTransform is not null || source.Mates.Any(DrivesOccurrence))
                diagnostics.Add(new("assembly-placement-authority-conflict", $"Occurrence '{path}' has both authored placement and another placement driver."));
            var from = Resolve(layout.From, path, []); var to = Resolve(layout.Target.From, path, []);
            if (from is null || to is null) continue;
            if (from.Value.Owner.StableId != owner.StableId || to.Value.Owner.StableId == owner.StableId)
            { diagnostics.Add(new("assembly-placement-invalid-owner", $"Placement '{path}' requires a source frame on itself and a target on another occurrence.")); continue; }
            var suffix = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..16];
            var a = Publish(owner, "__LayoutSource" + suffix, from.Value.Frame, layout.From);
            var b = Publish(to.Value.Owner, "__LayoutTarget" + suffix, Compose(to.Value.Frame, layout.Target, diagnostics), layout.Target.From);
            var name = "__Layout" + suffix;
            if (layout.JointFamily is { } family)
            {
                interfaces.Add(new("interface:" + name, name, [new("A", ["DatumFrameCapable"]),new("B", ["DatumFrameCapable"])],
                    [new(PlacementConstraintKind.FrameCoincident,"A",".","B",".")],
                    AdmittedFreeMotions: family == MechanicalInterfaceFamily.Revolute ? ["rotation:about-axis"] : [],
                    Family: family, SourceSpan: layout.Target.SourceSpan));
                mates.Add(new(layout.MateName!,name,[new("A",b),new("B",a)],layout.Target.SourceSpan));
            }
            else
            {
                interfaces.Add(new("interface:" + name, name, [new("Moving", ["DatumFrameCapable"]),new("Target", ["DatumFrameCapable"])],
                    [new(PlacementConstraintKind.FrameCoincident,"Moving",".","Target",".")], SourceSpan: layout.Target.SourceSpan));
                mates.Add(new(name,name,[new("Moving",a),new("Target",b)],layout.Target.SourceSpan));
            }
        }
        // Validate unused named transforms too: a malformed/cyclic declaration must not disappear.
        foreach (var name in named.Keys) Resolve(name + ".Frame", root.Path.ToString(), []);
        return (source with { Interfaces = interfaces, Mates = mates }, original.Select(i => instances[i.Path.ToString()]).ToArray());
    }

    private static IEnumerable<(AssemblyMemberSource Member, string Path)> Walk(AssemblyMemberSource member, string path)
    {
        yield return (member,path);
        foreach (var child in member.Children) foreach (var item in Walk(child,path + "." + child.Name)) yield return item;
    }
    internal static Transform3D Matrix(ExactDatumFrameBinding f) => Transform3D.FromRowMajor(
        [f.XAxisX,f.XAxisY,f.XAxisZ,0,f.YAxisX,f.YAxisY,f.YAxisZ,0,f.ZAxisX,f.ZAxisY,f.ZAxisZ,0,f.OriginX,f.OriginY,f.OriginZ,1]);
    internal static Transform3D Compose(Transform3D frame, AssemblyFrameTransformSource spec, List<AssemblyDiagnostic> diagnostics)
    {
        var basis = Transform3D.Identity;
        if (spec.Normal is { } n && spec.Up is { } u)
        {
            var z = new Vector3D(n[0],n[1],n[2]); var up = new Vector3D(u[0],u[1],u[2]);
            if (!z.TryNormalize(out z) || !(up - z * up.Dot(z)).TryNormalize(out var y))
            { diagnostics.Add(new("assembly-frame-invalid-basis", $"Frame '{spec.Name}' has zero Normal or parallel Up.")); return frame; }
            var x = y.Cross(z);
            basis = Transform3D.FromRowMajor([x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1]);
        }
        var radians = spec.RotationDegrees * Math.PI / 180;
        var rotation = spec.RotationAxis switch { "X" => Transform3D.CreateRotationX(radians), "Y" => Transform3D.CreateRotationY(radians), _ => Transform3D.CreateRotationZ(radians) };
        return rotation * basis * Transform3D.CreateTranslation(new(spec.Translation[0],spec.Translation[1],spec.Translation[2])) * frame;
    }
}
