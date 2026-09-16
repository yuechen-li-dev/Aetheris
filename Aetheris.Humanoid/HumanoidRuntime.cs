using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public static class HumanoidValidator
{
    public static HumanoidValidationResult Validate(CanonicalHumanoid humanoid)
    {
        var d=new List<HumanoidDiagnostic>();var s=humanoid.Surface;
        if(s.TopologyId!=CanonicalAdultStandardV1.TopologyId||s.BindingTriangulationId!=CanonicalAdultStandardV1.BindingTriangulationId)d.Add(Error(HumanoidDiagnosticCode.HUM008_InvalidTopology,"Canonical topology or binding-triangulation identity is not X0 v1."));
        if(s.Vertices.Count is <8000 or >20000)d.Add(new(HumanoidDiagnosticCode.HUM008_InvalidTopology,HumanoidDiagnosticSeverity.Warning,$"X0 standard planning band is 8k-20k vertices; generated {s.Vertices.Count}."));
        for(var i=0;i<s.Vertices.Count;i++)
        {
            var v=s.Vertices[i];if(!Finite(v.Position))d.Add(Error(HumanoidDiagnosticCode.HUM007_NonFiniteCoordinate,"Vertex is non-finite.",v.Id));
            if(v.SymmetryPartnerIndex<0||v.SymmetryPartnerIndex>=s.Vertices.Count||s.Vertices[v.SymmetryPartnerIndex].SymmetryPartnerIndex!=i)d.Add(Error(HumanoidDiagnosticCode.HUM009_InvalidSymmetry,"Vertex symmetry is not an in-range involution.",v.Id));
        }
        for(var i=0;i<s.Faces.Count;i++)
        {
            var f=s.Faces[i];if(new[]{f.A,f.B,f.C}.Any(x=>x<0||x>=s.Vertices.Count)||f.A==f.B||f.B==f.C||f.C==f.A){d.Add(Error(HumanoidDiagnosticCode.HUM008_InvalidTopology,"Face indices are invalid or repeated.",f.Id));continue;}
            var a=s.Vertices[f.A].Position;var b=s.Vertices[f.B].Position;var c=s.Vertices[f.C].Position;if((b-a).Cross(c-a).LengthSquared<4e-8)d.Add(Error(HumanoidDiagnosticCode.HUM008_InvalidTopology,"Face area is below the X0 0.0001 mm2 collapse threshold.",f.Id));
            if(f.SymmetryPartnerIndex is int pair&&(pair<0||pair>=s.Faces.Count||s.Faces[pair].SymmetryPartnerIndex!=i))d.Add(Error(HumanoidDiagnosticCode.HUM009_InvalidSymmetry,"Declared face symmetry is not an in-range involution.",f.Id));
        }
        if(ConnectivityHash(s.Faces)!=s.ConnectivityHash)d.Add(Error(HumanoidDiagnosticCode.HUM015_ConnectivityChanged,"Connectivity hash does not match canonical face indices."));
        if(s.BindingTriangles.Count!=s.Faces.Count||s.BindingTriangles.Where((x,i)=>x.FaceIndex!=i||x.A!=s.Faces[i].A||x.B!=s.Faces[i].B||x.C!=s.Faces[i].C).Any())d.Add(Error(HumanoidDiagnosticCode.HUM014_InvalidBinding,"Binding triangulation does not match stable canonical faces."));
        if(s.SkinWeights.Count!=s.Vertices.Count)d.Add(Error(HumanoidDiagnosticCode.HUM005_SkinWeightsInvalid,"Every canonical vertex requires one deformation-weight record."));
        foreach(var w in s.SkinWeights)
        {
            var sum=w.Weights.Sum(x=>x.Weight);if(w.VertexIndex<0||w.VertexIndex>=s.Vertices.Count||w.Weights.Count==0||w.Weights.Any(x=>x.JointIndex<0||x.JointIndex>=humanoid.Skeleton.Joints.Count||!double.IsFinite(x.Weight)||x.Weight<0||x.Weight>1)||double.Abs(sum-1)>CanonicalAdultStandardV1.WeightTolerance)d.Add(Error(HumanoidDiagnosticCode.HUM005_SkinWeightsInvalid,$"Skin weights are invalid or sum to {sum:R}.",$"vertex:{w.VertexIndex}"));
        }
        ValidateSkeleton(humanoid.Skeleton,d);
        foreach(var landmark in humanoid.Landmarks.OfType<SurfaceLandmark>())
        {
            if(landmark.Binding.TopologyId!=s.TopologyId||!s.Faces.Any(x=>x.Id==landmark.Binding.FaceId)||landmark.Binding.TriangleWithinFace!=0||double.Abs(landmark.Binding.BarycentricA+landmark.Binding.BarycentricB+landmark.Binding.BarycentricC-1)>1e-9||new[]{landmark.Binding.BarycentricA,landmark.Binding.BarycentricB,landmark.Binding.BarycentricC}.Any(x=>x<0||x>1))d.Add(Error(HumanoidDiagnosticCode.HUM014_InvalidBinding,"Surface landmark binding is invalid.",landmark.Id));
        }
        foreach(var id in RequiredLandmarkIds.Where(id=>humanoid.Landmarks.All(x=>x.Id!=id)))d.Add(Error(HumanoidDiagnosticCode.HUM001_MissingRequiredLandmark,"Required X0 landmark is absent.",id));
        var bind=HumanoidPosing.EvaluateVertices(humanoid,new(CanonicalAdultStandardV1.RestPoseId,[],HumanoidTransform.Identity));for(var i=0;i<bind.Count;i++)if((bind[i]-s.Vertices[i].Position).Length>1e-3)d.Add(Error(HumanoidDiagnosticCode.HUM010_InvalidSkeleton,"Bind-pose reconstruction exceeded 0.001 mm.",s.Vertices[i].Id));
        return new(!d.Any(x=>x.Severity==HumanoidDiagnosticSeverity.Error),d);
    }
    private static void ValidateSkeleton(HumanoidSkeleton skeleton,List<HumanoidDiagnostic> d)
    {
        if(skeleton.SkeletonId!=CanonicalAdultStandardV1.SkeletonId)d.Add(Error(HumanoidDiagnosticCode.HUM010_InvalidSkeleton,"Unexpected canonical skeleton identity."));
        for(var i=0;i<skeleton.Joints.Count;i++){var j=skeleton.Joints[i];if(j.ParentIndex is int p&&(p<0||p>=i))d.Add(Error(HumanoidDiagnosticCode.HUM010_InvalidSkeleton,"Parent must precede child in acyclic canonical order.",j.Id));var identity=j.GlobalBind*j.InverseBind;if(!NearIdentity(identity,1e-4f))d.Add(Error(HumanoidDiagnosticCode.HUM010_InvalidSkeleton,"Global bind and inverse bind do not reconstruct identity.",j.Id));}
        foreach(var kind in Enum.GetValues<HumanoidJointKind>().Where(k=>k is not HumanoidJointKind.LeftEye and not HumanoidJointKind.RightEye and not HumanoidJointKind.Jaw))if(skeleton.Joints.All(x=>x.Kind!=kind))d.Add(Error(HumanoidDiagnosticCode.HUM010_InvalidSkeleton,"Required canonical joint is missing.",kind.ToString()));
    }
    private static bool NearIdentity(Matrix4x4 m,float e)=>MathF.Abs(m.M11-1)<e&&MathF.Abs(m.M22-1)<e&&MathF.Abs(m.M33-1)<e&&MathF.Abs(m.M44-1)<e&&MathF.Abs(m.M12)<e&&MathF.Abs(m.M13)<e&&MathF.Abs(m.M14)<e&&MathF.Abs(m.M21)<e&&MathF.Abs(m.M23)<e&&MathF.Abs(m.M24)<e&&MathF.Abs(m.M31)<e&&MathF.Abs(m.M32)<e&&MathF.Abs(m.M34)<e&&MathF.Abs(m.M41)<e&&MathF.Abs(m.M42)<e&&MathF.Abs(m.M43)<e;
    private static bool Finite(Point3D p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z);
    private static HumanoidDiagnostic Error(HumanoidDiagnosticCode c,string m,string? e=null)=>new(c,HumanoidDiagnosticSeverity.Error,m,e);
    private static string ConnectivityHash(IEnumerable<HumanoidFace> faces)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";",faces.Select(f=>$"{f.A},{f.B},{f.C}")))));
    public static readonly string[] RequiredLandmarkIds=["surface.head.crown","surface.face.chin","surface.face.nose-tip","surface.torso.sternum","surface.torso.navel","surface.shoulder.acromion.left","surface.shoulder.acromion.right","surface.pelvis.asis.left","surface.pelvis.asis.right","surface.elbow.tip.left","surface.elbow.tip.right","surface.wrist.ulnar.left","surface.wrist.ulnar.right","surface.hand.middle-tip.left","surface.hand.middle-tip.right","surface.knee.patella.left","surface.knee.patella.right","surface.ankle.lateral.left","surface.ankle.lateral.right","surface.foot.heel.left","surface.foot.heel.right","surface.foot.toe-tip.left","surface.foot.toe-tip.right"];
}

