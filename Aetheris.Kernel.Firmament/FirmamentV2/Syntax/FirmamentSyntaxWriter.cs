namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Serialization boundary for existing domain adapters. This writes structured
/// syntax; it performs no name resolution, expression evaluation or source parsing.</summary>
internal sealed class FirmamentSyntaxWriter(string? source = null)
{
    internal static string Statements(IEnumerable<StatementSyntax> statements, string? source = null) => new FirmamentSyntaxWriter(source).WriteStatements(statements);
    internal static string Statement(StatementSyntax statement, string? source = null) => new FirmamentSyntaxWriter(source).WriteStatement(statement);
    internal static string Expression(ExpressionSyntax expression, string? source = null) => new FirmamentSyntaxWriter(source).WriteExpression(expression);
    internal static string Type(TypeSyntax type) => new FirmamentSyntaxWriter().WriteType(type);

    private string WriteStatements(IEnumerable<StatementSyntax> statements) => string.Join("\n", statements.Select(WriteStatement));
    private string WriteBlock(BlockSyntax block) => "{\n" + WriteStatements(block.Statements) + "\n}";
    private string WriteType(TypeSyntax type) => type.Name.Text + (type.Arguments.Count == 0 && type.Span.Length <= type.Name.Span.Length ? "" : "<" + WriteArguments(type.Arguments) + ">");
    private string WriteArguments(IEnumerable<ArgumentSyntax> args) => string.Join(", ", args.Select(a =>
        (a.Name is null ? "" : a.Name.Text + ": ") + WriteExpression(a.Value) + (a.Default is null ? "" : " = " + WriteExpression(a.Default))));
    private string WriteStatement(StatementSyntax s) => s switch
    {
        DeclarationSyntax d => WriteDeclaration(d),
        FieldSyntax f => f.Name.Text + ": " + WriteExpression(f.Value) + ";",
        DirectiveSyntax d => d.Kind.ToString().ToLowerInvariant() + " " + WriteExpression(d.Value),
        ExpressionStatementSyntax e => WriteExpression(e.Value),
        ReturnSyntax r => "return " + WriteExpression(r.Value),
        StatementBlockSyntax b => WriteBlock(b.Body),
        MappingSyntax m => m.Key.Text + " => " + WriteStatement(m.Value),
        RoleRequirementSyntax r => "Role " + r.Name.Text + " requires " + string.Join(", ", r.Capabilities.Select(c => c.Text)),
        InterfaceRelationSyntax r => "Lower " + r.Kind.Text + " " + WriteExpression(r.First) + " " + WriteExpression(r.Second) + (r.Option is null ? "" : " " + WriteExpression(r.Option)),
        InterfaceFitSyntax f => "Fit " + WriteExpression(f.Shaft) + " inside " + WriteExpression(f.Bore) + (f.PerSide ? " per-side" : ""),
        InterfaceMotionSyntax m => "Allow " + m.Kind.Text + ": " + m.Axis.Text,
        InterfacePolicySyntax p => p.Kind.Text + " " + string.Join(" ", p.Values.Select(a => a.Name!.Text + " " + WriteExpression(a.Value))),
        ReplacementSyntax r => "replace " + WriteExpression(r.Target) + " with " + WriteDeclaration(r.Replacement),
        _ => throw new InvalidOperationException($"Unowned syntax serialization: {s.GetType().Name}.")
    };
    private string WriteDeclaration(DeclarationSyntax d)
    {
        var head = string.Join(" ", d.Modifiers.Select(WriteType).Append(WriteType(d.Type)))
            + (d.Target is not null ? " " + WriteExpression(d.Target) : d.Name is null ? "" : " " + d.Name.Text)
            + (!d.HasParameterList && d.Parameters.Count == 0 && d.ResultType is null ? "" : "(" + WriteArguments(d.Parameters) + ")")
            + (d.Annotation is null ? "" : ": " + WriteExpression(d.Annotation))
            + (d.ResultType is null ? "" : " -> " + WriteExpression(d.ResultType))
            + string.Concat(d.Clauses.Select(c => " " + c.Kind + (c.Kind == ClauseKind.Key ? ":" : "") + " " + WriteExpression(c.Value)))
            + (d.Initializer is null ? "" : " = " + WriteExpression(d.Initializer));
        if (d.Occurrence) return "<" + head + ">\n" + WriteStatements(d.Body?.Statements ?? []) + "\n</" + WriteType(d.Type) + ">";
        return head + (d.Body is null ? ";" : " " + WriteBlock(d.Body));
    }
    private string WriteExpression(ExpressionSyntax e) => e switch
    {
        ReferenceSyntax r => r.Name.Text,
        LiteralSyntax l => l.Raw,
        UnarySyntax u => (u.Operator switch { UnaryOperator.Positive => "+", UnaryOperator.Negative => "-", UnaryOperator.Not => "!", _ => "Reverse " }) + WriteExpression(u.Operand),
        BinarySyntax { Operator: BinaryOperator.Pipeline or BinaryOperator.Direction } b => WriteExpression(b.Left) + " " + Operator(b.Operator) + " " + WriteExpression(b.Right),
        BinarySyntax b => "(" + WriteExpression(b.Left) + " " + Operator(b.Operator) + " " + WriteExpression(b.Right) + ")",
        QuantitySyntax q => WriteExpression(q.Value) + q.Unit.Text,
        SequenceSyntax s => (s.Kind == SequenceKind.List ? "[" : "(") + WriteArguments(s.Items) + (s.Kind == SequenceKind.List ? "]" : ")"),
        MemberSyntax m => WriteExpression(m.Target) + "." + m.Member.Text,
        IndexSyntax i => WriteExpression(i.Target) + "[" + (i.Index is null ? "" : WriteExpression(i.Index)) + "]",
        CallSyntax c => WriteExpression(c.Target) + "(" + WriteArguments(c.Arguments) + ")",
        SpecializationSyntax s => WriteSpecialization(s),
        ObjectSyntax o => (o.Type is null ? "" : WriteExpression(o.Type) + " ") + (o.Name is null ? "" : o.Name.Text + " ") + WriteBlock(o.Body),
        WithSyntax w => WriteExpression(w.Target) + " with " + WriteBlock(w.Body),
        FrameSyntax f => WriteExpression(f.Origin) + " " + string.Join(" ", f.Axes.Select(a => a.Name!.Text + WriteExpression(a.Value))),
        AliasSyntax a => WriteExpression(a.Value) + " as " + a.Name.Text,
        TraceSyntax t => WriteExpression(t.Source) + (t.From is null ? "" : " from " + WriteExpression(t.From)) + (t.To is null ? "" : " to " + WriteExpression(t.To)),
        MemberSetSyntax m => WriteExpression(m.Target) + ".[" + WriteArguments(m.Members) + "]",
        TypedValueSyntax t => WriteExpression(t.Type) + " = " + WriteExpression(t.Value),
        ProvenanceSyntax p => WriteExpression(p.Value) + " from " + p.Source.Raw,
        TypeParameterSyntax p => "type " + p.Name.Text + (p.Constraint is null ? "" : " satisfies " + WriteExpression(p.Constraint)),
        MatchSyntax m => "Match " + WriteExpression(m.Scrutinee) + " { " + string.Join("; ", m.Arms.Select(a => WriteExpression(a.Pattern) + " => " + WriteExpression(a.Value))) + " }",
        ToleranceSyntax t => WriteExpression(t.Nominal) + " tol " + WriteExpression(t.Plus) + (t.Minus is null ? "" : " -" + WriteExpression(t.Minus)),
        _ => throw new InvalidOperationException($"Unowned expression serialization: {e.GetType().Name}.")
    };
    private string WriteSpecialization(SpecializationSyntax specialization)
    {
        var span = specialization.Span;
        var target = specialization.Target;
        var arguments = specialization.Arguments;
        // Assembly definition identities historically include the application spelling.
        // Preserve its punctuation/trivia while writing already-bound argument ASTs.
        // No identifier replacement or semantic interpretation occurs here.
        if (source is null || span.Start < 0 || span.Start + span.Length > source.Length
            || target.Span.Start < span.Start || target.Span.Start + target.Span.Length > span.Start + span.Length
            || source.Substring(target.Span.Start, target.Span.Length) != WriteExpression(target)
            || arguments.Any(a => a.Default is not null || a.AuthoredValueSpan.Start < target.Span.Start + target.Span.Length
                || a.AuthoredValueSpan.Start + a.AuthoredValueSpan.Length > span.Start + span.Length))
            return WriteExpression(target) + "<" + WriteArguments(arguments) + ">";
        var text = source.Substring(span.Start, span.Length);
        foreach (var argument in arguments.OrderByDescending(a => a.AuthoredValueSpan.Start))
            text = text.Remove(argument.AuthoredValueSpan.Start - span.Start, argument.AuthoredValueSpan.Length)
                .Insert(argument.AuthoredValueSpan.Start - span.Start, WriteExpression(argument.Value));
        return text;
    }

    internal static string Operator(BinaryOperator op) => op switch
    {
        BinaryOperator.Pipeline => "|>", BinaryOperator.Direction => "->", BinaryOperator.Or => "||", BinaryOperator.And => "&&",
        BinaryOperator.Equal => "==", BinaryOperator.NotEqual => "!=", BinaryOperator.Less => "<", BinaryOperator.LessEqual => "<=",
        BinaryOperator.Greater => ">", BinaryOperator.GreaterEqual => ">=", BinaryOperator.Add => "+", BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*", BinaryOperator.Divide => "/", BinaryOperator.Remainder => "%", BinaryOperator.Power => "^",
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };
}
