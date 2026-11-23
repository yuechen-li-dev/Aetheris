using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.PlasticShell;

/// <summary>
/// A <c>Mold</c> block declares the tooling intent for a part: pull direction, parting, cosmetic faces and the
/// molding policy. The part is built through the ordinary compiler, unchanged; the Mold block only reads it.
/// </summary>
public static partial class MoldFirmament
{
    private static readonly HashSet<string> Fields = new(StringComparer.OrdinalIgnoreCase)
        { "part", "material", "policy", "stage", "pull", "parting", "cosmetic", "texture", "minimumDraft", "inserts" };

    public static bool LooksLikeMold(string? source) => source is not null && source.Contains("Mold", StringComparison.Ordinal)
        && FirmamentSyntaxParser.Parse(source).Document.Statements.OfType<DeclarationSyntax>().Any(d => d.Type.Name.Text == "Mold");

    public static MoldCompileResult CompileFile(string path) => Compile(File.ReadAllText(path), Path.GetFullPath(path));

    public static MoldCompileResult Compile(string source, string sourcePath = "authored.firmament")
    {
        var diagnostics = new List<MoldDiagnostic>();
        var document = FirmamentSyntaxParser.Parse(source, sourcePath).Document;
        var molds = document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Mold").ToArray();
        if (molds.Length != 1 || molds[0].Name is null)
            return Fail(diagnostics, MoldDiagnosticCodes.SourceInvalid, "Declare exactly one named Mold block per file.");
        var mold = molds[0];
        var intent = ReadIntent(source, document, mold, diagnostics);
        if (intent is null || diagnostics.Any(d => d.Severity == PlasticDiagnosticSeverity.Error)) return new(false, intent, null, null, diagnostics);

        // The part builds exactly as it would without the Mold block: blank the block out, keeping every other span.
        var partSource = string.Concat(source.AsSpan(0, mold.Span.Start), new string(' ', mold.Span.Length), source.AsSpan(mold.Span.Start + mold.Span.Length));
        var built = FirmamentBuildAndExport.CompileSource(partSource, Path.GetDirectoryName(sourcePath));
        if (!built.IsSuccess)
        {
            diagnostics.AddRange(built.Diagnostics.Select(d => new MoldDiagnostic(MoldDiagnosticCodes.PartBuildFailed, PlasticDiagnosticSeverity.Error, d.Message, intent.PartName)));
            return new(false, intent, null, null, diagnostics);
        }
        var body = Step242Importer.ImportBody(built.Value.StepText);
        if (!body.IsSuccess)
            return Fail(diagnostics, MoldDiagnosticCodes.PartBuildFailed, "The part's STEP did not import as one closed body: " + string.Join("; ", body.Diagnostics.Select(d => d.Message)), intent.PartName, intent);

        var report = MoldAnalysis.Analyze(body.Value, intent, diagnostics, out var inserts);
        if (report is not null && intent.Requirements is { Count: > 0 } requires && report.Measurements is { } measured)
            report = report with { Requirements = CheckRequirements(source, requires, measured, diagnostics, intent.MoldName) };
        string? cavityStep = null, coreStep = null;
        Dictionary<string, string>? slideSteps = null;
        if (inserts is not null)
        {
            var cavity = Step242Exporter.ExportBody(inserts.Cavity);
            var core = Step242Exporter.ExportBody(inserts.Core);
            var slides = inserts.Slides.Select(s => (s.Name, Step: Step242Exporter.ExportBody(s.Body))).ToArray();
            if (cavity.IsSuccess && core.IsSuccess && slides.All(s => s.Step.IsSuccess))
            {
                cavityStep = cavity.Value; coreStep = core.Value;
                slideSteps = slides.ToDictionary(s => s.Name, s => s.Step.Value, StringComparer.Ordinal);
            }
            else diagnostics.Add(new(MoldDiagnosticCodes.InsertsUnavailable, PlasticDiagnosticSeverity.Error,
                "The inserts were cut but did not export: " + string.Join("; ", cavity.Diagnostics.Concat(core.Diagnostics).Concat(slides.SelectMany(s => s.Step.Diagnostics)).Select(d => d.Message)), intent.MoldName));
        }
        return new(report is not null && !diagnostics.Any(d => d.Severity == PlasticDiagnosticSeverity.Error), intent, report, built.Value.StepText, diagnostics, cavityStep, coreStep, slideSteps);
    }

