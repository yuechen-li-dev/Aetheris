using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentPanelPerformance(double ParseBindMilliseconds, double PanelConstructionMilliseconds);
public sealed record FirmamentPanelCompilation(string ModelName, IReadOnlyList<PanelIr> Panels,
    IReadOnlyList<string> Diagnostics, FirmamentPanelPerformance Performance,
    IReadOnlyList<ConceptIrTemplateInstantiation>? TemplateInstantiations = null)
{
    public bool IsSuccess => Panels.Count > 0 && Diagnostics.Count == 0;
}

/// <summary>Bounded Surfacing M1 bridge for ordinary canonical Firmament Panel declarations.</summary>
public static class FirmamentPanelCompiler
{
    public const string InvalidPanel = "firmament-panel-invalid";
    public const string UnsupportedSurface = "firmament-panel-surface-unsupported";
    public const string MissingSurface = "firmament-panel-surface-missing";

    public static FirmamentPanelCompilation Compile(string source,
        IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var parseWatch = Stopwatch.StartNew(); var diagnostics = new List<string>();
        var model = Regex.Match(source, @"\bModel\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
        if (!model.Success) diagnostics.Add(FirmamentV2Parser.MissingModel);
        var units = Regex.Match(source, @"\bUnits\s*:\s*(?<unit>[A-Za-z]+)", RegexOptions.CultureInvariant);
        if (!units.Success || units.Groups["unit"].Value != "mm") diagnostics.Add(FirmamentV2Parser.MissingUnits);
        var declarations = Blocks(source, @"\bPanel\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{").ToArray();
        if (declarations.Length == 0) diagnostics.Add("firmament-panel-missing");
        parseWatch.Stop();

        var constructionWatch = Stopwatch.StartNew(); var panels = new List<PanelIr>();
        foreach (var declaration in declarations)
        {
            try
            {
                var panel = CompilePanel(declaration.Name, declaration.Body, diagnostics);
                if (panel is not null) panels.Add(panel);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                diagnostics.Add($"{InvalidPanel}:{declaration.Name}:{exception.Message}");
            }
        }
        constructionWatch.Stop();
        return new(model.Success ? model.Groups["name"].Value : "InvalidPanelModel", panels,
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            new(parseWatch.Elapsed.TotalMilliseconds, constructionWatch.Elapsed.TotalMilliseconds), templateInstantiations);
    }

    private static PanelIr? CompilePanel(string name, string body, ICollection<string> diagnostics)
    {
        var surface = Regex.Match(body, @"\bSurface\s*:\s*(?<kind>ParametricSurface|HyperbolicParaboloid|ParabolicCylinder|EllipticParaboloid|Helicoid|RuledSurface|RuledTransition|BoundaryPatch|SectionSurface)\s*\{", RegexOptions.CultureInvariant);
        if (!surface.Success) { diagnostics.Add($"{MissingSurface}:{name}"); return null; }
        var open = body.IndexOf('{', surface.Index); var close = Matching(body, open, '{', '}');
        if (close < 0) { diagnostics.Add($"{InvalidPanel}:{name}:surface-block"); return null; }
        var surfaceBody = body[(open + 1)..close];
        var orientation = string.Equals(Field(body, "Orientation"), "Back", StringComparison.Ordinal)
            ? new PanelOrientation(PanelNormalOrientation.ReversedSupportNormal, PanelMaterialSide.Back) : PanelOrientation.Front;
        var thicknessText = Field(body, "Thickness");
        double? thickness = thicknessText is null ? null : Length(thicknessText);
        var material = Field(body, "Material")?.Trim('"');
        PanelResult result = surface.Groups["kind"].Value switch
        {
            "HyperbolicParaboloid" => PanelFactory.FromParametric(MathematicalSurfaces.HyperbolicParaboloid(name, LengthRequired(surfaceBody,"Width")/2, LengthRequired(surfaceBody,"Depth")/2, LengthRequired(surfaceBody,"Rise")),orientation,thickness,material),
            "ParabolicCylinder" => PanelFactory.FromParametric(MathematicalSurfaces.ParabolicCylinder(name, LengthRequired(surfaceBody,"Width")/2, LengthRequired(surfaceBody,"Depth")/2, LengthRequired(surfaceBody,"Rise")),orientation,thickness,material),
            "EllipticParaboloid" => PanelFactory.FromParametric(MathematicalSurfaces.EllipticParaboloid(name, LengthRequired(surfaceBody,"Width")/2, LengthRequired(surfaceBody,"Depth")/2, LengthRequired(surfaceBody,"Rise")),orientation,thickness,material),
            "Helicoid" => PanelFactory.FromParametric(MathematicalSurfaces.Helicoid(name,LengthRequired(surfaceBody,"Radius"),LengthRequired(surfaceBody,"Rise"),Number(surfaceBody,"Turns",1)),orientation,thickness,material),
            "ParametricSurface" => PanelFactory.FromParametric(Parametric(name,surfaceBody),orientation,thickness,material),
            "RuledSurface" => PanelFactory.FromRuled(Ruled(name,surfaceBody,RuledConstructionKind.RuledSurface),orientation,thickness,material),
            "RuledTransition" => PanelFactory.FromRuled(Ruled(name,surfaceBody,RuledConstructionKind.RuledTransition),orientation,thickness,material),
            "BoundaryPatch" => PanelFactory.FromBoundaryPatch(Boundary(name,surfaceBody),orientation,thickness,material),
            "SectionSurface" => PanelFactory.FromSectionSurface(Sections(name,surfaceBody),orientation,thickness,material),
            _ => new(null,[new(UnsupportedSurface,$"Surface kind '{surface.Groups["kind"].Value}' is not admitted.")])
        };
        foreach (var diagnostic in result.Diagnostics) diagnostics.Add($"{diagnostic.Code}:{name}:{diagnostic.Message}");
        return result.Panel;
    }

    private static ParametricSurfaceIr Parametric(string name,string body)
    {
        var u=Vector2(body,"DomainU");var v=Vector2(body,"DomainV");
        var parserX=new ExpressionParser(FieldRequired(body,"X"));var parserY=new ExpressionParser(FieldRequired(body,"Y"));var parserZ=new ExpressionParser(FieldRequired(body,"Z"));
        return new(name,SurfaceConstructionKind.ParametricSurface,new(new(u[0],u[1]),new(v[0],v[1])),new(parserX.Parse(),parserY.Parse(),parserZ.Parse()),$"firmament:Panel:{name}:ParametricSurface");
    }

    private static RuledSurfaceIr Ruled(string name,string body,RuledConstructionKind kind)
    {
        var a=NamedBoundary(body,"BoundaryA");var b=NamedBoundary(body,"BoundaryB");
        return new(name,kind,a,b,new(a.StableId,$"firmament:Panel:{name}","BoundaryA"),new(b.StableId,$"firmament:Panel:{name}","BoundaryB"));
    }

    private static BoundaryPatchIr Boundary(string name,string body)
    {
        var south=NamedBoundary(body,"South");var north=NamedBoundary(body,"North");var west=NamedBoundary(body,"West");var east=NamedBoundary(body,"East");
        var values=new[]{south,north,west,east};
        return new(name,south,north,west,east,values.Select(item=>new BoundaryProvenance(item.StableId,$"firmament:Panel:{name}",item.StableId)).ToArray());
    }

    private static SectionSurfaceIr Sections(string name,string body)
    {
        var match=Regex.Match(body,@"\bSections\s*:\s*\[",RegexOptions.CultureInvariant);if(!match.Success)throw new ArgumentException("SectionSurface requires Sections: [ ... ].");
        var open=body.IndexOf('[',match.Index);var close=Matching(body,open,'[',']');if(close<0)throw new ArgumentException("SectionSurface Sections list is incomplete.");
        var sectionBody=body[(open+1)..close];var sections=Blocks(sectionBody,@"\b(?<kind>Line|Arc|Circle)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{").Select(block=>BoundaryValue($"{name}:{block.Name}",block.Kind,block.Body)).ToArray();
        if(sections.Length<2)throw new ArgumentException("SectionSurface requires at least two explicit sections.");
        return new(name,sections,sections.Select((item,index)=>new BoundaryProvenance(item.StableId,$"firmament:Panel:{name}",$"section-{index}")).ToArray());
    }

    private static RuledBoundary NamedBoundary(string body,string field)
    {
        var match=Regex.Match(body,$@"\b{Regex.Escape(field)}\s*:\s*(?<kind>Line|Arc|Circle)\s*\{{",RegexOptions.CultureInvariant);
        if(!match.Success)throw new ArgumentException($"Missing {field} boundary.");var open=body.IndexOf('{',match.Index);var close=Matching(body,open,'{','}');
        if(close<0)throw new ArgumentException($"Incomplete {field} boundary.");return BoundaryValue(field,match.Groups["kind"].Value,body[(open+1)..close]);
    }

    private static RuledBoundary BoundaryValue(string id,string kind,string body) => kind switch
    {
        "Line"=>new RuledBoundary.Line(id,Point(body,"Start"),Point(body,"End")),
        "Circle"=>new RuledBoundary.Circle(id,Point(body,"Center"),Direction(body,"Normal"),LengthRequired(body,"Radius"),Direction(body,"ReferenceAxis")),
        "Arc"=>new RuledBoundary.Arc(id,Point(body,"Center"),Direction(body,"Normal"),LengthRequired(body,"Radius"),Direction(body,"ReferenceAxis"),Angle(body,"StartAngle"),Angle(body,"SweepAngle")),
        _=>throw new NotSupportedException($"Boundary kind '{kind}' is not admitted.")
    };

    private static string FieldRequired(string body,string name)=>Field(body,name)??throw new ArgumentException($"Missing field '{name}'.");
    private static string? Field(string body,string name)
    {
        var match=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*(?<value>[^;\r\n}}]+)\s*;?",RegexOptions.CultureInvariant);
        return match.Success?match.Groups["value"].Value.Trim():null;
    }
    private static double LengthRequired(string body,string name)=>Length(FieldRequired(body,name));
    private static double Length(string value)
    {var match=Regex.Match(value,@"^(?<n>[-+0-9.eE]+)\s*mm$");return match.Success&&double.TryParse(match.Groups["n"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var result)?result:throw new ArgumentException($"Expected millimetre length, received '{value}'.");}
    private static double Number(string body,string name,double fallback)
    {var value=Field(body,name);return value is null?fallback:double.Parse(value,CultureInfo.InvariantCulture);}
    private static double Angle(string body,string name)
    {var value=FieldRequired(body,name);var match=Regex.Match(value,@"^(?<n>[-+0-9.eE]+)\s*(?<u>deg|rad)$");if(!match.Success)throw new ArgumentException($"Expected angle for '{name}'.");var n=double.Parse(match.Groups["n"].Value,CultureInfo.InvariantCulture);return match.Groups["u"].Value=="deg"?n*Math.PI/180:n;}
    private static double[] Vector2(string body,string name)=>Vector(body,name,2,false);
    private static Point3D Point(string body,string name){var v=Vector(body,name,3,true);return new(v[0],v[1],v[2]);}
    private static Direction3D Direction(string body,string name){var v=Vector(body,name,3,false);return Direction3D.Create(new(v[0],v[1],v[2]));}
    private static double[] Vector(string body,string name,int count,bool lengths)
    {var raw=FieldRequired(body,name);var match=Regex.Match(raw,@"^\[(?<v>[^]]+)\]$");if(!match.Success)throw new ArgumentException($"Field '{name}' requires a vector.");var values=match.Groups["v"].Value.Split(',',StringSplitOptions.TrimEntries);if(values.Length!=count)throw new ArgumentException($"Field '{name}' requires {count} values.");return values.Select(value=>lengths?Length(value):double.Parse(value,CultureInfo.InvariantCulture)).ToArray();}

    private static IEnumerable<(string Name,string Kind,string Body)> Blocks(string source,string pattern)
    {foreach(Match match in Regex.Matches(source,pattern,RegexOptions.CultureInvariant)){var open=source.IndexOf('{',match.Index);var close=Matching(source,open,'{','}');if(close<0)continue;yield return(match.Groups["name"].Value,match.Groups["kind"].Success?match.Groups["kind"].Value:string.Empty,source[(open+1)..close]);}}
    private static int Matching(string text,int open,char opening,char closing)
    {var depth=0;for(var i=open;i<text.Length;i++){if(text[i]==opening)depth++;else if(text[i]==closing&&--depth==0)return i;}return -1;}

    private sealed class ExpressionParser
    {
        private readonly string text;private int position;
        internal ExpressionParser(string text)=>this.text=text;
        internal SurfaceScalarExpression Parse(){var result=Add();Skip();if(position!=text.Length)throw new ArgumentException($"Unexpected parametric expression token at '{text[position..]}'.");return result;}
        private SurfaceScalarExpression Add(){var value=Multiply();while(true){Skip();if(Take('+'))value=SurfaceExpression.Add(value,Multiply());else if(Take('-'))value=SurfaceExpression.Subtract(value,Multiply());else return value;}}
        private SurfaceScalarExpression Multiply(){var value=Power();while(true){Skip();if(Take('*'))value=SurfaceExpression.Multiply(value,Power());else if(Take('/'))value=SurfaceExpression.Divide(value,Power());else return value;}}
        private SurfaceScalarExpression Power(){var value=Unary();Skip();if(Take('^')){Skip();var exponent=(int)ReadNumber(out var unit);if(unit)throw new ArgumentException("Expression exponent must be dimensionless.");value=SurfaceExpression.Power(value,exponent);}return value;}
        private SurfaceScalarExpression Unary(){Skip();if(Take('-'))return SurfaceExpression.Multiply(SurfaceExpression.Number(-1),Unary());if(Take('+'))return Unary();return Primary();}
        private SurfaceScalarExpression Primary(){Skip();if(Take('(')){var value=Add();Skip();if(!Take(')'))throw new ArgumentException("Parametric expression is missing ')'.");return value;}if(char.IsDigit(Peek())||Peek()=='.'){var n=ReadNumber(out var mm);return mm?SurfaceExpression.Length(n):SurfaceExpression.Number(n);}var id=ReadIdentifier();if(id=="u")return SurfaceExpression.U;if(id=="v")return SurfaceExpression.V;if(id is "sin" or "cos"){Skip();if(!Take('('))throw new ArgumentException($"{id} requires parentheses.");var value=Add();if(!Take(')'))throw new ArgumentException($"{id} is missing ')'.");return id=="sin"?SurfaceExpression.Sin(value):SurfaceExpression.Cos(value);}throw new ArgumentException($"Unknown parametric expression symbol '{id}'.");}
        private double ReadNumber(out bool mm){Skip();var start=position;while(position<text.Length&&(char.IsDigit(text[position])||text[position] is '.' or 'e' or 'E' or '+' or '-'))position++;var n=double.Parse(text[start..position],CultureInfo.InvariantCulture);mm=text.AsSpan(position).StartsWith("mm",StringComparison.Ordinal);if(mm)position+=2;return n;}
        private string ReadIdentifier(){Skip();var start=position;while(position<text.Length&&(char.IsLetterOrDigit(text[position])||text[position]=='_'))position++;return text[start..position];}
        private char Peek(){Skip();return position<text.Length?text[position]:'\0';}private bool Take(char c){Skip();if(position<text.Length&&text[position]==c){position++;return true;}return false;}private void Skip(){while(position<text.Length&&char.IsWhiteSpace(text[position]))position++;}
    }
}