public static class HumanoidMeasurements
{
    public static IReadOnlyList<HumanoidMeasurement> Evaluate(CanonicalHumanoid humanoid)
        => humanoid.MeasurementProtocols.Select(p=>Evaluate(humanoid,p)).ToArray();
    public static HumanoidMeasurement Evaluate(CanonicalHumanoid h,MeasurementProtocol protocol)
    {
        try
        {
            var value=protocol.Measurement switch
            {
                HumanoidMeasurementId.Height=>Surface(h,"surface.head.crown").Z,
                HumanoidMeasurementId.ShoulderWidth=>Distance(Surface(h,"surface.shoulder.acromion.left"),Surface(h,"surface.shoulder.acromion.right")),
                HumanoidMeasurementId.HipWidth=>Distance(Surface(h,"surface.pelvis.asis.left"),Surface(h,"surface.pelvis.asis.right")),
                HumanoidMeasurementId.ArmLength=>Chain(h,HumanoidJointKind.LeftShoulder,HumanoidJointKind.LeftElbow,HumanoidJointKind.LeftWrist),
                HumanoidMeasurementId.LegLength=>Chain(h,HumanoidJointKind.LeftHip,HumanoidJointKind.LeftKnee,HumanoidJointKind.LeftAnkle),
                HumanoidMeasurementId.FootLength=>Distance(Surface(h,"surface.foot.heel.left"),Surface(h,"surface.foot.toe-tip.left")),
                HumanoidMeasurementId.HandLength=>Distance(Joint(h,HumanoidJointKind.LeftWrist),Surface(h,"surface.hand.middle-tip.left")),
                HumanoidMeasurementId.ChestDepth=>Depth(h,HumanoidRegionKind.Chest),
                _=>throw new NotSupportedException("Protocol evaluator is not implemented in X0.")
            };
            return new(protocol.Measurement,value,protocol.ProtocolId,protocol.PoseId,true,"Valid for canonical adult X0 measurement pose; engineering proxy only.");
        }
        catch(Exception ex){return new(protocol.Measurement,null,protocol.ProtocolId,protocol.PoseId,false,ex.Message);}
    }
    private static Point3D Surface(CanonicalHumanoid h,string id)=>CanonicalAdultTemplate.Resolve(h.Surface,((SurfaceLandmark)h.Landmarks.Single(x=>x.Id==id)).Binding);
    private static Point3D Joint(CanonicalHumanoid h,HumanoidJointKind kind){var m=h.Skeleton.Joints.Single(x=>x.Kind==kind).GlobalBind;return new(m.M41,m.M42,m.M43);}
    private static double Chain(CanonicalHumanoid h,params HumanoidJointKind[] kinds)=>kinds.Zip(kinds.Skip(1),(a,b)=>Distance(Joint(h,a),Joint(h,b))).Sum();
    private static double Distance(Point3D a,Point3D b)=>(a-b).Length;
    private static double Depth(CanonicalHumanoid h,HumanoidRegionKind r){var p=h.Surface.Vertices.Where(x=>x.Region==r).Select(x=>x.Position.Y).ToArray();if(p.Length==0)throw new InvalidOperationException("Region is empty.");return p.Max()-p.Min();}
}

