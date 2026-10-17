using System.Collections.Immutable;

namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Lexical Template declarations and applications from the production AST.
/// Text slices only transport checked nodes into the existing specialization lowerer.</summary>
internal sealed class FirmamentTemplateSyntax
{
    internal sealed class Scope(Scope? parent, string identity)
    {
        private readonly FirmamentBindingScope symbols = new(parent?.symbols, identity);
        private readonly Dictionary<DeclarationSyntax, TemplateDeclarationIr> declarations = parent?.declarations ?? new();
        internal string Identity => symbols.Identity;
        internal bool Declare(DeclarationSyntax syntax, TemplateDeclarationIr template)
        {
            declarations[syntax] = template;
            return symbols.Declare("Template", syntax);
        }
        internal TemplateDeclarationIr? Resolve(string name) => symbols.Resolve("Template", name) is { } syntax
            ? declarations[syntax] : null;
    }

    internal Scope Root { get; }
    internal Scope ExportScope { get; }
    internal int ExportPosition { get; }
    internal List<TemplateDeclarationIr> Declarations { get; } = [];
    internal List<TemplateDeclarationIr> RemovableDeclarations { get; } = [];
    internal List<TemplateApplicationIr> Applications { get; } = [];
    internal List<FirmamentV2ValidationDiagnostic> Diagnostics { get; } = [];

