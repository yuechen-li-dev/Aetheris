using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.SheetMetal;
using Aetheris.FEA.Mechanics;

namespace Aetheris.FEA.Analysis;

public enum ThinWallAnalysisSourceKind { Synthetic, NativeSheetMetal }
public enum MidsurfacePatchKind { PlanarPanel, CylindricalBend }
public enum MidsurfacePatchEdge { RMinimum, RMaximum, SMinimum, SMaximum }
public enum PatchInterfaceOrientation { Same, Reversed }

/// <summary>Analytic surface data projected from product authority; all lengths are SI.</summary>
public sealed record MidsurfaceSurfaceMap(
    MidsurfacePatchKind Kind,
    Point3D Origin,
    Vector3D AxisR,
    Vector3D AxisS,
    Vector3D MaterialNormal,
    double RadiusMeters = 0,
    double AngleRadians = 0);

public sealed record MidsurfacePatch(
    string Identity,
    MidsurfacePatchKind Kind,
    MidsurfaceSurfaceMap SurfaceMap,
    IContinuumRegion ParameterDomain,
    double ThicknessMeters,
    string Material,
    int OpeningCount,
    string SourceAuthority);

public sealed record MidsurfacePatchInterface(
    string Identity,
    string PatchA,
    MidsurfacePatchEdge EdgeA,
    string PatchB,
    MidsurfacePatchEdge EdgeB,
    PatchInterfaceOrientation Orientation,
    Point3D StartMeters,
    Point3D EndMeters,
    double MaximumMismatchMeters,
    string SemanticBendId);

/// <summary>Read-only analysis projection. SheetMetalPartIr remains the product geometry authority.</summary>
public sealed record MidsurfacePatchGraph(
    ThinWallAnalysisSourceKind AnalysisSource,
    string SheetMetalBody,
    double ThicknessMeters,
    string Material,
    IReadOnlyList<MidsurfacePatch> Patches,
    IReadOnlyList<MidsurfacePatchInterface> Interfaces,
    int OpeningCount,
    string SourceAuthority)
{
    public int PanelPatchCount => Patches.Count(x => x.Kind == MidsurfacePatchKind.PlanarPanel);
    public int BendPatchCount => Patches.Count(x => x.Kind == MidsurfacePatchKind.CylindricalBend);
}

public sealed record ThinWallAuthorityProjection(MidsurfacePatchGraph? Graph,IReadOnlyList<AnalysisDiagnostic> Diagnostics)
{
    public bool IsSuccess => Graph is not null && Diagnostics.All(x => x.Severity != AnalysisDiagnosticSeverity.Error);
}

public static class ThinWallAnalysisAuthority
{
    private const double MillimetersToMeters = .001;
    private const double CoincidenceToleranceMeters = 1e-8;