public static class HumanoidPosing
{
    public static IReadOnlyList<Matrix4x4> GlobalPose(CanonicalHumanoid h,HumanoidPoseState pose)
        => GlobalPose(h.Skeleton, pose);

    public static IReadOnlyList<Matrix4x4> GlobalPose(HumanoidSkeleton skeleton,HumanoidPoseState pose)
    {
        var rotations=pose.LocalRotations.ToDictionary(x=>x.Joint,x=>x.LocalRotation);var result=new Matrix4x4[skeleton.Joints.Count];
        for(var i=0;i<result.Length;i++){var j=skeleton.Joints[i];var q=rotations.GetValueOrDefault(j.Kind,Quaternion.Identity);var local=Matrix4x4.CreateFromQuaternion(q)*j.LocalRest.Matrix;result[i]=j.ParentIndex is int parent?local*result[parent]:local*pose.RootTransform.Matrix;}return result;
    }
    public static IReadOnlyList<Point3D> EvaluateVertices(CanonicalHumanoid h,HumanoidPoseState pose)
        => EvaluateVertices(h.Surface, h.Skeleton, pose);

    /// <summary>Shared skin evaluator for canonical instances and explicitly unpromoted adoption candidates.</summary>
    public static IReadOnlyList<Point3D> EvaluateVertices(HumanoidSurface surface,HumanoidSkeleton skeleton,HumanoidPoseState pose)
    {
        var globals=GlobalPose(skeleton,pose);var output=new Point3D[surface.Vertices.Count];
        foreach(var skin in surface.SkinWeights){var source=surface.Vertices[skin.VertexIndex].Position;var p=new Vector3((float)source.X,(float)source.Y,(float)source.Z);var sum=Vector3.Zero;foreach(var w in skin.Weights){var transformed=Vector3.Transform(p,skeleton.Joints[w.JointIndex].InverseBind*globals[w.JointIndex]);sum+=transformed*(float)w.Weight;}output[skin.VertexIndex]=new(sum.X,sum.Y,sum.Z);}return output;
    }
    public static AttachmentFrame EvaluateAttachment(CanonicalHumanoid h,string siteId,HumanoidPoseState pose)
    {
        var site=h.Attachments.Single(x=>x.Id==siteId);var positions=EvaluateVertices(h,pose);var face=h.Surface.Faces.Single(x=>x.Id==site.PositionBinding.FaceId);var b=site.PositionBinding;var p=new Point3D(positions[face.A].X*b.BarycentricA+positions[face.B].X*b.BarycentricB+positions[face.C].X*b.BarycentricC,positions[face.A].Y*b.BarycentricA+positions[face.B].Y*b.BarycentricB+positions[face.C].Y*b.BarycentricC,positions[face.A].Z*b.BarycentricA+positions[face.B].Z*b.BarycentricB+positions[face.C].Z*b.BarycentricC);var globals=GlobalPose(h,pose);var jointIndex=h.Skeleton.Joints.ToList().FindIndex(x=>x.Kind==site.FollowJoint);var bind=h.Skeleton.Joints[jointIndex].InverseBind*globals[jointIndex];return site.NeutralFrame with{Position=p,Tangent=Transform(site.NeutralFrame.Tangent,bind),Normal=Transform(site.NeutralFrame.Normal,bind),Binormal=Transform(site.NeutralFrame.Binormal,bind)};
        static Vector3D Transform(Vector3D v,Matrix4x4 m){var x=Vector3.TransformNormal(new((float)v.X,(float)v.Y,(float)v.Z),m);var result=new Vector3D(x.X,x.Y,x.Z);return result.TryNormalize(out var n)?n:result;}
    }
}

