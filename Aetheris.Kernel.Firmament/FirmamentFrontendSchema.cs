using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

/// <summary>Program-level selection of an existing Firmament frontend.  This is deliberately
/// not a second parser or semantic IR: it only makes the current parser boundary explicit.</summary>
public enum FirmamentFrontendSchema { Mechanical, WireForm, Sweep, SectionChain, Garment, Piping, Structural, Sculpting, Analysis, PlasticShell }

public sealed record FirmamentFrontendSelection(FirmamentFrontendSchema? Schema, string Source, IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<FirmamentV2ValidationDiagnostic> LocatedDiagnostics { get; init; } = [];
    public FirmamentV2SourceSpan? DirectiveSpan { get; init; }
    public bool IsExplicit => Schema is not null;
    public bool IsSuccess => Diagnostics.Count == 0;
    public FirmamentV2ParseResult Failure(string originalSource) => FirmamentV2ParseResult.Failure(Diagnostics) with
    {
        OriginalSource = originalSource, SyntaxDiagnostics = LocatedDiagnostics, ParserRoute = "UnifiedDirectives", OwnsUnifiedAdmission = true
    };
}

public static class FirmamentFrontendSchemas
{
    public static readonly IReadOnlyList<string> Names = ["Mechanical", "WireForm", "Sweep", "SectionChain", "Garment", "Piping", "Structural", "Sculpting", "Analysis", "PlasticShell"];

    public static FirmamentFrontendSelection Select(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Contains("schema", StringComparison.Ordinal) && !FirmamentV2Parser.IsV2AdmissionCandidate(source)) return new(null, source, []);
        var syntax = FirmamentSyntaxParser.Parse(source);
        if (syntax.Diagnostics.FirstOrDefault(d => d.Code == "firmament-schema-malformed") is { SourceSpan: { } malformedSpan } malformed)
            return Reject(malformed.Code, malformed.Message, malformedSpan);
        // All recognized V2 documents share structural admission before a domain
        // reader can discard unconsumed source. Serialized V1 keeps its explicit
        // boundary; structural parsing does not perform domain capability checks.
        if (!syntax.IsSuccess && FirmamentV2Parser.IsV2AdmissionCandidate(source))
            return new(null, source, syntax.Diagnostics.Select(d => d.Code).ToArray()) { LocatedDiagnostics = syntax.Diagnostics };
        var declarations = FirmamentSyntaxTraversal.Statements(syntax.Document).OfType<DirectiveSyntax>()
            .Where(d => d.Kind == DirectiveKind.Schema).ToArray();
        if (declarations.Length == 0) return new(null, source, []);
        if (declarations.Length != 1) return Reject("firmament-schema-duplicate", "Declare exactly one frontend schema.", declarations[1].Span);
        var declaration = declarations[0];
        var first = syntax.Document.Tokens.First(t => t.Kind is not (SyntaxTokenKind.Comment or SyntaxTokenKind.Whitespace));
        if (!ReferenceEquals(syntax.Document.Statements.FirstOrDefault(), declaration) || first.Span.Start != declaration.Span.Start)
            return Reject("firmament-schema-late", "Place the schema before all declarations and directives.", declaration.Span);
        if (declaration.Value is not ReferenceSyntax reference)
            return Reject("firmament-schema-malformed", "A schema name must be an unqualified frontend identifier.", declaration.Value.Span);
        var name = reference.Name.Text;
        if (!Enum.TryParse<FirmamentFrontendSchema>(name, false, out var schema))
            return Reject($"firmament-schema-unknown:{name}:available={string.Join(',', Names)}", $"Unknown schema '{name}'. Available schemas: {string.Join(", ", Names)}.", reference.Span);
        // Erase the selector without moving author-source diagnostic offsets.
        var span = declaration.Span;
        var erased = new string(source.AsSpan(span.Start, span.Length).ToArray().Select(character => character is '\r' or '\n' ? character : ' ').ToArray());
        var normalized = source.Remove(span.Start, span.Length).Insert(span.Start, erased);
        if (DomainMismatch(schema, normalized, syntax.Document) is { } mismatch)
            return Reject(mismatch.Code, mismatch.Message, declaration.Span, schema);
        return new(schema, normalized, []) { DirectiveSpan = declaration.Span };

