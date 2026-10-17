using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Shared finite-domain admission. Selection is separate from value
/// evaluation: every arm is bound, but only the selected value is evaluated by
/// its existing scalar or spatial owner.</summary>
internal sealed class FirmamentChoiceBinding
{
    internal sealed record Choice(string Name, IReadOnlyList<string> Cases, FirmamentV2SourceSpan Span);
    internal IReadOnlyDictionary<string, Choice> Types { get; }
    internal IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics { get; }

    internal FirmamentChoiceBinding(IEnumerable<Choice> types)
    {
        Types = types.ToDictionary(t => t.Name, StringComparer.Ordinal);
        Diagnostics = [];
    }

    internal FirmamentChoiceBinding(string source)
    {
        var types = new Dictionary<string, Choice>(StringComparer.Ordinal);
        var errors = new List<FirmamentV2ValidationDiagnostic>();
        foreach (var d in FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document)
                     .Where(d => d.Type.Name.Text == "Enum" && d.Name is not null))
        {
            if (!char.IsUpper(d.Name!.Text[0]) && !d.Name.Text.StartsWith("__firmamentData_", StringComparison.Ordinal))
                Error(ConceptIrResolver.InvalidEnumName, $"Enum type '{d.Name.Text}' must use PascalCase.", d.Name.Span);
            var cases = new List<string>();
            foreach (var statement in d.Body?.Statements ?? [])
            {
                if (statement is not ExpressionStatementSyntax { Value: ReferenceSyntax r }
                    || r.Name.Text.Length == 0 || !char.IsUpper(r.Name.Text[0]))
                { Error(ConceptIrResolver.InvalidEnumName, "An Enum case must be a PascalCase tag without fields or payload.", statement.Span); continue; }
                if (cases.Contains(r.Name.Text, StringComparer.Ordinal))
                    Error(ConceptIrResolver.DuplicateEnumVariant, $"Duplicate case '{r.Name.Text}' in {d.Name!.Text}.", r.Span);
                else cases.Add(r.Name.Text);
            }
            if (cases.Count == 0) Error("firmament-static-enum-empty", $"Enum '{d.Name!.Text}' must declare at least one case.", d.Span);
            if (!types.TryAdd(d.Name!.Text, new(d.Name.Text, cases, d.Span)))
                Error("firmament-binding-duplicate-name", $"Duplicate Enum '{d.Name.Text}'.", d.Span);
        }
        Types = types;
        Diagnostics = errors;
        void Error(string code, string message, FirmamentV2SourceSpan span) => errors.Add(new(code, "error", message)
            { SourceSpan = span, Stage = "Choice binding" });
    }

    internal string? Check(MatchSyntax match, string? domain, Func<ExpressionSyntax, string?> typeOf,
        Action<FirmamentV2ValidationDiagnostic> error, IReadOnlyList<string>? finitePatternDomain = null)
    {
        var expected = domain == "Int" && finitePatternDomain is not null ? finitePatternDomain
            : domain == "Bool" ? new[] { "true", "false" }
            : domain is not null && Types.TryGetValue(domain, out var choice) ? choice.Cases : null;
        if (expected is null)
        { Fail(ConceptIrResolver.InvalidMatchScrutinee, $"match requires an Enum or Bool; found {domain ?? "an unresolved value"}.", match.Scrutinee.Span); return null; }
        if (match.Arms.Count == 0)
        { Fail(ConceptIrResolver.NonExhaustiveMatch, "match requires at least one arm.", match.Span); return null; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var failed = false;
        foreach (var arm in match.Arms)
        {
            var tag = Pattern(arm.Pattern, domain!);
            if (tag is null || !expected.Contains(tag, StringComparer.Ordinal))
            { Fail(domain == "Bool" ? ConceptIrResolver.InvalidBooleanArm : ConceptIrResolver.UnknownEnumVariant,
                $"Unknown case '{FirmamentSyntaxWriter.Expression(arm.Pattern)}' for {domain}.", arm.Pattern.Span); failed = true; }
            else if (!seen.Add(tag))
            { Fail(ConceptIrResolver.DuplicateMatchArm, $"Duplicate match arm '{tag}' for {domain}.", arm.Pattern.Span); failed = true; }
        }
        foreach (var missing in expected.Where(c => !seen.Contains(c)))
        { Fail(ConceptIrResolver.NonExhaustiveMatch, $"Non-exhaustive match on {domain}. Missing arm: {missing}.", match.Span); failed = true; }
        string? result = null;
        foreach (var arm in match.Arms)
        {
            var type = typeOf(arm.Value);
            if (type is null) { failed = true; continue; }
            result ??= type;
            if (result != type)
            { Fail(ConceptIrResolver.MatchArmTypeMismatch, $"Match arm '{FirmamentSyntaxWriter.Expression(arm.Pattern)}' requires {result}; found {type}.", arm.Value.Span); failed = true; }
        }
        return failed ? null : result;
        void Fail(string code, string message, FirmamentV2SourceSpan span) => error(new(code, "error", message)
            { SourceSpan = span, Stage = "Choice binding" });
    }

    internal static string? Pattern(ExpressionSyntax expression, string domain) => expression switch
    {
        ReferenceSyntax r when domain != "Int" && r.Name.Text != "_" => r.Name.Text,
        LiteralSyntax { Kind: LiteralKind.Number } n when domain == "Int"
            && long.TryParse(n.Raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer) => integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
        LiteralSyntax { Kind: LiteralKind.Boolean } b when domain == "Bool" => b.Raw,
        MemberSyntax { Target: ReferenceSyntax type } m when type.Name.Text == domain => m.Member.Text,
        _ => null
    };

    internal static IReadOnlyList<MatchSyntax> OutermostMatches(DocumentSyntax document)
    {
        var result = new List<MatchSyntax>();
        var coveredUntil = -1;
        foreach (var match in FirmamentSyntaxTraversal.Expressions(document).OfType<MatchSyntax>()
            .OrderBy(m => m.Span.Start).ThenByDescending(m => m.Span.Length))
            if (match.Span.Start >= coveredUntil)
            { result.Add(match); coveredUntil = match.Span.Start + match.Span.Length; }
        return result;
    }

    // Type algebra is shared by choice admission and scalar inference. Values
    // and domain objects are evaluated only by their existing owners.
    internal static string? Infer(ExpressionSyntax e, Func<ExpressionSyntax, string?> reference,
        Func<MatchSyntax, string?> match) => e switch
    {
        ReferenceSyntax or MemberSyntax or CallSyntax or IndexSyntax or WithSyntax => reference(e),
        MatchSyntax m => match(m),
        ToleranceSyntax t => Infer(t.Nominal, reference, match),
        TypedValueSyntax t => FirmamentSyntaxValues.Reference(t.Type),
        ObjectSyntax o => FirmamentSyntaxValues.Reference(o.Type),
        QuantitySyntax q => q.Unit.Text switch { "mm" => "Length", "deg" => "Angle", "s" or "ms" => "Time", _ => null },
        LiteralSyntax { Kind: LiteralKind.Boolean } => "Bool",
        LiteralSyntax { Kind: LiteralKind.String } => "String",
        LiteralSyntax { Kind: LiteralKind.Number } n => n.Raw.Contains('.') || n.Raw.Contains('e', StringComparison.OrdinalIgnoreCase) ? "Float" : "Int",
        SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } g => Infer(g.Items[0].Value, reference, match),
        SequenceSyntax { Kind: SequenceKind.List } list => SequenceType(list, reference, match),
        UnarySyntax u => Infer(u.Operand, reference, match) is { } type
            ? u.Operator == UnaryOperator.Not ? type == "Bool" ? "Bool" : null
                : u.Operator is UnaryOperator.Positive or UnaryOperator.Negative && type is "Int" or "Float" or "Length" or "Angle" or "Time" ? type : null : null,
        BinarySyntax b => BinaryType(Infer(b.Left, reference, match), b.Operator, Infer(b.Right, reference, match)),
        _ => null
    };

    private static string? SequenceType(SequenceSyntax list, Func<ExpressionSyntax, string?> reference, Func<MatchSyntax, string?> match)
    {
        var types = list.Items.Select(a => Infer(a.Value, reference, match)).ToArray();
        if (types.Length == 0 || types.Any(t => t is null)) return null;
        var type = types.All(t => t is "Int" or "Float") && types.Contains("Float") ? "Float" : types[0];
        return types.All(t => t == type || type == "Float" && t == "Int") ? type + "[" + types.Length + "]" : null;
    }

    internal static string? BinaryType(string? left, BinaryOperator op, string? right)
    {
        if (left is null || right is null) return null;
        if (!FirmamentV2Parser.TryParseLetType(left, out var l) || !FirmamentV2Parser.TryParseLetType(right, out var r)) return null;
        return FirmamentV2Parser.CompileTimeArithmeticType(l, FirmamentSyntaxWriter.Operator(op), r)?.ToString();
    }
}