public static class HumanoidMorphing
{
    public static CanonicalHumanoid Apply(CanonicalHumanoid h,IReadOnlyDictionary<HumanoidMorphId,double> values)
    {
        foreach(var (id,value) in values){var channel=h.MorphChannels.SingleOrDefault(x=>x.Id==id)??throw new ArgumentException($"Unsupported morph {id}.");if(value<channel.Minimum||value>channel.Maximum||!double.IsFinite(value))throw new HumanoidDomainException(new(HumanoidDiagnosticCode.HUM013_MorphOutOfBounds,HumanoidDiagnosticSeverity.Error,$"{id} value {value:R} is outside [{channel.Minimum:R},{channel.Maximum:R}].",id.ToString()));}
        Point3D Map(Point3D p,HumanoidRegionKind? region)
        {
            var q=p;
            if(values.TryGetValue(HumanoidMorphId.Height,out var height)){var concurrentLeg=values.GetValueOrDefault(HumanoidMorphId.LegLength,0);var d=height-1750-concurrentLeg;var factor=p.Z<=520?.45*p.Z/520:p.Z<=930?.45+.25*(p.Z-520)/410:.70+.30*double.Clamp((p.Z-930)/820,0,1);q+=new Vector3D(0,0,d*factor);}
            if(values.TryGetValue(HumanoidMorphId.LegLength,out var leg)&&leg!=0){var f=double.Clamp(p.Z/925,0,1);if(p.Z<925)q+=new Vector3D(0,0,leg*f);else q+=new Vector3D(0,0,leg);}
            if(values.TryGetValue(HumanoidMorphId.ShoulderWidth,out var shoulder)&&shoulder!=0){var zBlend=double.Clamp(1-double.Abs(p.Z-1340)/300,0,1);q+=new Vector3D(Math.Sign(p.X)*shoulder*zBlend,0,0);}
            if(values.TryGetValue(HumanoidMorphId.ArmLength,out var arm)&&arm!=0&&region is not null&&region.ToString()!.ContainsAny("Shoulder","UpperArm","Elbow","Forearm","Wrist","Hand","Thumb","Index","Middle","Ring","Little")){var side=Math.Sign(p.X);var shoulderPoint=new Point3D(side*185,0,1345);var v=q-shoulderPoint;var baseLength=500d;q=shoulderPoint+v*((baseLength+arm)/baseLength);}
            return q;
        }
        var vertices=h.Surface.Vertices.Select(v=>v with{Position=Map(v.Position,v.Region)}).ToArray();var oldJoints=h.Skeleton.Joints;var globals=oldJoints.Select(j=>Map(new(j.GlobalBind.M41,j.GlobalBind.M42,j.GlobalBind.M43),RegionForJoint(j.Kind))).ToArray();var joints=new HumanoidJoint[oldJoints.Count];for(var i=0;i<joints.Length;i++){var old=oldJoints[i];var parent=old.ParentIndex is int pi?globals[pi]:Point3D.Origin;var local=globals[i]-parent;var global=Matrix4x4.CreateTranslation((float)globals[i].X,(float)globals[i].Y,(float)globals[i].Z);Matrix4x4.Invert(global,out var inv);joints[i]=old with{LocalRest=new(new(local.X,local.Y,local.Z),Quaternion.Identity),GlobalBind=global,InverseBind=inv};}
        var skeleton=h.Skeleton with{Joints=joints};var surface=h.Surface with{Vertices=vertices};var frame=values.TryGetValue(HumanoidMorphId.Height,out var requestedHeight)?h.Frame with{NormalizedHeightMm=requestedHeight}:h.Frame;var updated=h with{Frame=frame,Surface=surface,Skeleton=skeleton,ShapeRevision=h.ShapeRevision+1,PoseState=new(CanonicalAdultStandardV1.RestPoseId,[],HumanoidTransform.Identity)};return updated with{Attachments=updated.Attachments.Select(site=>site with{NeutralFrame=site.NeutralFrame with{Position=CanonicalAdultTemplate.Resolve(surface,site.PositionBinding)}}).ToArray()};
    }
    private static HumanoidRegionKind? RegionForJoint(HumanoidJointKind k){var s=k.ToString();if(s.Contains("Shoulder"))return s.StartsWith("Left")?HumanoidRegionKind.LeftShoulder:HumanoidRegionKind.RightShoulder;if(s.Contains("Elbow"))return s.StartsWith("Left")?HumanoidRegionKind.LeftElbow:HumanoidRegionKind.RightElbow;if(s.Contains("Wrist")||s.Contains("Thumb")||s.Contains("Index")||s.Contains("Middle")||s.Contains("Ring")||s.Contains("Little"))return s.StartsWith("Left")?HumanoidRegionKind.LeftHand:HumanoidRegionKind.RightHand;if(s.Contains("Hip"))return s.StartsWith("Left")?HumanoidRegionKind.LeftThigh:HumanoidRegionKind.RightThigh;if(s.Contains("Knee"))return s.StartsWith("Left")?HumanoidRegionKind.LeftKnee:HumanoidRegionKind.RightKnee;if(s.Contains("Ankle")||s.Contains("Toe"))return s.StartsWith("Left")?HumanoidRegionKind.LeftFoot:HumanoidRegionKind.RightFoot;return null;}
    private static bool ContainsAny(this string value,params string[] tokens)=>tokens.Any(value.Contains);
}

