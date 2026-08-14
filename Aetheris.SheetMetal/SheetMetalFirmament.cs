using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public sealed record AuthoredFlangeSpec(string Name,string Side,double Length,double AngleRadians,double InsideRadius,SheetBendDirection Direction);
public sealed record AuthoredHoleSpec(string Name,double X,double Y,double Diameter);
public sealed record SheetMetalAuthoringSpec(string Name,double Thickness,double BaseWidth,double BaseDepth,string? Material,double KFactor,AuthoredFlangeSpec Left,AuthoredFlangeSpec Right,IReadOnlyList<AuthoredHoleSpec> Holes);
public sealed record SheetMetalAuthoringResult(bool IsSuccess,SheetMetalAuthoringSpec? Spec,SheetMetalPartIr? Part,SheetMetalFlatPatternIr? FlatPattern,IReadOnlyList<SheetMetalDiagnostic> Diagnostics);

/// <summary>Firmament V2 module-owned high-level syntax for the bounded two-flange M1 family.</summary>
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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);var full=Path.GetFullPath(path);if(!File.Exists(full))return Failure($"Sheet Metal Firmament source was not found: {full}");
        return Compile(File.ReadAllText(full),full);
    }

    public static SheetMetalAuthoringResult Compile(string source,string sourcePath="authored.firmament")
    {
        ArgumentNullException.ThrowIfNull(source);var clean=Regex.Replace(source,@"//.*?$|#.*?$",string.Empty,RegexOptions.Multiline);
        if(RecoveredSheetMetalFirmament.IsRecovered(clean))return RecoveredSheetMetalFirmament.Compile(clean,sourcePath);
        var header=Regex.Match(clean,@"\bSheetMetal\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",Rx);if(!header.Success)return Failure("Expected `SheetMetal <Name> { ... }`.");
        if(!TryScalar(clean,"Thickness","mm",out var thickness)||thickness<=0)return Failure("Thickness must be a positive millimetre value.");
        var baseMatch=Regex.Match(clean,@"\bBase\s*:\s*Rectangle\s*\(\s*(?<w>[+-]?[0-9.]+)\s*mm\s*,\s*(?<d>[+-]?[0-9.]+)\s*mm\s*\)\s*;",Rx);
        if(!baseMatch.Success)return Failure("Expected `Base: Rectangle(<width>mm, <depth>mm);`.");
        var width=Num(baseMatch,"w");var depth=Num(baseMatch,"d");if(width<=0||depth<=0)return Failure("Base rectangle dimensions must be positive.");
        var material=Regex.Match(clean,"\\bMaterial\\s*:\\s*\"(?<v>[^\"]+)\"\\s*;",Rx) is {Success:true} mm?mm.Groups["v"].Value:null;
        var k=TryScalar(clean,"KFactor",null,out var parsedK)?parsedK:SheetMetalFlattenPolicy.Default.KFactor;if(k<0||k>1)return Failure("KFactor must be between 0 and 1.");
        var flanges=Regex.Matches(clean,@"\bFlange\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}",Rx).Cast<Match>().Select(ParseFlange).ToArray();
        if(flanges.Any(f=>f is null))return Failure("Each flange requires From: Base.Left|Right, Length, Angle, and InsideRadius.");
        var typed=flanges.Cast<AuthoredFlangeSpec>().ToArray();var left=typed.SingleOrDefault(f=>f.Side.Equals("Left",StringComparison.OrdinalIgnoreCase));var right=typed.SingleOrDefault(f=>f.Side.Equals("Right",StringComparison.OrdinalIgnoreCase));
        if(left is null||right is null||typed.Length!=2)return Failure("M1 authored lowering requires exactly one Base.Left and one Base.Right flange.");
        if(Math.Abs(left.AngleRadians-Math.PI/2)>1e-8||Math.Abs(right.AngleRadians-Math.PI/2)>1e-8)return Failure("M1 formed topology supports 90 degree two-flange channels only; other angles remain typed unsupported.");
        if(left.InsideRadius<0||right.InsideRadius<0||Math.Abs(left.InsideRadius-right.InsideRadius)>1e-8)return Failure("M1 formed topology requires equal non-negative inside radii on both flanges.");
        if(left.Length<=left.InsideRadius+thickness||right.Length<=right.InsideRadius+thickness)return Failure("Flange length must exceed inside radius plus thickness.");
        var holes=Regex.Matches(clean,@"\bHole\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}",Rx).Cast<Match>().Select(ParseHole).ToArray();
        if(holes.Any(h=>h is null))return Failure("Each hole requires On: Base, Center: (xmm, ymm), and Diameter.");
        var typedHoles=holes.Cast<AuthoredHoleSpec>().ToArray();if(typedHoles.Any(h=>h.Diameter<=0||h.X-h.Diameter/2<=0||h.X+h.Diameter/2>=width||h.Y-h.Diameter/2<=0||h.Y+h.Diameter/2>=depth))return Failure("Authored holes must have positive diameter and remain strictly inside the base boundary.");
        var spec=new SheetMetalAuthoringSpec(header.Groups["name"].Value,thickness,width,depth,material,k,left,right,typedHoles);
        var bodyResult=AuthoredUChannelBrepBuilder.Build(spec);if(!bodyResult.IsSuccess||bodyResult.Value is null)return new(false,spec,null,null,bodyResult.Diagnostics.Select(d=>new SheetMetalDiagnostic("sheetmetal-formed-brep-failed",SheetMetalDiagnosticSeverity.Error,d.Message)).ToArray());
        var part=CreatePart(spec,bodyResult.Value,sourcePath);var flat=SheetMetalFlattener.Flatten(part);return new(true,spec,part,flat,part.Diagnostics.Concat(flat.Diagnostics).ToArray());
    }

    private static SheetMetalPartIr CreatePart(SheetMetalAuthoringSpec s,BrepBody body,string sourcePath)
    {
        var t=s.Thickness;var r=s.Left.InsideRadius;var zAxis=new Vector3D(0,0,1);var yAxis=new Vector3D(0,1,0);var xAxis=new Vector3D(1,0,0);var midZ=t/2;
        var baseRegion=new SheetRegionIr("region-base",SheetRegionKind.Planar,new(DevelopabilityKind.Developable,"authored analytic plane",0,0,"Authored planar base."),new(new(0,0,midZ),zAxis,xAxis,yAxis,true),null,[new(0,0,midZ),new(s.BaseWidth,0,midZ),new(s.BaseWidth,s.BaseDepth,midZ),new(0,s.BaseDepth,midZ)],s.BaseWidth*s.BaseDepth,new("Firmament declaration","authored semantics",[],[],sourcePath),[new(SheetEvidenceKind.Authored,"base-region","High-level Rectangle base declaration.")]);
        var leftX=-(r+t/2);var rightX=s.BaseWidth+r+t/2;var leftRegion=new SheetRegionIr("region-left-flange",SheetRegionKind.Planar,new(DevelopabilityKind.Developable,"authored analytic plane",0,0,"Authored planar flange."),new(new(leftX,0,r+t),new(-1,0,0),yAxis,zAxis,true),null,[new(leftX,0,r+t),new(leftX,s.BaseDepth,r+t),new(leftX,s.BaseDepth,s.Left.Length),new(leftX,0,s.Left.Length)],s.BaseDepth*(s.Left.Length-r-t),new("Firmament declaration","authored semantics",[],[],sourcePath),[new(SheetEvidenceKind.Authored,"flange","Base.Left flange declaration.")]);
        var rightRegion=new SheetRegionIr("region-right-flange",SheetRegionKind.Planar,new(DevelopabilityKind.Developable,"authored analytic plane",0,0,"Authored planar flange."),new(new(rightX,0,r+t),new(1,0,0),yAxis,zAxis,true),null,[new(rightX,0,r+t),new(rightX,0,s.Right.Length),new(rightX,s.BaseDepth,s.Right.Length),new(rightX,s.BaseDepth,r+t)],s.BaseDepth*(s.Right.Length-r-t),new("Firmament declaration","authored semantics",[],[],sourcePath),[new(SheetEvidenceKind.Authored,"flange","Base.Right flange declaration.")]);
        SheetRegionIr bendRegion(string id,double x)=>new(id,SheetRegionKind.CylindricalBend,new(DevelopabilityKind.Developable,"authored analytic cylinder",0,0,"Authored cylindrical bend."),null,new(new(x,s.BaseDepth/2d,r+t),yAxis,r+t/2,r,Math.PI/2,s.BaseDepth,true),[],Math.PI/2*(r+t/2)*s.BaseDepth,new("Firmament declaration","authored semantics",[],[],sourcePath),[new(SheetEvidenceKind.Authored,"bend-region","90 degree cylindrical bend declaration.")]);
        var leftBendRegion=bendRegion("region-left-bend",0);var rightBendRegion=bendRegion("region-right-bend",s.BaseWidth);
        SheetBendIr bend(string id,double x,string a,string b,AuthoredFlangeSpec flange)=>new(id,new(x,s.BaseDepth/2d,r+t),yAxis,flange.AngleRadians,r,t,flange.Direction,a,b,SheetNeutralAxisPolicy.KFactorPolicy(s.KFactor),new("Firmament declaration","authored semantics",[],[],sourcePath),[new(SheetEvidenceKind.Authored,"bend","Axis, angle, radius, direction, and adjacency authored explicitly.")]);
        var bends=new[]{bend("bend-left",0,"region-base","region-left-flange",s.Left),bend("bend-right",s.BaseWidth,"region-base","region-right-flange",s.Right)};
        var features=s.Holes.Select(h=>new SheetFeatureIr($"feature-{h.Name}",SheetFeatureKind.CircularHole,"region-base",new(h.X,h.Y,midZ),h.Diameter,[],new("Firmament declaration","authored semantics",[],[],sourcePath),[new(SheetEvidenceKind.Authored,"through-hole","Authored Base circular through-hole.",h.Diameter)])).ToArray();
        var policy=new SheetMetalFlattenPolicy(s.KFactor);return new($"sheetmetal-{s.Name}",t,s.Material,"region-base",[baseRegion,leftRegion,rightRegion,leftBendRegion,rightBendRegion],bends,features,policy,SheetMetalRecognitionStatus.Complete,"Firmament V2 SheetMetal authored semantics",[new(SheetEvidenceKind.Authored,"constant-thickness","Thickness is an authored domain value.",t),new(SheetEvidenceKind.Authored,"stationary-region","Base region authored explicitly.")],[],body);
    }

    private static AuthoredFlangeSpec? ParseFlange(Match m){var b=m.Groups["body"].Value;var from=Regex.Match(b,@"\bFrom\s*:\s*Base\.(?<side>Left|Right)\s*;",Rx);if(!from.Success||!TryScalar(b,"Length","mm",out var len)||!TryScalar(b,"Angle","deg",out var deg)||!TryScalar(b,"InsideRadius","mm",out var r))return null;var direction=Regex.Match(b,@"\bDirection\s*:\s*(?<d>Up|Down)\s*;",Rx) is {Success:true} d&&d.Groups["d"].Value.Equals("Down",StringComparison.OrdinalIgnoreCase)?SheetBendDirection.Down:SheetBendDirection.Up;return new(m.Groups["name"].Value,from.Groups["side"].Value,len,deg*Math.PI/180d,r,direction);}
    private static AuthoredHoleSpec? ParseHole(Match m){var b=m.Groups["body"].Value;if(!Regex.IsMatch(b,@"\bOn\s*:\s*Base\s*;",Rx)||!TryScalar(b,"Diameter","mm",out var d))return null;var c=Regex.Match(b,@"\bCenter\s*:\s*\(\s*(?<x>[+-]?[0-9.]+)\s*mm\s*,\s*(?<y>[+-]?[0-9.]+)\s*mm\s*\)\s*;",Rx);return c.Success?new(m.Groups["name"].Value,Num(c,"x"),Num(c,"y"),d):null;}
    private static bool TryScalar(string text,string name,string? unit,out double value){var suffix=unit is null?string.Empty:@"\s*"+Regex.Escape(unit);var m=Regex.Match(text,@"\b"+Regex.Escape(name)+@"\s*:\s*(?<v>[+-]?[0-9]+(?:\.[0-9]+)?)"+suffix+@"\s*;",Rx);value=m.Success?Num(m,"v"):0;return m.Success;}
    private static double Num(Match m,string group)=>double.Parse(m.Groups[group].Value,NumberStyles.Float,CultureInfo.InvariantCulture);
    private static SheetMetalAuthoringResult Failure(string message)=>new(false,null,null,null,[new("sheetmetal-firmament-invalid",SheetMetalDiagnosticSeverity.Error,message)]);
}

