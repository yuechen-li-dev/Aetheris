using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Consumption at the wire operation owner; field vocabulary comes from
/// the reads performed by that owner, not a separate validation schema.</summary>
internal sealed class WireFormFields
{
    private readonly string source;
    private readonly FirmamentSyntaxFields fields;
    private readonly List<string> diagnostics = [];
    public DeclarationSyntax Declaration { get; }

    public WireFormFields(string kind, string body)
    {
        source = kind + " {\n" + body + "\n}";
        var syntax = FirmamentSyntaxParser.Parse(source);
        diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
        // A malformed fragment must remain a diagnostic, including callers of
        // the direct API that have not passed document admission first.
        Declaration = syntax.Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault()
            ?? FirmamentSyntaxParser.Parse(kind + " {}").Document.Statements.OfType<DeclarationSyntax>().Single();
        fields = new(Declaration, diagnostics);
    }

    public string? Read(string name)
    {
        var value = fields.Take(name);
        return value is null ? null : source.Substring(value.Span.Start, value.Span.Length);
    }

    public string? Complete(IReadOnlySet<StatementSyntax>? children = null)
    {
        fields.Complete(consumedStatements: children);
        return diagnostics.Count == 0 ? null : string.Join("; ", diagnostics.Select(d =>
            FirmamentV2.FirmamentInterop.FirmamentV2DiagnosticAdapter.FromParserDiagnosticCode(d).Message));
    }
}
