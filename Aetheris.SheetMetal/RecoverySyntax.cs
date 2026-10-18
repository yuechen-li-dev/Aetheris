using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Scope=Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxScope;
namespace Aetheris.SheetMetal;
internal static class RecoverySyntax
{
    internal static Scope? Root(string source,string path,out IReadOnlyList<SheetMetalDiagnostic> diagnostics)
    {
        var syntax=FirmamentSyntaxParser.Parse(source,path);
        var errors=syntax.Diagnostics.Select(d=>new SheetMetalDiagnostic(d.Code,SheetMetalDiagnosticSeverity.Error,d.Message){SourceFile=d.SourceFile,SourceSpan=d.SourceSpan,Context=d.Target,LocationNote=d.LocationNote}).ToList();
        var roots=syntax.Document.Statements.OfType<DeclarationSyntax>().Where(d=>d.Type.Name.Text=="SheetMetal").ToArray();
        if(roots.Length!=1 || syntax.Document.Statements.Count!=1)errors.Add(new("sheetmetal-recovery-document-invalid",SheetMetalDiagnosticSeverity.Error,"Recovered/reconstructed source requires exactly one SheetMetal declaration."));
        diagnostics=errors;return errors.Count==0?new(roots[0],source,path):null;
    }
    internal static string? Token(Scope scope,string field)=>scope.Word(field);
    internal static string? Quoted(Scope scope,string field)
    {
        var value=scope.Take(field);if(value is LiteralSyntax {Kind:LiteralKind.String})return FirmamentSyntaxValues.Reference(value);
        if(value is not null)scope.Invalid(field,"Expected a quoted identity.");return null;
    }
    internal static bool Scalar(Scope scope,string field,string? unit,out double value)
    {
        var expression=scope.Take(field);value=0;if(expression is null)return false;
        // Area is serialized recovery evidence, not a new general dimensional algebra.
        if(unit=="mm2" && expression is QuantitySyntax {Unit.Text:"mm2"} area)expression=area.Value;
        if(FirmamentSyntaxValues.TryMeasure(expression,unit=="mm2"?"":unit??"",out value) && double.IsFinite(value))return true;
        scope.Invalid(field);return false;
    }
    private static ExpressionSyntax? VectorExpression(ExpressionSyntax? expression)=>expression is SequenceSyntax {Kind:SequenceKind.Tuple} tuple?tuple with {Kind=SequenceKind.List}:expression;
    private static bool Components(ExpressionSyntax? expression,string unit,out double[] values)=>FirmamentSyntaxValues.Vector(VectorExpression(expression),unit,out values) && values.Length==3 && values.All(double.IsFinite);
    internal static bool Point(Scope scope,string field,bool units,out Point3D value)
    {
        var expression=scope.Take(field);value=default;if(Components(expression,units?"mm":"",out var v)){value=new(v[0],v[1],v[2]);return true;}
        if(expression is not null)scope.Invalid(field);return false;
    }
    internal static bool Vector(Scope scope,string field,out Vector3D value){var valid=Point(scope,field,false,out var p);value=new(p.X,p.Y,p.Z);return valid;}
    internal static IReadOnlyList<Point3D> Points(Scope scope,string field)
    {
        var expression=scope.Take(field);if(expression is null)return [];
        if(expression is not SequenceSyntax {Kind:SequenceKind.List} list){scope.Invalid(field);return [];}
        var result=new List<Point3D>();
        foreach(var item in list.Items)if(item.Name is null && item.Default is null && Components(item.Value,"mm",out var v))result.Add(new(v[0],v[1],v[2]));else scope.Invalid(field);
        return result;
    }
    internal static IReadOnlyList<int> IntList(Scope scope,string field)
    {
        var expression=scope.Take(field);if(expression is null)return [];
        if(FirmamentSyntaxValues.Vector(expression,"",out var values) && values.All(v=>double.IsFinite(v)&&v>=0&&v<=int.MaxValue&&v==Math.Truncate(v)))return values.Select(v=>(int)v).ToArray();
        scope.Invalid(field);return [];
    }
    internal static IReadOnlyList<string> StringList(Scope scope,string field)
    {
        var expression=scope.Take(field);if(expression is null)return [];
        if(expression is SequenceSyntax {Kind:SequenceKind.List} list && list.Items.All(i=>i.Name is null && i.Default is null && i.Value is LiteralSyntax {Kind:LiteralKind.String}))return list.Items.Select(i=>FirmamentSyntaxValues.Reference(i.Value)!).ToArray();
        scope.Invalid(field);return [];
    }
    internal static bool Bool(Scope scope,string field,bool fallback)
    {
        var expression=scope.Take(field);if(expression is null)return fallback;
        if(expression is LiteralSyntax {Kind:LiteralKind.Boolean} boolean && bool.TryParse(boolean.Raw,out var value))return value;
        scope.Invalid(field);return fallback;
    }
    internal static IReadOnlyList<SheetMetalDiagnostic> Close(Scope root)=>root.CompleteTree().Select(d=>new SheetMetalDiagnostic(d.Code,SheetMetalDiagnosticSeverity.Error,d.Message){SourceFile=d.SourceFile,SourceSpan=d.SourceSpan,Context=d.Target,LocationNote=d.LocationNote}).ToArray();
}