internal static class AuthoredUChannelBrepBuilder
{
    private enum EdgeKind{Line,Arc}
    private sealed record CrossEdge(Point3D Start,Point3D End,EdgeKind Kind,Point3D? Center,double Radius,Vector3D? Axis,bool Inner=false);
    private readonly record struct Use(EdgeId Edge,bool Reverse);
    private static readonly Direction3D PlusX=Direction3D.Create(new Vector3D(1,0,0));private static readonly Direction3D PlusY=Direction3D.Create(new Vector3D(0,1,0));private static readonly Direction3D MinusY=Direction3D.Create(new Vector3D(0,-1,0));private static readonly Direction3D PlusZ=Direction3D.Create(new Vector3D(0,0,1));

    public static KernelResult<BrepBody> Build(SheetMetalAuthoringSpec s)
    {
        var t=s.Thickness;var r=s.Left.InsideRadius;var ro=r+t;var w=s.BaseWidth;var d=s.BaseDepth;var ll=s.Left.Length;var lr=s.Right.Length;
        var p=new[]{new Point3D(-ro,0,ll),new(-ro,0,ro),new(0,0,0),new(w,0,0),new(w+ro,0,ro),new(w+ro,0,lr),new(w+r,0,lr),new(w+r,0,ro),new(w,0,t),new(0,0,t),new(-r,0,ro),new(-r,0,ll)};
        var edgeSpec=new[]{Line(0,1),Arc(1,2,new(0,0,ro),ro,MinusY.ToVector()),Line(2,3),Arc(3,4,new(w,0,ro),ro,MinusY.ToVector()),Line(4,5),Line(5,6),Line(6,7),Arc(7,8,new(w,0,ro),r,PlusY.ToVector(),true),Line(8,9),Arc(9,10,new(0,0,ro),r,PlusY.ToVector(),true),Line(10,11),Line(11,0)};
        var b=new TopologyBuilder();var g=new BrepGeometryStore();var bindings=new BrepBindingModel();var points=new Dictionary<VertexId,Point3D>();var v0=new VertexId[p.Length];var v1=new VertexId[p.Length];
        for(var i=0;i<p.Length;i++){v0[i]=b.AddVertex();v1[i]=b.AddVertex();points[v0[i]]=p[i];points[v1[i]]=p[i]+new Vector3D(0,d,0);}
        var e0=new EdgeId[p.Length];var e1=new EdgeId[p.Length];var ey=new EdgeId[p.Length];for(var i=0;i<p.Length;i++){var n=(i+1)%p.Length;e0[i]=b.AddEdge(v0[i],v0[n]);e1[i]=b.AddEdge(v1[i],v1[n]);ey[i]=b.AddEdge(v0[i],v1[i]);BindCross(e0[i],edgeSpec[i],0);BindCross(e1[i],edgeSpec[i],d);BindLine(ey[i],points[v0[i]],points[v1[i]]);}
        var bottomHoles=new List<EdgeId>();var topHoles=new List<EdgeId>();foreach(var h in s.Holes){bottomHoles.Add(AddCircle(h.X,h.Y,0,h.Diameter/2));topHoles.Add(AddCircle(h.X,h.Y,t,h.Diameter/2));}
        var faces=new List<FaceId>();var start=AddFace([[..e0.Select(x=>new Use(x,false))]]);BindPlane(start,new(0,0,0),MinusY,PlusX);faces.Add(start);var end=AddFace([[..e1.Reverse().Select(x=>new Use(x,true))]]);BindPlane(end,new(0,d,0),PlusY,PlusX);faces.Add(end);
        for(var i=0;i<p.Length;i++){var n=(i+1)%p.Length;var loops=new List<IReadOnlyList<Use>> { new Use[] { new(e0[i],false),new(ey[n],false),new(e1[i],true),new(ey[i],true) } };if(i==2)loops.AddRange(bottomHoles.Select(e=>(IReadOnlyList<Use>)new Use[] { new(e,true) }));if(i==8)loops.AddRange(topHoles.Select(e=>(IReadOnlyList<Use>)new Use[] { new(e,false) }));var face=AddFace(loops);BindSide(face,edgeSpec[i]);faces.Add(face);}
        for(var i=0;i<bottomHoles.Count;i++){var face=AddFace([[new(bottomHoles[i],false)],[new(topHoles[i],true)]]);var h=s.Holes[i];BindSurface(face,SurfaceGeometry.FromCylinder(new CylinderSurface(new(h.X,h.Y,0),PlusZ,h.Diameter/2,PlusX)),false);faces.Add(face);}
        var shell=b.AddShell(faces);b.AddBody([shell]);var body=new BrepBody(b.Model,g,bindings,points);var preflight=BrepExportPreflight.Validate(body);if(!preflight.IsValid)return KernelResult<BrepBody>.Failure(preflight.Diagnostics.Where(x=>x.Severity==BrepExportPreflightSeverity.Error).Select(x=>new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,$"{x.Code}: {x.Message}",x.Context)).ToArray());return KernelResult<BrepBody>.Success(body);