    internal FirmamentTemplateSyntax(string source, List<string> diagnostics, Scope? parent = null)
    {
        Root = new(parent, parent is null ? "document" : parent.Identity + "/specialization");
        ExportScope = Root;
        var bodyScopes = new Dictionary<DeclarationSyntax, Scope>();
        var parsed = FirmamentSyntaxParser.Parse(source);
        if (!parsed.IsSuccess)
        {
            // This owner cannot relabel another construct's syntax failure as a
            // Template error. The normal frontend/owning expander presents it.
            var tokens = parsed.Document.Tokens.Where(t => t.Kind is not (SyntaxTokenKind.Whitespace or SyntaxTokenKind.Comment)).ToArray();
            var hasProductPrefix = tokens.Zip(tokens.Skip(1)).Any(pair => pair.First.Kind == SyntaxTokenKind.Identifier
                && pair.First.Text == "Template" && pair.Second.Text == "<");
            if (hasProductPrefix || FirmamentSyntaxTraversal.Declarations(parsed.Document).Any(d => d.Modifiers.Any(m => m.Name.Text == "Template")))
                diagnostics.AddRange(parsed.Diagnostics.Where(d => d.Severity is "fatal" or "error")
                    .Select(d => FirmamentV2TemplateExpansion.Prefix + "syntax: " + d.Code + ": " + d.Message));
            return;
        }
        Visit(parsed.Document.Statements, Root, null);
        // Existing host libraries use one Model as their module container. Keep
        // that explicit compatibility form; never pool declarations from siblings.
        var models = parsed.Document.Statements.OfType<DeclarationSyntax>()
            .Where(d => d.Type.Name.Text == "Model" && d.Body is not null && d.Modifiers.Count == 0).ToArray();
        if (!Declarations.Any(d => ReferenceEquals(d.DefinitionScope, Root)) && models.Length == 1
            && bodyScopes.TryGetValue(models[0], out var moduleScope))
        { ExportScope = moduleScope; ExportPosition = models[0].Body!.ContentSpan.Start; }
        foreach (var group in Declarations.GroupBy(d => d.Name).Where(g => g.Count() > 1))
            foreach (var template in group) template.IdentityNamespace = template.DefinitionScope!.Identity;

        void Visit(IReadOnlyList<StatementSyntax> statements, Scope scope, TemplateDeclarationIr? owner)
        {
            var definitions = new Dictionary<DeclarationSyntax, TemplateDeclarationIr>();
            // Register all siblings before resolving any application (forward references).
            foreach (var declaration in statements.OfType<DeclarationSyntax>())
            {
                var modifier = declaration.Modifiers.FirstOrDefault(m => m.Name.Text == "Template");
                if (modifier is null) continue; // finite feature Templates have their existing owner
                var kind = Kind(declaration);
                if (kind is null || declaration.Name is null || declaration.Body is null
                    || declaration.HasParameterList || declaration.Initializer is not null
                    || declaration.ResultType is not null || declaration.Clauses.Count != 0
                    || declaration.Type.Span.Length != declaration.Type.Name.Span.Length || declaration.Occurrence
                    || modifier.Span.Length == modifier.Name.Span.Length
                    || declaration.Modifiers.Count(m => m.Name.Text == "Template") != 1
                    || declaration.Modifiers.Count(m => m.Name.Text == "Concept") > 1
                    || declaration.Modifiers.Any(m => m.Name.Text is not ("Template" or "Concept")))
                {
                    Error(FirmamentV2TemplateExpansion.Prefix + "invalid-declaration-header", "A product Template requires a named body and cannot carry ignored parameters, placement or initializer clauses.", declaration);
                    continue;
                }
                var parameters = ImmutableArray.CreateBuilder<TemplateParameterIr>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in modifier.Arguments)
                {
                    if (parameter.Name is null || !names.Add(parameter.Name.Text))
                    {
                        diagnostics.Add((parameter.Name is null ? FirmamentV2TemplateExpansion.InvalidParameter
                            : FirmamentV2TemplateExpansion.DuplicateParameter) + ":" + parameter.Name?.Text);
                        continue;
                    }
                    if (parameter.Value is TypeParameterSyntax { Constraint: ReferenceSyntax constraint } type && parameter.Default is null)
                        parameters.Add(new TemplateTypeParameterIr(type.Name.Text, constraint.Name.Text, parameter.Span));
                    else if (parameter.Value is ReferenceSyntax value)
                        parameters.Add(new TemplateValueParameterIr(parameter.Name.Text, value.Name.Text,
                            parameter.Default is null ? null : Text(parameter.Default.Span), parameter.Span) { DefaultSpan = parameter.Default?.Span });
                    else diagnostics.Add(FirmamentV2TemplateExpansion.InvalidParameter + ":" + parameter.Name.Text);
                }
                var template = new TemplateDeclarationIr(declaration.Name.Text, kind,
                    declaration.Annotation is null ? "" : ": " + Text(declaration.Annotation.Span),
                    parameters.ToImmutable(), Text(declaration.Body.ContentSpan), declaration.Span)
                {
                    DefinitionScope = scope,
                    BodySpan = declaration.Body.ContentSpan,
                    BodyScope = new(scope, scope.Identity + "/" + kind + " " + declaration.Name.Text + "@" + declaration.Span.Start),
                };
                if (!scope.Declare(declaration, template)) Error(FirmamentV2TemplateExpansion.DuplicateName + ":" + template.Name, $"Template '{template.Name}' is already declared in scope '{scope.Identity}'.", declaration);
                definitions[declaration] = template;
                Declarations.Add(template);
                if (owner is null) RemovableDeclarations.Add(template);
            }
            foreach (var statement in statements)
            {
                if (statement is StatementBlockSyntax block) { Visit(block.Body.Statements, scope, owner); continue; }
                if (statement is MappingSyntax mapping)
                {
                    Visit([mapping.Value], new(scope, scope.Identity + "/mapping " + mapping.Key.Text), owner);
                    continue;
                }
                if (statement is not DeclarationSyntax declaration) continue;
                if (definitions.TryGetValue(declaration, out var definition))
                {
                    Visit(declaration.Body!.Statements, definition.BodyScope!, definition);
                    continue;
                }
                if (Kind(declaration) is { } kind && declaration.Name is not null
                    && declaration.Initializer is SpecializationSyntax { Target: ReferenceSyntax target } application)
                {
                    var template = scope.Resolve(target.Name.Text);
                    if (template is null)
                    {
                        Error(FirmamentV2TemplateExpansion.Prefix + "not-found:" + target.Name.Text, $"Template '{target.Name.Text}' is not visible from scope '{scope.Identity}'. Declare it in this scope or an enclosing scope.", declaration);
                        continue;
                    }
                    if (declaration.HasParameterList || declaration.Annotation is not null || declaration.ResultType is not null
                        || declaration.Body is not null || declaration.Clauses.Count != 0 || declaration.Modifiers.Any(m => m.Name.Text != "Concept"))
                    {
                        Error(FirmamentV2TemplateExpansion.Prefix + "invalid-application-header:" + declaration.Name.Text, "A Template application cannot carry ignored placement, parameter, annotation or body clauses. Put supported placement in the resulting construction.", declaration);
                        continue;
                    }
                    var arguments = ImmutableArray.CreateBuilder<TemplateArgumentIr>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var argument in application.Arguments)
                    {
                        if (argument.Name is null || argument.Default is not null)
                            diagnostics.Add(FirmamentV2TemplateExpansion.UnknownArgument + ":" + Text(argument.Span));
                        else if (!seen.Add(argument.Name.Text)) diagnostics.Add(FirmamentV2TemplateExpansion.DuplicateArgument + ":" + argument.Name.Text);
                        else arguments.Add(new(argument.Name.Text, Text(argument.Value.Span), argument.Span));
                    }
                    var bound = new TemplateApplicationIr(kind, declaration.Name.Text, target.Name.Text, arguments.ToImmutable(), declaration.Span)
                        { BoundTemplate = template, BindingScope = scope.Identity };
                    if (owner is null) Applications.Add(bound);
                    else owner.BodyApplications.Add(bound);
                }
                if (declaration.Body is { } body)
                {
                    var child = new Scope(scope, scope.Identity + "/" + declaration.Type.Name.Text + " " + declaration.Name?.Text + "@" + declaration.Span.Start);
                    bodyScopes[declaration] = child;
                    Visit(body.Statements, child, owner);
                }
            }
        }
        void Error(string code, string message, DeclarationSyntax declaration)
        {
            diagnostics.Add(code);
            Diagnostics.Add(new(code, "fatal", message, declaration.Name?.Text, declaration.Type.Name.Text)
            {
                SourceFile = "inline#pre-template-expansion", SourceSpan = declaration.Span,
                IsOriginalSource = false, Stage = "Template binding",
                LocationNote = "Location identifies the enclosing declaration before Template specialization; original-source mapping is applied by the frontend where available.",
            });
        }
        string Text(FirmamentV2SourceSpan span) => source.Substring(span.Start, span.Length);
    }

    private static string? Kind(DeclarationSyntax declaration) => declaration.Type.Name.Text switch
    {
        "Struct" when declaration.Modifiers.Any(m => m.Name.Text == "Concept") => "Concept Struct",
        "Struct" or "Model" or "Panel" or "SheetMetal" or "ProfileDelta" or "Profile" or "Subassembly" => declaration.Type.Name.Text,
        _ => null,
    };
}
