using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.SheetMetal;

internal static class SheetMetalSyntaxDiagnostics
{
    internal static string? Path(ExpressionSyntax? value) => value switch
    {
        IndexSyntax index when Path(index.Target) is { } target && FirmamentSyntaxValues.TryMeasure(index.Index,"",out var number)
            && number>=0 && number==Math.Truncate(number) => target+"["+number.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+"]",
        _ => FirmamentSyntaxValues.Reference(value)
    };
    internal static SheetMetalDiagnostic At(FirmamentDomainBinding binding,string code,string message,FirmamentV2SourceSpan? span,string? context=null)
    {
        var original=span is {} location?binding.OriginalSpan(location):null;
        return new(code,SheetMetalDiagnosticSeverity.Error,message)
        {
            SourceFile=binding.Document.SourceFile,SourceSpan=original,Context=context,
            LocationNote=binding.IsExpanded ? original is null ? "Expanded source; original-source provenance is unavailable."
                : "Location identifies the original enclosing declaration after expansion." : null
        };
    }
    internal static IReadOnlyList<SheetMetalDiagnostic> Binding(FirmamentDomainBinding binding) => binding.Diagnostics.Select(d=>
        new SheetMetalDiagnostic(d.Code,d.Severity is "warning"?SheetMetalDiagnosticSeverity.Warning:d.Severity is "info"?SheetMetalDiagnosticSeverity.Information:SheetMetalDiagnosticSeverity.Error,d.Message)
        { SourceFile=binding.Document.SourceFile,SourceSpan=d.SourceSpan,Context=d.Target,LocationNote=d.LocationNote }).ToArray();
    internal static IReadOnlyList<SheetMetalDiagnostic> Codes(IEnumerable<string> codes,FirmamentDomainBinding binding) => codes.Distinct(StringComparer.Ordinal).Select(text=>
    {
        var parts=text.Split(':');var code=parts[0];var type=parts.ElementAtOrDefault(1);var field=parts.ElementAtOrDefault(2);
        var owners=FirmamentSyntaxTraversal.Declarations(binding.Document).Where(d=>d.Type.Name.Text==type).ToArray();
        var objects=FirmamentSyntaxTraversal.Expressions(binding.Document).OfType<ObjectSyntax>().Where(o=>FirmamentSyntaxValues.Reference(o.Type)==type).ToArray();
        var fields=owners.SelectMany(d=>d.Body?.Statements.OfType<FieldSyntax>()??[])
            .Concat(objects.SelectMany(o=>o.Body.Statements.OfType<FieldSyntax>()))
            .Where(f=>field is not null && FirmamentSyntaxFields.SameField(f.Name.Text,field)).ToArray();
        var span=fields.Length==1?fields[0].Span:owners.Length==1?owners[0].Span:(FirmamentV2SourceSpan?)null;
        var message=code switch
        {
            FirmamentV2Parser.CanonicalFieldUnknown=>$"Unknown field '{field}' in {type}.",
            "firmament-v2-field-duplicate"=>$"Duplicate field '{field}' in {type}; canonical and compatibility aliases cannot both be supplied.",
            "firmament-v2-field-value-invalid"=>$"Invalid value for field '{field}' in {type}; check its required type and units.",
            FirmamentV2Parser.CanonicalDeclarationUnknown=>$"Unsupported statement or header '{field}' in {type}.",
            _=>text
        };
        return At(binding,code,message,span,type);
    }).ToArray();
}
