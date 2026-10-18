using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Piping;

public static partial class PipingAuthoring
{
    private enum RouteSpaceField { Clearance }
    private enum PipePolicyField { OuterDiameter, WallThickness, Material }
    private enum KeepOutField { Min, Max, Concept }
    private enum EquipmentField { KeepOut }
    private enum PortField { PipePolicy, Position, Direction, ConnectionType, Equipment, NozzleLength }
    private enum ConnectionField { From, To, PipePolicy, Service, Reducer, Adapter }
    private enum RouteField { Connection, Through, LockedSegments, Provenance, BendRadius }
    private enum RouteRequestField { Connection, Clearance, BendRadius, HardWaypoints, AcceptAs }
    private enum LocalRerouteField { Route, FromAnchor, ToAnchor, Avoid, Clearance }
    internal static IReadOnlyList<string> FieldsFor(DeclarationSyntax declaration) => declaration.Type.Name.Text switch
    {
        "RouteSpace" => Enum.GetNames<RouteSpaceField>(),
        "PipePolicy" => Enum.GetNames<PipePolicyField>(),
        "KeepOut" => Enum.GetNames<KeepOutField>(),
        "Equipment" => Enum.GetNames<EquipmentField>(),
        "Port" => Enum.GetNames<PortField>(),
        "Connection" => Enum.GetNames<ConnectionField>(),
        "Route" => Enum.GetNames<RouteField>(),
        "RouteRequest" => Enum.GetNames<RouteRequestField>(),
        "LocalReroute" => Enum.GetNames<LocalRerouteField>(),
        _ => []
    };

