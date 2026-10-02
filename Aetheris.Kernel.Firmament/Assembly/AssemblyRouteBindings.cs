using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record AssemblyRouteInput(string Parameter, double[] Point, string On);
public sealed record AssemblyRouteBindingEvidence(string Occurrence, string Parameter, string Port,
    double[] WorldPoint, double[] LocalPoint, string SpecializedDefinition);

/// <summary>Late, fixed-placement point binding. The existing M0 solver and
/// world semantic query own placement; ordinary typed Templates own specialization.</summary>
internal static class AssemblyRouteBindings
{
    internal static IReadOnlyList<AssemblyRouteInput>? Parse(string body, List<AssemblyDiagnostic> diagnostics)
    {
        var headers = Regex.Matches(body, @"\bBind\s*\{");
        if (headers.Count == 0) return null;
        if (headers.Count != 1) { Error("duplicate", "One Bind block is allowed."); return []; }
        var content = AssemblyM0Parser.BalancedBody(body, headers[0].Index + headers[0].Length - 1, diagnostics, "route Bind");
        if (content is null) return [];
        var inputs = new List<AssemblyRouteInput>(); var remaining = content;
        foreach (Match input in Regex.Matches(content, @"(?<name>[A-Za-z_]\w*)\s*\{(?<body>[^{}]*)\}"))
        {
            var fields = input.Groups["body"].Value;
            var at = Regex.Match(fields, @"\bAt\s*:\s*Point(?<dimension>[23])\((?<point>[^()]+)\)\s*;");
            var on = Regex.Match(fields, @"\bOn\s*:\s*(?<path>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+)\s*;");
            var rest = fields;
            foreach (var field in new[] { at, on }.Where(m => m.Success).OrderByDescending(m => m.Index)) rest = rest.Remove(field.Index, field.Length);
            var point = at.Groups["point"].Value.Split(',').Select(v =>
                Materializer.WireFormAuthoring.TryLength(v.Trim(), out var value) ? value : double.NaN).ToArray();
            if (!at.Success || !on.Success || !string.IsNullOrWhiteSpace(rest)
                || point.Length != int.Parse(at.Groups["dimension"].Value) || point.Any(v => !double.IsFinite(v))
                || inputs.Any(i => i.Parameter == input.Groups["name"].Value))
            { Error("input-invalid", input.Groups["name"].Value); continue; }
            inputs.Add(new(input.Groups["name"].Value, point.Length == 2 ? [point[0], point[1], 0] : point, on.Groups["path"].Value));
        }
        foreach (Match match in Regex.Matches(content, @"[A-Za-z_]\w*\s*\{[^{}]*\}").Cast<Match>().Reverse()) remaining = remaining.Remove(match.Index, match.Length);
        if (!string.IsNullOrWhiteSpace(remaining) || inputs.Count is < 1 or > 16) Error("input-invalid", "Expected 1..16 named point bindings.");
        return inputs;
        void Error(string code, string message) => diagnostics.Add(new("assembly-route-bind-" + code, message));
    }

    internal static AssemblySource? Resolve(AssemblySource source, List<AssemblyDiagnostic> diagnostics,
        out IReadOnlyList<AssemblyRouteBindingEvidence> evidence)
    {
        var bound = new List<AssemblyRouteBindingEvidence>(); evidence = bound;
        if (!source.Root.Flatten().Any(m => m.RouteInputs is not null)) return source;
        var placed = new AssemblyM0Compiler().Compile(source);
        if (!placed.IsSuccess || placed.Ir is null) { diagnostics.AddRange(placed.Diagnostics); return null; }
        if ((placed.Ir.Joints ?? []).Any())
        { diagnostics.Add(new("assembly-route-bind-motion-not-qualified", "Bound routes require a completely fixed assembly; posed or moving routes are deferred.")); return null; }
        var declarations = source.DefinitionSource ?? "";
        AssemblyMemberSource Visit(AssemblyMemberSource member, string path)
        {
            var children = member.Children.Select(c => Visit(c, path + "." + c.Name)).ToArray();
            if (member.RouteInputs is null) return member with { Children = children };
            var templateName = member.DefinitionIdentity.Split('<')[0];
            var template = Regex.Match(declarations, $@"\b(?:Struct|Model)\s+{Regex.Escape(templateName)}\s*\{{");
            var templateBody = template.Success ? AssemblyM0Parser.BalancedBody(declarations, template.Index + template.Length - 1, diagnostics, "route template") : null;
            if (member.Kind != AssemblyInstanceKind.Part || templateBody is null || !Materializer.WireRouteAuthoring.IsSource(templateBody) || Regex.IsMatch(templateBody, @"\bExpose\b"))
            { diagnostics.Add(new("assembly-route-bind-consumer-invalid", path + ": Bind requires a point-driven WireRoute Template without route-dependent published outputs.")); return member; }
            var instance = placed.Ir.Instances.Single(i => i.Path.ToString() == path);
            if (instance.ResolvedTransform is null) { diagnostics.Add(new("assembly-route-bind-placement-unresolved", path)); return member; }
            var inverse = Transform3D.FromRowMajor(instance.ResolvedTransform.Matrix).Inverse();
            var arguments = new List<string>(); var rows = new List<(AssemblyRouteInput Input, Point3D World, Point3D Local)>();
            foreach (var input in member.RouteInputs)
            {
                if (!AssemblyM0Compiler.TryResolve(AssemblyPath.Parse(input.On), placed.Ir.Instances, out var reference)
                    || !reference!.Value.TryBinding<ExactDatumFrameBinding>(out _))
                { diagnostics.Add(new("assembly-route-bind-port-unresolved", path + ":" + input.Parameter + ":" + input.On + ": a public DatumFrame is required")); continue; }
                var frame = (ExactDatumFrameBinding)AssemblyWorldQuery.Resolve(placed.Ir, reference.Value.StableIdentity);
                var p = input.Point;
                var world = new Point3D(frame.OriginX + frame.XAxisX * p[0] + frame.YAxisX * p[1] + frame.ZAxisX * p[2],
                    frame.OriginY + frame.XAxisY * p[0] + frame.YAxisY * p[1] + frame.ZAxisY * p[2],
                    frame.OriginZ + frame.XAxisZ * p[0] + frame.YAxisZ * p[1] + frame.ZAxisZ * p[2]);
                var local = inverse.Apply(world);
                arguments.Add(input.Parameter + ":Point3(" + string.Join(",", new[] { local.X, local.Y, local.Z }.Select(v => v.ToString("0.################", CultureInfo.InvariantCulture) + "mm")) + ")");
                rows.Add((input, world, local));
            }
            if (!member.DefinitionIdentity.EndsWith('>')) { diagnostics.Add(new("assembly-route-bind-template-required", path)); return member; }
            var prefix = member.DefinitionIdentity[..^1];
            var identity = prefix + (prefix.EndsWith('<') ? "" : ",") + string.Join(",", arguments) + ">";
            foreach (var row in rows) bound.Add(new(path, row.Input.Parameter, row.Input.On, [row.World.X, row.World.Y, row.World.Z], [row.Local.X, row.Local.Y, row.Local.Z], identity));
            return member with { Children = children, DefinitionIdentity = identity };
        }
        var root = Visit(source.Root, source.Root.Name);
        return diagnostics.Any(d => d.Severity == AssemblyDiagnosticSeverity.Error) ? null : source with { Root = root };
    }
}
