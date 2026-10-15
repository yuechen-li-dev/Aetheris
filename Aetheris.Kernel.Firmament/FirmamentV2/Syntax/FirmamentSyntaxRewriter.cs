namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Immutable syntax transformation. Identifier declarations, field labels and
/// member labels are distinct from references; none are inferred from source substrings.</summary>
internal class FirmamentSyntaxRewriter
{
    internal virtual BlockSyntax Block(BlockSyntax block) => block with { Statements = block.Statements.Select(Statement).ToArray() };
    internal virtual ArgumentSyntax Argument(ArgumentSyntax a) => a with { Value = Expression(a.Value), Default = a.Default is null ? null : Expression(a.Default) };
    internal virtual TypeSyntax Type(TypeSyntax t) => t with { Arguments = t.Arguments.Select(Argument).ToArray() };
    internal virtual StatementSyntax Statement(StatementSyntax s) => s switch
    {
        DeclarationSyntax d => d with { Type = Type(d.Type), Modifiers = d.Modifiers.Select(Type).ToArray(),
            Parameters = d.Parameters.Select(Argument).ToArray(), Annotation = Optional(d.Annotation), ResultType = Optional(d.ResultType),
            Target = Optional(d.Target), Clauses = d.Clauses.Select(c => c with { Value = Expression(c.Value) }).ToArray(),
            Initializer = Optional(d.Initializer), Body = d.Body is null ? null : Block(d.Body) },
        FieldSyntax f => f with { Value = Expression(f.Value) },
        DirectiveSyntax d => d with { Value = Expression(d.Value) },
        ExpressionStatementSyntax e => e with { Value = Expression(e.Value) },
        ReturnSyntax r => r with { Value = Expression(r.Value) },
        MappingSyntax m => m with { Value = Statement(m.Value) },
        InterfaceRelationSyntax r => r with { First = Expression(r.First), Second = Expression(r.Second), Option = Optional(r.Option) },
        InterfaceFitSyntax f => f with { Shaft = Expression(f.Shaft), Bore = Expression(f.Bore) },
        InterfacePolicySyntax p => p with { Values = p.Values.Select(Argument).ToArray() },
        ReplacementSyntax r => r with { Target = Expression(r.Target), Replacement = (DeclarationSyntax)Statement(r.Replacement) },
        RoleRequirementSyntax or InterfaceMotionSyntax => s,
        _ => throw new InvalidOperationException($"Unowned syntax transformation: {s.GetType().Name}.")
    };
    protected ExpressionSyntax? Optional(ExpressionSyntax? e) => e is null ? null : Expression(e);
    internal virtual ExpressionSyntax Expression(ExpressionSyntax e) => e switch
    {
        UnarySyntax u => u with { Operand = Expression(u.Operand) },
        BinarySyntax b => b with { Left = Expression(b.Left), Right = Expression(b.Right) },
        QuantitySyntax q => q with { Value = Expression(q.Value) },
        ToleranceSyntax t => t with { Nominal = Expression(t.Nominal), Plus = Expression(t.Plus), Minus = Optional(t.Minus) },
        SequenceSyntax s => s with { Items = s.Items.Select(Argument).ToArray() },
        MemberSyntax m => m with { Target = Expression(m.Target) },
        IndexSyntax i => i with { Target = Expression(i.Target), Index = Optional(i.Index) },
        CallSyntax c => c with { Target = Expression(c.Target), Arguments = c.Arguments.Select(Argument).ToArray() },
        SpecializationSyntax s => s with { Target = Expression(s.Target), Arguments = s.Arguments.Select(Argument).ToArray() },
        ObjectSyntax o => o with { Type = Optional(o.Type), Body = Block(o.Body) },
        WithSyntax w => w with { Target = Expression(w.Target), Body = Block(w.Body) },
        FrameSyntax f => f with { Origin = Expression(f.Origin), Axes = f.Axes.Select(Argument).ToArray() },
        AliasSyntax a => a with { Value = Expression(a.Value) },
        TraceSyntax t => t with { Source = Expression(t.Source), From = Optional(t.From), To = Optional(t.To) },
        MemberSetSyntax m => m with { Target = Expression(m.Target), Members = m.Members.Select(Argument).ToArray() },
        TypedValueSyntax t => t with { Type = Expression(t.Type), Value = Expression(t.Value) },
        ProvenanceSyntax p => p with { Value = Expression(p.Value) },
        TypeParameterSyntax p => p with { Constraint = Optional(p.Constraint) },
        MatchSyntax m => m with { Scrutinee = Expression(m.Scrutinee), Arms = m.Arms.Select(a => a with { Pattern = Expression(a.Pattern), Value = Expression(a.Value) }).ToArray() },
        ReferenceSyntax or LiteralSyntax => e,
        _ => throw new InvalidOperationException($"Unowned expression transformation: {e.GetType().Name}.")
    };
}

