using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Motion is admitted from the shared AST into the existing occurrence
/// graph. This owner never creates product instances or interprets geometry.</summary>
internal static class AssemblyMotionAuthoring
{
    internal static IReadOnlyList<DeclarationSyntax> Extract(DocumentSyntax document, ref string source, string file, List<AssemblyDiagnostic> diagnostics)
    {
        var allowed = document.Statements.OfType<DeclarationSyntax>().SelectMany(d => d.Type.Name.Text == "Assembly" && d.Modifiers.Count == 0
            ? new[] { d }.Concat(d.Body?.Statements.OfType<DeclarationSyntax>() ?? []) : [d]).ToArray();
        var result = FirmamentSyntaxTraversal.Declarations(document).Where(d => d.Type.Name.Text is "Motion" or "Keyframes").ToArray();
        foreach (var d in result.Where(d => !allowed.Contains(d)))
            diagnostics.Add(Error("scope-unsupported", "Motion and Keyframes belong to the root Assembly or its module, not a reusable definition or construction scope.", file, d.Span));
        var chars = source.ToCharArray();
        foreach (var d in result) Array.Fill(chars, ' ', d.Span.Start, d.Span.Length);
        source = new(chars); return result;
    }

    internal static IReadOnlyList<AssemblyMotionIr> Bind(AssemblySource source, AssemblyIr assembly, List<AssemblyDiagnostic> diagnostics)
    {
        if (source.MotionSyntax.Count == 0) return [];
        var coordinates = (assembly.Joints ?? []).SelectMany(j => AssemblyMotion.CoordinateNames(assembly.Name, j,
            j.Family == MechanicalInterfaceFamily.Prismatic ? AssemblyMotionDimension.Length : AssemblyMotionDimension.Angle).Select(name => (Name: name, Joint: j)));
        foreach (var group in coordinates.GroupBy(c => c.Name).Where(g => g.Select(c => c.Joint.MateStableId).Distinct().Count() > 1))
            diagnostics.Add(new("assembly-motion-coordinate-ambiguous", $"Coordinate '{group.Key}' names more than one realized Mate; give the Mates unambiguous names."));
        if (diagnostics.Any(d => d.Severity == AssemblyDiagnosticSeverity.Error)) return [];
        var curves = new Dictionary<string, AssemblyMotionCurveIr>(StringComparer.Ordinal);
        foreach (var d in source.MotionSyntax.Where(d => d.Type.Name.Text == "Keyframes"))
        {
            var read = new Reader(d, source.SourceIdentity, diagnostics, "Keyframe", generic: true);
            var types = d.Type.Arguments.Select(a => a is { Name: null, Default: null, Value: ReferenceSyntax r } ? r.Name.Text : "").ToArray();
            if (types.Length != 2 || !Dimension(types.ElementAtOrDefault(0), out var domain) || domain == AssemblyMotionDimension.Length
                || !Dimension(types.ElementAtOrDefault(1), out var value) || value == AssemblyMotionDimension.Time)
            { read.Error("curve-types-invalid", "Keyframes requires <Time|Angle, Angle|Length>."); read.Complete(); continue; }
            var periodExpression = read.Fields.Take("Period"); double? period = null;
            if (periodExpression is not null)
            {
                if (FirmamentSyntaxValues.TryMeasure(periodExpression, AssemblyMotion.Unit(domain), out var number) && number > 0) period = number;
                else read.Error("period-invalid", $"Keyframes period requires positive {domain}.");
            }
            var curve = Curve(read, domain, value, period);
            if (!curves.TryAdd((d.Name?.Text ?? "<unnamed>"), curve)) read.Error("curve-duplicate", $"Keyframes '{d.Name?.Text}' is duplicated.");
            read.Complete();
        }
        var result = new List<AssemblyMotionIr>();
        foreach (var d in source.MotionSyntax.Where(d => d.Type.Name.Text == "Motion"))
        {
            var read = new Reader(d, source.SourceIdentity, diagnostics, "Track");
            var duration = read.Fields.Number("Duration", "s");
            if (!double.IsFinite(duration) || duration <= 0) read.Error("duration-invalid", "Motion duration requires a positive Time in s or ms.");
            var repeat = read.Fields.Reference("Repeat");
            if (!Enum.TryParse<AssemblyMotionRepeat>(repeat, out var repeating) || !Enum.IsDefined(repeating))
                read.Error("repeat-invalid", "Motion repeat requires Once or Loop.");
            var tracks = new List<AssemblyMotionTrackIr>();
            foreach (var track in d.Body?.Statements.OfType<DeclarationSyntax>().Where(t => t.Type.Name.Text == "Track") ?? [])
            {
                var item = new Reader(track, source.SourceIdentity, diagnostics, "Keyframe");
                var target = item.Fields.Reference("Target") ?? "";
                var joint = (assembly.Joints ?? []).SingleOrDefault(j => AssemblyMotion.CoordinateNames(assembly.Name, j,
                    j.Family == MechanicalInterfaceFamily.Prismatic ? AssemblyMotionDimension.Length : AssemblyMotionDimension.Angle).Contains(target, StringComparer.Ordinal));
                if (joint is null || joint.DegreesOfFreedom != 1)
                { item.Error("target-invalid", $"Track '{track.Name?.Text}' target '{target}' must name one realized Revolute angle or Prismatic translation."); item.Complete(); continue; }
                var dimension = joint.Family == MechanicalInterfaceFamily.Revolute ? AssemblyMotionDimension.Angle : AssemblyMotionDimension.Length;
                var values = item.Fields.Reference("Values");
                var input = item.Fields.Take("Input");
                AssemblyMotionCurveIr curve;
                var dependencies = new List<string>();
                if (values is null)
                {
                    if (input is not null) item.Error("input-invalid", "Inline Keyframes use clip Time; input requires a reusable values profile.");
                    curve = Curve(item, AssemblyMotionDimension.Time, dimension, null);
                    if (curve.Keys.Count > 0 && (curve.Keys[0].At != 0 || curve.Keys[^1].At != duration))
                        item.Error("key-domain-invalid", "Inline keys must start at 0s and end at Motion duration.");
                }
                else
                {
                    if (!curves.TryGetValue(values, out curve!))
                    { item.Error("curve-unresolved", $"Keyframes '{values}' was not found."); item.Complete(); continue; }
                    if (curve.ValueDimension != dimension) item.Error("value-type-mismatch", $"Track target requires {dimension}; '{values}' supplies {curve.ValueDimension}.");
                    if (track.Body?.Statements.OfType<DeclarationSyntax>().Any() == true)
                        item.Error("multiple-values", "Track cannot combine a values profile with inline Keyframes.");
                    var variables = AssemblyMotion.Variables(assembly, new Dictionary<string, double>());
                    if (input is null ? curve.Domain != AssemblyMotionDimension.Time
                        : !FirmamentNumericEvaluation.TryEvaluate(input, out var value, variables, typeOnly: true) || value.Unit != AssemblyMotion.Unit(curve.Domain))
                        item.Error("input-type-mismatch", $"Track input must resolve joint coordinates and produce {curve.Domain}.");
                    if (input is not null)
                    {
                        var doc = new DocumentSyntax(source.SourceIdentity, [new ExpressionStatementSyntax(input, input.Span)], [], input.Span);
                        var references = FirmamentSyntaxTraversal.Expressions(doc).Select(FirmamentSyntaxValues.Reference).OfType<string>().ToHashSet(StringComparer.Ordinal);
                        dependencies.AddRange((assembly.Joints ?? []).Where(j => AssemblyMotion.CoordinateNames(assembly.Name, j,
                            j.Family == MechanicalInterfaceFamily.Prismatic ? AssemblyMotionDimension.Length : AssemblyMotionDimension.Angle).Any(references.Contains)).Select(j => j.MateStableId));
                    }
                }
                tracks.Add(new((track.Name?.Text ?? "<unnamed>"), joint.MateStableId, joint.Name, curve, input, dependencies, item.Span));
                item.Complete();
            }
            if (tracks.Count == 0) read.Error("tracks-missing", "Motion requires at least one admitted Track.");
            foreach (var group in tracks.GroupBy(t => t.Name).Where(g => g.Count() > 1)) read.Error("track-duplicate", $"Track name '{group.Key}' is duplicated.");
            foreach (var group in tracks.GroupBy(t => t.MateStableId).Where(g => g.Count() > 1)) read.Error("multiple-writers", $"Multiple Tracks drive realized Mate '{group.First().JointName}'.");
            if (result.Any(m => m.Name == (d.Name?.Text ?? "<unnamed>"))) read.Error("duplicate", $"Motion '{(d.Name?.Text ?? "<unnamed>")}' is duplicated.");
            var ordered = new List<AssemblyMotionTrackIr>();
            var pending = tracks.ToList();
            var written = tracks.Select(t => t.MateStableId).ToHashSet(StringComparer.Ordinal);
            var resolved = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count > 0)
            {
                var ready = pending.Where(t => t.Dependencies.All(id => !written.Contains(id) || resolved.Contains(id))).ToArray();
                if (ready.Length == 0) { read.Error("driver-cycle", "Motion coordinate drivers contain a dependency cycle."); break; }
                foreach (var track in ready) { ordered.Add(track); resolved.Add(track.MateStableId); pending.Remove(track); }
            }
            result.Add(new((d.Name?.Text ?? "<unnamed>"), duration, repeating, ordered, read.Span));
            read.Complete();
        }
        if (diagnostics.Any(d => d.Severity == AssemblyDiagnosticSeverity.Error)) return result;
        var compiled = assembly with { Motions = result };
        foreach (var clip in result)
        {
            var start = AssemblyMotion.Evaluate(compiled, clip.Name, 0);
            var end = AssemblyMotion.Evaluate(compiled, clip.Name, clip.DurationSeconds, preserveEndpoint: true);
            diagnostics.AddRange(start.Diagnostics.Concat(end.Diagnostics).Distinct());
            if (clip.Repeat == AssemblyMotionRepeat.Loop && start.IsSuccess && end.IsSuccess
                && start.Instances.Zip(end.Instances).Any(pair => pair.First.ResolvedTransform!.Matrix.Zip(pair.Second.ResolvedTransform!.Matrix).Any(v => Math.Abs(v.First - v.Second) > 1e-9)))
                diagnostics.Add(new("assembly-motion-loop-discontinuous", $"Motion '{clip.Name}' has discontinuous occurrence poses at its loop endpoints.") { SourceSpan = clip.SourceSpan });
        }
        return result;
    }

    private static AssemblyMotionCurveIr Curve(Reader read, AssemblyMotionDimension domain, AssemblyMotionDimension value, double? period)
    {
        var interpolation = read.Fields.Reference("Interpolation");
        if (!Enum.TryParse<AssemblyMotionInterpolation>(interpolation, out var mode) || !Enum.IsDefined(mode))
            read.Error("interpolation-invalid", "Specify Linear, Hold or Cosine interpolation.");
        var keys = new List<AssemblyMotionKeyIr>();
        foreach (var key in read.Declaration.Body?.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Keyframe") ?? [])
        {
            var item = new Reader(key, read.File, read.Diagnostics);
            var at = item.Fields.Number("At", AssemblyMotion.Unit(domain));
            var amount = item.Fields.Number("Value", AssemblyMotion.Unit(value));
            if (!double.IsFinite(at) || !double.IsFinite(amount)) item.Error("key-type-mismatch", $"Keyframe requires finite {domain} at and {value} value.");
            if (keys.Any(k => k.Name == (key.Name?.Text ?? "<unnamed>"))) item.Error("key-duplicate", $"Keyframe '{(key.Name?.Text ?? "<unnamed>")}' is duplicated.");
            if (keys.Count > 0 && at <= keys[^1].At) item.Error("key-order-invalid", "Keyframe at values must be strictly increasing and unique.");
            keys.Add(new((key.Name?.Text ?? "<unnamed>"), at, amount, item.Span)); item.Complete();
        }
        if (keys.Count < 2) read.Error("keys-missing", "A curve requires at least two named Keyframes.");
        if (period is { } length && keys.Count > 0)
        {
            if (keys[0].At != 0 || keys[^1].At != length) read.Error("period-endpoints-invalid", "Periodic curves must include keys at zero and period.");
            if (keys[0].Value != keys[^1].Value) read.Error("period-values-discontinuous", "Periodic profile endpoint values must agree; use unwrapped inline keys for full-turn motion.");
        }
        return new((read.Declaration.Name?.Text ?? "<unnamed>"), domain, value, mode, period, keys);
    }
    private static bool Dimension(string? name, out AssemblyMotionDimension dimension) => Enum.TryParse(name, out dimension) && Enum.IsDefined(dimension);
    private static AssemblyDiagnostic Error(string code, string message, string file, FirmamentV2SourceSpan span) =>
        new("assembly-motion-" + code, message) { SourceSpan = new(file, span.Start, span.Length) };
    private sealed class Reader
    {
        internal readonly DeclarationSyntax Declaration;
        internal readonly string File;
        internal readonly List<AssemblyDiagnostic> Diagnostics;
        internal readonly FirmamentSyntaxFields Fields;
        private readonly List<string> errors = [];
        internal SemanticSourceSpan Span => new(File, Declaration.Span.Start, Declaration.Span.Length);
        internal Reader(DeclarationSyntax declaration, string file, List<AssemblyDiagnostic> diagnostics, string? child = null, bool generic = false)
        {
            Declaration = declaration; File = file; Diagnostics = diagnostics;
            Fields = new(declaration with { Body = declaration.Body is null ? null : declaration.Body with
                { Statements = declaration.Body.Statements.OfType<FieldSyntax>().Cast<StatementSyntax>().ToArray() } }, errors);
            if (declaration.Name is null || declaration.Body is null || declaration.Modifiers.Count > 0 || declaration.Clauses.Count > 0
                || declaration.Annotation is not null || declaration.ResultType is not null || declaration.Initializer is not null || declaration.Target is not null
                || declaration.HasParameterList || declaration.Occurrence || !generic && declaration.Type.Arguments.Count > 0)
                Error("header-invalid", $"Invalid {declaration.Type.Name.Text} declaration header.");
            foreach (var statement in declaration.Body?.Statements.Where(s => s is not FieldSyntax) ?? [])
                if (statement is not DeclarationSyntax d || d.Type.Name.Text != child)
                    Diagnostics.Add(AssemblyMotionAuthoring.Error("statement-unknown", $"{declaration.Type.Name.Text} admits fields{(child is null ? "" : " and " + child + " declarations")}.", File, statement.Span));
        }
        internal void Error(string code, string message) => Diagnostics.Add(AssemblyMotionAuthoring.Error(code, message, File, Declaration.Span));
        internal void Complete()
        {
            Fields.Complete();
            foreach (var message in errors) Error("field-invalid", Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop.FirmamentV2DiagnosticAdapter.MessageFor(message));
        }
    }
}
