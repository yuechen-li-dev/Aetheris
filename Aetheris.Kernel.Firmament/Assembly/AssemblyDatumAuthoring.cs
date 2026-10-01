using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

internal sealed record AssemblyLayoutDatum(string Identity, string Plane, AssemblyFrameTransformSource Transform, bool IsAxis = false);

/// <summary>Bounded datum-directed Fixed seating. No constraint search or synthetic parts.</summary>
internal static class AssemblyDatumAuthoring
{
    internal static IReadOnlyList<AssemblyLayoutDatum> ParseLayouts(string source, string sourceIdentity,
        List<AssemblyDiagnostic> diagnostics, out string declarations)
    {
        var result = new List<AssemblyLayoutDatum>();
        var masked = source.ToCharArray();
        foreach (Match header in Regex.Matches(source, @"\bConcept\s+Struct\s+(?<name>\w+)\s*\{"))
        {
            if (!IsTopLevel(source, header.Index)) continue;
            var body = AssemblyM0Parser.BalancedBody(source, header.Index + header.Length - 1, diagnostics, "Concept layout");
            if (body is null || !Regex.IsMatch(body, @"\b(?:Plane|Axis)\s+\w+\s*\{")) continue;
            var owner = header.Groups["name"].Value;
            var remaining = body;
            var planes = new Dictionary<string, AssemblyFrameTransformSource>(StringComparer.Ordinal);
            foreach (Match member in Regex.Matches(body, @"\bAxis\s+(?<name>\w+)\s*\{"))
            {
                var fields = AssemblyM0Parser.BalancedBody(body, member.Index + member.Length - 1, diagnostics, "layout axis");
                if (fields is null) continue;
                var name = member.Groups["name"].Value;
                var errorsBefore = diagnostics.Count;
                var origin = Vector(fields, "Origin", 3, true, diagnostics);
                var direction = Vector(fields, "Direction", 3, false, diagnostics);
                var reference = Vector(fields, "Reference", 3, false, diagnostics);
                CheckFields(fields, ["Origin", "Direction", "Reference"], diagnostics);
                if (diagnostics.Count != errorsBefore) continue;
                var z = new Vector3D(direction[0], direction[1], direction[2]);
                var radial = new Vector3D(reference[0], reference[1], reference[2]);
                if (!TryUnit(z, out z) || !TryUnit(radial, out radial) || !TryUnit(radial - z * radial.Dot(z), out var x))
                {
                    diagnostics.Add(new("assembly-axis-invalid-basis", $"Axis '{owner}.{name}' requires a nonzero Direction and a nonparallel Reference."));
                    continue;
                }
                var y = z.Cross(x);
                var transform = new AssemblyFrameTransformSource(owner + "_" + name, "World", origin, "Z", 0,
                    [z.X,z.Y,z.Z], [y.X,y.Y,y.Z], new(sourceIdentity, header.Index + member.Index, member.Length + fields.Length + 1));
                result.Add(new(owner + "." + name, owner + "." + name, transform, IsAxis: true));
                remaining = remaining.Replace(member.Value + fields + "}", "", StringComparison.Ordinal);
            }
            foreach (Match member in Regex.Matches(body, @"\bPlane\s+(?<name>\w+)\s*\{"))
            {
                var fields = AssemblyM0Parser.BalancedBody(body, member.Index + member.Length - 1, diagnostics, "layout plane")!;
                if (fields is null) continue;
                var name = member.Groups["name"].Value;
                AssemblyFrameTransformSource transform;
                var span = new SemanticSourceSpan(sourceIdentity, header.Index + member.Index, member.Length + fields.Length + 1);
                if (Regex.IsMatch(fields, @"\bFrom\s*:"))
                {
                    var from = Regex.Match(fields, @"\bFrom\s*:\s*(?<v>(?:SectionChainFile|LoftFile)<""[^""]+"">(?:\.\w+)+|\w+(?:\.\w+)+)\s*;?");
                    if (!from.Success || Regex.Matches(fields, @"\bFrom\s*:").Count != 1)
                        diagnostics.Add(new("assembly-datum-source-invalid", "From requires one published frame or Concept plane."));
                    var offset = Measure(fields, "Offset", "mm", diagnostics);
                    var clocking = Measure(fields, "Clocking", "deg", diagnostics);
                    var planeRemaining = from.Success ? fields.Remove(from.Index, from.Length) : fields;
                    planeRemaining = Regex.Replace(planeRemaining, @"\b(?:Offset|Clocking)\s*:\s*[-+0-9.eE]+(?:mm|deg)\s*;?", "");
                    if (!string.IsNullOrWhiteSpace(planeRemaining.Replace(";", "")))
                        diagnostics.Add(new("assembly-datum-fields-invalid", "Derived Plane admits From, Offset and Clocking only."));
                    transform = new(owner + "_" + name, from.Groups["v"].Value, [0,0,offset], "Z", clocking, SourceSpan: span);
                }
                else
                {
                    var origin = Vector(fields, "Origin", 3, true, diagnostics);
                    var normal = Vector(fields, "Normal", 3, false, diagnostics);
                    var up = Vector(fields, "Up", 3, false, diagnostics);
                    CheckFields(fields, ["Origin", "Normal", "Up"], diagnostics);
                    transform = new(owner + "_" + name, "World", origin, "Z", 0, normal, up, span);
                    AssemblyFrameAuthoring.Compose(Transform3D.Identity, transform, diagnostics);
                }
                if (!planes.TryAdd(name, transform)) diagnostics.Add(new("assembly-datum-duplicate", $"Duplicate plane '{owner}.{name}'."));
                result.Add(new(owner + "." + name, owner + "." + name, transform));
                remaining = remaining.Replace(member.Value + fields + "}", "", StringComparison.Ordinal);
            }
            foreach (Match member in Regex.Matches(body, @"\bDatumFrame\s+(?<name>\w+)\s*\{"))
            {
                var fields = AssemblyM0Parser.BalancedBody(body, member.Index + member.Length - 1, diagnostics, "layout frame")!;
                if (fields is null) continue;
                var planeName = Field(fields, "On", diagnostics);
                var at = Vector(fields, "At", 2, true, diagnostics);
                var x = Vector(fields, "X", 2, false, diagnostics);
                CheckFields(fields, ["On", "At", "X"], diagnostics);
                if (!planes.TryGetValue(planeName, out var plane))
                { diagnostics.Add(new("assembly-datum-plane-unresolved", $"Frame '{owner}.{member.Groups["name"].Value}' references unknown plane '{planeName}'.")); continue; }
                if (Math.Sqrt(x[0]*x[0] + x[1]*x[1]) < 1e-12)
                    diagnostics.Add(new("assembly-datum-invalid-direction", "DatumFrame X must be nonzero."));
                var angle = Math.Atan2(x[1], x[0]) * 180 / Math.PI;
                var name = member.Groups["name"].Value;
                var frame = new AssemblyFrameTransformSource(owner + "_" + name, owner + "." + planeName, [at[0], at[1], 0], "Z", angle, SourceSpan: plane.SourceSpan);
                result.Add(new(owner + "." + name, owner + "." + planeName, frame));
                remaining = remaining.Replace(member.Value + fields + "}", "", StringComparison.Ordinal);
            }
            if (!string.IsNullOrWhiteSpace(remaining.Replace(";", "")))
                diagnostics.Add(new("assembly-datum-invalid-layout", $"Unsupported members in Concept Struct '{owner}'."));
            Array.Fill(masked, ' ', header.Index, header.Length + body.Length + 1);
        }
        if (result.Select(d => d.Identity).Distinct(StringComparer.Ordinal).Count() != result.Count)
            diagnostics.Add(new("assembly-datum-duplicate", "Concept layout datum identities must be unique."));
        foreach (var value in result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = value;
            while (current is not null)
            {
                if (!seen.Add(current.Identity))
                { diagnostics.Add(new("assembly-datum-dependency-cycle", $"Concept datum cycle at '{current.Identity}'.")); break; }
                current = result.FirstOrDefault(d => d.Identity == current.Transform.From);
            }
        }
        declarations = new string(masked);
        return result;
    }

