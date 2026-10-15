using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Bind shared syntax into the existing relationship IR. No placement or
/// capability solving is performed here; those remain compiler responsibilities.</summary>
internal static class AssemblyInterfaceSyntax
{
    // The sole field vocabulary for this contract owner. User role names A/B
    // remain domain symbols; built-in fields use the shared casing aliases.
    private static readonly string[] Fields = ["Continuity", "Correspondence", "GapTolerance", "ShaftAngle", "EngagementPhase", "AllowedDirection", "Gap", "Clocking", "Orientation"];
    internal sealed record Options(string? Continuity, string Correspondence, double GapTolerance,
        GearInterfaceOptions? Gear, FixedSeatingOptions? Seating);
    internal static bool HasContractHeader(DeclarationSyntax declaration) => declaration.Parameters.Count == 0
        && declaration.Modifiers.Count == 0 && declaration.Clauses.Count == 0 && declaration.Initializer is null
        && declaration.ResultType is null && declaration.Annotation is null && declaration.Target is null && !declaration.Occurrence;

    internal static AssemblyDatumContract BindDatumContract(DeclarationSyntax contract, string file, List<AssemblyDiagnostic> diagnostics)
    {
        var fields = contract.Body!.Statements.OfType<FieldSyntax>().ToArray();
        foreach (var statement in contract.Body.Statements)
            if (statement is not FieldSyntax field || !new[] { "Datum", "Members" }.Any(n => FirmamentSyntaxFields.SameField(field.Name.Text, n)))
                diagnostics.Add(Diagnostic("assembly-datum-fields-invalid", "Datum Interface admits only datum and members fields.", file, statement.Span));
        var datums = fields.Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Datum")).ToArray();
        var members = fields.Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Members")).ToArray();
        var datum = datums.Length == 1 ? Path(datums[0].Value) : null;
        if (datum is null) diagnostics.Add(Diagnostic("assembly-datum-field-invalid", "Datum requires exactly one declared semantic datum path.", file, contract.Span));
        var paths = members.Length == 1 && members[0].Value is SequenceSyntax { Kind: SequenceKind.List } list
            && list.Items.All(i => i.Name is null && i.Default is null) ? list.Items.Select(i => Path(i.Value)).ToArray() : [];
        if (paths.Length == 0 || paths.Any(p => p is null || !p.Contains('.')) || paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
            diagnostics.Add(Diagnostic("assembly-datum-members-invalid", "Members requires one nonempty unique list of semantic port paths.", file, contract.Span));
        return new(datum ?? "", paths.OfType<string>().ToArray());
    }

    internal static Options BindOptions(DeclarationSyntax contract, MechanicalInterfaceFamily family, string file, List<AssemblyDiagnostic> diagnostics)
    {
        FieldSyntax? Field(string name)
        {
            var matches = contract.Body!.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, name)).ToArray();
            if (matches.Length > 1) diagnostics.Add(Diagnostic(name is "Gap" or "Clocking" or "Orientation" ? "assembly-interface-invalid-seating" : "assembly-interface-field-duplicate",
                $"Interface field '{name}' occurs more than once.", file, matches[1].Span));
            return matches.FirstOrDefault();
        }
        double? Number(string name, string unit)
        {
            var field = Field(name); if (field is null) return null;
            var value = Scalar(field.Value, unit);
            if (value is null) diagnostics.Add(Diagnostic(name is "Gap" or "Clocking" ? "assembly-interface-invalid-seating" : "assembly-interface-field-invalid",
                $"Interface field '{name}' requires a finite value in {unit}.", file, field.Span));
            return value;
        }
        string? Symbol(string name, params string[] admitted)
        {
            var field = Field(name); if (field is null) return null;
            var value = (field.Value as ReferenceSyntax)?.Name.Text;
            if (value is null || admitted.Length > 0 && !admitted.Contains(value))
                diagnostics.Add(Diagnostic(name == "Orientation" ? "assembly-interface-invalid-seating" : "assembly-interface-field-invalid",
                    $"Invalid {name}; expected {(admitted.Length == 0 ? "an identifier" : string.Join(" or ", admitted))}.", file, field.Span));
            return value;
        }
        var continuity = Symbol("Continuity", "G0", "G1");
        var correspondence = Symbol("Correspondence", "OppositeDirections", "SameDirection") ?? "OppositeDirections";
        var gapTolerance = Number("GapTolerance", "mm") ?? 1e-6;
        var shaftAngle = Number("ShaftAngle", "deg"); var phase = Number("EngagementPhase", "deg"); var direction = Symbol("AllowedDirection");
        var gap = Number("Gap", "mm"); var clocking = Number("Clocking", "deg");
        var orientation = Symbol("Orientation", "SameDirection", "OpposedDirection");
        FixedSeatingOptions? seating = null;
        if (contract.Body!.Statements.OfType<FieldSyntax>().Any(f => new[] { "Gap", "Clocking", "Orientation" }.Any(n => FirmamentSyntaxFields.SameField(f.Name.Text, n))))
        {
            if (family != MechanicalInterfaceFamily.Fixed)
                diagnostics.Add(Diagnostic("assembly-interface-seating-family-unsupported", "Gap/Clocking/Orientation seating fields are admitted only on Interface<Fixed>.", file, contract.Span));
            seating = new(gap ?? 0, clocking ?? 0, orientation == "OpposedDirection" ? DatumOrientationRelation.OpposedDirection : DatumOrientationRelation.SameDirection);
        }
        if (family != MechanicalInterfaceFamily.Gear && (shaftAngle is not null || phase is not null || direction is not null))
            diagnostics.Add(Diagnostic("assembly-interface-gear-options-family", "ShaftAngle/EngagementPhase/AllowedDirection require Interface<Gear>.", file, contract.Span));
        return new(continuity, correspondence, gapTolerance,
            family == MechanicalInterfaceFamily.Gear ? new(shaftAngle, phase, direction) : null, seating);
    }

    internal static InterfaceRequirementDefinition[] BindRelations(DeclarationSyntax contract, string file, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<InterfaceRequirementDefinition>();
        foreach (var relation in contract.Body!.Statements.OfType<InterfaceRelationSyntax>())
        {
            if (!Enum.TryParse<PlacementConstraintKind>(relation.Kind.Text, out var kind) || !Enum.IsDefined(kind))
            { Error("assembly-interface-relation-unknown", $"Unknown relationship '{relation.Kind.Text}'.", relation.Span); continue; }
            var first = Endpoint(relation.First, kind == PlacementConstraintKind.FrameCoincident);
            var second = Endpoint(relation.Second, kind == PlacementConstraintKind.FrameCoincident);
            if (first is null || second is null)
            { Error("assembly-interface-relation-endpoint", "A relationship requires Role.Member endpoints (or bare frame roles).", relation.Span); continue; }
            double offset = 0;
            var orientation = DatumOrientationRelation.SameDirection;
            if (relation.Option is not null)
            {
                if (kind == PlacementConstraintKind.FrameCoincident && relation.Option is ReferenceSyntax direction
                    && Enum.TryParse(direction.Name.Text, out orientation) && Enum.IsDefined(orientation)) { }
                else if (kind != PlacementConstraintKind.FrameCoincident && Scalar(relation.Option, "mm") is { } value) offset = value;
                else { Error("assembly-interface-relation-option", "Expected a length offset or a supported frame orientation.", relation.Option.Span); continue; }
            }
            result.Add(new(kind, first.Value.Role, first.Value.Member, second.Value.Role, second.Value.Member, offset, orientation));
        }
        return result.ToArray();

        void Error(string code, string message, FirmamentV2SourceSpan span) => diagnostics.Add(Diagnostic(code, message, file, span));
    }

    internal static InterfaceFitDefinition? BindFit(DeclarationSyntax contract, string file, List<AssemblyDiagnostic> diagnostics)
    {
        var fits = contract.Body!.Statements.OfType<InterfaceFitSyntax>().ToArray();
        var policies = contract.Body.Statements.OfType<InterfacePolicySyntax>().ToArray();
        if (fits.Length > 1 || policies.GroupBy(p => p.Kind.Text).Any(g => g.Count() > 1))
            diagnostics.Add(Diagnostic("assembly-interface-duplicate-clause", "Fit, ClearancePolicy and Variation may each occur once.", file, contract.Span));
        InterfaceClearancePolicyDefinition? policy = null;
        ManufacturingVariationDefinition? variation = null;
        foreach (var clause in policies)
        {
            // These are the existing domain IR parameters, now bound from named AST operands.
            var names = clause.Kind.Text == "ClearancePolicy" ? new[] { "Minimum", "Maximum" }
                : ["Linear", "Thickness", "BendAngle", "BendLocation", "Coating", "CoatingTolerance", "Engagement"];
            var values = new List<double>();
            foreach (var name in names)
            {
                var operands = clause.Values.Where(v => v.Name?.Text == name).ToArray();
                var value = operands.Length == 1 ? Scalar(operands[0].Value, name == "BendAngle" ? "deg" : "mm") : null;
                if (value is null)
                    diagnostics.Add(Diagnostic("assembly-interface-policy-invalid", $"{clause.Kind.Text} requires one finite {name} in {(name == "BendAngle" ? "deg" : "mm")}.", file, clause.Span));
                values.Add(value ?? 0);
            }
            foreach (var operand in clause.Values.Where(v => !names.Contains(v.Name?.Text)))
                diagnostics.Add(Diagnostic("assembly-interface-policy-field-unknown", $"Unknown {clause.Kind.Text} operand '{operand.Name?.Text}'.", file, operand.Span));
            if (clause.Kind.Text == "ClearancePolicy") policy = new(values[0], values[1]);
            else variation = new(values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
        }
        if (fits.Length == 0)
        {
            if (policies.Length > 0) diagnostics.Add(Diagnostic("assembly-interface-fit-missing", "ClearancePolicy/Variation requires a Fit relationship.", file, contract.Span));
            return null;
        }
        var fit = fits[0]; var shaft = Endpoint(fit.Shaft); var bore = Endpoint(fit.Bore);
        if (shaft is null || bore is null)
        { diagnostics.Add(Diagnostic("assembly-interface-fit-endpoint", "Fit requires Role.Dimension inside Role.Dimension.", file, fit.Span)); return null; }
        return new(shaft.Value.Role, shaft.Value.Member, bore.Value.Role, bore.Value.Member, fit.PerSide ? .5 : 1, policy, variation);
    }

    internal static InterfacePredicateRequirementDefinition[] BindPredicates(DeclarationSyntax contract, string file, List<AssemblyDiagnostic> diagnostics)
    {
        var result = new List<InterfacePredicateRequirementDefinition>();
        foreach (var requirement in contract.Body!.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Require"))
        {
            if (requirement.Name is null || requirement.Initializer is not BinarySyntax expression
                || Scalar(expression.Left, "mm") is not { } left || Scalar(expression.Right, "mm") is not { } right
                || expression.Operator is not (BinaryOperator.Greater or BinaryOperator.Less or BinaryOperator.GreaterEqual or BinaryOperator.LessEqual or BinaryOperator.Equal))
            { diagnostics.Add(Diagnostic("assembly-interface-requirement-invalid", "Interface Require needs a named comparison of finite lengths.", file, requirement.Span)); continue; }
            var (symbol, passed) = expression.Operator switch
            {
                BinaryOperator.Greater => (">", left > right), BinaryOperator.Less => ("<", left < right),
                BinaryOperator.GreaterEqual => (">=", left >= right), BinaryOperator.LessEqual => ("<=", left <= right),
                _ => ("==", Math.Abs(left - right) <= 1e-12)
            };
            result.Add(new(requirement.Name.Text, FormattableString.Invariant($"{left:R}mm {symbol} {right:R}mm"), passed));
        }
        return result.ToArray();
    }

    internal static void CheckContract(DeclarationSyntax contract, InterfaceRoleDefinition[] roles,
        InterfaceRequirementDefinition[] relations, string file, List<AssemblyDiagnostic> diagnostics)
    {
        foreach (var duplicate in roles.GroupBy(r => r.Name).Where(g => g.Count() > 1))
            diagnostics.Add(Diagnostic("assembly-interface-role-duplicate", $"Role '{duplicate.Key}' is declared more than once.", file, contract.Span));
        var roleNames = roles.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        if (roles.Length == 0 && contract.Type.Arguments.Count > 0) { roleNames.Add("A"); roleNames.Add("B"); }
        foreach (var role in relations.SelectMany(r => new[] { r.FirstRole, r.SecondRole }).Distinct().Where(r => !roleNames.Contains(r)))
            diagnostics.Add(Diagnostic("assembly-interface-role-unresolved", $"Relationship references undeclared Role '{role}'.", file, contract.Span));
        foreach (var fit in contract.Body!.Statements.OfType<InterfaceFitSyntax>())
            foreach (var endpoint in new[] { Endpoint(fit.Shaft), Endpoint(fit.Bore) }.Where(e => e is not null && !roleNames.Contains(e.Value.Role)))
                diagnostics.Add(Diagnostic("assembly-interface-role-unresolved", $"Fit references undeclared Role '{endpoint!.Value.Role}'.", file, fit.Span));
        foreach (var statement in contract.Body.Statements)
        {
            var admitted = statement is RoleRequirementSyntax or InterfaceRelationSyntax or InterfaceFitSyntax or InterfaceMotionSyntax or InterfacePolicySyntax
                || statement is DeclarationSyntax { Type.Name.Text: "Require" }
                || statement is FieldSyntax field && (field.Name.Text is "A" or "B" || Fields.Any(name => FirmamentSyntaxFields.SameField(field.Name.Text, name)));
            if (!admitted) diagnostics.Add(Diagnostic("assembly-interface-statement-unknown", $"Unknown statement or field in Interface '{contract.Name?.Text}'.", file, statement.Span));
            if (statement is InterfaceMotionSyntax motion && (motion.Kind.Text is not ("rotation" or "translation")
                || motion.Axis.Text is not ("along-axis" or "about-axis")))
                diagnostics.Add(Diagnostic("assembly-interface-motion-invalid", "Allow requires translation:along-axis or rotation:about-axis.", file, motion.Span));
        }
    }

    internal static IReadOnlyList<MateSource> BindMates(string source, string file, List<AssemblyDiagnostic> diagnostics)
    {
        var syntax = FirmamentSyntaxParser.Parse(source, file);
        var result = new List<MateSource>();
        foreach (var declaration in FirmamentSyntaxEvidence.Declarations(syntax.Document).Where(d => d.Type.Name.Text is "Mate" or "Interface" && d.Body is not null))
        {
            var statements = declaration.Body!.Statements;
            var typed = declaration.Type.Name.Text == "Interface";
            if (!typed && (declaration.Parameters.Count != 0 || declaration.Type.Arguments.Count != 0 || declaration.Modifiers.Count != 0
                || declaration.Clauses.Count != 0 || declaration.Initializer is not null || declaration.ResultType is not null || declaration.Occurrence))
            { diagnostics.Add(Diagnostic("assembly-mate-header-unsupported", "Mate admits a name, Interface annotation and assignments; placement/header clauses are not supported.", file, declaration.Span)); continue; }
            if (typed && (declaration.Type.Arguments.Count == 0 || statements.OfType<RoleRequirementSyntax>().Any())) continue;
            if (statements.OfType<FieldSyntax>().Any(f => f.Name.Text == "Member")) continue; // existing datum-seat authority
            if (typed && !statements.OfType<FieldSyntax>().Any(f => f.Name.Text is "A" or "B")) continue;
            if (declaration.Name is null || !typed && declaration.Annotation is not ReferenceSyntax)
            { diagnostics.Add(Diagnostic("assembly-mate-malformed", "Mate requires a name, Interface name and role assignments.", file, declaration.Span)); continue; }
            foreach (var error in syntax.Diagnostics.Where(d => d.Severity is "error" or "fatal" && d.SourceSpan is { } span
                && span.Start >= declaration.Span.Start && span.Start < declaration.Span.Start + declaration.Span.Length))
                diagnostics.Add(Diagnostic(error.Code, error.Message, file, error.SourceSpan!));
            var roles = new List<MateRoleAssignment>();
            foreach (var statement in statements)
            {
                if (statement is FieldSyntax field)
                {
                    if (typed && field.Name.Text is not ("A" or "B")) continue;
                    var path = Path(field.Value);
                    if (path is null) diagnostics.Add(Diagnostic("assembly-mate-endpoint-invalid", $"Role '{field.Name.Text}' requires a semantic occurrence path.", file, field.Span));
                    else roles.Add(new(field.Name.Text, AssemblyPath.Parse(path)));
                }
                else if (!typed) diagnostics.Add(Diagnostic("assembly-mate-statement-unknown", "A Mate body contains only role assignments.", file, statement.Span));
            }
            foreach (var duplicate in roles.GroupBy(r => r.Role).Where(g => g.Count() > 1))
                diagnostics.Add(Diagnostic("assembly-mate-role-duplicate", $"Role '{duplicate.Key}' is assigned more than once.", file, declaration.Span));
            var name = declaration.Name.Text;
            result.Add(new(name, typed ? name : ((ReferenceSyntax)declaration.Annotation!).Name.Text, roles,
                new(file + "#assembly-body", declaration.Span.Start, declaration.Span.Length)));
        }
        return result;
    }

    private static (string Role, string Member)? Endpoint(ExpressionSyntax expression, bool allowBare = false) => expression switch
    {
        MemberSyntax { Target: ReferenceSyntax role } member => (role.Name.Text, member.Member.Text),
        ReferenceSyntax role when allowBare => (role.Name.Text, "."), _ => null
    };
    private static string? Path(ExpressionSyntax expression) => expression switch
    {
        ReferenceSyntax reference => reference.Name.Text,
        MemberSyntax member when Path(member.Target) is { } path => path + "." + member.Member.Text, _ => null
    };
    private static double? Scalar(ExpressionSyntax expression, string unit)
        => FirmamentSyntaxValues.TryMeasure(expression, unit, out var value) && double.IsFinite(value) ? value : null;
    internal static AssemblyDiagnostic Diagnostic(string code, string message, string file, FirmamentV2SourceSpan span)
        => new(code, message) { SourceSpan = new(file + "#expanded-assembly-scope", span.Start, span.Length),
            LocationNote = "Offsets are relative to the expanded Assembly scope; an exact original-file mapping is unavailable." };
}
