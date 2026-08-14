using System.Text.RegularExpressions;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalAuthoringTimings(double ParseMilliseconds,double FormedLowerMilliseconds,double FlatLowerMilliseconds);
public sealed record SheetMetalAuthoringResult(
    bool IsSuccess,
    SheetMetalConstructionSpec? Spec,
    SheetMetalPartIr? Part,
    SheetMetalFlatPatternIr? FlatPattern,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics,
    SheetMetalAuthoringTimings? Timings=null);

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
        var clean=Regex.Replace(source,@"//.*?$|#.*?$",string.Empty,RegexOptions.Multiline);
        return Regex.IsMatch(clean,@"^\s*SheetMetal\s+",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    }

    public static SheetMetalAuthoringResult CompileFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);var full=Path.GetFullPath(path);
        if(!File.Exists(full))return Failure($"Sheet Metal Firmament source was not found: {full}");
        return Compile(File.ReadAllText(full),full);
    }

    public static SheetMetalAuthoringResult Compile(string source,string sourcePath="authored.firmament")
    {
        ArgumentNullException.ThrowIfNull(source);var clean=Regex.Replace(source,@"//.*?$|#.*?$",string.Empty,RegexOptions.Multiline);
        if(ReconstructedSheetMetalFirmament.IsReconstructed(clean)&&Regex.IsMatch(clean,@"\bEvidenceSource\s*:",Rx))return ReconstructedSheetMetalFirmament.Compile(clean,sourcePath);
        if(RecoveredSheetMetalFirmament.IsRecovered(clean))return RecoveredSheetMetalFirmament.Compile(clean,sourcePath);
        return AuthoredSheetMetalCompiler.Compile(clean,sourcePath);
    }

    private static SheetMetalAuthoringResult Failure(string message)=>new(false,null,null,null,[new("sheetmetal-firmament-invalid",SheetMetalDiagnosticSeverity.Error,message)]);
}