    internal static AssemblyDatumContract ParseContract(string fields, List<AssemblyDiagnostic> diagnostics)
    {
        var datum = Field(fields, "Datum", diagnostics);
        var members = Regex.Match(fields, @"\bMembers\s*:\s*\[(?<v>[^]]*)\]\s*;?");
        if (Regex.Matches(fields, @"\bMembers\s*:").Count != 1) diagnostics.Add(new("assembly-datum-members-invalid", "Requires exactly one Members field."));
        if (!members.Success) diagnostics.Add(new("assembly-datum-members-missing", "Datum Fixed Interface requires Members."));
        var paths = members.Groups["v"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0 || paths.Distinct(StringComparer.Ordinal).Count() != paths.Length || paths.Any(p => !Regex.IsMatch(p, @"^\w+(?:\.\w+)+$")))
            diagnostics.Add(new("assembly-datum-members-invalid", "Members must be a nonempty unique list of semantic port paths."));
        CheckFields(fields, ["Datum", "Members"], diagnostics);
        return new(datum, paths);
    }

    internal static AssemblyMemberSource Bind(string body, AssemblyMemberSource root,
        IReadOnlyList<AssemblyLayoutDatum> layout, IReadOnlyList<InterfaceDefinition> interfaces,
        List<AssemblyDiagnostic> diagnostics, out IReadOnlyList<AssemblyFrameTransformSource> frames,
        Func<string, IReadOnlyList<SemanticValue>>? publishedPorts = null)
    {
        var usedFrames = new Dictionary<string, AssemblyFrameTransformSource>(StringComparer.Ordinal);
        var seats = new Dictionary<string, (AssemblyDatumSeat Seat, AssemblyFramePlacementSource Placement)>(StringComparer.Ordinal);
        var axisSeats = new Dictionary<string, (string Member, string Interface, string Datum, AssemblyFramePlacementSource Placement)>(StringComparer.Ordinal);
        var resolved = new Dictionary<string, AssemblyLayoutDatum>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        var mateNames = new HashSet<string>(StringComparer.Ordinal);
        AssemblyLayoutDatum? Resolve(AssemblyLayoutDatum value)
        {
            if (resolved.TryGetValue(value.Identity, out var cached)) return cached;
            if (!active.Add(value.Identity))
            { diagnostics.Add(new("assembly-datum-dependency-cycle", $"Concept datum cycle at '{value.Identity}'.")); return null; }
            var from = value.Transform.From;
            Transform3D basis = Transform3D.Identity;
            if (layout.FirstOrDefault(d => d.Identity == from) is { } parent)
            {
                var r = Resolve(parent);
                if (r is null) { active.Remove(value.Identity); return null; }
                basis = AssemblyFrameAuthoring.Compose(Transform3D.Identity, r.Transform, diagnostics);
            }
            else if (from != "World")
            {
                var file = Regex.Match(from, @"^(?<id>(?:SectionChainFile|LoftFile)<""[^""]+"">)\.(?<port>\w+)\.Frame$");
                IReadOnlyList<SemanticValue>? ports = null;
                string port = "";
                Transform3D placement = Transform3D.Identity;
                if (file.Success)
                { ports = publishedPorts?.Invoke(file.Groups["id"].Value); port = file.Groups["port"].Value; }
                else
                {
                    var path = from.Split('.');
                    if (path.Length == 4 && path[0] == root.Name && path[3] == "Frame"
                        && root.Children.FirstOrDefault(c => c.Name == path[1] && c.Kind == AssemblyInstanceKind.Part) is { } child)
                    {
                        // A source must already have an independent placement. Never
                        // evaluate the very occurrence that this datum will place.
                        if ((child.ExplicitTransform is null && child.FramePlacement?.Target.From != "World") || Regex.IsMatch(body,
                            $@"\bMember\s*:\s*{Regex.Escape(root.Name + "." + child.Name)}\."))
                        { diagnostics.Add(new("assembly-datum-source-placement-dependent", $"'{from}' requires an independently placed direct Part.")); active.Remove(value.Identity); return null; }
                        ports = child.ExposedSemantics; port = path[2];
                        if (child.ExplicitTransform is { } explicitPlacement) placement = Transform3D.FromRowMajor(explicitPlacement.Matrix);
                        else
                        {
                            var authored = child.FramePlacement!;
                            var local = Transform3D.Identity;
                            if (authored.From != "Origin")
                            {
                                var pieces = authored.From.Split('.');
                                var sourcePort = ports.FirstOrDefault(p => p.ExposedName == pieces[0]);
                                if (pieces.Length != 2 || pieces[1] != "Frame" ||
                                    sourcePort is null || !sourcePort.ExposedMembers.TryGetValue("Frame", out var sourceFrame) ||
                                    !sourceFrame.TryBinding<ExactDatumFrameBinding>(out var exactSource))
                                { diagnostics.Add(new("assembly-datum-source-unresolved", $"Cannot resolve source Placement '{authored.From}'.")); active.Remove(value.Identity); return null; }
                                local = AssemblyFrameAuthoring.Matrix(exactSource);
                            }
                            placement = local.Inverse() * AssemblyFrameAuthoring.Compose(Transform3D.Identity, authored.Target, diagnostics);
                        }
                    }
                }
                var semantic = ports?.FirstOrDefault(p => p.ExposedName == port);
                if (semantic is null || !semantic.ExposedMembers.TryGetValue("Frame", out var member) || !member.TryBinding<ExactDatumFrameBinding>(out var frame))
                { diagnostics.Add(new("assembly-datum-source-unresolved", $"Cannot resolve published frame '{from}' in '{root.Name}'.")); active.Remove(value.Identity); return null; }
                basis = AssemblyFrameAuthoring.Matrix(frame) * placement;
            }
            var m = AssemblyFrameAuthoring.Compose(basis, value.Transform, diagnostics).ToRowMajor();
            var result = value with { Transform = value.Transform with { From = "World", Translation = [m[12],m[13],m[14]], RotationDegrees = 0,
                Normal = [m[8],m[9],m[10]], Up = [m[4],m[5],m[6]] } };
            active.Remove(value.Identity);
            resolved[value.Identity] = result;
            return result;
        }
        foreach (Match header in Regex.Matches(body, @"\bMate\s+(?<name>\w+)\s*:\s*(?<type>\w+)\s*\{"))
        {
            var fields = AssemblyM0Parser.BalancedBody(body, header.Index + header.Length - 1, diagnostics, "datum Mate")!;
            if (fields is null) continue;
            var definition = interfaces.FirstOrDefault(i => i.Name == header.Groups["type"].Value);
            var contract = definition?.DatumContract;
            if (contract is null)
            {
                if (Regex.IsMatch(fields, @"\bMember\s*:")) diagnostics.Add(new("assembly-datum-interface-unresolved", "Member/At Mate requires a datum-directed Fixed Interface."));
                continue;
            }
            var member = Field(fields, "Member", diagnostics);
            if (!mateNames.Add(header.Groups["name"].Value))
            { diagnostics.Add(new("assembly-datum-mate-duplicate", $"Datum Mate '{header.Groups["name"].Value}' is duplicated in '{root.Name}'.")); continue; }
            if (layout.FirstOrDefault(d => d.Identity == contract.Datum) is { IsAxis: true } axis)
            {
                if (!contract.Members.Contains(member, StringComparer.Ordinal))
                    diagnostics.Add(new("assembly-datum-member-outside-contract", $"'{member}' is not a member of '{definition!.Name}'."));
                var axisParts = member.Split('.');
                var child = axisParts.Length == 3 && axisParts[0] == root.Name ? root.Children.FirstOrDefault(c => c.Name == axisParts[1]) : null;
                if (child is null)
                { diagnostics.Add(new("assembly-datum-member-scope", "Axis seating requires a direct occurrence's published semantic port.")); continue; }
                var port = child.ExposedSemantics.FirstOrDefault(p => p.ExposedName == axisParts[2]);
                if (!ValidAxisPort(port))
                { diagnostics.Add(new("assembly-axis-port-invalid", $"'{member}' requires an exact Axis and an aligned seating Frame with origin on the axis.")); continue; }
                if (!Regex.IsMatch(fields, @"\bAt\s*:")) diagnostics.Add(new("assembly-axis-station-missing", "Axis seating requires At in mm."));
                var station = Measure(fields, "At", "mm", diagnostics);
                var clocking = Measure(fields, "Clocking", "deg", diagnostics);
                var axisOrientation = Regex.IsMatch(fields, @"\bOrientation\s*:") ? Field(fields, "Orientation", diagnostics) : "SameDirection";
                if (axisOrientation is not ("SameDirection" or "OpposedDirection"))
                    diagnostics.Add(new("assembly-datum-orientation-invalid", "Orientation requires SameDirection or OpposedDirection."));
                CheckFields(fields, ["Member", "At", "Clocking", "Orientation"], diagnostics);
                var resolvedAxis = Resolve(axis);
                if (resolvedAxis is null) continue;
                var basis = AssemblyFrameAuthoring.Compose(Transform3D.Identity, resolvedAxis.Transform, diagnostics);
                var axisTarget = AssemblyFrameAuthoring.Compose(basis, new("AxisStation", "World", [0,0,station], "Z", clocking), diagnostics).ToRowMajor();
                var sign = axisOrientation == "OpposedDirection" ? -1 : 1;
                var name = resolvedAxis.Transform.Name + "__" + header.Groups["name"].Value;
                usedFrames[name] = new(name, "World", [axisTarget[12],axisTarget[13],axisTarget[14]], "Z", 0,
                    [sign*axisTarget[8],sign*axisTarget[9],sign*axisTarget[10]], [sign*axisTarget[4],sign*axisTarget[5],sign*axisTarget[6]], axis.Transform.SourceSpan);
                var placement = new AssemblyFramePlacementSource(axisParts[2] + ".Frame", new(name, name + ".Frame", [0,0,0], "Z", 0), definition!.Family, header.Groups["name"].Value,
                    new(definition.Name, contract.Datum, member, station, clocking, Enum.TryParse<DatumOrientationRelation>(axisOrientation, out var relation) ? relation : DatumOrientationRelation.SameDirection));
                var axisOccurrence = root.Name + "." + child.Name;
                if (seats.ContainsKey(axisOccurrence) || !axisSeats.TryAdd(axisOccurrence, (member, definition.Name, contract.Datum, placement)))
                    diagnostics.Add(new("assembly-placement-authority-conflict", $"'{axisOccurrence}' has multiple datum Mates."));
                continue;
            }
            if (definition!.Family != MechanicalInterfaceFamily.Fixed)
            { diagnostics.Add(new("assembly-datum-family-invalid", "A plane datum requires Interface<Fixed>; Revolute requires a Concept Axis.")); continue; }
            var at = Field(fields, "At", diagnostics);
            var orientation = Field(fields, "Orientation", diagnostics);
            var support = Regex.IsMatch(fields, @"\bSupport\s*:\s*true\b");
            if (Regex.Matches(fields, @"\bSupport\s*:").Count > 1) diagnostics.Add(new("assembly-datum-support-invalid", "Support cannot be duplicated."));
            CheckFields(fields, ["Member", "At", "Orientation", "Support"], diagnostics);
            if (Regex.IsMatch(fields, @"\bSupport\s*:") && !Regex.IsMatch(fields, @"\bSupport\s*:\s*(true|false)\b"))
                diagnostics.Add(new("assembly-datum-support-invalid", "Support requires true or false."));
            if (!contract.Members.Contains(member, StringComparer.Ordinal))
                diagnostics.Add(new("assembly-datum-member-outside-contract", $"'{member}' is not a member of '{header.Groups["type"].Value}'."));
            var datumSource = layout.FirstOrDefault(d => d.Identity == contract.Datum && d.Plane == d.Identity);
            var targetSource = layout.FirstOrDefault(d => d.Identity == at);
            var datum = datumSource is null ? null : Resolve(datumSource);
            var target = targetSource is null ? null : Resolve(targetSource);
            if (datum is null || target is null || target.Identity == target.Plane || target.Plane != contract.Datum)
            { diagnostics.Add(new("assembly-datum-target-invalid", $"'{at}' must be a frame on Concept plane '{contract.Datum}'.")); continue; }
            if (orientation is not ("SameDirection" or "OpposedDirection"))
                diagnostics.Add(new("assembly-datum-orientation-invalid", "Orientation requires SameDirection or OpposedDirection."));
            var parts = member.Split('.');
            if (parts.Length != 3 || parts[0] != root.Name)
            { diagnostics.Add(new("assembly-datum-member-scope", "Datum seating currently admits a direct part's published semantic port in the owning assembly.")); continue; }
            var occurrence = parts[0] + "." + parts[1];
            if (root.Children.FirstOrDefault(c => c.Name == parts[1]) is not { Kind: AssemblyInstanceKind.Part })
            { diagnostics.Add(new("assembly-datum-member-scope", $"'{occurrence}' must be a direct Part occurrence.")); continue; }
            var targetSpec = target.Transform;
            if (orientation == "OpposedDirection")
            {
                var m = AssemblyFrameAuthoring.Compose(Transform3D.Identity, targetSpec, diagnostics).ToRowMajor();
                targetSpec = targetSpec with { Normal = [-m[8], -m[9], -m[10]], Up = [-m[4], -m[5], -m[6]] };
            }
            targetSpec = targetSpec with { Name = targetSpec.Name + "__" + header.Groups["name"].Value };
            usedFrames[targetSpec.Name] = targetSpec;
            var seat = new AssemblyDatumSeat(header.Groups["type"].Value, contract.Datum, parts[2] + ".Frame",
                datum.Transform.Translation.ToArray(), datum.Transform.Normal!.ToArray(), support);
            if (axisSeats.ContainsKey(occurrence) || !seats.TryAdd(occurrence, (seat, new(parts[2] + ".Frame", new(header.Groups["name"].Value,
                targetSpec.Name + ".Frame", [0,0,0], "Z", 0)))))
                diagnostics.Add(new("assembly-placement-authority-conflict", $"'{occurrence}' has multiple datum-directed Mates."));
        }
        // Each member of a contract declared in this scope must be realized once.
        foreach (var definition in interfaces.Where(i => i.DatumContract is not null))
        foreach (var member in definition.DatumContract!.Members.Where(p => p.StartsWith(root.Name + ".", StringComparison.Ordinal)))
            if (!seats.Any(p => p.Value.Seat.Interface == definition.Name && p.Key + "." + p.Value.Seat.Port[..^6] == member)
                && !axisSeats.Any(p => p.Value.Interface == definition.Name && p.Value.Member == member))
                diagnostics.Add(new("assembly-datum-member-unseated", $"Contract member '{member}' requires one datum Mate."));
        var children = root.Children.Select(child =>
        {
            if (axisSeats.TryGetValue(root.Name + "." + child.Name, out var axisSeating))
            {
                if (child.FramePlacement is not null || child.ExplicitTransform is not null)
                    diagnostics.Add(new("assembly-placement-authority-conflict", $"'{root.Name}.{child.Name}' has authored Placement and axis Mate."));
                return child with { FramePlacement = axisSeating.Placement, PlacementAuthority = PlacementAuthority.MateDerived,
                    Provenance = [.. child.Provenance ?? [], new("concept-axis-seating", axisSeating.Datum, axisSeating.Interface),
                        new("concept-axis-member", axisSeating.Member, axisSeating.Placement.MateName!)] };
            }
            if (!seats.TryGetValue(root.Name + "." + child.Name, out var seating)) return child;
            if (child.FramePlacement is not null || child.ExplicitTransform is not null)
                diagnostics.Add(new("assembly-placement-authority-conflict", $"'{root.Name}.{child.Name}' has authored Placement and datum Mate."));
            return child with { FramePlacement = seating.Placement, DatumSeat = seating.Seat,
                PlacementAuthority = PlacementAuthority.AuthoredFrame,
                Provenance = [.. child.Provenance ?? [], new("concept-datum-seating", seating.Seat.Datum, seating.Seat.Interface),
                    new("concept-datum-derivation", layout.First(d => d.Identity == seating.Seat.Datum).Transform.From, seating.Seat.Datum)] };
        }).ToArray();
        frames = usedFrames.Values.ToArray();
        return root with { Children = children };
    }

