using System.Diagnostics;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Boundaries;

internal sealed record CompositeAxisCandidateTrace(
    string Plan,
    bool Admissible,
    double UtilityScore,
    double Conditioning,
    double EstimatedCost,
    string? RejectionReason,
    bool Selected);

internal sealed record ExactSupportCompositeResult(
    double OccupancyFraction,
    double BoundaryArea,
    IReadOnlyDictionary<string,double> BoundaryAreaByFace,
    string Method,
    int CirQueries,
    int ExactSupportQueries,
    int RayEvaluations,
    int AdaptiveSubdivisions,
    double EstimatedAbsoluteVolumeError,
    double EstimatedAbsoluteAreaError,
    bool UsedJudgmentEngine,
    IReadOnlyList<CompositeAxisCandidateTrace> AxisCandidates,
    double PlanningMilliseconds,
    double IntegrationMilliseconds,
    double RuntimeMilliseconds);

/// <summary>
/// Integrates a fixed lattice cell as a two-dimensional local composite domain. Exact support rays
/// partition each line into CIR-classified occupied intervals. Boundary area uses the same roots and
/// assigns each differential to its best-conditioned Cartesian projection exactly once.
/// </summary>
internal static class ExactSupportCompositeIntegrator
{
    private enum Axis { X, Y, Z }
    private readonly record struct AxisFrame(Axis Axis,Point3D Origin,Vector3D Direction,Vector3D U,Vector3D V,double ULength,double VLength,double RayLength);
    private sealed record AxisContext(IReadOnlyDictionary<Axis,double> Conditioning,IReadOnlyDictionary<Axis,double> Costs);
    private sealed class Counters { public int Cir,Exact,Rays,Subdivisions;public double VolumeError,AreaError; }
    private sealed record RayValue(double OccupiedLength,IReadOnlyDictionary<FaceId,double> AreaByFace,int RootCount);
    private sealed record IntegralValue(double Volume,IReadOnlyDictionary<FaceId,double> AreaByFace,double VolumeError,double AreaError);

    public static bool TryIntegrate(BoundingBox3D cell,IContinuumRegion region,WholeShellBoundaryQuery shell,
        IReadOnlyList<WholeShellBoundaryCandidate> faces,bool useUtilityScoring,out ExactSupportCompositeResult? result,out string rejection)
    {
        var start=Stopwatch.GetTimestamp();result=null;rejection=string.Empty;
        try
        {
            var frames=new[]{Frame(cell,Axis.X),Frame(cell,Axis.Y),Frame(cell,Axis.Z)};
            var conditioning=frames.ToDictionary(f=>f.Axis,f=>Conditioning(f,shell,faces,cell));
            var costs=frames.ToDictionary(f=>f.Axis,f=>faces.Count*(1d+(1d-conditioning[f.Axis])));
            var usedJudgment=useUtilityScoring&&faces.Count>1;Axis selected;
            var context=new AxisContext(conditioning,costs);var candidates=frames.Select((frame,index)=>new JudgmentCandidate<AxisContext>(
                $"composite-rays-{frame.Axis.ToString().ToLowerInvariant()}",c=>c.Conditioning[frame.Axis]>1e-5d,
                c=>(100d*c.Conditioning[frame.Axis])-(2d*c.Costs[frame.Axis]),
                c=>$"conditioning {c.Conditioning[frame.Axis]:R} is tangent/degenerate",index)).ToArray();
            JudgmentResult<AxisContext>? judgment=null;
            if(usedJudgment){judgment=new JudgmentEngine<AxisContext>().Evaluate(context,candidates);if(!judgment.Value.IsSuccess){rejection="No admissible exact-support projection axis.";return false;}selected=Parse(judgment.Value.Selection!.Value.Candidate.Name);}
            else if(!useUtilityScoring)selected=frames.Select(f=>f.Axis).First(axis=>conditioning[axis]>1e-5d);
            else selected=conditioning.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key).First().Key;
            var traces=candidates.Select(c=>
            {var axis=Parse(c.Name);var admitted=c.IsAdmissible(context);var score=admitted?c.Score(context):double.NegativeInfinity;return new CompositeAxisCandidateTrace(c.Name,admitted,score,conditioning[axis],costs[axis],admitted?null:c.RejectionReason?.Invoke(context),axis==selected);}).ToArray();

            var planningMilliseconds=Stopwatch.GetElapsedTime(start).TotalMilliseconds;var integrationStart=Stopwatch.GetTimestamp();
            var counters=new Counters();var volumeFrame=frames.Single(f=>f.Axis==selected);
            var volumePass=IntegrateAxis(cell,region,shell,faces,volumeFrame,includeVolume:true,counters);
            var areaByFace=new Dictionary<FaceId,double>();Add(areaByFace,volumePass.AreaByFace,1d);
            var areaError=volumePass.AreaError;
            foreach(var frame in frames.Where(f=>f.Axis!=selected))
            {var pass=IntegrateAxis(cell,region,shell,faces,frame,includeVolume:false,counters);Add(areaByFace,pass.AreaByFace,1d);areaError+=pass.AreaError;}
            var cellVolume=Volume(cell);var byFace=areaByFace.OrderBy(x=>x.Key.Value).ToDictionary(x=>x.Key.Value.ToString(),x=>x.Value,StringComparer.Ordinal);
            result=new(double.Clamp(volumePass.Volume/cellVolume,0d,1d),byFace.Values.Sum(),byFace,
                $"exact-support-adaptive-composite-rays-{selected.ToString().ToLowerInvariant()}",counters.Cir,counters.Exact,counters.Rays,counters.Subdivisions,
                volumePass.VolumeError,areaError,usedJudgment,traces,planningMilliseconds,Stopwatch.GetElapsedTime(integrationStart).TotalMilliseconds,Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return true;
        }
        catch(Exception ex) when(ex is InvalidOperationException or NotSupportedException or ArgumentException)
        {rejection=ex.Message;result=null;return false;}
    }