        FirmamentFrontendSelection Reject(string code, string message, FirmamentV2SourceSpan location, FirmamentFrontendSchema? selectedSchema = null) =>
            new(selectedSchema, source, [code]) { LocatedDiagnostics = [Located(source, code, message, location)] };
    }

    // Specialization cannot introduce a material owner outside the selected frontend.
    internal static FirmamentV2ValidationDiagnostic? DomainMismatch(FirmamentFrontendSchema schema, string source,
        DocumentSyntax? syntax = null, FirmamentFrontendSchema? admittedSchema = null)
    {
        if (admittedSchema is { } bound)
            return bound != schema ? new($"firmament-schema-mismatch:declared={schema}:source={bound}", "fatal", $"Schema {schema} cannot compile a {bound} document.") : null;
        syntax ??= FirmamentSyntaxParser.Parse(source).Document;
        if (schema != FirmamentFrontendSchema.Mechanical && schema != FirmamentFrontendSchema.Garment
            && FirmamentSyntaxTraversal.Declarations(syntax).Any(d => d.Type.Name.Text == "Feature"))
            return new($"firmament-feature-schema-unsupported:{schema}", "fatal", $"Feature recipes are not supported by schema {schema}.");
        var detected = DetectDistinctFrontend(source);
        if (detected is null && FirmamentV2Parser.MechanicalDeclarations(syntax.Statements)
            .Any(d => FirmamentV2Parser.IsPrimitiveDeclaration(d) || d.Type.Name.Text is "Compose" or "Extrude" or "Revolve" or "BodyState" or "SculptState" || Enum.TryParse<GearFamily>(d.Type.Name.Text, out _)))
            detected = FirmamentFrontendSchema.Mechanical;
        return detected is not null && detected != schema
            ? new($"firmament-schema-mismatch:declared={schema}:source={detected}", "fatal", $"Schema {schema} cannot compile a {detected} document.") : null;
    }

    internal static FirmamentV2ValidationDiagnostic Located(string source, string code, string message, FirmamentV2SourceSpan span)
    {
        var prefix = source.AsSpan(0, span.Start);
        var line = 1; var column = 1;
        foreach (var character in prefix) { if (character == '\n') { line++; column = 1; } else column++; }
        return new(code, "fatal", message, Target: "document directive") { SourceSpan = span, Line = line, Column = column, Stage = "frontend admission" };
    }

    private static FirmamentFrontendSchema? DetectDistinctFrontend(string source) =>
        FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d=>d.Type.Name.Text=="LinearElastic" && d.Modifiers.Any(m=>m.Name.Text is "Analysis" or "analysis")) ? FirmamentFrontendSchema.Analysis :
        FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d=>d.Type.Name.Text=="PlasticShell") ? FirmamentFrontendSchema.PlasticShell :
        Structural.StructuralAuthoring.IsStructuralSource(source) ? FirmamentFrontendSchema.Structural :
        Aetheris.Surfacing.SculptingAuthoring.IsSculptingSource(source) ? FirmamentFrontendSchema.Sculpting :
        Piping.PipingAuthoring.IsPipingSource(source) ? FirmamentFrontendSchema.Piping :
        Garment.GarmentAuthoring.HasRoot(source) ? FirmamentFrontendSchema.Garment :
        WireFormAuthoring.IsWireFormSource(source) ? FirmamentFrontendSchema.WireForm :
        CircularSweepAuthoring.IsSweepSource(source) ? FirmamentFrontendSchema.Sweep :
        SectionChainAuthoringParser.IsSectionChainSource(source) ? FirmamentFrontendSchema.SectionChain : null;
}
