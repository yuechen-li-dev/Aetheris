using System.Globalization;
using System.Text;

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
/// Small module-owned reusable template surface. Expansion emits ordinary SheetMetal
/// declarations so lowering, DFM, correspondence, and Concept Paths are not bypassed.
/// </summary>
public static class SheetMetalTemplates
{
    public static string LBracket(string name,LBracketSpec spec)=>Emit(name,spec.Width,spec.Depth,spec.Policy,[new("Wall","Front",spec.FlangeHeight)]);
    public static string UChannel(string name,UChannelSpec spec)=>Emit(name,spec.Width,spec.Depth,spec.Policy,[new("LeftWall","Left",spec.WallHeight),new("RightWall","Right",spec.WallHeight)]);
    public static string FourWallTray(string name,FourWallTraySpec spec)=>Emit(name,spec.Width,spec.Depth,spec.Policy,
        [new("Front","Front",spec.WallHeight),new("Right","Right",spec.WallHeight),new("Rear","Rear",spec.WallHeight),new("Left","Left",spec.WallHeight)]);

    private static string Emit(string name,double width,double depth,SheetMetalTemplatePolicy policy,IReadOnlyList<(string Name,string Edge,double Height)> flanges)
    {
        Positive(width,nameof(width));Positive(depth,nameof(depth));Positive(policy.Thickness,nameof(policy.Thickness));Positive(policy.InsideRadius,nameof(policy.InsideRadius),allowZero:true);
        if(policy.KFactor is <0 or >1)throw new ArgumentOutOfRangeException(nameof(policy.KFactor));
        var b=new StringBuilder();b.Append("SheetMetal ").Append(Safe(name)).AppendLine(" {");
        b.Append("  Thickness: ").Append(N(policy.Thickness)).AppendLine("mm;");b.Append("  KFactor: ").Append(N(policy.KFactor)).AppendLine(";");
        if(!string.IsNullOrWhiteSpace(policy.Material))b.Append("  Material: \"").Append(policy.Material!.Replace("\"","\\\"",StringComparison.Ordinal)).AppendLine("\";");
        b.Append("  Base Base { Profile: Rectangle { Width: ").Append(N(width)).Append("mm; Height: ").Append(N(depth)).AppendLine("mm; }; }");
        foreach(var flange in flanges)
        {
            Positive(flange.Height,nameof(flange.Height));b.Append("  Flange ").Append(flange.Name).Append(" { From: Base.").Append(flange.Edge).Append("; Height: ").Append(N(flange.Height)).Append("mm; Angle: 90deg; Radius: ").Append(N(policy.InsideRadius)).Append("mm;");
            if(policy.CornerPolicy==SheetCornerPolicy.Mitered)b.Append(" Corner: Miter;");
            else if(policy.ReliefPolicy!=SheetReliefPolicy.None)b.Append(" Relief: ").Append(policy.ReliefPolicy switch{SheetReliefPolicy.Round=>"Round",SheetReliefPolicy.Rectangular=>"Rectangular",_=>"Auto"}).Append(';');
            b.AppendLine(" }");
        }
        b.AppendLine("}");return b.ToString();
    }
    private static string N(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
    private static void Positive(double value,string name,bool allowZero=false){if(!double.IsFinite(value)||(allowZero?value<0:value<=0))throw new ArgumentOutOfRangeException(name);}
    private static string Safe(string value){if(string.IsNullOrWhiteSpace(value)||!char.IsLetter(value[0])&&value[0]!='_')throw new ArgumentException("Template instance name must be a Firmament identifier.",nameof(value));if(value.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'))throw new ArgumentException("Template instance name must be a Firmament identifier.",nameof(value));return value;}
}