public sealed class HumanoidDomainException(HumanoidDiagnostic diagnostic):InvalidOperationException(diagnostic.Message){public HumanoidDiagnostic Diagnostic{get;}=diagnostic;}

public static class HumanoidArtifactIO
{
    public static JsonSerializerOptions JsonOptions { get; }=CreateOptions();
    private static JsonSerializerOptions CreateOptions(){var o=new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase,IncludeFields=true};o.Converters.Add(new JsonStringEnumConverter());return o;}
    public static void Save(CanonicalHumanoid humanoid,string path){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path,JsonSerializer.Serialize(humanoid,JsonOptions));}
    public static CanonicalHumanoid Load(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("schemaVersion", out var schema) || schema.GetString() != "aetheris.humanoid.canonical.v1")
            throw new InvalidDataException("Expected a canonical humanoid artifact; an adoption candidate is not a canonical runtime instance.");
        var result = JsonSerializer.Deserialize<CanonicalHumanoid>(json, JsonOptions);
        if (result?.Surface is null || result.Skeleton is null || result.Provenance is null)
            throw new InvalidDataException("Canonical humanoid artifact is incomplete.");
        return result;
    }
    public static void SaveObj(CanonicalHumanoid humanoid,string path,IReadOnlyList<Point3D>? positions=null)
    {
        positions??=humanoid.Surface.Vertices.Select(x=>x.Position).ToArray();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);using var w=new StreamWriter(path,false,new UTF8Encoding(false));w.WriteLine("# Aetheris canonical humanoid display mesh; millimeters; +X anatomical right +Y forward +Z up");foreach(var p in positions)w.WriteLine(FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}"));foreach(var group in humanoid.Surface.Faces.Select((f,i)=>(f,i)).GroupBy(x=>x.f.Region)){w.WriteLine("g "+group.Key);foreach(var (f,_) in group)w.WriteLine($"f {f.A+1} {f.B+1} {f.C+1}");}
    }
}
