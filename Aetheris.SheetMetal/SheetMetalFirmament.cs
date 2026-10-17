using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalAuthoringTimings(
    double ParseMilliseconds,
    double FormedLowerMilliseconds,
    double FlatLowerMilliseconds,
    double SemanticResolveMilliseconds = 0d);
public sealed record SheetMetalAuthoringResult(
    bool IsSuccess,
    SheetMetalConstructionSpec? Spec,
    SheetMetalPartIr? Part,
    SheetMetalFlatPatternIr? FlatPattern,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics,
    SheetMetalAuthoringTimings? Timings=null,
    IReadOnlyList<ConceptIrTemplateInstantiation>? TemplateInstantiations=null);

/// <summary>
/// Entry point for the module-owned Sheet Metal dialects. Recovered and historical
/// evidence-linked M2 sources remain readable; normal authored/reconstructed source
/// is lowered by the source-independent M3 compiler.
/// </summary>
public static class SheetMetalFirmament
{
    private const RegexOptions Rx=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Singleline;

    public static bool LooksLikeSheetMetal(string source)
    {
        if(source is null)return false;
        var document=FirmamentSyntaxParser.Parse(source).Document;
        return document.Statements.OfType<DeclarationSyntax>().Any(d=>d.Type.Name.Text=="SheetMetal")
            || document.Statements.OfType<DirectiveSyntax>().Any(d=>d.Kind==DirectiveKind.Use && FirmamentSyntaxValues.Reference(d.Value)=="SheetMetal.ProductFamilies");
    }

    public static SheetMetalAuthoringResult CompileFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);var full=Path.GetFullPath(path);
        if(!File.Exists(full))return Failure($"Sheet Metal Firmament source was not found: {full}");
        return Compile(File.ReadAllText(full),full);
    }

    // Resolve the domain's schemas before any shared Record/Template admission,
    // including assemblies that defer SheetMetal body materialization.
    public static string ResolveImports(string source)
    {
        var directives=FirmamentSyntaxParser.Parse(source).Document.Statements.OfType<DirectiveSyntax>()
            .Where(d=>d.Kind==DirectiveKind.Use).ToArray();
        var libraries=new List<string>();
        foreach(var directive in directives.OrderByDescending(d=>d.Span.Start))
        {
            var library=FirmamentSyntaxValues.Reference(directive.Value) switch
            {
                "Profile.Modifications"=>ProfileModificationTemplateLibrary.Source,
                "SheetMetal.ProductFamilies"=>SheetMetalTemplateLibrary.Source,
                _=>null
            };
            if(library is null)continue;
            if(!libraries.Contains(library,StringComparer.Ordinal))libraries.Add(library);
            source=source.Remove(directive.Span.Start,directive.Span.Length);
        }
        if(libraries.Count>0)source=string.Join(Environment.NewLine,libraries)+Environment.NewLine+source;
        return source;
    }

    public static SheetMetalAuthoringResult Compile(string source,string sourcePath="authored.firmament")
    {
        ArgumentNullException.ThrowIfNull(source);
        var originalSource=source;
        source = ResolveImports(source);
        // Preserve strings and offsets. Recovery retains its historical domain owner;
        // authored source is expanded and bound once by the shared frontend below.
        var cleanChars=source.ToCharArray();
        foreach(var token in FirmamentSyntaxParser.Parse(source).Document.Tokens.Where(t=>t.Kind==SyntaxTokenKind.Comment))
            for(var i=token.Span.Start;i<token.Span.Start+token.Span.Length;i++)if(cleanChars[i] is not ('\r' or '\n'))cleanChars[i]=' ';
        var clean=new string(cleanChars);
        if(ReconstructedSheetMetalFirmament.IsReconstructed(clean)&&Regex.IsMatch(clean,@"\bEvidenceSource\s*:",Rx))return ReconstructedSheetMetalFirmament.Compile(clean,sourcePath);
        if(RecoveredSheetMetalFirmament.IsRecovered(clean))return RecoveredSheetMetalFirmament.Compile(clean,sourcePath);
        var result=AuthoredSheetMetalCompiler.Compile(clean,sourcePath,originalSource);
        return result;
    }

    private static SheetMetalAuthoringResult Failure(string message)=>new(false,null,null,null,[new("sheetmetal-firmament-invalid",SheetMetalDiagnosticSeverity.Error,message)]);
}
