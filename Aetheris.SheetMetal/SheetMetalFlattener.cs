using System.Globalization;
using System.Text;
using Aetheris.Kernel.Core.Math;

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
                bendRegions.Add(new($"flat-{bend.StableId}",bend.StableId,SheetRegionKind.CylindricalBend,[seamStart,seamEnd,shiftedEnd,shiftedStart],"exact neutral-axis cylindrical unroll"));
                var centerShift=Scale(targetPerp,allowance/2d);
                bendLines.Add(new(bend.StableId,Add(seamStart,centerShift),Add(seamEnd,centerShift),bend.Direction,bend.BendAngleRadians,bend.InsideRadius,bend.Thickness,policy.KFactor,allowance));
                evidence.Add(new(SheetEvidenceKind.Derived,"bend-allowance","angle * (inside radius + K * thickness)",allowance,null,bend.Source.FaceIds));
            }
        }

        var flatRegions=mappings.Values.OrderBy(m=>m.Region.StableId,StringComparer.Ordinal).Select(m=>new FlatRegion2D(
            $"flat-{m.Region.StableId}",m.Region.StableId,SheetRegionKind.Planar,NormalizePolygon(m.Region.Boundary3D.Select(m.Map)),"composed analytic plane-to-flat transform")).ToList();
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
            else loop=NormalizePolygon(feature.Boundary3D.Select(mapping.Map));
            if(loop.Count>=3)cuts.Add(new(feature.StableId,feature.Kind,loop,feature.OwningRegionId));
            else diagnostics.Add(new(SheetMetalDiagnosticCodes.FeatureMappingFailure,SheetMetalDiagnosticSeverity.Warning,$"Feature '{feature.StableId}' did not yield a closed 2D loop."));
        }

        if(mappings.Count<planar.Count)diagnostics.Add(new(SheetMetalDiagnosticCodes.DisconnectedGraph,SheetMetalDiagnosticSeverity.Warning,$"Flattened {mappings.Count} of {planar.Count} planar regions in the base-region bend component."));
        var planarFlat=flatRegions.Where(r=>r.Kind==SheetRegionKind.Planar).ToArray();var overlaps=FindOverlaps(planarFlat);
        if(overlaps.Count>0)diagnostics.Add(new(SheetMetalDiagnosticCodes.FlatOverlap,SheetMetalDiagnosticSeverity.Error,$"Flattened planar regions overlap: {string.Join(", ",overlaps.Select(x=>$"{x.A}/{x.B}"))}."));
        var allPoints=flatRegions.SelectMany(r=>r.Boundary).Concat(cuts.SelectMany(c=>c.Boundary)).ToArray();
        if(allPoints.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)))return Unsupported("Flat lowering produced non-finite coordinates.");
        var bounds=allPoints.Length==0?null:new FlatPatternBounds(allPoints.Min(p=>p.X),allPoints.Min(p=>p.Y),allPoints.Max(p=>p.X),allPoints.Max(p=>p.Y));
        var boundary=ConvexHull(flatRegions.SelectMany(r=>r.Boundary));
        var status=overlaps.Count>0?FlatPatternStatus.Overlapping:(mappings.Count<planar.Count||part.RecognitionStatus==SheetMetalRecognitionStatus.Partial?FlatPatternStatus.Partial:FlatPatternStatus.Valid);
        var hash=Hash(flatRegions,bendLines,cuts,policy,status);
        return new($"flat-{part.StableId}",status,flatRegions,bendLines.OrderBy(b=>b.BendId,StringComparer.Ordinal).ToArray(),cuts,mappings.Values.OrderBy(m=>m.Region.StableId,StringComparer.Ordinal).Select(m=>m.Public()).ToArray(),boundary,bounds,policy,evidence,diagnostics,hash);

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
    private static IReadOnlyList<SheetPoint2> NormalizePolygon(IEnumerable<SheetPoint2> points)=>ConvexHull(points.DistinctBy(p=>(Math.Round(p.X,9),Math.Round(p.Y,9))));

    private static IReadOnlyList<SheetPoint2> ConvexHull(IEnumerable<SheetPoint2> input)
    {
        var p=input.DistinctBy(x=>(Math.Round(x.X,9),Math.Round(x.Y,9))).OrderBy(x=>x.X).ThenBy(x=>x.Y).ToArray();if(p.Length<=2)return p;
        var lower=new List<SheetPoint2>();foreach(var x in p){while(lower.Count>=2&&Cross(lower[^2],lower[^1],x)<=1e-10)lower.RemoveAt(lower.Count-1);lower.Add(x);}var upper=new List<SheetPoint2>();foreach(var x in p.Reverse()){while(upper.Count>=2&&Cross(upper[^2],upper[^1],x)<=1e-10)upper.RemoveAt(upper.Count-1);upper.Add(x);}return lower.Take(lower.Count-1).Concat(upper.Take(upper.Count-1)).ToArray();
    }
    private static IReadOnlyList<(string A,string B)> FindOverlaps(IReadOnlyList<FlatRegion2D> regions){var result=new List<(string,string)>();for(var i=0;i<regions.Count;i++)for(var j=i+1;j<regions.Count;j++)if(PolygonsOverlap(regions[i].Boundary,regions[j].Boundary))result.Add((regions[i].SourceRegionId,regions[j].SourceRegionId));return result;}
    private static bool PolygonsOverlap(IReadOnlyList<SheetPoint2> a,IReadOnlyList<SheetPoint2>b){if(a.Count<3||b.Count<3)return false;foreach(var axis in Axes(a).Concat(Axes(b))){var aa=a.Select(p=>Dot(p,axis)).ToArray();var bb=b.Select(p=>Dot(p,axis)).ToArray();if(Math.Min(aa.Max(),bb.Max())-Math.Max(aa.Min(),bb.Min())<=1e-7)return false;}return true;}
    private static IEnumerable<SheetPoint2> Axes(IReadOnlyList<SheetPoint2> p){for(var i=0;i<p.Count;i++){var e=Sub(p[(i+1)%p.Count],p[i]);yield return Normalize(new SheetPoint2(-e.Y,e.X));}}
    private static string Hash(IReadOnlyList<FlatRegion2D> regions,IReadOnlyList<FlatBendLine>bends,IReadOnlyList<FlatCutLoop>cuts,SheetMetalFlattenPolicy policy,FlatPatternStatus status){var sb=new StringBuilder().Append(status).Append('|').Append(Q(policy.KFactor));foreach(var r in regions.OrderBy(x=>x.StableId,StringComparer.Ordinal)){sb.Append('|').Append(r.StableId);foreach(var p in r.Boundary)sb.Append('|').Append(Q(p.X)).Append(',').Append(Q(p.Y));}foreach(var b in bends.OrderBy(x=>x.BendId,StringComparer.Ordinal))sb.Append('|').Append(b.BendId).Append(':').Append(Q(b.BendAngleRadians)).Append(':').Append(Q(b.BendAllowance));foreach(var c in cuts.OrderBy(x=>x.FeatureId,StringComparer.Ordinal))sb.Append('|').Append(c.FeatureId);return SheetMetalRecognizer.StableHash(sb.ToString());static string Q(double value)=>Math.Round(value,9,MidpointRounding.AwayFromZero).ToString("R",CultureInfo.InvariantCulture);}
    private static double Cross(SheetPoint2 a,SheetPoint2 b,SheetPoint2 c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
    private static SheetPoint2 Add(SheetPoint2 a,SheetPoint2 b)=>new(a.X+b.X,a.Y+b.Y);private static SheetPoint2 Sub(SheetPoint2 a,SheetPoint2 b)=>new(a.X-b.X,a.Y-b.Y);private static SheetPoint2 Scale(SheetPoint2 a,double s)=>new(a.X*s,a.Y*s);private static double Dot(SheetPoint2 a,SheetPoint2 b)=>a.X*b.X+a.Y*b.Y;private static SheetPoint2 Normalize(SheetPoint2 a){var l=Math.Sqrt(Dot(a,a));return l<=1e-12?new(1,0):Scale(a,1/l);}private static Vector3D Normalize(Vector3D v)=>v.TryNormalize(out var n)?n:v;
}