    private static IntegralValue IntegrateAxis(BoundingBox3D cell,IContinuumRegion region,WholeShellBoundaryQuery shell,
        IReadOnlyList<WholeShellBoundaryCandidate> faces,AxisFrame frame,bool includeVolume,Counters counters)
    {
        var curvedContact=faces.Any(f=>f.SupportKind==SurfaceGeometryKind.Torus)||faces.Count>=3;
        var resolution=includeVolume?(curvedContact?12:10):12;
        if(resolution>(includeVolume?10:8))counters.Subdivisions++;
        var fine=StructuredMidpoint(cell,region,shell,faces,frame,resolution,includeVolume,counters);
        // The exact line partition removes the dominant normal-direction error. This conservative
        // footprint estimate is used for reporting/plan utility; refinement policy can promote the
        // structured resolution without subdividing the lattice cell.
        var cellVolume=Volume(cell);var volumeError=includeVolume?cellVolume/(resolution*resolution*4d):0d;
        var projectedArea=frame.ULength*frame.VLength;var areaError=projectedArea/(resolution*resolution*2d);
        counters.VolumeError+=volumeError;counters.AreaError+=areaError;return fine with{VolumeError=volumeError,AreaError=areaError};
    }

    private static IntegralValue StructuredMidpoint(BoundingBox3D cell,IContinuumRegion region,WholeShellBoundaryQuery shell,
        IReadOnlyList<WholeShellBoundaryCandidate> faces,AxisFrame frame,int resolution,bool includeVolume,Counters counters)
    {
        var volume=0d;var area=new Dictionary<FaceId,double>();var du=frame.ULength/resolution;var dv=frame.VLength/resolution;var weight=du*dv;
        for(var j=0;j<resolution;j++)for(var i=0;i<resolution;i++)
        {
            var sample=Sample(cell,region,shell,faces,frame,(i+.5d)*du,(j+.5d)*dv,includeVolume,counters);
            if(includeVolume)volume+=sample.OccupiedLength*weight;Add(area,sample.AreaByFace,weight);
        }
        return new(volume,area,0d,0d);
    }

