using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.SheetMetal;

public sealed record SheetMetalTemplatePolicy(
    double Thickness,
    double InsideRadius,
    double KFactor = .42d,
    string? Material = null,
    SheetCornerPolicy CornerPolicy = SheetCornerPolicy.Open,
    SheetReliefPolicy ReliefPolicy = SheetReliefPolicy.Auto);
public sealed record LBracketSpec(double Width,double Depth,double FlangeHeight,SheetMetalTemplatePolicy Policy);
public sealed record UChannelSpec(double Width,double Depth,double WallHeight,SheetMetalTemplatePolicy Policy);
public sealed record FourWallTraySpec(double Width,double Depth,double WallHeight,SheetMetalTemplatePolicy Policy);

/// <summary>
/// Compatibility C# entry points over the module's ordinary user-defined Firmament
/// Templates. No Sheet Metal construction is implemented in this host wrapper.
/// </summary>
public static class SheetMetalTemplates
{
    public static string LBracket(string name,LBracketSpec spec)=>Expand("LBracket",name,"LBracketSpec",Fields(spec.Policy,("Width",Mm(spec.Width)),("Depth",Mm(spec.Depth)),("FlangeHeight",Mm(spec.FlangeHeight))));
    public static string UChannel(string name,UChannelSpec spec)=>Expand("UChannel",name,"UChannelSpec",Fields(spec.Policy,("Width",Mm(spec.Width)),("Depth",Mm(spec.Depth)),("WallHeight",Mm(spec.WallHeight))));
    public static string FourWallTray(string name,FourWallTraySpec spec)=>Expand("FourWallTray",name,"TraySpec",Fields(spec.Policy,("Width",Mm(spec.Width)),("Depth",Mm(spec.Depth)),("WallHeight",Mm(spec.WallHeight))));

    private static string Expand(string template,string name,string recordType,Dictionary<string,string> fields)
    {
        var expansion=FirmamentTemplateHostBridge.Expand(SheetMetalTemplateLibrary.Source,template,Safe(name),
            new Dictionary<string,FirmamentHostArgument>(StringComparer.Ordinal){["Spec"]=new("",recordType,fields)},out var diagnostics);
        return expansion?.ExpandedSource??throw new InvalidOperationException($"Firmament Template '{template}' specialization failed: {string.Join("; ",diagnostics)}");
    }
    private static Dictionary<string,string> Policy(SheetMetalTemplatePolicy policy)
    {
        Positive(policy.Thickness,nameof(policy.Thickness));Positive(policy.InsideRadius,nameof(policy.InsideRadius),true);
        if(policy.KFactor is <0 or >1)throw new ArgumentOutOfRangeException(nameof(policy.KFactor));
        return new(StringComparer.Ordinal){["Thickness"]=Mm(policy.Thickness),["InsideRadius"]=N(policy.InsideRadius)+"mm",["KFactor"]=N(policy.KFactor),["ReliefPolicy"]=policy.ReliefPolicy switch{SheetReliefPolicy.Round=>"Round",SheetReliefPolicy.Rectangular=>"Rectangular",_=>"Auto"}};
    }
    private static Dictionary<string,string> Fields(SheetMetalTemplatePolicy policy,params (string Name,string Value)[] values){var fields=Policy(policy);foreach(var value in values)fields[value.Name]=value.Value;return fields;}
    private static string Mm(double value){Positive(value,nameof(value));return N(value)+"mm";}
    private static string N(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
    private static void Positive(double value,string name,bool allowZero=false){if(!double.IsFinite(value)||(allowZero?value<0:value<=0))throw new ArgumentOutOfRangeException(name);}
    private static string Safe(string value){if(string.IsNullOrWhiteSpace(value)||!char.IsLetter(value[0])&&value[0]!='_')throw new ArgumentException("Template instance name must be a Firmament identifier.",nameof(value));if(value.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'))throw new ArgumentException("Template instance name must be a Firmament identifier.",nameof(value));return value;}
}