    // The AST owns statement boundaries. Only direct children of PipingSystem
    // enter this binder; a declaration inside a Port cannot become a system peer.
    private static Parsed? Parse(string source, string sourceIdentity, List<string> errors,
        List<FirmamentV2ValidationDiagnostic> located)
    {
        var frontend = FirmamentFrontendSchemas.Select(source);
        errors.AddRange(frontend.Diagnostics);
        located.AddRange(frontend.LocatedDiagnostics);
        if (!frontend.IsSuccess) return null;
        var binding = new FirmamentDomainBinding(frontend.Source, sourceIdentity, "PipingSystem", new HashSet<string>(StringComparer.Ordinal));
        located.AddRange(binding.Diagnostics);
        errors.AddRange(binding.Diagnostics.Where(d => d.Severity is "error" or "fatal").Select(d => d.Code + ": " + d.Message));
        if (errors.Count != 0) return null;

        var systems = new List<DeclarationSyntax>();
        var imports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var use in binding.Document.Statements.OfType<DirectiveSyntax>().Where(d => d.Kind == DirectiveKind.Use))
        {
            var module = FirmamentSyntaxValues.Reference(use.Value);
            if (module is not null && FirmamentStandardLibraryResolver.IsModule(module)) imports.Add(module);
            else Unknown(use, $"Standard library module '{module}' (not found in the shipped catalogue)");
        }
        Visit(binding.Document.Statements);
        if (systems.Count != 1)
            errors.Add("piping-system-cardinality: exactly one PipingSystem is required");
        if (errors.Count != 0) return null;
        var system = systems[0];
        var children = system.Body?.Statements.OfType<DeclarationSyntax>().ToArray() ?? [];
        var systemFields = new PipingFields(system with { Body = system.Body is null ? null : system.Body with
            { Statements = system.Body.Statements.Where(s => s is not DeclarationSyntax).ToArray() } }, errors, located, binding);
        systemFields.Complete();
        var owned = new Dictionary<string, List<PipingFields>>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            if (child.Type.Name.Text is not ("PipePolicy" or "KeepOut" or "Equipment" or "Port" or "Connection"
                or "RouteSpace" or "Route" or "RouteRequest" or "LocalReroute"))
            {
                Unknown(child, "PipingSystem");
                continue;
            }
            if (!owned.TryGetValue(child.Type.Name.Text, out var entries)) owned[child.Type.Name.Text] = entries = [];
            entries.Add(new(child, errors, located, binding));
        }
        IEnumerable<PipingFields> Of(string type) => owned.GetValueOrDefault(type) ?? [];
        // Diagnose duplicate names before resolving with SingleOrDefault.
        foreach (var group in owned.Values)
            foreach (var duplicate in group.GroupBy(f => f.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                var kind = duplicate.First().Kind;
                var label = kind == "PipePolicy" ? "pipe-policy" : kind.ToLowerInvariant();
                errors.Add($"piping-duplicate-{label}:{duplicate.Key}");
            }
        if (errors.Count != 0) return null;

        var spaces = Of("RouteSpace").ToArray();
        if (spaces.Length > 1) errors.Add("piping-route-space-cardinality: at most one system default RouteSpace is supported");
        var defaultClearance = 25d;
        foreach (var f in spaces) { defaultClearance = f.Length(nameof(RouteSpaceField.Clearance), 25); f.Nonnegative(nameof(RouteSpaceField.Clearance), defaultClearance); f.Complete(); }

        var policies = new List<PipePolicyIr>();
        foreach (var f in Of("PipePolicy"))
        {
            var od = f.Length(nameof(PipePolicyField.OuterDiameter)); var wall = f.Length(nameof(PipePolicyField.WallThickness)); var material = f.Text(nameof(PipePolicyField.Material));
            if (od <= 0 || wall <= 0) errors.Add($"piping-pipe-policy-positive:{f.Name}");
            if (2 * wall >= od) errors.Add($"piping-pipe-wall-too-thick:{f.Name}");
            if (!new StandardLibrary.Materials.MaterialResolver().Resolve(material).IsSuccess)
                errors.Add($"piping-material-unresolved:{f.Name}:{material}");
            policies.Add(new($"standard:piping:pipe:{Q(od)}:{Q(wall)}:{material}", f.Name, od, wall, material));
            f.Complete();
        }
        if (policies.Count == 0) errors.Add("piping-pipe-policy-required: declare at least one PipePolicy");
        var keepouts = new List<KeepOutIr>();
        foreach (var f in Of("KeepOut"))
        {
            var min = f.Point(nameof(KeepOutField.Min)); var max = f.Point(nameof(KeepOutField.Max));
            var concept = f.Text(nameof(KeepOutField.Concept), "KeepOut");
            if (max.X <= min.X || max.Y <= min.Y || max.Z <= min.Z) errors.Add($"piping-keepout-invalid-bounds:{f.Name}");
            keepouts.Add(new($"concept:keepout:{f.Name}", f.Name, min, max, concept)); f.Complete();
        }
        var equipment = new List<PipingEquipmentIr>();
        foreach (var f in Of("Equipment"))
        {
            var keepout = f.Text(nameof(EquipmentField.KeepOut));
            if (!keepouts.Any(k => k.Name == keepout)) errors.Add($"piping-equipment-unknown-keepout:{f.Name}:{keepout}");
            equipment.Add(new($"equipment:{f.Name}", f.Name, keepout)); f.Complete();
        }
        var ports = new List<PipingPortInterfaceIr>();
        foreach (var f in Of("Port"))
        {
            var policy = f.Text(nameof(PortField.PipePolicy)); var position = f.Point(nameof(PortField.Position)); var direction = f.Vector(nameof(PortField.Direction), "");
            var type = f.Choice(nameof(PortField.ConnectionType), PipingConnectionType.Generic);
            var owner = f.Text(nameof(PortField.Equipment), ""); var nozzleLength = f.Length(nameof(PortField.NozzleLength), 0);
            if (!policies.Any(p => p.Name == policy)) errors.Add($"piping-port-unknown-policy:{f.Name}:{policy}");
            if (!Axis(direction)) errors.Add($"piping-port-direction-nonorthogonal:{f.Name}");
            string? ownerKeepout = null;
            if (owner.Length > 0)
            {
                var e = equipment.SingleOrDefault(e => e.Name == owner);
                if (e is null) errors.Add($"piping-port-unknown-equipment:{f.Name}:{owner}");
                else ownerKeepout = e.KeepOut;
                if (nozzleLength <= 0) errors.Add($"piping-nozzle-length-invalid:{f.Name}");
            }
            else if (nozzleLength != 0) errors.Add($"piping-nozzle-owner-required:{f.Name}");
            f.Nonnegative(nameof(PortField.NozzleLength), nozzleLength);
            ports.Add(new($"interface:port:{f.Name}", f.Name, position, direction, policy, type, $"datum-frame:port:{f.Name}",
                owner.Length > 0 ? owner : null, ownerKeepout, nozzleLength)); f.Complete();
        }
        var connections = new List<PipingConnectionIr>();
        foreach (var f in Of("Connection"))
        {
            var from = f.Text(nameof(ConnectionField.From)); var to = f.Text(nameof(ConnectionField.To)); var policy = f.Text(nameof(ConnectionField.PipePolicy));
            var service = f.Text(nameof(ConnectionField.Service), ""); var allowReducer = f.Boolean(nameof(ConnectionField.Reducer)); var allowAdapter = f.Boolean(nameof(ConnectionField.Adapter));
            f.Complete();
            var fp = ports.SingleOrDefault(p => p.Name == from); var tp = ports.SingleOrDefault(p => p.Name == to);
            if (fp is null || tp is null) { errors.Add($"piping-connection-unknown-port:{f.Name}"); continue; }
            if (!policies.Any(p => p.Name == policy)) { errors.Add($"piping-connection-unknown-policy:{f.Name}:{policy}"); continue; }
            var reducer = fp.PipePolicy != tp.PipePolicy; var adapter = fp.ConnectionType != tp.ConnectionType;
            if (reducer && !allowReducer) errors.Add($"piping-incompatible-port:{f.Name}: different pipe policies require reducer: true");
            if (adapter && !allowAdapter) errors.Add($"piping-incompatible-port:{f.Name}: connection styles require adapter: true");
            connections.Add(new($"connection:{f.Name}", f.Name, from, to, policy, service.Length > 0 ? service : null, reducer, adapter));
        }
        if (errors.Count != 0) return null;
        var routes = new List<RouteIr>();
        foreach (var f in Of("Route"))
        {
            var connectionName = f.Text(nameof(RouteField.Connection)); var points = f.Points(nameof(RouteField.Through));
            var locks = f.Integers(nameof(RouteField.LockedSegments)); var provenance = f.Choice(nameof(RouteField.Provenance), RouteProvenance.Explicit);
            var connection = connections.SingleOrDefault(c => c.Name == connectionName);
            var policy = policies.SingleOrDefault(p => p.Name == connection?.PipePolicy);
            var bend = f.Length(nameof(RouteField.BendRadius), Math.Max(50, (policy?.OuterDiameterMm ?? 25) * 2));
            f.Positive(nameof(RouteField.BendRadius), bend); f.Complete();
            if (connection is null) { errors.Add($"piping-route-unknown-connection:{f.Name}"); continue; }
            var from = ports.Single(p => p.Name == connection.FromPort); var to = ports.Single(p => p.Name == connection.ToPort);
            if (points.Count < 2 || !Near(points[0], from.Position) || !Near(points[^1], to.Position))
                errors.Add($"piping-route-endpoints-mismatch:{f.Name}");
            if (locks.Any(i => i >= points.Count - 1)) f.Invalid(nameof(RouteField.LockedSegments), "Use segment indices within this route.");
            if (errors.Count != 0) continue;
            var anchors = points.Select((p, i) => new RouteAnchorIr($"route:{f.Name}:anchor:{i}", i, p,
                i == 0 || i == points.Count - 1 ? RouteAnchorKind.Endpoint : RouteAnchorKind.HardWaypoint)).ToArray();
            routes.Add(OrthogonalAutoRouter.BuildRoute(f.Name, connection.Name, from.StableId, to.StableId, anchors, bend, provenance, sourceIdentity, locks));
        }
        var provisional = new Parsed(system.Name!.Text, policies, equipment, ports, connections, keepouts, routes, [], [], defaultClearance);
        var requests = new List<AcceptedRequest>();
        foreach (var f in Of("RouteRequest"))
        {
            var connectionName = f.Text(nameof(RouteRequestField.Connection)); var clearance = f.Length(nameof(RouteRequestField.Clearance), defaultClearance);
            var connection = connections.SingleOrDefault(c => c.Name == connectionName);
            var policy = policies.SingleOrDefault(p => p.Name == connection?.PipePolicy);
            var bend = f.Length(nameof(RouteRequestField.BendRadius), Math.Max(50, (policy?.OuterDiameterMm ?? 25) * 2));
            var waypoints = f.Points(nameof(RouteRequestField.HardWaypoints), optional: true); var acceptAs = f.Text(nameof(RouteRequestField.AcceptAs), "");
            f.Nonnegative(nameof(RouteRequestField.Clearance), clearance); f.Positive(nameof(RouteRequestField.BendRadius), bend); f.Complete();
            if (connection is null) { errors.Add($"piping-route-request-unknown-connection:{f.Name}"); continue; }
            requests.Add(new(RequestFor(connection, provisional, f.Name, clearance) with { HardWaypoints = waypoints, BendRadiusMm = bend }, acceptAs));
        }
        var reroutes = new List<RerouteSpec>();
        foreach (var f in Of("LocalReroute"))
        {
            var route = f.Text(nameof(LocalRerouteField.Route)); var from = f.Integer(nameof(LocalRerouteField.FromAnchor)); var to = f.Integer(nameof(LocalRerouteField.ToAnchor));
            var avoid = f.Names(nameof(LocalRerouteField.Avoid)); var clearance = f.Length(nameof(LocalRerouteField.Clearance), defaultClearance);
            f.Nonnegative(nameof(LocalRerouteField.Clearance), clearance); f.Complete();
            reroutes.Add(new(f.Name, route, from, to, avoid, clearance));
        }
        return provisional with { Requests = requests, Reroutes = reroutes };

        void Visit(IReadOnlyList<StatementSyntax> statements)
        {
            var bindings = new HashSet<string>(StringComparer.Ordinal);
            foreach (var statement in statements)
            {
                if (statement is DirectiveSyntax { Kind: DirectiveKind.Use } && ReferenceEquals(statements, binding.Document.Statements)) continue;
                if (statement is DeclarationSyntax data && binding.OwnsData(data)) continue;
                if (statement is DeclarationSyntax d && d.Type.Name.Text == "PipingSystem") { systems.Add(d); continue; }
                if (statement is DeclarationSyntax { Type.Name.Text: "Let", Initializer: { } value, Name: { } name } alias
                    && alias.Body is null && alias.Parameters.Count == 0 && !alias.HasParameterList && alias.Modifiers.Count == 0
                    && alias.Type.Arguments.Count == 0 && alias.Clauses.Count == 0 && alias.Annotation is null && alias.ResultType is null)
                {
                    // A catalogue alias is compile-time data, not a material request.
                    // Bind it against the existing shipped catalogue, never an allowlist
                    // of fixture names or unconsumed declarations.
                    var reference = FirmamentSyntaxValues.Reference(value) ?? "";
                    var split = reference.LastIndexOf('.');
                    if (split < 1 || !imports.Contains(reference[..split]) || !FirmamentStandardLibraryResolver.IsExport(reference[..split], reference[(split + 1)..]))
                        Unknown(alias, $"Piping catalogue binding '{reference}' (requires a used, known Standard export)");
                    if (!bindings.Add(name.Text)) Unknown(alias, "duplicate catalogue binding");
                    continue;
                }
                if (statement is DeclarationSyntax wrapper && wrapper.Type.Name.Text is "Model" or "Struct")
                {
                    var fields = new PipingFields(wrapper with { Body = wrapper.Body is null ? null : wrapper.Body with
                        { Statements = wrapper.Body.Statements.Where(s => s is not DeclarationSyntax).ToArray() } }, errors, located, binding);
                    // Piping's historical unitless document header means mm;
                    // retain it while rejecting an explicitly conflicting unit.
                    var units = fields.Text("Units", "mm");
                    if (units != "mm") fields.Invalid("Units", "Piping models use millimetres (units: mm).");
                    fields.Complete();
                    Visit(wrapper.Body?.Statements.OfType<DeclarationSyntax>().Cast<StatementSyntax>().ToArray() ?? []);
                }
                else Unknown(statement, "Piping document");
            }
        }
        void Unknown(StatementSyntax statement, string context)
        {
            var name = (statement as DeclarationSyntax)?.Type.Name.Text ?? "statement";
            var code = $"{FirmamentV2Parser.CanonicalDeclarationUnknown}:{context}:{name}";
            errors.Add(code);
            located.Add(new(code.Split(':')[0], "error", $"Unsupported {name} in {context}.")
                { SourceFile = sourceIdentity, SourceSpan = binding.OriginalSpan(statement.Span), LocationNote = binding.IsExpanded ? "Location identifies the original enclosing declaration after compile-time expansion." : null, Stage = "Binding", Source = "Firmament.Piping" });
        }
    }

    private sealed class PipingFields(DeclarationSyntax declaration, List<string> errors, List<FirmamentV2ValidationDiagnostic> located, FirmamentDomainBinding binding)
    {
        private readonly List<string> fieldErrors = [];
        private FirmamentSyntaxFields? fields;
        private FirmamentSyntaxFields Fields => fields ??= new(declaration, fieldErrors, bind: binding.Nominal);
        public string Name => declaration.Name?.Text ?? "";
        public string Kind => declaration.Type.Name.Text;
        private ExpressionSyntax? Take(string name, bool required)
        {
            var value = Fields.Take(name);
            if (value is null && required) Invalid(name, $"Required field '{FirmamentSourceSpelling.PreferredField(name)}' is missing.");
            return value;
        }
        public string Text(string name, string? fallback = null)
        {
            var value = Take(name, fallback is null);
            if (value is null) return fallback ?? "";
            if (FirmamentSyntaxValues.Reference(value) is { Length: > 0 } text) return text;
            Invalid(name, "Expected a name or nonempty string."); return fallback ?? "";
        }
        public double Length(string name, double? fallback = null)
        {
            var value = Take(name, fallback is null);
            if (value is null) return fallback ?? 0;
            // Historical numeric lengths are millimetres. Directions are dimensionless.
            if (FirmamentSyntaxValues.TryMeasure(value, "mm", out var number, "mm") && double.IsFinite(number)) return number;
            Invalid(name, "Expected a finite length in mm."); return fallback ?? 0;
        }
        public Vector3D Vector(string name, string unit)
        {
            var value = Take(name, true);
            if (FirmamentSyntaxValues.Vector(value, unit, out var values, unit) && values.Length == 3 && values.All(double.IsFinite))
                return new(values[0], values[1], values[2]);
            if (value is not null) Invalid(name, unit.Length == 0 ? "Expected three dimensionless components." : "Expected three lengths in mm.");
            return default;
        }
        public Point3D Point(string name) { var v = Vector(name, "mm"); return new(v.X, v.Y, v.Z); }
        private IReadOnlyList<ExpressionSyntax> Items(string name, bool optional)
        {
            var value = Take(name, !optional);
            if (value is SequenceSyntax { Kind: SequenceKind.List } list && list.Items.All(i => i.Name is null && i.Default is null))
                return list.Items.Select(i => i.Value).ToArray();
            if (value is not null) Invalid(name, "Expected a list.");
            return [];
        }
        public IReadOnlyList<Point3D> Points(string name, bool optional = false)
        {
            var result = new List<Point3D>();
            foreach (var value in Items(name, optional))
            {
                if (FirmamentSyntaxValues.Vector(value, "mm", out var v, "mm") && v.Length == 3 && v.All(double.IsFinite)) result.Add(new(v[0], v[1], v[2]));
                else Invalid(name, "Each waypoint must contain three lengths in mm.");
            }
            return result;
        }
        public bool Boolean(string name)
        {
            var value = Take(name, false);
            if (value is null) return false;
            if (value is LiteralSyntax { Kind: LiteralKind.Boolean } literal) return literal.Raw == "true";
            Invalid(name, "Expected true or false."); return false;
        }
        public T Choice<T>(string name, T fallback) where T : struct, Enum
        {
            var value = Take(name, false);
            if (value is null) return fallback;
            var word = FirmamentSyntaxValues.Reference(value);
            if (word is not null && Enum.GetNames<T>().Contains(word, StringComparer.Ordinal) && Enum.TryParse<T>(word, out var choice)) return choice;
            Invalid(name, $"Expected one of {string.Join(", ", Enum.GetNames<T>())}."); return fallback;
        }
        private int Index(string name, ExpressionSyntax? value)
        {
            if (FirmamentSyntaxValues.TryMeasure(value, "", out var n) && double.IsFinite(n) && n >= 0 && n <= int.MaxValue && n == Math.Truncate(n)) return (int)n;
            if (value is not null) Invalid(name, "Expected a nonnegative integer index.");
            return -1;
        }
        public int Integer(string name) => Index(name, Take(name, true));
        public IReadOnlyList<int> Integers(string name) => Items(name, true).Select(v => Index(name, v)).ToArray();
        public IReadOnlyList<string> Names(string name) => Items(name, true).Select(v =>
        {
            var text = FirmamentSyntaxValues.Reference(v);
            if (text is not null) return text;
            Invalid(name, "Expected KeepOut names."); return "";
        }).ToArray();
        public void Nonnegative(string name, double value) { if (value < 0) Invalid(name, "Expected a nonnegative length."); }
        public void Positive(string name, double value) { if (value <= 0) Invalid(name, "Expected a positive length."); }
        public void Invalid(string name, string message)
        {
            var code = $"piping-field-value-invalid:{Kind}:{Name}:{name}";
            errors.Add(code + ": " + message);
            var field = declaration.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, name));
            located.Add(new("piping-field-value-invalid", "error", $"{Kind} {Name}, field '{FirmamentSourceSpelling.PreferredField(name)}': {message}")
                { SourceSpan = binding.OriginalSpan(field?.Span ?? declaration.Span), LocationNote = binding.IsExpanded ? "Location identifies the original enclosing declaration after compile-time expansion." : null, Stage = "Binding", Source = "Firmament.Piping" });
        }
        public void Complete()
        {
            if (declaration.Name is null || declaration.Body is null || declaration.Type.Arguments.Count > 0 || declaration.Modifiers.Count > 0 || declaration.HasParameterList)
                fieldErrors.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:{Kind}:header");
            Fields.Complete();
            foreach (var code in fieldErrors)
            {
                errors.Add(code);
                var fieldName = code.Split(':').Last();
                var field = declaration.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, fieldName));
                var child = declaration.Body?.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text == fieldName);
                located.Add(new(code.Split(':')[0], "error", FirmamentV2.FirmamentInterop.FirmamentV2DiagnosticAdapter.MessageFor(code))
                    { SourceSpan = binding.OriginalSpan(field?.Name.Span ?? child?.Span ?? declaration.Span), LocationNote = binding.IsExpanded ? "Location identifies the original enclosing declaration after compile-time expansion." : null, Stage = "Binding", Source = "Firmament.Piping" });
            }
        }
    }
}