/// <summary>Substitutes already-bound AST values. Nested parameters and mapping binders
/// shadow their enclosing environment. Declaration renaming is explicit and separate.</summary>
internal sealed class FirmamentSyntaxSubstitution(IReadOnlyDictionary<string, ExpressionSyntax> values,
    IReadOnlyDictionary<string, string>? renames = null) : FirmamentSyntaxRewriter
{
    private readonly Stack<HashSet<string>> shadows = new();
    private bool Shadowed(string name) => shadows.Any(s => s.Contains(name.Split('.')[0]));
    internal override StatementSyntax Statement(StatementSyntax statement)
    {
        var names = statement switch
        {
            DeclarationSyntax d => d.Parameters.Select(p => p.Name?.Text).OfType<string>()
                .Concat(d.Modifiers.Where(m => m.Name.Text == "Template").SelectMany(m => m.Arguments).Select(p => p.Name?.Text).OfType<string>())
                .Concat(d.Type.Name.Text == "Feature" ? d.Body?.Statements.OfType<DeclarationSyntax>().Where(l => l.Type.Name.Text == "let").Select(l => l.Name?.Text).OfType<string>() ?? [] : []),
            MappingSyntax m => [m.Key.Text],
            _ => []
        };
        shadows.Push(names.ToHashSet(StringComparer.Ordinal));
        var rewritten = base.Statement(statement);
        shadows.Pop();
        if (rewritten is DeclarationSyntax { Name: { } name } declaration && renames?.TryGetValue(name.Text, out var replacement) == true)
            return declaration with { Name = name with { Text = replacement } };
        return rewritten;
    }
    internal override ExpressionSyntax Expression(ExpressionSyntax expression)
    {
        // A constructor's type is not a value reference, even when a parameter
        // happens to have the same spelling (Profile: Profile, for example).
        if (expression is ObjectSyntax obj) return obj with { Body = Block(obj.Body) };
        if (expression is TypedValueSyntax typed) return typed with { Value = Expression(typed.Value) };
        if (expression is SpecializationSyntax specialization) return specialization with { Arguments = specialization.Arguments.Select(Argument).ToArray() };
        if (expression is CallSyntax { Target: ReferenceSyntax } call) return call with { Arguments = call.Arguments.Select(Argument).ToArray() };
        if (FirmamentSyntaxValues.Reference(expression) is { } name && expression is ReferenceSyntax or MemberSyntax && !Shadowed(name))
        {
            if (values.TryGetValue(name, out var value)) return value;
            if (renames?.TryGetValue(name, out var replacement) == true) return new ReferenceSyntax(new(replacement, expression.Span), expression.Span);
        }
        return base.Expression(expression);
    }
    // This is value substitution. Declaration type/variant names occupy the type
    // namespace; product Template type specialization has its separate owner.
    internal override TypeSyntax Type(TypeSyntax type) => type;
}
