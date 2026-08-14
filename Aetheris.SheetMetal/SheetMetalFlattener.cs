using System.Globalization;
using System.Text;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.SheetMetal;

/// <summary>Deterministic analytic unfolding for planar regions connected by cylindrical bends.</summary>
public static class SheetMetalFlattener
{
    private sealed record Mapping(
        SheetRegionIr Region,
        SheetPoint2 FlatOrigin,
        SheetPoint2 FlatU,
        SheetPoint2 FlatV)
    {
        public SheetPoint2 Map(Point3D point)
        {
            var plane=Region.Plane!;var d=point-plane.Origin;
            return Add(FlatOrigin,Add(Scale(FlatU,d.Dot(plane.UAxis)),Scale(FlatV,d.Dot(plane.VAxis))));
        }
        public SourceToFlatMapping Public()=>new(Region.StableId,Region.Plane!.Origin,Region.Plane.UAxis,Region.Plane.VAxis,FlatOrigin,FlatU,FlatV);
    }

    public static SheetMetalFlatPatternIr Flatten(SheetMetalPartIr part, SheetMetalFlattenPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(part);policy??=part.FlatPatternPolicy;
        var diagnostics=new List<SheetMetalDiagnostic>();var evidence=new List<SheetEvidence>();
        var planar=part.Regions.Where(r=>r.Kind==SheetRegionKind.Planar&&r.Plane is not null).ToDictionary(r=>r.StableId,StringComparer.Ordinal);
        if(!planar.TryGetValue(part.BaseRegionId,out var baseRegion))return Unsupported("The selected base region is not a planar reference region.");

        var mappings=new Dictionary<string,Mapping>(StringComparer.Ordinal);
        var baseMap=BaseMapping(baseRegion);mappings[baseRegion.StableId]=baseMap;
        var pending=new Queue<string>();pending.Enqueue(baseRegion.StableId);
        var bendRegions=new List<FlatRegion2D>();var bendLines=new List<FlatBendLine>();
        while(pending.Count>0)
        {
            var currentId=pending.Dequeue();var current=mappings[currentId];
            foreach(var bend in part.Bends.Where(b=>b.AdjacentRegionA==currentId||b.AdjacentRegionB==currentId).OrderBy(b=>b.StableId,StringComparer.Ordinal))
            {
                var neighborId=bend.AdjacentRegionA==currentId?bend.AdjacentRegionB:bend.AdjacentRegionA;
                if(mappings.ContainsKey(neighborId)||!planar.TryGetValue(neighborId,out var neighbor))continue;
                var allowance=policy.BendAllowance(bend.BendAngleRadians,bend.InsideRadius,bend.Thickness);
                var axis=Normalize(bend.AxisDirection);var half=Math.Max(FindBendAxisLength(part,bend),1e-6)/2d;
                var sourceStart=bend.AxisOrigin-axis*half;var sourceEnd=bend.AxisOrigin+axis*half;
                var seamStart=current.Map(ProjectToPlane(sourceStart,current.Region.Plane!));var seamEnd=current.Map(ProjectToPlane(sourceEnd,current.Region.Plane!));
                var flatAxis=Normalize(Sub(seamEnd,seamStart));var perp=new SheetPoint2(-flatAxis.Y,flatAxis.X);
                var seamMid=Scale(Add(seamStart,seamEnd),.5d);var currentCentroid=current.Map(Centroid(current.Region.Boundary3D));
                var currentSide=Dot(Sub(currentCentroid,seamMid),perp)>=0?1d:-1d;var targetPerp=Scale(perp,-currentSide);
                var neighborPlane=neighbor.Plane!;var neighborCentroid=Centroid(neighbor.Boundary3D);var neighborAxisPoint=ProjectToPlane(bend.AxisOrigin,neighborPlane);
                var neighborPerp3=neighborPlane.Normal.Cross(axis);if(!neighborPerp3.TryNormalize(out neighborPerp3))neighborPerp3=neighborPlane.VAxis;
                if((neighborCentroid-neighborAxisPoint).Dot(neighborPerp3)<0)neighborPerp3=-neighborPerp3;
                var targetAxis=flatAxis;if(axis.Dot(neighborPlane.UAxis)*Dot(targetAxis,flatAxis)<0)targetAxis=Scale(flatAxis,-1);
                var targetSeamMid=Add(seamMid,Scale(targetPerp,allowance));
                var neighborMap=MappingFromAxis(neighbor,axis,neighborPerp3,targetAxis,targetPerp,neighborAxisPoint,targetSeamMid);
                mappings[neighborId]=neighborMap;pending.Enqueue(neighborId);

                var shiftedStart=Add(seamStart,Scale(targetPerp,allowance));var shiftedEnd=Add(seamEnd,Scale(targetPerp,allowance));
                var bendBoundary = new SheetPoint2[] { seamStart,seamEnd,shiftedEnd,shiftedStart };
                bendRegions.Add(new($"flat-{bend.StableId}",bend.StableId,SheetRegionKind.CylindricalBend,bendBoundary,"exact neutral-axis cylindrical unroll",Contour($"flat-{bend.StableId}",bendBoundary,"cylindrical neutral-axis unroll")));
                var centerShift=Scale(targetPerp,allowance/2d);
                bendLines.Add(new(bend.StableId,Add(seamStart,centerShift),Add(seamEnd,centerShift),bend.Direction,bend.BendAngleRadians,bend.InsideRadius,bend.Thickness,policy.KFactor,allowance));
                evidence.Add(new(SheetEvidenceKind.Derived,"bend-allowance","angle * (inside radius + K * thickness)",allowance,null,bend.Source.FaceIds));
            }
        }

        var flatRegions=mappings.Values.OrderBy(m=>m.Region.StableId,StringComparer.Ordinal).Select(m=>
        {
            var boundary=NormalizeSourcePolygon(m.Region.Boundary3D.Select(m.Map));
            return new FlatRegion2D($"flat-{m.Region.StableId}",m.Region.StableId,SheetRegionKind.Planar,boundary,"exact ordered source-edge vertices through composed analytic plane-to-flat transform",Contour($"flat-{m.Region.StableId}",boundary,"analytic plane-to-flat transform"));
        }).ToList();
        flatRegions.AddRange(bendRegions.OrderBy(r=>r.StableId,StringComparer.Ordinal));
        var cuts=new List<FlatCutLoop>();
        foreach(var feature in part.Features.OrderBy(f=>f.StableId,StringComparer.Ordinal))
        {
            if(!mappings.TryGetValue(feature.OwningRegionId,out var mapping)){diagnostics.Add(new(SheetMetalDiagnosticCodes.FeatureMappingFailure,SheetMetalDiagnosticSeverity.Warning,$"Feature '{feature.StableId}' belongs to an unflattened region."));continue;}
            IReadOnlyList<SheetPoint2> loop;
            if(feature.Kind==SheetFeatureKind.CircularHole&&feature.Diameter is { } diameter)
            {
                var c=mapping.Map(feature.Center);loop=Enumerable.Range(0,48).Select(i=>{var a=2*Math.PI*i/48d;return new SheetPoint2(c.X+diameter/2d*Math.Cos(a),c.Y+diameter/2d*Math.Sin(a));}).ToArray();
            }
            else loop=NormalizeSourcePolygon(feature.Boundary3D.Select(mapping.Map));
            if(loop.Count>=3)
            {
                var exact=feature.Kind==SheetFeatureKind.CircularHole&&feature.Diameter is { } exactDiameter
                    ? CircleContour(feature.StableId,mapping.Map(feature.Center),exactDiameter/2d)
                    : Contour(feature.StableId,loop,"mapped exact feature boundary");
                cuts.Add(new(feature.StableId,feature.Kind,loop,feature.OwningRegionId,exact));
            }
            else diagnostics.Add(new(SheetMetalDiagnosticCodes.FeatureMappingFailure,SheetMetalDiagnosticSeverity.Warning,$"Feature '{feature.StableId}' did not yield a closed 2D loop."));
        }
        var flatReliefs=BuildReliefs(part,mappings);

        if(mappings.Count<planar.Count)diagnostics.Add(new(SheetMetalDiagnosticCodes.DisconnectedGraph,SheetMetalDiagnosticSeverity.Warning,$"Flattened {mappings.Count} of {planar.Count} planar regions in the base-region bend component."));
        var planarFlat=flatRegions.Where(r=>r.Kind==SheetRegionKind.Planar).ToArray();var overlaps=FindOverlaps(planarFlat);
        if(overlaps.Count>0)diagnostics.Add(new(SheetMetalDiagnosticCodes.FlatOverlap,SheetMetalDiagnosticSeverity.Error,$"Flattened planar regions overlap: {string.Join(", ",overlaps.Select(x=>$"{x.A}/{x.B}"))}."));
        var allPoints=flatRegions.SelectMany(r=>r.Boundary).Concat(cuts.SelectMany(c=>c.Boundary)).ToArray();
        if(allPoints.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)))return Unsupported("Flat lowering produced non-finite coordinates.");
        var bounds=allPoints.Length==0?null:new FlatPatternBounds(allPoints.Min(p=>p.X),allPoints.Min(p=>p.Y),allPoints.Max(p=>p.X),allPoints.Max(p=>p.Y));
        var authored=part.Provenance.Contains("source-independent",StringComparison.OrdinalIgnoreCase);
        var exactBlank=authored?BuildExactBlank(part,flatRegions,flatReliefs,diagnostics):null;
        var boundary=exactBlank is not null?ContourVertices(exactBlank):authored?StitchBoundary(flatRegions):ConvexHull(flatRegions.SelectMany(r=>r.Boundary));
        if(authored&&exactBlank is null)diagnostics.Add(new(SheetMetalDiagnosticCodes.ExactBlankContour,SheetMetalDiagnosticSeverity.Warning,"Authored flat material regions contain a point-touch/open-corner topology that the exact single-loop contract rejects; compatibility boundary remains available."));
        if(exactBlank is not null)
        {
            var contourValidation=PlanarContourKernel.Validate(exactBlank);
            diagnostics.AddRange(contourValidation.Diagnostics.Where(x=>x.Severity==PlanarContourDiagnosticSeverity.Error).Select(x=>new SheetMetalDiagnostic(SheetMetalDiagnosticCodes.ImpossibleTopology,SheetMetalDiagnosticSeverity.Error,$"Exact blank contour: {x.Code}: {x.Message}")));
        }
        var status=overlaps.Count>0?FlatPatternStatus.Overlapping:(mappings.Count<planar.Count||part.RecognitionStatus==SheetMetalRecognitionStatus.Partial?FlatPatternStatus.Partial:FlatPatternStatus.Valid);
        var hash=Hash(flatRegions,bendLines,cuts,flatReliefs,policy,status);
        return new($"flat-{part.StableId}",status,flatRegions,bendLines.OrderBy(b=>b.BendId,StringComparer.Ordinal).ToArray(),cuts,mappings.Values.OrderBy(m=>m.Region.StableId,StringComparer.Ordinal).Select(m=>m.Public()).ToArray(),boundary,bounds,policy,evidence,diagnostics,hash,exactBlank,flatReliefs);

        SheetMetalFlatPatternIr Unsupported(string message)=>new($"flat-{part.StableId}",FlatPatternStatus.Unsupported,[],[],[],[],[],null,policy,[],[new(SheetMetalDiagnosticCodes.UnsupportedBendTopology,SheetMetalDiagnosticSeverity.Error,message)],SheetMetalRecognizer.StableHash(message));
    }

    private static Mapping BaseMapping(SheetRegionIr region)
    {
        var p=region.Plane!;var raw=region.Boundary3D.Select(x=>new SheetPoint2((x-p.Origin).Dot(p.UAxis),(x-p.Origin).Dot(p.VAxis))).ToArray();
        var minX=raw.Length==0?0:raw.Min(x=>x.X);var minY=raw.Length==0?0:raw.Min(x=>x.Y);
        return new(region,new(-minX,-minY),new(1,0),new(0,1));
    }

    private static Mapping MappingFromAxis(SheetRegionIr region,Vector3D sourceAxis,Vector3D sourcePerp,SheetPoint2 flatAxis,SheetPoint2 flatPerp,Point3D anchor,SheetPoint2 target)
    {
        var p=region.Plane!;var flatU=Add(Scale(flatAxis,p.UAxis.Dot(sourceAxis)),Scale(flatPerp,p.UAxis.Dot(sourcePerp)));
        var flatV=Add(Scale(flatAxis,p.VAxis.Dot(sourceAxis)),Scale(flatPerp,p.VAxis.Dot(sourcePerp)));
        var d=anchor-p.Origin;var contribution=Add(Scale(flatU,d.Dot(p.UAxis)),Scale(flatV,d.Dot(p.VAxis)));
        return new(region,Sub(target,contribution),flatU,flatV);
    }

    private static double FindBendAxisLength(SheetMetalPartIr part,SheetBendIr bend)=>part.Regions
        .Where(r=>r.Cylinder is not null)
        .OrderBy(r=>(r.Cylinder!.AxisOrigin-bend.AxisOrigin).Length+Math.Abs(r.Cylinder.InsideRadius-bend.InsideRadius)+(1d-Math.Abs(r.Cylinder.AxisDirection.Dot(bend.AxisDirection)))*1000d)
        .FirstOrDefault()?.Cylinder?.AxisLength??0d;
    private static Point3D ProjectToPlane(Point3D point,SheetPlaneReference plane)=>point-plane.Normal*((point-plane.Origin).Dot(plane.Normal));
    private static Point3D Centroid(IReadOnlyList<Point3D> points)=>points.Count==0?Point3D.Origin:new(points.Average(p=>p.X),points.Average(p=>p.Y),points.Average(p=>p.Z));
    public static IReadOnlyList<SheetPoint2> NormalizeSourcePolygon(IEnumerable<SheetPoint2> points)
    {
        var ordered=points.DistinctBy(p=>(Math.Round(p.X,9),Math.Round(p.Y,9))).ToArray();
        if(ordered.Length<3)return ordered;
        if(Math.Abs(SignedArea(ordered))>1e-10&&!SelfIntersects(ordered))return ordered;
        return ConvexHull(ordered);
    }

    private static double SignedArea(IReadOnlyList<SheetPoint2> p){var sum=0d;for(var i=0;i<p.Count;i++){var q=p[(i+1)%p.Count];sum+=p[i].X*q.Y-q.X*p[i].Y;}return sum/2d;}
    private static bool SelfIntersects(IReadOnlyList<SheetPoint2> p)
    {
        for(var i=0;i<p.Count;i++)for(var j=i+1;j<p.Count;j++)
        {
            if(j==i||j==(i+1)%p.Count||i==(j+1)%p.Count)continue;
            if(Intersects(p[i],p[(i+1)%p.Count],p[j],p[(j+1)%p.Count]))return true;
        }
        return false;
        static bool Intersects(SheetPoint2 a,SheetPoint2 b,SheetPoint2 c,SheetPoint2 d)
        {
            var ab1=Cross(a,b,c);var ab2=Cross(a,b,d);var cd1=Cross(c,d,a);var cd2=Cross(c,d,b);
            return Math.Sign(ab1)!=Math.Sign(ab2)&&Math.Sign(cd1)!=Math.Sign(cd2);
        }
    }

    private static IReadOnlyList<SheetPoint2> ConvexHull(IEnumerable<SheetPoint2> input)
    {
        var p=input.DistinctBy(x=>(Math.Round(x.X,9),Math.Round(x.Y,9))).OrderBy(x=>x.X).ThenBy(x=>x.Y).ToArray();if(p.Length<=2)return p;
        var lower=new List<SheetPoint2>();foreach(var x in p){while(lower.Count>=2&&Cross(lower[^2],lower[^1],x)<=1e-10)lower.RemoveAt(lower.Count-1);lower.Add(x);}var upper=new List<SheetPoint2>();foreach(var x in p.Reverse()){while(upper.Count>=2&&Cross(upper[^2],upper[^1],x)<=1e-10)upper.RemoveAt(upper.Count-1);upper.Add(x);}return lower.Take(lower.Count-1).Concat(upper.Take(upper.Count-1)).ToArray();
    }
    private static PlanarContour2 Contour(string id,IReadOnlyList<SheetPoint2> boundary,string provenance)=>PlanarContourKernel.FromPolygon(id,"XY",boundary.Select(p=>(p.X,p.Y)).ToArray(),$"SheetMetalFlatPatternIr:{provenance}");
    private static PlanarContour2 CircleContour(string id,SheetPoint2 center,double radius)
    {
        var c=(center.X,center.Y);var provenance=new ProfileSegmentProvenance($"{id}.circle",id,id,"SheetMetalFlatPatternIr:analytic circle","XY");
        return new(id,"XY",new($"{id}.outer",true,[new($"{id}.arc0",new LineArcCircularArc2D(c,radius,0,Math.PI),provenance),new($"{id}.arc1",new LineArcCircularArc2D(c,radius,Math.PI,Math.PI),provenance with { StableId=$"{id}.circle.1" })]),[],$"SheetMetalFlatPatternIr:analytic circle radius={radius:R}");
    }
    private static PlanarContour2? BuildExactBlank(SheetMetalPartIr part,IReadOnlyList<FlatRegion2D> regions,IReadOnlyList<FlatReliefLoop> reliefs,ICollection<SheetMetalDiagnostic> diagnostics)
    {
        var usable=regions.Where(x=>x.ExactContour is not null).OrderBy(x=>x.StableId,StringComparer.Ordinal).ToArray();
        if(usable.Length!=regions.Count)return null;
        var profiles=usable.ToDictionary(x=>x.StableId,x=>PlanarContourKernel.ToResolvedProfile(x.ExactContour!,x.StableId),StringComparer.Ordinal);
        foreach(var relief in reliefs)profiles[relief.ReliefId]=PlanarContourKernel.ToResolvedProfile(relief.ExactContour,relief.ReliefId);
        var operations=usable.Select((x,index)=>new PrismaticProfileOperation(x.StableId,index==0?PrismaticProfileIntent.Base:PrismaticProfileIntent.Add,x.StableId,0,1,"SheetMetalBlankRegion",x.SourceRegionId))
            .Concat(reliefs.Select(x=>new PrismaticProfileOperation(x.ReliefId,PrismaticProfileIntent.Remove,x.ReliefId,0,1,"SheetMetalCornerRelief",x.ReliefId))).ToArray();
        var composed=ProfileArrangementBuilder.Compose("XY",operations,profiles,$"sheetmetal-blank:{part.StableId}");
        if(composed.Region is null)
        {
            foreach(var message in composed.Arrangement.Diagnostics)diagnostics.Add(new(SheetMetalDiagnosticCodes.ExactBlankContour,SheetMetalDiagnosticSeverity.Warning,$"Exact blank composition: {message}"));
            return null;
        }
        var contour=PlanarContourKernel.FromResolvedProfile(composed.Region.Outer,$"SheetMetal exact material composition:{part.StableId}") with { StableId=$"flat-{part.StableId}.blank" };
        var validation=PlanarContourKernel.Validate(contour);
        if(validation.IsValid)return contour;
        foreach(var item in validation.Diagnostics)diagnostics.Add(new(SheetMetalDiagnosticCodes.ExactBlankContour,SheetMetalDiagnosticSeverity.Warning,$"Exact blank composition: {item.Code}: {item.Message}"));
        return null;
    }
    private static IReadOnlyList<SheetPoint2> ContourVertices(PlanarContour2 contour)=>contour.OuterLoop.Segments.Select(segment=>segment.Geometry switch
    {
        LineArcLineSegment2D line=>new SheetPoint2(line.Start.X,line.Start.Y),
        LineArcCircularArc2D arc=>new SheetPoint2(arc.Center.X+arc.Radius*Math.Cos(arc.StartAngleRadians),arc.Center.Y+arc.Radius*Math.Sin(arc.StartAngleRadians)),
        LineArcFullCircle2D circle=>new SheetPoint2(circle.Center.X+circle.Radius,circle.Center.Y),
        _=>default
    }).ToArray();
    private static IReadOnlyList<FlatReliefLoop> BuildReliefs(SheetMetalPartIr part,IReadOnlyDictionary<string,Mapping> mappings)
    {
        if(!mappings.TryGetValue(part.BaseRegionId,out var mapping))return [];
        var basePoints=mapping.Region.Boundary3D.Select(mapping.Map).ToArray();if(basePoints.Length<3)return [];
        var minX=basePoints.Min(p=>p.X);var maxX=basePoints.Max(p=>p.X);var minY=basePoints.Min(p=>p.Y);var maxY=basePoints.Max(p=>p.Y);var result=new List<FlatReliefLoop>();
        foreach(var corner in part.Corners??[])
        {
            var relief=(part.Reliefs??[]).FirstOrDefault(x=>x.CornerId==corner.StableId);if(relief is null)continue;
            var tokens=corner.VertexName.Split('-',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);var right=tokens.Contains("Right",StringComparer.OrdinalIgnoreCase);var left=tokens.Contains("Left",StringComparer.OrdinalIgnoreCase);var front=tokens.Contains("Front",StringComparer.OrdinalIgnoreCase);var rear=tokens.Contains("Rear",StringComparer.OrdinalIgnoreCase);
            var c=new SheetPoint2(right?maxX:left?minX:(minX+maxX)/2,front?minY:rear?maxY:(minY+maxY)/2);var ix=right?-1d:1d;var iy=front?1d:-1d;
            PlanarContour2 contour;IReadOnlyList<SheetPoint2> sample;
            if(relief.Kind==SheetReliefKind.Round)
            {
                var radius=relief.Radius??relief.Width/2d;var inv=1/Math.Sqrt(2);var dir=(X:ix*inv,Y:iy*inv);var perp=(X:-dir.Y,Y:dir.X);var start=(X:c.X-dir.X*radius,Y:c.Y-dir.Y*radius);var end=(X:c.X+dir.X*Math.Max(radius,relief.Depth-radius),Y:c.Y+dir.Y*Math.Max(radius,relief.Depth-radius));
                var p0=(X:start.X+perp.X*radius,Y:start.Y+perp.Y*radius);var p1=(X:end.X+perp.X*radius,Y:end.Y+perp.Y*radius);var p2=(X:end.X-perp.X*radius,Y:end.Y-perp.Y*radius);var p3=(X:start.X-perp.X*radius,Y:start.Y-perp.Y*radius);var plus=Math.Atan2(perp.Y,perp.X);var minus=Math.Atan2(-perp.Y,-perp.X);var provenance=new ProfileSegmentProvenance($"{relief.StableId}.round",relief.StableId,corner.StableId,"exact round-ended bend relief","XY");
                contour=new(relief.StableId,"XY",new($"{relief.StableId}.outer",true,[new($"{relief.StableId}.side0",new LineArcLineSegment2D(p3,p2),provenance),new($"{relief.StableId}.end",new LineArcCircularArc2D(end,radius,minus,Math.PI),provenance with { StableId=$"{relief.StableId}.end" }),new($"{relief.StableId}.side1",new LineArcLineSegment2D(p1,p0),provenance with { StableId=$"{relief.StableId}.side1" }),new($"{relief.StableId}.start",new LineArcCircularArc2D(start,radius,plus,Math.PI),provenance with { StableId=$"{relief.StableId}.start" })]),[],$"{corner.StableId}: round relief width={relief.Width:R} depth={relief.Depth:R}");
                sample=Sample(contour);
            }
            else
            {
                var half=relief.Width/2d;var inv=1/Math.Sqrt(2);var dir=(X:ix*inv,Y:iy*inv);var perp=(X:-dir.Y,Y:dir.X);var start=(X:c.X-dir.X*relief.Width*2d,Y:c.Y-dir.Y*relief.Width*2d);var end=(X:c.X+dir.X*relief.Depth,Y:c.Y+dir.Y*relief.Depth);var points=new[]{new SheetPoint2(start.X-perp.X*half,start.Y-perp.Y*half),new SheetPoint2(end.X-perp.X*half,end.Y-perp.Y*half),new SheetPoint2(end.X+perp.X*half,end.Y+perp.Y*half),new SheetPoint2(start.X+perp.X*half,start.Y+perp.Y*half)};contour=Contour(relief.StableId,points,$"{corner.StableId}: rectangular diagonal relief width={relief.Width:R} depth={relief.Depth:R}");sample=points;
            }
            result.Add(new(relief.StableId,relief.Kind,sample,part.BaseRegionId,contour,relief.Width,relief.Depth));
        }
        return result;
    }
    private static IReadOnlyList<SheetPoint2> Sample(PlanarContour2 contour)=>contour.OuterLoop.Segments.SelectMany(segment=>segment.Geometry switch
    {
        LineArcLineSegment2D line=>new[]{new SheetPoint2(line.Start.X,line.Start.Y)},
        LineArcCircularArc2D arc=>Enumerable.Range(0,12).Select(i=>{var a=arc.StartAngleRadians+arc.SweepAngleRadians*i/12d;return new SheetPoint2(arc.Center.X+arc.Radius*Math.Cos(a),arc.Center.Y+arc.Radius*Math.Sin(a));}),
        _=>[]
    }).ToArray();
    private readonly record struct PointKey(long X,long Y){public static PointKey Of(SheetPoint2 p)=>new((long)Math.Round(p.X*1e8),(long)Math.Round(p.Y*1e8));}
    private readonly record struct SegmentKey(PointKey A,PointKey B)
    {
        public static SegmentKey Of(SheetPoint2 a,SheetPoint2 b){var x=PointKey.Of(a);var y=PointKey.Of(b);return x.X<y.X||x.X==y.X&&x.Y<=y.Y?new(x,y):new(y,x);}
    }
    private static IReadOnlyList<SheetPoint2> StitchBoundary(IReadOnlyList<FlatRegion2D> regions)
    {
        var exposed=new Dictionary<SegmentKey,(SheetPoint2 A,SheetPoint2 B)>();
        foreach(var region in regions.Where(r=>r.Boundary.Count>=3))for(var i=0;i<region.Boundary.Count;i++)
        {
            var a=region.Boundary[i];var b=region.Boundary[(i+1)%region.Boundary.Count];var key=SegmentKey.Of(a,b);if(!exposed.Remove(key))exposed[key]=(a,b);
        }
        if(exposed.Count==0)return [];
        var points=new Dictionary<PointKey,SheetPoint2>();
        foreach(var edge in exposed.Values){var a=PointKey.Of(edge.A);var b=PointKey.Of(edge.B);points[a]=edge.A;points[b]=edge.B;}
        var remaining=new HashSet<SegmentKey>(exposed.Keys);var loops=new List<IReadOnlyList<SheetPoint2>>();
        while(remaining.Count>0)
        {
            var seed=remaining.OrderBy(e=>e.A.X).ThenBy(e=>e.A.Y).First();var start=seed.A;var current=start;var loop=new List<SheetPoint2>();
            for(var guard=0;guard<=exposed.Count+1;guard++)
            {
                loop.Add(points[current]);SegmentKey? found=remaining.Where(e=>e.A==current||e.B==current).Select(e=>(SegmentKey?)e).FirstOrDefault();if(found is null)break;remaining.Remove(found.Value);current=found.Value.A==current?found.Value.B:found.Value.A;if(current==start)break;
            }
            if(loop.Count>=3&&current==start)loops.Add(loop);
        }
        return loops.OrderByDescending(x=>Math.Abs(SignedArea(x))).FirstOrDefault()??[];
    }
    private static IReadOnlyList<(string A,string B)> FindOverlaps(IReadOnlyList<FlatRegion2D> regions){var result=new List<(string,string)>();for(var i=0;i<regions.Count;i++)for(var j=i+1;j<regions.Count;j++)if(PolygonsOverlap(regions[i].Boundary,regions[j].Boundary))result.Add((regions[i].SourceRegionId,regions[j].SourceRegionId));return result;}
    private static bool PolygonsOverlap(IReadOnlyList<SheetPoint2> a,IReadOnlyList<SheetPoint2>b)
    {
        if(a.Count<3||b.Count<3)return false;
        for(var i=0;i<a.Count;i++)for(var j=0;j<b.Count;j++)if(ProperCross(a[i],a[(i+1)%a.Count],b[j],b[(j+1)%b.Count]))return true;
        return InteriorProbes(a).Any(p=>PointInPolygonStrict(p,b))||InteriorProbes(b).Any(p=>PointInPolygonStrict(p,a));
        static bool ProperCross(SheetPoint2 p,SheetPoint2 q,SheetPoint2 r,SheetPoint2 s){var a1=Cross(p,q,r);var a2=Cross(p,q,s);var b1=Cross(r,s,p);var b2=Cross(r,s,q);return a1*a2< -1e-14&&b1*b2< -1e-14;}
        static bool PointInPolygonStrict(SheetPoint2 point,IReadOnlyList<SheetPoint2> polygon){var inside=false;for(var i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];if(Math.Abs(Cross(a,b,point))<1e-8&&point.X>=Math.Min(a.X,b.X)-1e-8&&point.X<=Math.Max(a.X,b.X)+1e-8&&point.Y>=Math.Min(a.Y,b.Y)-1e-8&&point.Y<=Math.Max(a.Y,b.Y)+1e-8)return false;if((a.Y>point.Y)!=(b.Y>point.Y)&&point.X<(b.X-a.X)*(point.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;}return inside;}
        static IEnumerable<SheetPoint2> InteriorProbes(IReadOnlyList<SheetPoint2> p){var sign=SignedArea(p)>=0?1d:-1d;for(var i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];var dx=b.X-a.X;var dy=b.Y-a.Y;var len=Math.Sqrt(dx*dx+dy*dy);if(len>1e-12)yield return new((a.X+b.X)/2-sign*dy/len*1e-6,(a.Y+b.Y)/2+sign*dx/len*1e-6);}}
    }
    private static string Hash(IReadOnlyList<FlatRegion2D> regions,IReadOnlyList<FlatBendLine>bends,IReadOnlyList<FlatCutLoop>cuts,IReadOnlyList<FlatReliefLoop>reliefs,SheetMetalFlattenPolicy policy,FlatPatternStatus status){var sb=new StringBuilder().Append(status).Append('|').Append(Q(policy.KFactor));foreach(var r in regions.OrderBy(x=>x.StableId,StringComparer.Ordinal)){sb.Append('|').Append(r.StableId);foreach(var p in r.Boundary)sb.Append('|').Append(Q(p.X)).Append(',').Append(Q(p.Y));}foreach(var b in bends.OrderBy(x=>x.BendId,StringComparer.Ordinal))sb.Append('|').Append(b.BendId).Append(':').Append(b.Direction).Append(':').Append(Q(b.BendAngleRadians)).Append(':').Append(Q(b.BendAllowance));foreach(var c in cuts.OrderBy(x=>x.FeatureId,StringComparer.Ordinal))sb.Append('|').Append(c.FeatureId);foreach(var r in reliefs.OrderBy(x=>x.ReliefId,StringComparer.Ordinal))sb.Append('|').Append(r.ReliefId).Append(':').Append(r.Kind).Append(':').Append(Q(r.Width)).Append(':').Append(Q(r.Depth));return SheetMetalRecognizer.StableHash(sb.ToString());static string Q(double value)=>Math.Round(value,9,MidpointRounding.AwayFromZero).ToString("R",CultureInfo.InvariantCulture);}
    private static double Cross(SheetPoint2 a,SheetPoint2 b,SheetPoint2 c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    private static SheetPoint2 Add(SheetPoint2 a,SheetPoint2 b)=>new(a.X+b.X,a.Y+b.Y);private static SheetPoint2 Sub(SheetPoint2 a,SheetPoint2 b)=>new(a.X-b.X,a.Y-b.Y);private static SheetPoint2 Scale(SheetPoint2 a,double s)=>new(a.X*s,a.Y*s);private static double Dot(SheetPoint2 a,SheetPoint2 b)=>a.X*b.X+a.Y*b.Y;private static SheetPoint2 Normalize(SheetPoint2 a){var l=Math.Sqrt(Dot(a,a));return l<=1e-12?new(1,0):Scale(a,1/l);}private static Vector3D Normalize(Vector3D v)=>v.TryNormalize(out var n)?n:v;
}