        CrossEdge Line(int a,int q)=>new(p[a],p[q],EdgeKind.Line,null,0,null);CrossEdge Arc(int a,int q,Point3D c,double radius,Vector3D axis,bool inner=false)=>new(p[a],p[q],EdgeKind.Arc,c,radius,axis,inner);
        void BindCross(EdgeId edge,CrossEdge spec,double y){var a=spec.Start+new Vector3D(0,y,0);var q=spec.End+new Vector3D(0,y,0);if(spec.Kind==EdgeKind.Line){BindCurve(edge,CurveGeometry.FromLine(new Line3Curve(a,Direction3D.Create(q-a))),0,(q-a).Length);}else{var center=spec.Center!.Value+new Vector3D(0,y,0);BindCurve(edge,CurveGeometry.FromCircle(new Circle3Curve(center,Direction3D.Create(spec.Axis!.Value),spec.Radius,Direction3D.Create(a-center))),0,Math.PI/2);}}
        void BindLine(EdgeId edge,Point3D a,Point3D q)=>BindCurve(edge,CurveGeometry.FromLine(new Line3Curve(a,Direction3D.Create(q-a))),0,(q-a).Length);
        EdgeId AddCircle(double x,double y,double z,double radius){var vertex=b.AddVertex();points[vertex]=new Point3D(x+radius,y,z);var edge=b.AddEdge(vertex,vertex);BindCurve(edge,CurveGeometry.FromCircle(new Circle3Curve(new(x,y,z),PlusZ,radius,PlusX)),0,2*Math.PI);return edge;}
        void BindCurve(EdgeId edge,CurveGeometry curve,double from,double to){var id=new CurveGeometryId(g.Curves.Count()+1);g.AddCurve(id,curve);bindings.AddEdgeBinding(new(edge,id,new(from,to)));}
        FaceId AddFace(IReadOnlyList<IReadOnlyList<Use>> loops){var ids=new List<LoopId>();foreach(var uses in loops){var loop=b.AllocateLoopId();var co=uses.Select(_=>b.AllocateCoedgeId()).ToArray();for(var i=0;i<co.Length;i++)b.AddCoedge(new(co[i],uses[i].Edge,loop,co[(i+1)%co.Length],co[(i+co.Length-1)%co.Length],uses[i].Reverse));b.AddLoop(new Loop(loop,co));ids.Add(loop);}return b.AddFace(ids);}
        void BindSide(FaceId face,CrossEdge spec){if(spec.Kind==EdgeKind.Arc){BindSurface(face,SurfaceGeometry.FromCylinder(new CylinderSurface(spec.Center!.Value,PlusY,spec.Radius,Direction3D.Create(spec.Start-spec.Center.Value))),!spec.Inner);}else{var direction=spec.End-spec.Start;var normal=Direction3D.Create(direction.Cross(PlusY.ToVector()));var u=Math.Abs(normal.ToVector().Dot(PlusY.ToVector()))>.99?PlusX:PlusY;BindSurface(face,SurfaceGeometry.FromPlane(new PlaneSurface(spec.Start,normal,u)));}}
        void BindPlane(FaceId face,Point3D origin,Direction3D normal,Direction3D u)=>BindSurface(face,SurfaceGeometry.FromPlane(new PlaneSurface(origin,normal,u)));
        void BindSurface(FaceId face,SurfaceGeometry surface,bool same=true){var id=new SurfaceGeometryId(g.Surfaces.Count()+1);g.AddSurface(id,surface);bindings.AddFaceBinding(new(face,id,same));}
    }
}
