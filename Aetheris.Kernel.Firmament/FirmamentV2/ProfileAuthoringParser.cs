using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Bounded parser for the first scaffold-referenced profile source route.</summary>
public static class ProfileAuthoringParser
{
    public const string SegmentEndpointMustReferenceNamedPoint = "ProfileSegmentEndpointMustReferenceNamedPoint";
    private static readonly Regex Point = new(@"\bPoint2\s+(?<n>\w+)\s*\{\s*Position\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*(?:\]|\))", RegexOptions.Singleline);
    private static readonly Regex Line = new(@"\bLine2\s+(?<n>\w+)\s*\{\s*From\s*:\s*(?<a>\w+)\s*;?\s*To\s*:\s*(?<b>\w+)", RegexOptions.Singleline);
    private static readonly Regex Circle = new(@"\bCircle2\s+(?<n>\w+)\s*\{\s*Center\s*:\s*(?<c>\w+)\s*;?\s*Radius\s*:\s*(?<r>[-+.\d]+)mm", RegexOptions.Singleline);
    private static readonly Regex Rect = new(@"\bRect2\s+(?<n>\w+)\s*\{\s*Center\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*(?:\]|\))\s*;?\s*Size\s*:\s*\[(?<w>[-+.\d]+)mm\s*,\s*(?<h>[-+.\d]+)mm\]", RegexOptions.Singleline);
    private static readonly Regex Header = new(@"\bProfile\s+(?<n>\w+)\s+Using\s+(?<layout>\w+)\s*\{(?<body>[\s\S]*)", RegexOptions.CultureInvariant);
    private static readonly Regex ConstructionPlaneDeclaration = new(@"\bConstruction\s+Plane\s+(?<name>\w+)\s*\{\s*Trace\s*:\s*(?<trace>[\w.]+)\s*;?\s*\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Segment = new(@"\bSegment\s+(?<n>\w+)\s*\{\s*Trace\s*:\s*(?<trace>[\w.]+)\s*;?\s*From\s*:\s*(?<from>[\w.]+)\s*;?\s*To\s*:\s*(?<to>[\w.]+)(?:\s*;?\s*Sweep\s*:\s*(?<sweep>Clockwise|CounterClockwise))?", RegexOptions.Singleline);
    private static readonly Regex Extrude = new(@"\bExtrude\s+\w+\s*\{\s*Profile\s*:\s*(?<p>\w+)\s*;?\s*From\s*:\s*(?<a>[-+.\d]+)mm\s*;?\s*To\s*:\s*(?<b>[-+.\d]+)mm", RegexOptions.Singleline);

    public static bool IsProfileSource(string source) => Header.IsMatch(source);
    public static (ResolvedProfile2D? Profile, double Height, IReadOnlyList<string> Diagnostics) Parse(string source)
    {
        var d=new List<string>(); var h=Header.Match(source); if(!h.Success)return(null,0,["profile-source-missing-profile"]);
        ConstructionPlane? constructionPlane = null;
        var planeDeclaration = ConstructionPlaneDeclaration.Matches(source).Cast<Match>().SingleOrDefault(x => string.Equals(x.Groups["name"].Value, h.Groups["layout"].Value, StringComparison.Ordinal));
        if (planeDeclaration is not null)
        {
            if (!ConceptIrResolver.TryResolvePlane(source, planeDeclaration.Groups["trace"].Value, out var conceptPlane, out var traceDiagnostic) || conceptPlane is null)
                d.Add(traceDiagnostic ?? "ConstructionPlaneTraceMissing");
            else if (!ConstructionPlane.TryTrace("construction:" + planeDeclaration.Groups["name"].Value, conceptPlane, $"offset:{planeDeclaration.Index}", out constructionPlane, out var frameDiagnostic))
                d.Add(frameDiagnostic ?? "ConstructionPlaneFrameInvalid");
        }
        else constructionPlane = ConstructionPlane.WorldXY; // Existing Concept-layout Profiles retain their global-XY compatibility frame.
        var pts=new Dictionary<string,(double X,double Y)>(StringComparer.Ordinal);
        foreach(Match m in Point.Matches(source))pts[m.Groups["n"].Value]=(N(m,"x"),N(m,"y"));
        foreach (Match m in Rect.Matches(source))
        {
            var w=N(m,"w"); var rectHeight=N(m,"h"); var x=N(m,"x"); var y=N(m,"y"); var n=m.Groups["n"].Value;
            if (!double.IsFinite(w) || !double.IsFinite(rectHeight) || w<=0 || rectHeight<=0) { d.Add($"rect2-invalid-size:{n}"); continue; }
            pts[$"{n}.BottomLeft"]=(x-w/2,y-rectHeight/2); pts[$"{n}.BottomRight"]=(x+w/2,y-rectHeight/2); pts[$"{n}.TopRight"]=(x+w/2,y+rectHeight/2); pts[$"{n}.TopLeft"]=(x-w/2,y+rectHeight/2);
        }
        var lines=new Dictionary<string,LineArcLineSegment2D>(StringComparer.Ordinal);
        foreach(Match m in Line.Matches(source)){if(!pts.TryGetValue(m.Groups["a"].Value,out var a)||!pts.TryGetValue(m.Groups["b"].Value,out var b))d.Add($"profile-layout-unresolved-line:{m.Groups["n"].Value}");else lines[m.Groups["n"].Value]=new(a,b);}
        foreach (Match m in Rect.Matches(source))
        {
            var n=m.Groups["n"].Value;
            if (pts.TryGetValue($"{n}.BottomLeft",out var bl) && pts.TryGetValue($"{n}.BottomRight",out var br) && pts.TryGetValue($"{n}.TopRight",out var tr) && pts.TryGetValue($"{n}.TopLeft",out var tl))
            { lines[$"{n}.Bottom"]=new(bl,br); lines[$"{n}.Right"]=new(br,tr); lines[$"{n}.Top"]=new(tr,tl); lines[$"{n}.Left"]=new(tl,bl); }
        }
        var circles=new Dictionary<string,((double X,double Y) C,double R)>(StringComparer.Ordinal);
        foreach(Match m in Circle.Matches(source)){if(!pts.TryGetValue(m.Groups["c"].Value,out var c))d.Add($"profile-layout-unresolved-circle:{m.Groups["n"].Value}");else circles[m.Groups["n"].Value]=(c,N(m,"r"));}
        var segments=new List<ResolvedProfileSegment2D>();
        foreach (Match rawSegment in Regex.Matches(h.Groups["body"].Value, @"\bSegment\s+(?<name>\w+)\s*\{[\s\S]*?\b(?<endpoint>From|To)\s*:\s*(?<value>\[[^\]]*\]|Point2\s*\([^)]*\))", RegexOptions.CultureInvariant))
            d.Add($"{SegmentEndpointMustReferenceNamedPoint}:{rawSegment.Groups["name"].Value}:{rawSegment.Groups["endpoint"].Value}");
        foreach(Match m in Segment.Matches(h.Groups["body"].Value))
        { var n=m.Groups["n"].Value; if(!pts.TryGetValue(m.Groups["from"].Value,out var a)||!pts.TryGetValue(m.Groups["to"].Value,out var b)){d.Add($"profile-segment-unresolved:{n}");continue;} var trace=m.Groups["trace"].Value; LineArcProfileCurve2D? g=null;
          if(lines.TryGetValue(trace,out var line)){if(!OnLine(a,line)||!OnLine(b,line))d.Add($"profile-endpoint-not-on-guide:{n}:{trace}"); g=new LineArcLineSegment2D(a,b);}
          else if(circles.TryGetValue(trace,out var circle)){if(!OnCircle(a,circle)||!OnCircle(b,circle)||!m.Groups["sweep"].Success){d.Add($"profile-arc-invalid:{n}:{trace}");continue;}var sa=Math.Atan2(a.Y-circle.C.Y,a.X-circle.C.X);var sw=Math.Atan2(b.Y-circle.C.Y,b.X-circle.C.X)-sa;var ccw=m.Groups["sweep"].Value=="CounterClockwise";while(ccw&&sw<=0)sw+=2*Math.PI;while(!ccw&&sw>=0)sw-=2*Math.PI;g=new LineArcCircularArc2D(circle.C,circle.R,sa,sw);}
          else {d.Add($"profile-guide-missing:{n}:{trace}");continue;} segments.Add(new(n,g,new($"profile:{h.Groups["n"].Value}.Outer.{n}",$"concept:{h.Groups["layout"].Value}.{trace}","source",$"Trace({trace})","XY"))); }
        var e=Extrude.Match(source);var localStart=e.Success?N(e,"a"):0;var localEnd=e.Success?N(e,"b"):0;var height=e.Success?Math.Abs(localEnd-localStart):0;if(!e.Success||e.Groups["p"].Value!=h.Groups["n"].Value)d.Add("profile-extrude-missing-or-mismatched");
        return d.Count>0?(null,height,d):(new ResolvedProfile2D(h.Groups["n"].Value,h.Groups["layout"].Value,[new ResolvedProfileLoop2D("Outer",true,segments)],constructionPlane,localStart,localEnd),height,d);
    }
    private static double N(Match m,string n)=>double.Parse(m.Groups[n].Value,CultureInfo.InvariantCulture); private static bool OnLine((double X,double Y)p,LineArcLineSegment2D l)=>Math.Abs((l.End.X-l.Start.X)*(p.Y-l.Start.Y)-(l.End.Y-l.Start.Y)*(p.X-l.Start.X))<1e-7; private static bool OnCircle((double X,double Y)p,((double X,double Y)C,double R)c)=>Math.Abs(Math.Sqrt((p.X-c.C.X)*(p.X-c.C.X)+(p.Y-c.C.Y)*(p.Y-c.C.Y))-c.R)<1e-7;
}
