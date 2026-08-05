namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Immutable, resolved material boundary. It is neither a sketch nor a constraint solver.</summary>
public sealed record ProfileSegmentProvenance(string StableId, string ConceptStableId, string SourceSpan, string Derivation, string SourceFrame);
public sealed record ResolvedProfileSegment2D(string Name, LineArcProfileCurve2D Geometry, ProfileSegmentProvenance Provenance);
public sealed record ResolvedProfileLoop2D(string Name, bool IsOuter, IReadOnlyList<ResolvedProfileSegment2D> Segments);
public sealed record ResolvedProfile2D(string Name, string PlaneFrame, IReadOnlyList<ResolvedProfileLoop2D> Loops, ConstructionPlane? ConstructionPlane = null, double? LocalStartDepth = null, double? LocalEndDepth = null)
{
    public ConstructionPlane EffectiveConstructionPlane => ConstructionPlane ?? Materializer.ConstructionPlane.WorldXY;
}
public sealed record ResolvedProfileValidationResult(bool IsValid, double SignedArea, IReadOnlyList<string> Diagnostics);

public static class ResolvedProfile2DValidator
{
    private const double Tol = 1e-7;
    public static ResolvedProfileValidationResult Validate(ResolvedProfile2D profile)
    {
        var d = new List<string>();
        if (profile.Loops.Count == 0 || profile.Loops.Count(x => x.IsOuter) != 1) d.Add($"profile:{profile.Name}: exactly one declared outer loop is required");
        var outer = profile.Loops.SingleOrDefault(x => x.IsOuter);
        if (outer is null) return new(false, 0, d.Append($"profile:{profile.Name}: outer loop is required").ToArray());
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var loop in profile.Loops)
        {
            if (loop.Segments.Count == 0 || (loop.Segments.Count < 3 && !(loop.Segments.Count == 1 && loop.Segments[0].Geometry is LineArcFullCircle2D))) d.Add($"profile:{profile.Name}: loop '{loop.Name}' requires a closed boundary");
            for (var i=0;i<loop.Segments.Count;i++)
            {
                var s=loop.Segments[i]; if(!names.Add(s.Name))d.Add($"profile:{profile.Name}: duplicate segment '{s.Name}'");
                if (s.Geometry is LineArcFullCircle2D && loop.Segments.Count == 1) continue;
                if(!Endpoints(s.Geometry,out var start,out var end)){d.Add($"profile:{profile.Name}: unsupported unbounded segment '{s.Name}'");continue;}
                if(Distance(start,end)<=Tol)d.Add($"profile:{profile.Name}: zero-length segment '{s.Name}'");
                var next=loop.Segments[(i+1)%loop.Segments.Count]; if(Endpoints(next.Geometry,out var ns,out _)&&Distance(end,ns)>Tol)d.Add($"profile:{profile.Name}: endpoint mismatch '{s.Name}' -> '{next.Name}'");
            }
            for (var i=0;i<loop.Segments.Count;i++) for (var j=i+1;j<loop.Segments.Count;j++)
            {
                var a=loop.Segments[i]; var b=loop.Segments[j];
                if (SameLine(a.Geometry,b.Geometry)) d.Add($"profile:{profile.Name}: duplicate coincident segment '{a.Name}' / '{b.Name}'");
                if (!Adjacent(i,j,loop.Segments.Count) && a.Geometry is LineArcLineSegment2D && b.Geometry is LineArcLineSegment2D && Endpoints(a.Geometry,out var as_,out var ae) && Endpoints(b.Geometry,out var bs,out var be) && ProperIntersection(as_,ae,bs,be)) d.Add($"profile:{profile.Name}: self-intersection '{a.Name}' / '{b.Name}'");
            }
        }
        var area=outer.Segments.Sum(s=>Endpoints(s.Geometry,out var a,out var b)?a.X*b.Y-b.X*a.Y:0)/2d;
        if(area<=Tol)d.Add($"profile:{profile.Name}: outer winding must be CounterClockwise with nonzero area");
        return new(d.Count==0,area,d);
    }
    public static LineArcProfileExtrudeResult Extrude(ResolvedProfile2D profile,double height)
    {
        var validation=Validate(profile); if(!validation.IsValid)return new(LineArcProfileExtrudeStatus.Rejected,null,validation.Diagnostics);
        return LineArcProfileExtrudeEmitter.TryEmit(profile, height);
    }
    private static bool Endpoints(LineArcProfileCurve2D c,out (double X,double Y) a,out (double X,double Y) b){switch(c){case LineArcLineSegment2D l:a=l.Start;b=l.End;return true;case LineArcCircularArc2D x:a=(x.Center.X+x.Radius*Math.Cos(x.StartAngleRadians),x.Center.Y+x.Radius*Math.Sin(x.StartAngleRadians));b=(x.Center.X+x.Radius*Math.Cos(x.StartAngleRadians+x.SweepAngleRadians),x.Center.Y+x.Radius*Math.Sin(x.StartAngleRadians+x.SweepAngleRadians));return true;default:a=default;b=default;return false;}}
    private static double Distance((double X,double Y)a,(double X,double Y)b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private static bool Adjacent(int i,int j,int count)=>j==i+1 || (i==0 && j==count-1);
    private static bool SameLine(LineArcProfileCurve2D a,LineArcProfileCurve2D b)=>a is LineArcLineSegment2D x&&b is LineArcLineSegment2D y&&((Distance(x.Start,y.Start)<=Tol&&Distance(x.End,y.End)<=Tol)||(Distance(x.Start,y.End)<=Tol&&Distance(x.End,y.Start)<=Tol));
    private static bool ProperIntersection((double X,double Y)a,(double X,double Y)b,(double X,double Y)c,(double X,double Y)d)
    { var ab1=Orientation(a,b,c);var ab2=Orientation(a,b,d);var cd1=Orientation(c,d,a);var cd2=Orientation(c,d,b);return ab1*ab2 < -Tol && cd1*cd2 < -Tol; }
    private static double Orientation((double X,double Y)a,(double X,double Y)b,(double X,double Y)c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
}
