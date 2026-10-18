using Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;
using System.Globalization;

namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Consumption of an existing declaration tree by its semantic owner.
/// This is not a parser or a field schema. Owners take their fields/children,
/// then close the tree; every remaining statement is an error.</summary>
public sealed class FirmamentSyntaxScope
{
    private readonly string source;
    private readonly string sourceFile;
    private readonly Func<ExpressionSyntax, ExpressionSyntax>? bind;
    private readonly Func<FirmamentV2SourceSpan, FirmamentV2SourceSpan?>? origin;
    private readonly List<FirmamentSyntaxScope> scopes;
    private readonly Dictionary<DeclarationSyntax, FirmamentSyntaxScope> children = [];
    private readonly HashSet<StatementSyntax> consumed = [];
    private readonly List<string> errors = [];
    private readonly FirmamentSyntaxFields fields;
    private readonly HashSet<string> modifiers = new(StringComparer.Ordinal);
    private bool admitAnnotation;
    private bool admitInitializer;
    private bool admitPlacement;
    public FirmamentSyntaxScope AdmitPlacement() { admitPlacement=true; return this; }
    public FirmamentSyntaxScope AdmitAnnotation() { admitAnnotation=true; return this; }
    public FirmamentSyntaxScope AdmitInitializer() { admitInitializer=true; return this; }
    public DeclarationSyntax Declaration { get; }
    public string Name => Declaration.Name?.Text ?? Declaration.Type.Name.Text;
    public string Kind => Declaration.Type.Name.Text;
    public FirmamentV2SourceSpan Span => Declaration.Span;
    public int Start => Span.Start;
    public int Length => Span.Length;
    public FirmamentSyntaxScope Body => this;

    public FirmamentSyntaxScope(DeclarationSyntax declaration, string source, string sourceFile = "memory",
        Func<ExpressionSyntax, ExpressionSyntax>? bind = null,
        Func<FirmamentV2SourceSpan, FirmamentV2SourceSpan?>? origin = null)
        : this(declaration, source, sourceFile, bind, origin, []) { }

    private FirmamentSyntaxScope(DeclarationSyntax declaration, string source, string sourceFile,
        Func<ExpressionSyntax, ExpressionSyntax>? bind, Func<FirmamentV2SourceSpan, FirmamentV2SourceSpan?>? origin,
        List<FirmamentSyntaxScope> scopes)
    {
        Declaration = declaration; this.source = source; this.sourceFile = sourceFile;
        this.bind = bind; this.origin = origin; this.scopes = scopes; scopes.Add(this);
        fields = new(declaration, errors, bind: bind);
    }
    public FirmamentSyntaxScope AllowModifier(string modifier) { modifiers.Add(modifier); return this; }
    public IReadOnlyList<FirmamentSyntaxScope> Children(string kind, bool legacyCase = false)
    {
        var result = new List<FirmamentSyntaxScope>();
        foreach (var child in Declaration.Body?.Statements.OfType<DeclarationSyntax>() ?? [])
        {
            if (!string.Equals(child.Type.Name.Text, kind, legacyCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) continue;
            consumed.Add(child);
            if (!children.TryGetValue(child, out var scope)) children[child] = scope = new(child, source, sourceFile, bind, origin, scopes);
            result.Add(scope);
        }
        return result;
    }
    public void Consume(StatementSyntax statement) => consumed.Add(statement);
    private static string Canonical(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    public ExpressionSyntax? Take(string name) => fields.Take(Canonical(name));
    public IReadOnlyList<FieldSyntax> TakeAll(string name) => fields.TakeAll(Canonical(name)).Select(f=>f with { Value=bind?.Invoke(f.Value)??f.Value }).ToArray();
    public string? Word(string name) => fields.Reference(Canonical(name));
    public string? Raw(string name)
    {
        var authored = Declaration.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, Canonical(name)));
        var expression = Take(name);
        if (expression is null) return null;
        var text = FirmamentSyntaxWriter.Expression(expression);
        if (authored is not null && text == FirmamentSyntaxWriter.Expression(authored.Value))
            return source.Substring(authored.Value.Span.Start, authored.Value.Span.Length);
        if (FirmamentNumericEvaluation.TryEvaluate(expression, out var number) && double.IsFinite(number.Number))
            return number.Number.ToString("R", CultureInfo.InvariantCulture) + number.Unit;
        return text;
    }
    public void Invalid(string name, string? message = null) => errors.Add($"firmament-v2-field-value-invalid:{Kind}:{name}" + (message is null ? "" : ": " + message));
    public IReadOnlyList<FirmamentV2ValidationDiagnostic> CompleteTree()
    {
        var diagnostics = new List<FirmamentV2ValidationDiagnostic>();
        foreach (var scope in scopes)
        {
            if (scope.Declaration.Type.Arguments.Count != 0 || scope.Declaration.HasParameterList
                || scope.Declaration.Modifiers.Any(m => !scope.modifiers.Contains(m.Name.Text)))
                scope.errors.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:{scope.Kind}:header");
            scope.fields.Complete(scope.consumed, scope.admitAnnotation, scope.admitInitializer, scope.admitPlacement);
            foreach (var code in scope.errors.Distinct(StringComparer.Ordinal))
            {
                var parts = code.Split(':');
                var label = parts.Length > 2 ? parts[2] : "";
                var field = scope.Declaration.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, label));
                var child = scope.Declaration.Body?.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text == label);
                var span = field?.Name.Span ?? child?.Span ?? scope.Span;
                var mapped = origin?.Invoke(span) ?? (origin is null ? span : null);
                var explanation=code.IndexOf(": ",StringComparison.Ordinal);
                var message=FirmamentV2DiagnosticAdapter.MessageFor(code)+(explanation<0?"":" "+code[(explanation+2)..]);
                diagnostics.Add(new(parts[0], "error", message, label, scope.Kind + " " + scope.Name)
                { SourceFile = sourceFile, SourceSpan = mapped, IsOriginalSource = mapped is not null, Stage = "Binding",
                    LocationNote = mapped is null ? "Original-source provenance is unavailable after expansion." : mapped != span ? "Location identifies the original enclosing construct after compile-time expansion." : null });
            }
        }
        return diagnostics;
    }
}
