namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Shared structural traversal for syntax owners and developer inspection.</summary>
public static class FirmamentSyntaxTraversal
{
    public static IEnumerable<DeclarationSyntax> Declarations(DocumentSyntax document) =>
        Statements(document).OfType<DeclarationSyntax>();

    public static IEnumerable<StatementSyntax> Statements(DocumentSyntax document) => document.Statements.SelectMany(Descendants);

    public static IEnumerable<ExpressionSyntax> Expressions(DocumentSyntax document) =>
        Statements(document).SelectMany(s => s switch
        {
            FieldSyntax f => Expressions(f.Value),
            ExpressionStatementSyntax e => Expressions(e.Value),
            ReturnSyntax r => Expressions(r.Value),
            DeclarationSyntax d => (d.Annotation is null ? [] : Expressions(d.Annotation))
                .Concat(d.Initializer is null ? [] : Expressions(d.Initializer))
                .Concat(d.Clauses.SelectMany(c => Expressions(c.Value))),
            _ => []
        });

    public static IEnumerable<ExpressionSyntax> Expressions(ExpressionSyntax expression)
    {
        yield return expression;
        IEnumerable<ExpressionSyntax> children = expression switch
        {
            AliasSyntax a => [a.Value],
            BinarySyntax b => [b.Left, b.Right],
            UnarySyntax u => [u.Operand],
            QuantitySyntax q => [q.Value],
            SequenceSyntax s => s.Items.Select(a => a.Value),
            CallSyntax c => new[] { c.Target }.Concat(c.Arguments.Select(a => a.Value)),
            SpecializationSyntax s => new[] { s.Target }.Concat(s.Arguments.Select(a => a.Value)),
            MemberSyntax m => [m.Target],
            IndexSyntax i => i.Index is null ? [i.Target] : [i.Target, i.Index],
            WithSyntax w => [w.Target],
            TraceSyntax t => new[] { t.Source, t.From, t.To }.OfType<ExpressionSyntax>(),
            TypedValueSyntax t => [t.Type, t.Value],
            ObjectSyntax o => o.Type is null ? [] : [o.Type],
            FrameSyntax f => new[] { f.Origin }.Concat(f.Axes.Select(a => a.Value)),
            MemberSetSyntax m => new[] { m.Target }.Concat(m.Members.Select(a => a.Value)),
            MatchSyntax m => new[] { m.Scrutinee }.Concat(m.Arms.SelectMany(a => new[] { a.Pattern, a.Value })),
            _ => []
        };
        foreach (var child in children)
            foreach (var nested in Expressions(child)) yield return nested;
    }

    private static IEnumerable<StatementSyntax> Descendants(StatementSyntax statement)
    {
        yield return statement;
        foreach (var child in ChildStatements(statement))
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    public static IEnumerable<DeclarationSyntax> Declarations(StatementSyntax statement)
    {
        if (statement is DeclarationSyntax declaration) yield return declaration;
        foreach (var child in ChildStatements(statement))
            foreach (var nested in Declarations(child)) yield return nested;
    }

    private static IEnumerable<StatementSyntax> ChildStatements(StatementSyntax statement) => statement switch
    {
        DeclarationSyntax d => (d.Body?.Statements ?? []).Concat(d.Initializer is null ? [] : ExpressionBodies(d.Initializer))
            .Concat(d.Annotation is null ? [] : ExpressionBodies(d.Annotation)),
        FieldSyntax f => ExpressionBodies(f.Value),
        ExpressionStatementSyntax e => ExpressionBodies(e.Value),
        ReturnSyntax r => ExpressionBodies(r.Value),
        MappingSyntax m => [m.Value],
        _ => []
    };

    private static IEnumerable<StatementSyntax> ExpressionBodies(ExpressionSyntax expression) => expression switch
    {
        ObjectSyntax o => o.Body.Statements.Concat(o.Type is null ? [] : ExpressionBodies(o.Type)),
        WithSyntax w => w.Body.Statements.Concat(ExpressionBodies(w.Target)),
        SequenceSyntax s => s.Items.SelectMany(a => ExpressionBodies(a.Value)),
        CallSyntax c => ExpressionBodies(c.Target).Concat(c.Arguments.SelectMany(a => ExpressionBodies(a.Value))),
        SpecializationSyntax s => ExpressionBodies(s.Target).Concat(s.Arguments.SelectMany(a => ExpressionBodies(a.Value))),
        BinarySyntax b => ExpressionBodies(b.Left).Concat(ExpressionBodies(b.Right)),
        UnarySyntax u => ExpressionBodies(u.Operand),
        MemberSyntax m => ExpressionBodies(m.Target),
        IndexSyntax i => ExpressionBodies(i.Target).Concat(i.Index is null ? [] : ExpressionBodies(i.Index)),
        QuantitySyntax q => ExpressionBodies(q.Value),
        FrameSyntax f => ExpressionBodies(f.Origin).Concat(f.Axes.SelectMany(a => ExpressionBodies(a.Value))),
        AliasSyntax a => ExpressionBodies(a.Value),
        TraceSyntax t => ExpressionBodies(t.Source).Concat(t.From is null ? [] : ExpressionBodies(t.From)).Concat(t.To is null ? [] : ExpressionBodies(t.To)),
        TypedValueSyntax t => ExpressionBodies(t.Type).Concat(ExpressionBodies(t.Value)),
        MatchSyntax m => ExpressionBodies(m.Scrutinee).Concat(m.Arms.SelectMany(a => ExpressionBodies(a.Pattern).Concat(ExpressionBodies(a.Value)))),
        MemberSetSyntax m => ExpressionBodies(m.Target).Concat(m.Members.SelectMany(a => ExpressionBodies(a.Value))),
        _ => []
    };

}
