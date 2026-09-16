using System.Numerics;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public sealed record CorrespondingRegistrationConfig(string Id,int Iterations,double ProjectionFraction,double MaximumStepMm,double ConvergenceMm);
public sealed record CorrespondingRegistrationResult(CanonicalHumanoid Humanoid,double InitialRmsMm,double FinalRmsMm,int Iterations,bool Converged,IReadOnlyList<HumanoidDiagnostic> Diagnostics);

/// <summary>Deterministic same-topology witness for registration and identity laws.</summary>
public static class HumanoidCorrespondingRegistration
{
    public static CorrespondingRegistrationResult Fit(CanonicalHumanoid source,string targetTopologyId,IReadOnlyList<Point3D> targets,CorrespondingRegistrationConfig config)
    {
        if(targetTopologyId!=source.Surface.TopologyId||targets.Count!=source.Surface.Vertices.Count)
            return new(source,double.NaN,double.NaN,0,false,[new(HumanoidDiagnosticCode.HUM015_ConnectivityChanged,HumanoidDiagnosticSeverity.Error,"Corresponding registration requires the same topology identity and vertex count.")]);
        if(config.Iterations is <1 or >100||config.ProjectionFraction is <=0 or >1||config.MaximumStepMm<=0||config.ConvergenceMm<0||targets.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||!double.IsFinite(p.Z)))
            return new(source,double.NaN,double.NaN,0,false,[new(HumanoidDiagnosticCode.HUM008_InvalidTopology,HumanoidDiagnosticSeverity.Error,"Registration target or bounded configuration is invalid.")]);
        var positions=source.Surface.Vertices.Select(v=>v.Position).ToArray();var initial=Rms(positions,targets);var used=0;
        for(var iteration=1;iteration<=config.Iterations;iteration++)
        {
            used=iteration;
            for(var i=0;i<positions.Length;i++){var delta=targets[i]-positions[i];var length=delta.Length;if(length>config.MaximumStepMm)delta*=config.MaximumStepMm/length;positions[i]+=delta*config.ProjectionFraction;}
            if(Rms(positions,targets)<=config.ConvergenceMm)break;
        }
        var vertices=source.Surface.Vertices.Select((v,i)=>v with{Position=positions[i]}).ToArray();var fitted=source with{Surface=source.Surface with{Vertices=vertices},ShapeRevision=source.ShapeRevision+1};var final=Rms(positions,targets);
        return new(fitted,initial,final,used,final<=config.ConvergenceMm,[]);
    }
    private static double Rms(IReadOnlyList<Point3D> a,IReadOnlyList<Point3D> b)=>double.Sqrt(a.Zip(b,(x,y)=>(x-y).LengthSquared).Average());
}

public sealed record PoseIntegrityEvidence(string PoseId,bool Finite,int InvertedFaces,int CollapsedFaces,double SurfaceAreaRatio,double AbsoluteVolumeRatio,IReadOnlyDictionary<string,Point3D> AttachmentPositions)
{
    public bool IsStable=>Finite&&InvertedFaces==0&&CollapsedFaces==0&&double.IsFinite(SurfaceAreaRatio)&&double.IsFinite(AbsoluteVolumeRatio);
}

public static class HumanoidPoseQualification
{
    public static PoseIntegrityEvidence Evaluate(CanonicalHumanoid humanoid,HumanoidPoseState pose)
    {
        var neutral=humanoid.Surface.Vertices.Select(v=>v.Position).ToArray();var posed=HumanoidPosing.EvaluateVertices(humanoid,pose);var finite=posed.All(Finite);var inverted=0;var collapsed=0;var neutralArea=0d;var posedArea=0d;var globals=HumanoidPosing.GlobalPose(humanoid,pose);var weights=humanoid.Surface.SkinWeights.ToDictionary(x=>x.VertexIndex);
        foreach(var face in humanoid.Surface.Faces)
        {
            var n0=(neutral[face.B]-neutral[face.A]).Cross(neutral[face.C]-neutral[face.A]);var n1=(posed[face.B]-posed[face.A]).Cross(posed[face.C]-posed[face.A]);neutralArea+=n0.Length/2;posedArea+=n1.Length/2;
            var dominant=new[]{face.A,face.B,face.C}.SelectMany(i=>weights[i].Weights).GroupBy(x=>x.JointIndex).Select(g=>(Joint:g.Key,Weight:g.Sum(x=>x.Weight))).OrderByDescending(x=>x.Weight).ThenBy(x=>x.Joint).First().Joint;var deformation=humanoid.Skeleton.Joints[dominant].InverseBind*globals[dominant];var expected=Vector3.TransformNormal(new((float)n0.X,(float)n0.Y,(float)n0.Z),deformation);var expectedNormal=new Vector3D(expected.X,expected.Y,expected.Z);
            if(n1.LengthSquared<4e-8)collapsed++;else if(expectedNormal.Dot(n1)<=0)inverted++;
        }
        var neutralVolume=Volume(neutral,humanoid.Surface.Faces);var posedVolume=Volume(posed,humanoid.Surface.Faces);var attachments=humanoid.Attachments.ToDictionary(x=>x.Id,x=>HumanoidPosing.EvaluateAttachment(humanoid,x.Id,pose).Position,StringComparer.Ordinal);
        return new(pose.PoseId,finite,inverted,collapsed,posedArea/neutralArea,neutralVolume==0?double.NaN:posedVolume/neutralVolume,attachments);
    }
    private static bool Finite(Point3D p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z);
    private static double Volume(IReadOnlyList<Point3D> p,IReadOnlyList<HumanoidFace> faces)=>double.Abs(faces.Sum(f=>{var a=p[f.A]-Point3D.Origin;var b=p[f.B]-Point3D.Origin;var c=p[f.C]-Point3D.Origin;return a.Dot(b.Cross(c))/6;}));
}