    private static RayValue Sample(BoundingBox3D cell,IContinuumRegion region,WholeShellBoundaryQuery shell,
        IReadOnlyList<WholeShellBoundaryCandidate> faces,AxisFrame frame,double u,double v,bool needOccupancy,Counters counters)
    {
        counters.Rays++;var origin=frame.Origin+(frame.U*u)+(frame.V*v);var scale=double.Max(1d,(region.Bounds.Max-region.Bounds.Min).Length);var tolerance=scale*5e-7d;
        var hits=new List<ExactSupportRayHit>();
        foreach(var face in faces)
        {
            var roots=ExactSupportRayIntersections.Intersect(shell,face,origin,frame.Direction,0d,frame.RayLength);
            foreach(var hit in roots)
            {
                counters.Exact++;
                if(!Contains(face.Bounds,hit.Point,tolerance)||!ConeBranch(shell,face,hit.Point,tolerance))continue;
                counters.Cir++;if(region.Classify(hit.Point,tolerance)!=ContinuumPointClassification.Boundary)continue;
                if(hits.Any(x=>double.Abs(x.Parameter-hit.Parameter)<=frame.RayLength*2e-8d))continue;
                hits.Add(hit);
            }
        }
        hits.Sort((a,b)=>a.Parameter.CompareTo(b.Parameter));var supplemental=new List<double>();
        if(needOccupancy&&hits.Count==0)
        {
            // Conservative face bounds can omit a trim-owner whose support lies exactly on a coarse
            // cell edge. Recover only that missing partition with a bounded one-dimensional CIR scan;
            // the production domain remains structured rays, not volumetric MSAA.
            const int scan=16;var previous=region.Classify(origin,tolerance)!=ContinuumPointClassification.Outside;counters.Cir++;
            for(var i=1;i<=scan;i++)
            {
                var t=frame.RayLength*i/scan;var current=region.Classify(origin+(frame.Direction*t),tolerance)!=ContinuumPointClassification.Outside;counters.Cir++;
                if(current!=previous)
                {
                    var lo=frame.RayLength*(i-1d)/scan;var hi=t;var left=previous;
                    for(var iteration=0;iteration<28;iteration++){var mid=.5d*(lo+hi);var state=region.Classify(origin+(frame.Direction*mid),tolerance)!=ContinuumPointClassification.Outside;counters.Cir++;if(state==left)lo=mid;else hi=mid;}
                    supplemental.Add(.5d*(lo+hi));
                }
                previous=current;
            }
        }
        var occupied=0d;
        if(needOccupancy)
        {var nodes=new[]{0d}.Concat(hits.Select(x=>x.Parameter)).Concat(supplemental).Concat(new[]{frame.RayLength}).Order().ToArray();for(var i=0;i<nodes.Length-1;i++){if(nodes[i+1]-nodes[i]<=1e-12d)continue;var middle=.5d*(nodes[i]+nodes[i+1]);counters.Cir++;if(region.Classify(origin+(frame.Direction*middle),tolerance)!=ContinuumPointClassification.Outside)occupied+=nodes[i+1]-nodes[i];}}
        var area=new Dictionary<FaceId,double>();foreach(var hit in hits)
        {
            var normal=hit.SupportNormal;if(!normal.TryNormalize(out normal))continue;var components=new[]{double.Abs(normal.X),double.Abs(normal.Y),double.Abs(normal.Z)};var owner=(Axis)Array.IndexOf(components,components.Max());
            if(owner!=frame.Axis)continue;var projection=double.Abs(normal.Dot(frame.Direction));if(projection<=1e-10d)continue;area[hit.FaceId]=area.GetValueOrDefault(hit.FaceId)+(1d/projection);
        }
        return new(occupied,area,hits.Count+supplemental.Count);
    }

    private static bool ConeBranch(WholeShellBoundaryQuery shell,WholeShellBoundaryCandidate face,Point3D world,double tolerance)
    {if(face.SupportKind!=SurfaceGeometryKind.Cone)return true;var cone=shell.Body.GetFaceSurface(face.FaceId).Cone!.Value;return cone.AxialParameterFromPoint(shell.Transform.Inverse().Apply(world))>=-tolerance;}
    private static double Conditioning(AxisFrame frame,WholeShellBoundaryQuery shell,IReadOnlyList<WholeShellBoundaryCandidate> faces,BoundingBox3D cell)
    {var values=new List<double>();var center=new Point3D((cell.Min.X+cell.Max.X)*.5d,(cell.Min.Y+cell.Max.Y)*.5d,(cell.Min.Z+cell.Max.Z)*.5d);foreach(var face in faces){var point=ExactSupportBoundaryQuery.ProjectToSupport(shell.Body,face.FaceId,center,shell.Transform);var normal=ExactSupportBoundaryQuery.ExactSupportNormal(shell.Body,face.FaceId,point,shell.Transform);values.Add(double.Abs(normal.Dot(frame.Direction)));}return values.Count==0?0d:values.Average();}
    private static AxisFrame Frame(BoundingBox3D b,Axis axis)=>axis switch
    {Axis.X=>new(axis,new(b.Min.X,b.Min.Y,b.Min.Z),new(1,0,0),new(0,1,0),new(0,0,1),b.Max.Y-b.Min.Y,b.Max.Z-b.Min.Z,b.Max.X-b.Min.X),Axis.Y=>new(axis,new(b.Min.X,b.Min.Y,b.Min.Z),new(0,1,0),new(1,0,0),new(0,0,1),b.Max.X-b.Min.X,b.Max.Z-b.Min.Z,b.Max.Y-b.Min.Y),_=>new(axis,new(b.Min.X,b.Min.Y,b.Min.Z),new(0,0,1),new(1,0,0),new(0,1,0),b.Max.X-b.Min.X,b.Max.Y-b.Min.Y,b.Max.Z-b.Min.Z)};
    private static Axis Parse(string name)=>name.EndsWith("-x",StringComparison.Ordinal)?Axis.X:name.EndsWith("-y",StringComparison.Ordinal)?Axis.Y:Axis.Z;
    private static bool Contains(BoundingBox3D b,Point3D p,double t)=>p.X>=b.Min.X-t&&p.X<=b.Max.X+t&&p.Y>=b.Min.Y-t&&p.Y<=b.Max.Y+t&&p.Z>=b.Min.Z-t&&p.Z<=b.Max.Z+t;
    private static double Volume(BoundingBox3D b)=>(b.Max.X-b.Min.X)*(b.Max.Y-b.Min.Y)*(b.Max.Z-b.Min.Z);
    private static void Add(Dictionary<FaceId,double> target,IReadOnlyDictionary<FaceId,double> source,double factor){foreach(var pair in source)target[pair.Key]=target.GetValueOrDefault(pair.Key)+(pair.Value*factor);}
}