    internal static IReadOnlyList<AssemblyAxisSeatEvidence> ValidateAxisSeats(AssemblySource source, AssemblyIr ir,
        List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<AssemblyAxisSeatEvidence>();
        foreach (var child in source.Root.Children.Where(c => c.FramePlacement?.AxisSeat is not null))
        {
            var placement = child.FramePlacement!;
            var seat = placement.AxisSeat!;
            var instance = ir.Instances.Single(i => i.Path.ToString() == source.Root.Name + "." + child.Name);
            var joint = ir.Joints?.SingleOrDefault(j => j.Name == placement.MateName);
            if (joint is null || instance.ResolvedTransform is null) continue; // Existing placement diagnostics own unresolved input.
            if (!AssemblyM0Compiler.TryResolve(AssemblyPath.Parse(seat.Member + ".Axis"), ir.Instances, out var axisReference)) continue;
            var axis = (ExactAxisBinding)AssemblyWorldQuery.Resolve(ir, axisReference!.Value.StableIdentity);
            var target = (ExactDatumFrameBinding)AssemblyWorldQuery.Resolve(ir, joint.ParentFrameSemanticId);
            var frame = (ExactDatumFrameBinding)AssemblyWorldQuery.Resolve(ir, joint.ChildFrameSemanticId);
            var direction = new Vector3D(axis.DirectionX,axis.DirectionY,axis.DirectionZ);
            direction.TryNormalize(out direction);
            var z = new Vector3D(target.ZAxisX,target.ZAxisY,target.ZAxisZ);
            var delta = new Vector3D(axis.OriginX-target.OriginX,axis.OriginY-target.OriginY,axis.OriginZ-target.OriginZ);
            var axisResidual = delta.Cross(z).Length;
            var stationResidual = new Vector3D(frame.OriginX-target.OriginX,frame.OriginY-target.OriginY,frame.OriginZ-target.OriginZ).Length;
            var x = new Vector3D(frame.XAxisX,frame.XAxisY,frame.XAxisZ);
            var targetX = new Vector3D(target.XAxisX,target.XAxisY,target.XAxisZ);
            var angle = Math.Max(Math.Atan2(direction.Cross(z).Length, direction.Dot(z)), Math.Atan2(x.Cross(targetX).Length, x.Dot(targetX)));
            var passed = axisResidual <= 1e-6 && stationResidual <= 1e-6 && angle <= 1e-6;
            result.Add(new(placement.MateName!, instance.StableId, seat, joint.DegreesOfFreedom, axisResidual, stationResidual, angle, passed));
            if (!passed) diagnostics.Add(new("assembly-axis-residual-failed", $"Axis Mate '{placement.MateName}' failed exact datum residual validation."));
        }
        return result;
    }

