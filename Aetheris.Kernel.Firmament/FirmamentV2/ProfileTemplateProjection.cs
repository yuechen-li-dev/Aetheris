using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Specialization hygiene for Profile-local construction. References and
/// published segment names are distinct from fields, types and string contents.</summary>
internal sealed class ProfileTemplateProjection(string instance) : FirmamentSyntaxRewriter
{
    private readonly Dictionary<BlockSyntax, FirmamentBindingScope> scopes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<DeclarationSyntax, string> names = new(ReferenceEqualityComparer.Instance);
    private readonly Stack<HashSet<string>> parameters = new();
    private FirmamentBindingScope scope = new(null, "profile");

    internal static string Render(string instance, string tail, string body, List<string> diagnostics)
    {
        var syntax = FirmamentSyntaxParser.Parse(body);
        if (!syntax.IsSuccess)
        {
            diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
            return "Profile " + instance + tail + " {" + body + "}";
        }
        var block = new BlockSyntax(syntax.Document.Statements, syntax.Document.Span) { Delimited = false };
        var projection = new ProfileTemplateProjection(instance);
        projection.Collect(block, null);
        var result = projection.Block(block).Statements;
        var guides = result.OfType<DeclarationSyntax>().Where(IsGuide).ToHashSet<StatementSyntax>();
        return FirmamentSyntaxWriter.Statements(result.Where(guides.Contains), body) + "\nProfile " + instance + tail + " {\n"
            + FirmamentSyntaxWriter.Statements(result.Where(s => !guides.Contains(s)), body) + "\n}";
    }

    private static bool IsGuide(DeclarationSyntax d) =>
        d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept")
        || ProfileAuthoringParser.OwnsDeclaration(d) && d.Type.Name.Text is not ("Profile" or "Span");

    private void Collect(BlockSyntax block, FirmamentBindingScope? parent)
    {
        var local = new FirmamentBindingScope(parent, "profile specialization");
        scopes.Add(block, local);
        foreach (var declaration in block.Statements.OfType<DeclarationSyntax>())
        {
            // Loop and Segment labels have their own identity namespace. They do
            // not shadow the construction guide a trace inside them references.
            if (declaration.Type.Name.Text is not ("Loop" or "Segment")) local.Declare("value", declaration);
            if (declaration.Name is {} name && IsGuide(declaration)) names.Add(declaration, instance + "_" + name.Text);
        }
        foreach (var declaration in block.Statements.OfType<DeclarationSyntax>())
            if (declaration.Body is {} child) Collect(child, local);
        foreach (var nested in block.Statements.OfType<StatementBlockSyntax>()) Collect(nested.Body, local);
    }

    internal override BlockSyntax Block(BlockSyntax block)
    {
        var enclosing = scope;
        if (!scopes.TryGetValue(block, out var local)) { Collect(block, enclosing); local = scopes[block]; }
        scope = local;
        var result = base.Block(block);
        scope = enclosing;
        return result;
    }

    internal override StatementSyntax Statement(StatementSyntax statement)
    {
        var shadowed = statement is DeclarationSyntax d ? d.Parameters.Select(p => p.Name?.Text).OfType<string>()
            : statement is MappingSyntax m ? [m.Key.Text] : Enumerable.Empty<string>();
        parameters.Push(shadowed.ToHashSet(StringComparer.Ordinal));
        var result = base.Statement(statement);
        parameters.Pop();
        if (statement is DeclarationSyntax original && result is DeclarationSyntax { Name: {} label } rewritten)
        {
            if (names.TryGetValue(original, out var renamed)) return rewritten with { Name = label with { Text = renamed } };
            // Retain existing published specialization identities when a Segment
            // was named after a local guide. Do not rename unrelated labels.
            if (original.Type.Name.Text == "Segment" && Resolve(label.Text) is {} guide && names.TryGetValue(guide, out renamed))
                return rewritten with { Name = label with { Text = renamed } };
        }
        return result;
    }

    private DeclarationSyntax? Resolve(string name) => parameters.Any(p => p.Contains(name)) ? null : scope.Resolve("value", name);
    private DeclarationSyntax? Referent(ExpressionSyntax expression) => expression switch
    {
        ReferenceSyntax r => Resolve(r.Name.Text),
        MemberSyntax m => Referent(m.Target)?.Body?.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Name?.Text == m.Member.Text),
        _ => null
    };

    internal override ExpressionSyntax Expression(ExpressionSyntax expression)
    {
        if (expression is ReferenceSyntax r && Resolve(r.Name.Text) is {} declaration && names.TryGetValue(declaration, out var name))
            return r with { Name = r.Name with { Text = name } };
        if (expression is MemberSyntax m)
            return m with { Target = Expression(m.Target), Member = Referent(m) is {} member && names.TryGetValue(member, out var renamed)
                ? m.Member with { Text = renamed } : m.Member };
        if (expression is AliasSyntax alias && Resolve(alias.Name.Text) is {} guide && names.TryGetValue(guide, out var label))
            return alias with { Value = Expression(alias.Value), Name = alias.Name with { Text = label } };
        // Constructor names and typed object names occupy the type namespace.
        if (expression is CallSyntax { Target: ReferenceSyntax } call) return call with { Arguments = call.Arguments.Select(Argument).ToArray() };
        if (expression is ObjectSyntax obj) return obj with { Body = Block(obj.Body) };
        if (expression is TypedValueSyntax typed) return typed with { Value = Expression(typed.Value) };
        return base.Expression(expression);
    }
}