    /// <summary>The measurement names a Mold <c>Require</c> can use, and their values as quantities.</summary>
    public static IReadOnlyDictionary<string, string> MeasurementValues(MoldMeasurements m)
    {
        static string Mm(double v) => $"{v.ToString("0.#########", CultureInfo.InvariantCulture)}mm";
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["draft.minimum"] = $"{m.MinimumDraftDegrees.ToString("0.#########", CultureInfo.InvariantCulture)}deg",
            ["wall.minimum"] = Mm(m.MinimumWall), ["wall.maximum"] = Mm(m.MaximumWall), ["wall.nominal"] = Mm(m.NominalWall),
            ["corner.sharp"] = m.SharpInsideCorners.ToString(CultureInfo.InvariantCulture), ["corner.minimumRadius"] = Mm(m.MinimumInsideRadius),
            ["undercuts"] = m.Undercuts.ToString(CultureInfo.InvariantCulture), ["actions"] = m.Actions.ToString(CultureInfo.InvariantCulture),
            ["gates"] = m.Gates.ToString(CultureInfo.InvariantCulture), ["ejectors"] = m.Ejectors.ToString(CultureInfo.InvariantCulture),
            ["knit.count"] = m.KnitLines.ToString(CultureInfo.InvariantCulture), ["knit.cosmetic"] = m.KnitLinesOnCosmetic.ToString(CultureInfo.InvariantCulture),
        };
        // The fill is measured only from declared gates.
        if (m.FlowRatio is { } ratio) values["flow.ratio"] = ratio.ToString("0.#########", CultureInfo.InvariantCulture);
        if (m.FlowLength is { } length) values["flow.length"] = Mm(length);
        return values;
    }

    private static IReadOnlyList<MoldRequirementEvidence> CheckRequirements(string source, IReadOnlyList<MoldRequirement> requires, MoldMeasurements measured,
        List<MoldDiagnostic> diagnostics, string owner)
    {
        var results = FirmamentRequirements.Check(source, requires.Select(r => (r.Name, r.Expression)).ToArray(), MeasurementValues(measured));
        foreach (var r in results.Where(r => !r.Passed))
            diagnostics.Add(new(r.Valid ? MoldDiagnosticCodes.RequirementFailed : MoldDiagnosticCodes.RequirementInvalid, PlasticDiagnosticSeverity.Error,
                r.Valid ? $"Require {r.Name} => {r.Expression} fails: {r.Message}" : $"Require {r.Name}: {r.Message}", owner));
        return results.Where(r => r.Name != "<concepts>").Select(r => new MoldRequirementEvidence(r.Name, r.Expression, r.Passed, r.Message)).ToArray();
    }

    private static MoldIntent? ReadIntent(string source, DocumentSyntax document, DeclarationSyntax mold, List<MoldDiagnostic> diagnostics)
    {
        var name = mold.Name!.Text;
        var fields = new Dictionary<string, FieldSyntax>(StringComparer.OrdinalIgnoreCase);
        var actionDeclarations = new List<DeclarationSyntax>();
        var gateDeclarations = new List<DeclarationSyntax>();
        var ejectorDeclarations = new List<DeclarationSyntax>();
        var requirements = new List<MoldRequirement>();
        foreach (var statement in mold.Body?.Statements ?? [])
        {
            if (statement is DeclarationSyntax { Type.Name.Text: "Action" } declaration) { actionDeclarations.Add(declaration); continue; }
            if (statement is DeclarationSyntax { Type.Name.Text: "Gate" } gate) { gateDeclarations.Add(gate); continue; }
            if (statement is DeclarationSyntax { Type.Name.Text: "Ejector" } ejector) { ejectorDeclarations.Add(ejector); continue; }
            if (statement is DeclarationSyntax { Type.Name.Text: "Require" } require)
            {
                if (require.Name is null || require.Initializer is null || require.Body is not null)
                    Error(diagnostics, MoldDiagnosticCodes.RequirementInvalid, "A Mold requirement is Require name => Concept(measurement, ...).", name);
                else if (requirements.Any(r => r.Name == require.Name.Text))
                    Error(diagnostics, MoldDiagnosticCodes.RequirementInvalid, $"Require {require.Name.Text} is declared twice.", name);
                else requirements.Add(new(require.Name.Text, source.Substring(require.Initializer.Span.Start, require.Initializer.Span.Length)));
                continue;
            }
            if (statement is not FieldSyntax field) { Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, $"Mold {name} takes fields, and Action, Gate, Ejector and Require declarations.", name); continue; }
            if (!Fields.Contains(field.Name.Text)) { Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, $"Unknown Mold field '{field.Name.Text}'. Known fields: {string.Join(", ", Fields.Order())}.", name); continue; }
            if (!fields.TryAdd(field.Name.Text, field)) Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, $"Mold field '{field.Name.Text}' is declared twice.", name);
        }
        string Text(ExpressionSyntax e) => source.Substring(e.Span.Start, e.Span.Length).Trim();

        var models = document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Model" && d.Name is not null).Select(d => d.Name!.Text).ToArray();
        var part = fields.TryGetValue("part", out var partField) ? Text(partField.Value) : models.Length == 1 ? models[0] : null;
        if (part is null || !models.Contains(part, StringComparer.Ordinal))
            Error(diagnostics, MoldDiagnosticCodes.PartUnresolved, part is null
                ? "Name the part: `part: ModelName`. The file declares " + (models.Length == 0 ? "no Model." : $"{models.Length} Models.")
                : $"part '{part}' is not a Model declared in this file.", name);

        if (!fields.TryGetValue("material", out var materialField)) { Error(diagnostics, MoldDiagnosticCodes.MaterialUnknown, $"Mold {name} needs a material: one of {string.Join(", ", MoldingMaterials.Defaults.Keys)}.", name); return null; }
        if (!MoldingMaterials.Defaults.TryGetValue(Text(materialField.Value), out var policy))
        { Error(diagnostics, MoldDiagnosticCodes.MaterialUnknown, $"Unknown material '{Text(materialField.Value)}'. Known: {string.Join(", ", MoldingMaterials.Defaults.Keys)}.", name); return null; }
        if (fields.TryGetValue("policy", out var policyField))
            policy = ReadPolicy(source, Text(policyField.Value), policy, diagnostics, name);
        if (fields.TryGetValue("minimumDraft", out var draftField))
        {
            if (TryAngle(Text(draftField.Value), out var degrees) && degrees >= 0 && degrees < 45) policy = policy with { MinimumDraftDegrees = degrees };
            else Error(diagnostics, MoldDiagnosticCodes.PolicyInvalid, "minimumDraft takes an angle in [0deg, 45deg).", name);
        }

        var stage = MoldStage.Concept;
        if (fields.TryGetValue("stage", out var stageField) && !Enum.TryParse(Text(stageField.Value), false, out stage))
            Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, "stage is Concept, Prototype or Tooling.", name);

        Direction3D? pull = null;
        if (!fields.TryGetValue("pull", out var pullField)) Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, "Mold needs pull: the direction the +pull half opens, such as +Z or [0, 0, 1].", name);
        else if (TryDirection(Text(pullField.Value), out var direction)) pull = direction;
        else Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, $"pull '{Text(pullField.Value)}' is not an axis (+Z) or a nonzero vector ([0, 0, 1]).", name);

        MoldPartingPlane? parting = null;
        if (fields.TryGetValue("parting", out var partingField) && pull is { } p)
            parting = ReadParting(source, partingField.Value, p, diagnostics, name);

        var cosmetic = fields.TryGetValue("cosmetic", out var cosmeticField) ? Selectors(source, cosmeticField.Value, diagnostics, name) : [];
        MoldTexture? texture = null;
        if (fields.TryGetValue("texture", out var textureField))
        {
            if (textureField.Value is not ObjectSyntax obj) Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, "texture is { faces: [face(...)], depth: 0.025mm }.", name);
            else
            {
                var inner = obj.Body.Statements.OfType<FieldSyntax>().ToDictionary(f => f.Name.Text, StringComparer.OrdinalIgnoreCase);
                if (!inner.TryGetValue("faces", out var faces) || !inner.TryGetValue("depth", out var depth) || !TryLength(Text(depth.Value), out var mm) || mm <= 0)
                    Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, "texture needs faces: [face(...)] and a positive depth.", name);
                else texture = new(Selectors(source, faces.Value, diagnostics, name), mm);
            }
        }
        var actions = new List<MoldActionIntent>();
        foreach (var declaration in actionDeclarations)
        {
            var actionName = declaration.Name?.Text;
            if (actionName is null) { Error(diagnostics, MoldDiagnosticCodes.ActionInvalid, "An Action needs a name: Action slide { pull: -X }.", name); continue; }
            var inner = (declaration.Body?.Statements ?? []).OfType<FieldSyntax>().ToDictionary(f => f.Name.Text, StringComparer.OrdinalIgnoreCase);
            foreach (var key in inner.Keys.Where(k => !k.Equals("pull", StringComparison.OrdinalIgnoreCase) && !k.Equals("near", StringComparison.OrdinalIgnoreCase)))
                Error(diagnostics, MoldDiagnosticCodes.ActionInvalid, $"Action {actionName}: unknown field '{key}'. An Action takes pull and, optionally, near.", name);
            if (!inner.TryGetValue("pull", out var actionPull) || !TryDirection(Text(actionPull.Value), out var actionDirection))
            { Error(diagnostics, MoldDiagnosticCodes.ActionInvalid, $"Action {actionName} needs pull: the direction it withdraws along, such as -X or [1, 0, 0].", name); continue; }
            if (pull is { } main && double.Abs(actionDirection.ToVector().Dot(main.ToVector())) > 1 - 1e-9)
            { Error(diagnostics, MoldDiagnosticCodes.ActionInvalid, $"Action {actionName} pulls along the main pull; the halves already move that way.", name); continue; }
            Point3D? near = null;
            if (inner.TryGetValue("near", out var nearField))
            {
                if (TryPoint(Text(nearField.Value), out var point)) near = point;
                else { Error(diagnostics, MoldDiagnosticCodes.ActionInvalid, $"Action {actionName}: near is a point [x, y, z] in mm.", name); continue; }
            }
            if (actions.Any(a => a.Name == actionName)) { Error(diagnostics, MoldDiagnosticCodes.ActionInvalid, $"Action {actionName} is declared twice.", name); continue; }
            actions.Add(new(actionName, actionDirection, near));
        }
        var gates = new List<MoldGateIntent>();
        foreach (var declaration in gateDeclarations)
        {
            var gateName = declaration.Name?.Text;
            if (gateName is null) { Error(diagnostics, MoldDiagnosticCodes.GateInvalid, "A Gate needs a name: Gate main { kind: Edge; at: [x, y, z] }.", name); continue; }
            var inner = (declaration.Body?.Statements ?? []).OfType<FieldSyntax>().ToDictionary(f => f.Name.Text, StringComparer.OrdinalIgnoreCase);
            foreach (var key in inner.Keys.Where(k => k is not ("kind" or "at")))
                Error(diagnostics, MoldDiagnosticCodes.GateInvalid, $"Gate {gateName}: unknown field '{key}'. A Gate takes kind and at.", name);
            if (!inner.TryGetValue("kind", out var kindField) || !Enum.TryParse<MoldGateKind>(Text(kindField.Value), false, out var kind))
            { Error(diagnostics, MoldDiagnosticCodes.GateInvalid, $"Gate {gateName} needs kind: Edge, Submarine, Pin or Direct.", name); continue; }
            if (!inner.TryGetValue("at", out var atField) || !TryPoint(Text(atField.Value), out var at))
            { Error(diagnostics, MoldDiagnosticCodes.GateInvalid, $"Gate {gateName} needs at: a point [x, y, z] in mm on the part.", name); continue; }
            if (gates.Any(g => g.Name == gateName)) { Error(diagnostics, MoldDiagnosticCodes.GateInvalid, $"Gate {gateName} is declared twice.", name); continue; }
            gates.Add(new(gateName, kind, at));
        }
        var ejectors = new List<MoldEjectorIntent>();
        foreach (var declaration in ejectorDeclarations)
        {
            var pinName = declaration.Name?.Text;
            if (pinName is null) { Error(diagnostics, MoldDiagnosticCodes.EjectorInvalid, "An Ejector needs a name: Ejector e1 { at: [x, y, z]; diameter: 4mm }.", name); continue; }
            var inner = (declaration.Body?.Statements ?? []).OfType<FieldSyntax>().ToDictionary(f => f.Name.Text, StringComparer.OrdinalIgnoreCase);
            foreach (var key in inner.Keys.Where(k => k is not ("at" or "diameter")))
                Error(diagnostics, MoldDiagnosticCodes.EjectorInvalid, $"Ejector {pinName}: unknown field '{key}'. An Ejector takes at and diameter.", name);
            if (!inner.TryGetValue("at", out var atField) || !TryPoint(Text(atField.Value), out var at))
            { Error(diagnostics, MoldDiagnosticCodes.EjectorInvalid, $"Ejector {pinName} needs at: a point [x, y, z] in mm on the part.", name); continue; }
            if (!inner.TryGetValue("diameter", out var diameterField) || !TryLength(Text(diameterField.Value), out var diameter) || diameter <= 0)
            { Error(diagnostics, MoldDiagnosticCodes.EjectorInvalid, $"Ejector {pinName} needs diameter: a positive length such as 4mm.", name); continue; }
            if (ejectors.Any(e => e.Name == pinName)) { Error(diagnostics, MoldDiagnosticCodes.EjectorInvalid, $"Ejector {pinName} is declared twice.", name); continue; }
            ejectors.Add(new(pinName, at, diameter));
        }
        double? margin = null;
        if (fields.TryGetValue("inserts", out var insertsField))
        {
            if (TryLength(Text(insertsField.Value), out var mm) && mm > 0) margin = mm;
            else Error(diagnostics, MoldDiagnosticCodes.SourceInvalid, "inserts takes the block's margin around the part, a positive length such as 10mm.", name);
        }
        return pull is null || part is null ? null : new MoldIntent(name, part, policy, stage, pull.Value, parting, cosmetic, texture, actions, margin, requirements, source, gates, ejectors);
    }

    /// <summary>A typed policy Static overrides the material's defaults field by field.</summary>
    private static MoldingPolicy ReadPolicy(string source, string staticName, MoldingPolicy policy, List<MoldDiagnostic> diagnostics, string owner)
    {
        var binding = new FirmamentDomainBinding(source, "mold-policy", "Mold", new HashSet<string>(StringComparer.Ordinal));
        double? Value(string field, string unit)
        {
            var reference = FirmamentSyntaxParser.Parse($"Model M {{ x: {staticName}.{field} }}").Document.Statements.OfType<DeclarationSyntax>().Single().Body!.Statements.OfType<FieldSyntax>().Single().Value;
            if (!binding.TryValue(reference, out var bound) || bound.Value.NumericValue is not { } number) return null;
            return (bound.Value.Unit ?? "") switch
            {
                "rad" when unit == "deg" => number * 180d / double.Pi,
                var u when u == unit || u == "" => number,
                _ => null,
            };
        }
        var draft = Value("MinimumDraft", "deg");
        if (draft is null) Error(diagnostics, MoldDiagnosticCodes.PolicyInvalid, $"policy '{staticName}' does not resolve to a Static with a MinimumDraft angle.", owner);
        return policy with
        {
            Name = $"{policy.Name} with {staticName}",
            MinimumDraftDegrees = draft ?? policy.MinimumDraftDegrees,
            MinimumWall = Value("MinimumWall", "mm") ?? policy.MinimumWall,
            MaximumWall = Value("MaximumWall", "mm") ?? policy.MaximumWall,
            MaximumFlowRatio = Value("MaximumFlowRatio", "") ?? policy.MaximumFlowRatio,
            MinimumInsideRadius = Value("MinimumInsideRadius", "mm") ?? policy.MinimumInsideRadius,
            EjectorClearance = Value("EjectorClearance", "mm") ?? policy.EjectorClearance,
        };
    }

    private static MoldPartingPlane? ReadParting(string source, ExpressionSyntax value, Direction3D pull, List<MoldDiagnostic> diagnostics, string owner)
    {
        string Text(ExpressionSyntax e) => source.Substring(e.Span.Start, e.Span.Length).Trim();
        if (value is not ObjectSyntax { Type: { } type } obj || Text(type) != "Plane")
        { Error(diagnostics, MoldDiagnosticCodes.PartingInvalid, "parting is Plane { at: face(+Z) } or Plane { origin: [x, y, z] } in this phase; stepped and contoured parting come later.", owner); return null; }
        var inner = obj.Body.Statements.OfType<FieldSyntax>().ToDictionary(f => f.Name.Text, StringComparer.OrdinalIgnoreCase);
        if (inner.Keys.Any(k => !k.Equals("at", StringComparison.OrdinalIgnoreCase) && !k.Equals("origin", StringComparison.OrdinalIgnoreCase)) || inner.Count != 1)
        { Error(diagnostics, MoldDiagnosticCodes.PartingInvalid, "A parting Plane takes exactly one of at: face(...) or origin: [x, y, z]; its normal is the pull.", owner); return null; }
        if (inner.TryGetValue("origin", out var origin))
        {
            if (TryPoint(Text(origin.Value), out var point)) return new(point, pull, $"origin {Text(origin.Value)}");
            Error(diagnostics, MoldDiagnosticCodes.PartingInvalid, "origin is [x, y, z] in mm.", owner); return null;
        }
        var selector = Text(inner["at"].Value);
        if (!SelectorRegex().IsMatch(selector) || !selector.StartsWith("face(", StringComparison.Ordinal)) { Error(diagnostics, MoldDiagnosticCodes.SelectorUnresolved, $"'{selector}' is not a face selector; use face(+Z) and the other axis faces.", owner); return null; }
        // The plane through that face is resolved against the built part.
        return new(new Point3D(double.NaN, double.NaN, double.NaN), pull, selector);
    }

    private static IReadOnlyList<string> Selectors(string source, ExpressionSyntax value, List<MoldDiagnostic> diagnostics, string owner)
    {
        var items = value is SequenceSyntax sequence ? sequence.Items.Select(i => i.Value) : [value];
        var result = new List<string>();
        foreach (var item in items)
        {
            var text = source.Substring(item.Span.Start, item.Span.Length).Trim();
            if (SelectorRegex().IsMatch(text)) result.Add(text);
            else Error(diagnostics, MoldDiagnosticCodes.SelectorUnresolved, $"'{text}' is not a face selector; this phase admits the axis faces face(+X) … face(-Z) and the halves side(+pull), side(-pull).", owner);
        }
        return result;
    }

    [GeneratedRegex(@"^(face\(\s*[+-][XYZ]\s*\)|side\(\s*[+-]pull\s*\))$")]
    internal static partial Regex SelectorRegex();

    internal static bool TryDirection(string text, out Direction3D direction)
    {
        direction = default;
        var axis = Regex.Match(text.Replace(" ", ""), @"^([+-])([XYZ])$");
        if (axis.Success)
        {
            var s = axis.Groups[1].Value == "-" ? -1d : 1d;
            direction = Direction3D.Create(axis.Groups[2].Value switch { "X" => new Vector3D(s, 0, 0), "Y" => new Vector3D(0, s, 0), _ => new Vector3D(0, 0, s) });
            return true;
        }
        if (!TryTriple(text, "", out var v) || v.Length < 1e-12) return false;
        direction = Direction3D.Create(v);
        return true;
    }

    private static bool TryPoint(string text, out Point3D point)
    {
        point = default;
        if (!TryTriple(text, "mm", out var v)) return false;
        point = new Point3D(v.X, v.Y, v.Z);
        return true;
    }

    private static bool TryTriple(string text, string unit, out Vector3D value)
    {
        value = default;
        var t = text.Trim();
        if (!t.StartsWith('[') || !t.EndsWith(']')) return false;
        var parts = t[1..^1].Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;
        var numbers = new double[3];
        for (var i = 0; i < 3; i++)
            if (!TryQuantity(parts[i], unit, out numbers[i]) && !double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])) return false;
        value = new Vector3D(numbers[0], numbers[1], numbers[2]);
        return true;
    }

    internal static bool TryAngle(string text, out double degrees)
    {
        if (TryQuantity(text, "deg", out degrees)) return true;
        if (TryQuantity(text, "rad", out var radians)) { degrees = radians * 180d / double.Pi; return true; }
        return false;
    }

    internal static bool TryLength(string text, out double millimetres) => TryQuantity(text, "mm", out millimetres);

    private static bool TryQuantity(string text, string unit, out double value)
    {
        value = double.NaN;
        var m = Regex.Match(text.Trim(), @"^([+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)\s*([A-Za-z]*)$");
        if (!m.Success || !string.Equals(m.Groups[2].Value, unit, StringComparison.Ordinal)) return false;
        return double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static void Error(List<MoldDiagnostic> diagnostics, string code, string message, string? entity) =>
        diagnostics.Add(new(code, PlasticDiagnosticSeverity.Error, message, entity));

    private static MoldCompileResult Fail(List<MoldDiagnostic> diagnostics, string code, string message, string? entity = null, MoldIntent? intent = null)
    {
        Error(diagnostics, code, message, entity);
        return new(false, intent, null, null, diagnostics);
    }
}