    private static bool ValidAxisPort(SemanticValue? port)
    {
        if (port is null || !port.ExposedMembers.TryGetValue("Axis", out var a) || !a.TryBinding<ExactAxisBinding>(out var axis)
            || !port.ExposedMembers.TryGetValue("Frame", out var f) || !f.TryBinding<ExactDatumFrameBinding>(out var frame)) return false;
        var direction = new Vector3D(axis.DirectionX,axis.DirectionY,axis.DirectionZ);
        var z = new Vector3D(frame.ZAxisX,frame.ZAxisY,frame.ZAxisZ);
        var delta = new Vector3D(frame.OriginX-axis.OriginX,frame.OriginY-axis.OriginY,frame.OriginZ-axis.OriginZ);
        return TryUnit(direction, out direction) && TryUnit(z, out z)
            && (direction - z).Length <= 1e-6 && delta.Cross(direction).Length <= 1e-6;
    }

    private static bool TryUnit(Vector3D value, out Vector3D unit)
    {
        var scale = Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        unit = Vector3D.Zero;
        return double.IsFinite(scale) && scale > 1e-12 && (value / scale).TryNormalize(out unit);
    }

    private static double Measure(string fields, string name, string unit, List<AssemblyDiagnostic> diagnostics)
    {
        var matches = Regex.Matches(fields, $@"\b{name}\s*:\s*(?<v>[-+0-9.eE]+){unit}\s*;?");
        if (!Regex.IsMatch(fields, $@"\b{name}\s*:")) return 0;
        if (matches.Count != 1 || Regex.Matches(fields, $@"\b{name}\s*:").Count != 1
            || !double.TryParse(matches[0].Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result))
        { diagnostics.Add(new("assembly-datum-measure-invalid", $"'{name}' requires one finite value in {unit}.")); return 0; }
        return result;
    }

