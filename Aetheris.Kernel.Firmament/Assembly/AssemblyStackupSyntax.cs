using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

/// <summary>Authoring binding only; dimensional paths and worst-case arithmetic
/// remain owned by AssemblyM0Compiler.</summary>
internal static class AssemblyStackupSyntax
{
    internal static IReadOnlyList<ToleranceStackupAssertSource> Bind(DeclarationSyntax owner, string file,
        List<AssemblyDiagnostic> diagnostics, Func<FirmamentV2SourceSpan, FirmamentV2SourceSpan?> origin)
    {
        var result = new List<ToleranceStackupAssertSource>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in owner.Body?.Statements.OfType<DeclarationSyntax>()
            .Where(d => d.Modifiers.Any(m => m.Name.Text == "Assert")) ?? [])
        {
            var name = declaration.Name?.Text ?? "";
            if (declaration.Type.Name.Text != "ToleranceStackup")
            { Error("assembly-assert-kind-unsupported", "Assembly assertions require ToleranceStackup.", declaration.Span); continue; }
            if (name.Length == 0 || declaration.Body is null || !names.Add(name))
            { Error("assembly-tolerance-assert-invalid", "ToleranceStackup requires a unique name and a field body.", declaration.Span); continue; }
            var fieldErrors = new List<string>();
            var fields = new FirmamentSyntaxFields(declaration, fieldErrors);
            var between = fields.Take("Between");
            var require = fields.Take("Require");
            fields.Complete();
            foreach (var error in fieldErrors)
            {
                var pieces = error.Split(':');
                var field = declaration.Body.Statements.OfType<FieldSyntax>()
                    .FirstOrDefault(f => pieces.Length > 2 && FirmamentSyntaxFields.SameField(f.Name.Text, pieces[2]));
                Error(pieces[0], $"Invalid ToleranceStackup '{name}': {string.Join(": ", pieces.Skip(1))}. Admitted fields are between and require.", field?.Span ?? declaration.Span);
            }
            var paths = between is SequenceSyntax { Kind: SequenceKind.List, Items.Count: 2 } sequence
                && sequence.Items.All(i => i.Name is null && i.Default is null)
                ? sequence.Items.Select(i => Path(i.Value)).ToArray() : [];
            var validPaths = paths.Length == 2 && paths.All(p => p is not null);
            var minimum = double.NaN;
            var validRequirement = require is BinarySyntax { Operator: BinaryOperator.GreaterEqual,
                Left: ReferenceSyntax { Name.Text: "clearance" or "Clearance" } } comparison
                && FirmamentSyntaxValues.TryMeasure(comparison.Right, "mm", out minimum) && double.IsFinite(minimum);
            if (!validPaths || !validRequirement)
            { Error("assembly-tolerance-assert-invalid", $"Assert ToleranceStackup '{name}' requires between: [From.Semantic, To.Semantic] and require: clearance >= length.", declaration.Span); continue; }
            if (fieldErrors.Count > 0) continue;
            var original = origin(declaration.Span);
            var span = original ?? origin(owner.Span);
            result.Add(new(name, AssemblyPath.Parse(paths[0]!), AssemblyPath.Parse(paths[1]!), minimum, "mm",
                span is null ? null : new SemanticSourceSpan(file, span.Start, span.Length))
                { LocationNote = original is null ? "Enclosing Assembly location; expansion retained no exact authored assertion location." : null });
        }
        return result;

        void Error(string code, string message, FirmamentV2SourceSpan location)
        {
            var original = origin(location); var span = original ?? origin(owner.Span);
            diagnostics.Add(new(code, message) { SourceSpan = span is null ? null : new(file, span.Start, span.Length),
                LocationNote = original is null ? "Enclosing Assembly location; expansion retained no exact authored assertion location." : null });
        }
    }

    private static string? Path(ExpressionSyntax expression) => expression switch
    {
        ReferenceSyntax r => r.Name.Text,
        MemberSyntax m when Path(m.Target) is { } path => path + "." + m.Member.Text,
        _ => null
    };
}
