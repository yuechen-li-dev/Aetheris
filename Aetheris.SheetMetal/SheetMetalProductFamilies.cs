using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.SheetMetal;

public sealed record ElectronicsEnclosureSpec(
    double Width,
    double Depth,
    double Height,
    double LidLipHeight,
    double Thickness,
    double InsideRadius,
    double KFactor=.42d,
    SheetReliefPolicy ReliefPolicy=SheetReliefPolicy.Rectangular);

public sealed record ManufacturedSheetMetalResult(
    string TemplateName,
    string SpecializationIdentity,
    SheetMetalAuthoringResult Compilation,
    SheetMetalDfmReport Dfm,
    IReadOnlyList<SheetMetalConceptPath> SemanticPaths,
    SheetMetalFabricationIr Fabrication,
    string FlatSvg)
{
    public SheetMetalPartIr Part=>Compilation.Part!;
    public SheetMetalFlatPatternIr FlatPattern=>Compilation.FlatPattern!;
}

/// <summary>
/// Typed Forge-facing convenience over the ordinary Firmament Template host bridge.
/// Geometry remains authored by the embedded user-readable Firmament module.
/// </summary>
public static class SheetMetalProductFamilies
{
    public static ManufacturedSheetMetalResult MakeEnclosure(ElectronicsEnclosureSpec spec,string instanceName="Enclosure")
    {
        Positive(spec.Width,nameof(spec.Width));Positive(spec.Depth,nameof(spec.Depth));Positive(spec.Height,nameof(spec.Height));
        Positive(spec.LidLipHeight,nameof(spec.LidLipHeight));Positive(spec.Thickness,nameof(spec.Thickness));
        if(spec.InsideRadius<0||!double.IsFinite(spec.InsideRadius))throw new ArgumentOutOfRangeException(nameof(spec.InsideRadius));
        if(spec.KFactor is <0 or >1||!double.IsFinite(spec.KFactor))throw new ArgumentOutOfRangeException(nameof(spec.KFactor));
        var fields=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["Width"]=Mm(spec.Width),["Depth"]=Mm(spec.Depth),["Height"]=Mm(spec.Height),
            ["LidLipHeight"]=Mm(spec.LidLipHeight),["Thickness"]=Mm(spec.Thickness),
            ["InsideRadius"]=Mm(spec.InsideRadius),["KFactor"]=N(spec.KFactor),
            ["ReliefPolicy"]=spec.ReliefPolicy switch { SheetReliefPolicy.Round=>"Round",SheetReliefPolicy.Rectangular=>"Rectangular",_=>"Auto" }
        };
        var arguments=new Dictionary<string,FirmamentHostArgument>(StringComparer.Ordinal)
        {
            ["Spec"]=new("", "EnclosureSpec",fields)
        };
        var expansion=FirmamentTemplateHostBridge.Expand(SheetMetalTemplateLibrary.Source,"ElectronicsEnclosure",instanceName,arguments,out var diagnostics)
            ?? throw new InvalidOperationException("Firmament enclosure Template specialization failed: "+string.Join("; ",diagnostics));
        if(diagnostics.Count>0)throw new InvalidOperationException("Firmament enclosure Template specialization failed: "+string.Join("; ",diagnostics));
        var compilation=SheetMetalFirmament.Compile(expansion.ExpandedSource,$"template:{expansion.SpecializationIdentity}");
        if(!compilation.IsSuccess||compilation.Part is null||compilation.FlatPattern is null)
            throw new InvalidOperationException("Specialized enclosure failed Sheet Metal lowering: "+string.Join("; ",compilation.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        var paths=SheetMetalConceptPaths.Inspect(compilation.Spec!,compilation.Part,compilation.FlatPattern);
        return new("ElectronicsEnclosure",expansion.SpecializationIdentity,compilation,
            SheetMetalDfm.Evaluate(compilation.Part,compilation.FlatPattern),paths,
            SheetMetalFabricationArtifacts.Create(compilation.Part,compilation.FlatPattern),SheetMetalSvgRenderer.Render(compilation.FlatPattern));
    }

    private static string Mm(double value)=>N(value)+"mm";
    private static string N(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
    private static void Positive(double value,string name){if(!double.IsFinite(value)||value<=0)throw new ArgumentOutOfRangeException(name);}
}