    private static string Field(string fields, string name, List<AssemblyDiagnostic> diagnostics)
    {
        var matches = Regex.Matches(fields, $@"\b{name}\s*:\s*(?<v>[A-Za-z_]\w*(?:\.\w+)*)\s*;?");
        if (matches.Count != 1) diagnostics.Add(new("assembly-datum-field-invalid", $"Requires exactly one '{name}' field."));
        return matches.Count == 0 ? "" : matches[0].Groups["v"].Value;
    }

    private static bool IsTopLevel(string source, int offset)
    {
        var depth = 0; var quoted = false; var escaped = false;
        for (var i = 0; i < offset; i++)
        {
            var c = source[i];
            if (c == '"' && !escaped) quoted = !quoted;
            if (!quoted) { if (c == '{') depth++; else if (c == '}') depth--; }
            escaped = quoted && c == '\\' && !escaped;
        }
        return depth == 0;
    }

    private static double[] Vector(string fields, string name, int count, bool length, List<AssemblyDiagnostic> diagnostics)
    {
        var matches = Regex.Matches(fields, $@"\b{name}\s*:\s*\[(?<v>[^]]+)\]\s*;?");
        var parts = matches.Count == 1 ? matches[0].Groups["v"].Value.Split(',', StringSplitOptions.TrimEntries) : [];
        var values = new double[count];
        if (parts.Length != count) { diagnostics.Add(new("assembly-datum-vector-invalid", $"'{name}' requires {count} components.")); return values; }
        for (var i = 0; i < count; i++)
        {
            var text = parts[i];
            if (length && text.EndsWith("mm", StringComparison.Ordinal)) text = text[..^2];
            else if (length) diagnostics.Add(new("assembly-datum-unit-invalid", $"'{name}' requires mm lengths."));
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i]))
                diagnostics.Add(new("assembly-datum-vector-invalid", $"'{name}' requires finite components."));
        }
        return values;
    }

    private static void CheckFields(string fields, string[] names, List<AssemblyDiagnostic> diagnostics)
    {
        var remaining = Regex.Replace(fields, $@"\b(?:{string.Join('|', names)})\s*:\s*(?:\[[^]]*\]|[-+0-9.eE]+(?:mm|deg)|[A-Za-z_]\w*(?:\.\w+)*)\s*;?", "");
        if (!string.IsNullOrWhiteSpace(remaining.Replace(";", ""))) diagnostics.Add(new("assembly-datum-fields-invalid", $"Unknown or malformed datum fields: {remaining.Trim()}"));
    }
}