    public static ThinWallAuthorityProjection Project(SheetMetalPartIr part,AnalysisProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(part);
        var diagnostics=new List<AnalysisDiagnostic>();
        AnalysisDiagnostic Error(string code,string message)=>new(code,AnalysisDiagnosticSeverity.Error,message,provenance);
        if(part.RecognitionStatus!=SheetMetalRecognitionStatus.Complete)
            diagnostics.Add(Error("thinwall-sheetmetal-incomplete","Native sheet-metal analysis requires Complete semantic SheetMetal authority."));
        if(!part.Provenance.Contains("Authored",StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(Error("thinwall-sheetmetal-source-unsupported","X1 admits authored native SheetMetal AIR only; recovered or reconstructed BRep evidence is not analysis authority."));
        if(!double.IsFinite(part.Thickness)||part.Thickness<=0)
            diagnostics.Add(Error("thinwall-thickness-invalid","Native SheetMetal thickness must be finite and positive."));
        if(string.IsNullOrWhiteSpace(part.Material))
            diagnostics.Add(Error("fea-missing-material","Native SheetMetal has no semantic material assignment."));
        if(diagnostics.Count>0)return new(null,diagnostics);

        var thickness=part.Thickness*MillimetersToMeters;
        var patches=new List<MidsurfacePatch>();
        foreach(var region in part.Regions.OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            switch(region.Kind)
            {
                case SheetRegionKind.Planar when region.Plane is not null:
                    patches.Add(ProjectPlanar(part,region,thickness,part.Material!));
                    break;
                case SheetRegionKind.CylindricalBend when region.Cylinder is not null:
                    try{patches.Add(ProjectCylinder(part,region,thickness,part.Material!));}
                    catch(InvalidOperationException ex){diagnostics.Add(Error("thinwall-sheetmetal-cylinder-invalid",$"Patch '{region.StableId}' cannot be parameterized: {ex.Message}"));}
                    break;
                default:
                    diagnostics.Add(Error("thinwall-sheetmetal-region-unsupported",$"Sheet region '{region.StableId}' has unsupported kind '{region.Kind}' or incomplete analytic surface authority."));
                    break;
            }
        }
        if(diagnostics.Count>0)return new(null,diagnostics);

        var interfaces=new List<MidsurfacePatchInterface>();
        foreach(var bend in part.Bends.OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            var bendRegionId=(part.Correspondence??[]).SingleOrDefault(x=>x.Kind.Equals("Bend",StringComparison.OrdinalIgnoreCase)&&x.SemanticId.Equals(bend.StableId,StringComparison.Ordinal))?.FormedId;
            if(string.IsNullOrWhiteSpace(bendRegionId))
            {
                diagnostics.Add(Error("thinwall-sheetmetal-adjacency-invalid",$"Bend '{bend.StableId}' has no exact semantic bend-region correspondence."));continue;
            }
            var bendPatch=patches.SingleOrDefault(x=>x.Identity==bendRegionId);
            var panelA=patches.SingleOrDefault(x=>x.Identity==bend.AdjacentRegionA);
            var panelB=patches.SingleOrDefault(x=>x.Identity==bend.AdjacentRegionB);
            if(bendPatch is null||panelA is null||panelB is null)
            {
                diagnostics.Add(Error("thinwall-sheetmetal-adjacency-invalid",$"Bend '{bend.StableId}' adjacency does not resolve to its two planar patches and cylindrical patch."));continue;
            }
            AddInterface(bend,bendPatch,MidsurfacePatchEdge.SMinimum,panelA,"A");
            AddInterface(bend,bendPatch,MidsurfacePatchEdge.SMaximum,panelB,"B");
        }
        if(diagnostics.Count>0)return new(null,diagnostics);
        var graph=new MidsurfacePatchGraph(ThinWallAnalysisSourceKind.NativeSheetMetal,part.StableId,thickness,part.Material!,patches,interfaces,part.Features.Count,
            "SheetMetalPartIr semantic AIR; geometric midsurface t/2; no BRep reverse engineering; manufacturing neutral-axis policy excluded");
        return new(graph,diagnostics);

        void AddInterface(SheetBendIr bend,MidsurfacePatch bendPatch,MidsurfacePatchEdge bendEdge,MidsurfacePatch panel,string suffix)
        {
            var b=EdgeEndpoints(bendPatch,bendEdge);var candidates=Enum.GetValues<MidsurfacePatchEdge>().Select(edge=>(Edge:edge,Ends:EdgeEndpoints(panel,edge))).ToArray();
            var selected=candidates.Select(x=>
            {
                var same=double.Max(Distance(b.Start,x.Ends.Start),Distance(b.End,x.Ends.End));
                var reversed=double.Max(Distance(b.Start,x.Ends.End),Distance(b.End,x.Ends.Start));
                return(x.Edge,Orientation:same<=reversed?PatchInterfaceOrientation.Same:PatchInterfaceOrientation.Reversed,Mismatch:double.Min(same,reversed));
            }).OrderBy(x=>x.Mismatch).First();
            if(selected.Mismatch>CoincidenceToleranceMeters)
            {
                diagnostics.Add(Error("thinwall-sheetmetal-interface-mismatch",$"Bend '{bend.StableId}' and panel '{panel.Identity}' do not share the same midsurface curve; mismatch {selected.Mismatch:R} m."));return;
            }
            interfaces.Add(new($"{bend.StableId}.{suffix}",bendPatch.Identity,bendEdge,panel.Identity,selected.Edge,selected.Orientation,b.Start,b.End,selected.Mismatch,bend.StableId));
        }
    }

    public static (Point3D Start,Point3D End) EdgeEndpoints(MidsurfacePatch patch,MidsurfacePatchEdge edge)
    {
        var b=patch.ParameterDomain.Bounds;
        return edge switch
        {
            MidsurfacePatchEdge.RMinimum=>(Evaluate(patch,b.Min.X,b.Min.Y,0).Position,Evaluate(patch,b.Min.X,b.Max.Y,0).Position),
            MidsurfacePatchEdge.RMaximum=>(Evaluate(patch,b.Max.X,b.Min.Y,0).Position,Evaluate(patch,b.Max.X,b.Max.Y,0).Position),
            MidsurfacePatchEdge.SMinimum=>(Evaluate(patch,b.Min.X,b.Min.Y,0).Position,Evaluate(patch,b.Max.X,b.Min.Y,0).Position),
            _=>(Evaluate(patch,b.Min.X,b.Max.Y,0).Position,Evaluate(patch,b.Max.X,b.Max.Y,0).Position)
        };
    }

    public static ThinWallMapEvaluation Evaluate(MidsurfacePatch patch,double r,double s,double t)
    {
        var m=patch.SurfaceMap;var half=patch.ThicknessMeters/2;
        if(m.Kind==MidsurfacePatchKind.PlanarPanel)
            return new(m.Origin+m.AxisR*r+m.AxisS*s,m.AxisR,m.AxisS,m.MaterialNormal*half);
        var sign=m.AngleRadians.Sign();var theta=s/m.RadiusMeters;var radial=m.AxisS*double.Cos(theta)+m.AxisR.Cross(m.AxisS)*(sign*double.Sin(theta));
        // AxisS is the geometric start radial. MaterialNormal may be its negative;
        // rotating both by the same signed angular direction preserves material side.
        var tangent=m.AxisR.Cross(radial)*sign;
        var normal0=m.MaterialNormal;var normal=normal0*double.Cos(theta)+m.AxisR.Cross(normal0)*(sign*double.Sin(theta));var tangentNormal=m.AxisR.Cross(normal)*sign;
        var current=m.RadiusMeters*radial+t*half*normal;
        return new(m.Origin+m.AxisR*r+current,m.AxisR,tangent+(t*half/m.RadiusMeters)*tangentNormal,normal*half);
    }

    private static MidsurfacePatch ProjectPlanar(SheetMetalPartIr part,SheetRegionIr region,double thickness,string material)
    {
        var p=region.Plane!;var u=Unit(p.UAxis);var v=Unit(p.VAxis);var n=Unit(p.Normal);var rSign=u.Cross(v).Dot(n)>=0?1d:-1d;
        var local=region.Boundary3D.Select(x=>(R:(x-p.Origin).Dot(u)*rSign*MillimetersToMeters,S:(x-p.Origin).Dot(v)*MillimetersToMeters)).ToArray();
        var bounds=new BoundingBox3D(new(local.Min(x=>x.R),local.Min(x=>x.S),-1),new(local.Max(x=>x.R),local.Max(x=>x.S),1));
        var contours=new List<ResolvedProfile2D>();
        if(region.ExactContour is not null)contours.Add(PlanarContourKernel.ToResolvedProfile(region.ExactContour,region.StableId));
        var features=part.Features.Where(x=>x.OwningRegionId==region.StableId).OrderBy(x=>x.StableId,StringComparer.Ordinal).ToArray();
        foreach(var feature in features)
        {
            if(feature.ExactContour is not null)contours.Add(PlanarContourKernel.ToResolvedProfile(feature.ExactContour,feature.StableId));
            else if(feature.Kind==SheetFeatureKind.CircularHole&&feature.Diameter is { } diameter)
                contours.Add(CircleProfile(feature.StableId,((feature.Center-p.Origin).Dot(u),(feature.Center-p.Origin).Dot(v)),diameter/2));
        }
        IContinuumRegion domain=contours.Count==0
            ?new PolygonParameterRegion(new(region.StableId+":parameter"),bounds,region.Boundary3D.Select(x=>((x-p.Origin).Dot(u),(x-p.Origin).Dot(v))).ToArray(),[],MillimetersToMeters,rSign)
            :new PolygonParameterRegion(new(region.StableId+":parameter"),bounds,null,contours,MillimetersToMeters,rSign);
        return new(region.StableId,MidsurfacePatchKind.PlanarPanel,new(MidsurfacePatchKind.PlanarPanel,Scale(p.Origin),u*rSign,v,n),domain,thickness,material,features.Length,"SheetRegionIr.Plane + exact semantic contour/features");
    }

    private static MidsurfacePatch ProjectCylinder(SheetMetalPartIr part,SheetRegionIr region,double thickness,string material)
    {
        if(region.Boundary3D.Count!=4)throw new InvalidOperationException("cylindrical bend boundary must contain four semantic corners");
        var c=region.Cylinder!;var length=c.AxisLength*MillimetersToMeters;var radius=c.GeometricMidRadius*MillimetersToMeters;
        if(radius<=thickness/2||length<=0||c.AngularSpanRadians<=0)throw new InvalidOperationException("radius, span, and axial length must be positive");
        var panelA=part.Bends.SingleOrDefault(x=>(part.Correspondence??[]).Any(y=>y.Kind=="Bend"&&y.SemanticId==x.StableId&&y.FormedId==region.StableId)) is { } bend
            ?part.Regions.SingleOrDefault(x=>x.StableId==bend.AdjacentRegionA):null;
        if(panelA?.Plane is null)throw new InvalidOperationException("material-normal orientation cannot be obtained from the semantic parent panel");
        var normal=Unit(panelA.Plane.Normal);var center=Scale(c.AxisOrigin);var originalAxis=Unit(c.AxisDirection);
        var corners=region.Boundary3D.Select(Scale).ToArray();
        foreach(var reverseAxis in new[]{false,true})
        {
            var axis=reverseAxis?-originalAxis:originalAxis;var start=reverseAxis?corners[1]:corners[0];var end=reverseAxis?corners[2]:corners[3];
            var axisOrigin=center-axis*(length/2);var radial=Unit(start-axisOrigin);var target=Unit(end-axisOrigin);
            foreach(var sign in new[]{1d,-1d})
            {
                var predicted=radial*double.Cos(c.AngularSpanRadians)+axis.Cross(radial)*(sign*double.Sin(c.AngularSpanRadians));
                if((predicted-target).Length>1e-7)continue;
                var tangent=axis.Cross(radial)*sign;
                if(axis.Cross(tangent).Dot(normal)<=0)continue;
                var bounds=new BoundingBox3D(new(0,0,-1),new(length,radius*c.AngularSpanRadians,1));
                var domain=new Aetheris.Continuum.Regions.Analytic.AxisAlignedBoxRegion(new(region.StableId+":parameter"),bounds);
                return new(region.StableId,MidsurfacePatchKind.CylindricalBend,new(MidsurfacePatchKind.CylindricalBend,axisOrigin,axis,radial,normal,radius,sign*c.AngularSpanRadians),domain,thickness,material,0,"SheetRegionIr.Cylinder.GeometricMidRadius + semantic bend boundary");
            }
        }
        throw new InvalidOperationException("semantic bend corners, axis, angle, and parent material normal do not define a positive analytic map");
    }

    private static ResolvedProfile2D CircleProfile(string id,(double X,double Y) center,double radius)
    {
        var provenance=new ProfileSegmentProvenance(id,id,id,"SheetMetal semantic circular opening","SheetRegionLocal[u,v]");
        return new(id,"SheetRegionLocal[u,v]",[new(id+".loop",false,[new(id,new LineArcFullCircle2D(center,radius),provenance)])]);
    }
    private static Point3D Scale(Point3D value)=>new(value.X*MillimetersToMeters,value.Y*MillimetersToMeters,value.Z*MillimetersToMeters);
    private static Vector3D Unit(Vector3D value){var l=value.Length;if(!double.IsFinite(l)||l<=0)throw new InvalidOperationException("zero or nonfinite direction");return value*(1/l);}
    private static double Distance(Point3D a,Point3D b)=>(a-b).Length;
    private static double Sign(this double value)=>value<0?-1:1;
}

internal sealed class PolygonParameterRegion : IContinuumRegion,IBoundsClassificationCapability
{
    private readonly IReadOnlyList<(double X,double Y)>? polygonMillimeters;
    private readonly IReadOnlyList<ResolvedProfile2D> profiles;
    private readonly double scale;
    private readonly double rSign;
    public PolygonParameterRegion(RegionId id,BoundingBox3D bounds,IReadOnlyList<(double X,double Y)>? polygonMillimeters,IReadOnlyList<ResolvedProfile2D> profiles,double scale,double rSign)
    {Id=id;Bounds=bounds;this.polygonMillimeters=polygonMillimeters;this.profiles=profiles;this.scale=scale;this.rSign=rSign;}
    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }
    public ContinuumPointClassification Classify(Point3D point,double tolerance=1e-9)
    {
        if(point.X<Bounds.Min.X-tolerance||point.X>Bounds.Max.X+tolerance||point.Y<Bounds.Min.Y-tolerance||point.Y>Bounds.Max.Y+tolerance)return ContinuumPointClassification.Outside;
        var p=(point.X/scale*rSign,point.Y/scale);
        if(profiles.Count>0)
        {
            var outer=ProfileArrangementBuilder.PointInProfile(profiles[0],p);if(outer==ArrangementPointLocation.Outside)return ContinuumPointClassification.Outside;
            if(profiles.Skip(1).Any(x=>ProfileArrangementBuilder.PointInProfile(x,p)!=ArrangementPointLocation.Outside))return ContinuumPointClassification.Outside;
            return outer==ArrangementPointLocation.OnBoundary?ContinuumPointClassification.Boundary:ContinuumPointClassification.Inside;
        }
        var inside=false;var poly=polygonMillimeters!;
        for(var i=0;i<poly.Count;i++)if(DistanceToSegment(p,poly[i],poly[(i+1)%poly.Count])<=tolerance/scale)return ContinuumPointClassification.Boundary;
        for(int i=0,j=poly.Count-1;i<poly.Count;j=i++)if((poly[i].Y>p.Item2)!=(poly[j].Y>p.Item2)&&p.Item1<(poly[j].X-poly[i].X)*(p.Item2-poly[i].Y)/(poly[j].Y-poly[i].Y)+poly[i].X)inside=!inside;
        return inside?ContinuumPointClassification.Inside:ContinuumPointClassification.Outside;
    }
    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds,double tolerance=1e-9)
    {
        if(bounds.Max.X<Bounds.Min.X||bounds.Min.X>Bounds.Max.X||bounds.Max.Y<Bounds.Min.Y||bounds.Min.Y>Bounds.Max.Y)return ContinuumBoundsClassification.Outside;
        var points=new[]{new Point3D(bounds.Min.X,bounds.Min.Y,0),new(bounds.Max.X,bounds.Min.Y,0),new(bounds.Max.X,bounds.Max.Y,0),new(bounds.Min.X,bounds.Max.Y,0),new((bounds.Min.X+bounds.Max.X)/2,(bounds.Min.Y+bounds.Max.Y)/2,0)};
        var states=points.Select(x=>Classify(x,tolerance)).ToArray();
        // Boundary-free rectangles are the common panel case. Profile-bearing cells
        // remain conservatively cut near the exact line/arc boxes.
        if(states.All(x=>x==ContinuumPointClassification.Inside)&&!MayMeetBoundary(bounds))return ContinuumBoundsClassification.Inside;
        if(states.All(x=>x==ContinuumPointClassification.Outside)&&!MayMeetBoundary(bounds))return ContinuumBoundsClassification.Outside;
        return ContinuumBoundsClassification.Cut;
    }
    private bool MayMeetBoundary(BoundingBox3D box)
    {
        if(profiles.Count==0){var polygon=polygonMillimeters!;return polygon.Zip(polygon.Skip(1).Append(polygon[0]),(a,b)=>(a,b)).Any(x=>Overlaps(box,x.a.X*scale*rSign,x.a.Y*scale,x.b.X*scale*rSign,x.b.Y*scale));}
        return profiles.SelectMany(x=>x.Loops).SelectMany(x=>x.Segments).Any(segment=>
        {
            var g=segment.Geometry;return g switch
            {
                LineArcLineSegment2D line=>Overlaps(box,line.Start.X*scale*rSign,line.Start.Y*scale,line.End.X*scale*rSign,line.End.Y*scale),
                LineArcCircularArc2D arc=>Overlaps(box,(arc.Center.X-arc.Radius)*scale*rSign,(arc.Center.Y-arc.Radius)*scale,(arc.Center.X+arc.Radius)*scale*rSign,(arc.Center.Y+arc.Radius)*scale),
                LineArcFullCircle2D circle=>Overlaps(box,(circle.Center.X-circle.Radius)*scale*rSign,(circle.Center.Y-circle.Radius)*scale,(circle.Center.X+circle.Radius)*scale*rSign,(circle.Center.Y+circle.Radius)*scale),
                _=>true
            };
        });
    }
    private static bool Overlaps(BoundingBox3D b,double x0,double y0,double x1,double y1)=>double.Max(x0,x1)>=b.Min.X&&double.Min(x0,x1)<=b.Max.X&&double.Max(y0,y1)>=b.Min.Y&&double.Min(y0,y1)<=b.Max.Y;
    private static double DistanceToSegment((double X,double Y) p,(double X,double Y) a,(double X,double Y) b){var dx=b.X-a.X;var dy=b.Y-a.Y;var d=dx*dx+dy*dy;if(d==0)return double.Sqrt((p.X-a.X)*(p.X-a.X)+(p.Y-a.Y)*(p.Y-a.Y));var t=double.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/d,0,1);var x=a.X+t*dx;var y=a.Y+t*dy;return double.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y));}
}
