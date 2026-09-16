using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Aetheris.Humanoid;

return Run(args);

static int Run(string[] args)
{
    if(args.Length==0||args[0] is "--help" or "-h"){Usage();return 0;}
    try
    {
        return args[0] switch
        {
            "generate"=>Generate(args[1..]),
            "inspect"=>Inspect(args[1..]),
            "morph"=>Morph(args[1..]),
            "pose"=>Pose(args[1..]),
            "sweep"=>Sweep(args[1..]),
            _=>Fail("Unknown humanoid X0 command.")
        };
    }
    catch(HumanoidDomainException ex){Console.Error.WriteLine($"{ex.Diagnostic.Code}: {ex.Message}");return 2;}
    catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
}

static int Generate(string[] args)
{
    var outDir=Value(args,"--out-dir")??Path.Combine("artifacts","local","humanoid-x0");Directory.CreateDirectory(outDir);var sw=Stopwatch.StartNew();var h=CanonicalAdultTemplate.Create();sw.Stop();var validation=HumanoidValidator.Validate(h);var artifact=Path.Combine(outDir,"canonical-adult-standard-v1.json");var obj=Path.Combine(outDir,"canonical-adult-standard-v1.obj");HumanoidArtifactIO.Save(h,artifact);HumanoidArtifactIO.SaveObj(h,obj);WriteSummary(h,validation,Path.Combine(outDir,"canonical-summary.json"),sw.Elapsed.TotalMilliseconds);Console.WriteLine(JsonSerializer.Serialize(new{success=validation.IsValid,artifact,obj,vertices=h.Surface.Vertices.Count,faces=h.Surface.Faces.Count,h.Surface.ConnectivityHash,diagnosticCounts=validation.Diagnostics.GroupBy(x=>new{x.Code,x.Severity}).Select(g=>new{g.Key.Code,g.Key.Severity,count=g.Count(),sample=g.First().Message})},HumanoidArtifactIO.JsonOptions));return validation.IsValid?0:2;
}
static int Inspect(string[] args)
{
    var input=Required(args,"--input");var h=HumanoidArtifactIO.Load(input);var validation=HumanoidValidator.Validate(h);Console.WriteLine(JsonSerializer.Serialize(new{validation.IsValid,h.Metadata,h.Frame,topology=new{h.Surface.TopologyId,h.Surface.ConnectivityHash,vertices=h.Surface.Vertices.Count,faces=h.Surface.Faces.Count,components=h.Surface.Components.Select(x=>new{x.Id,x.Kind,faces=x.FaceIndices.Count})},skeleton=new{h.Skeleton.SkeletonId,joints=h.Skeleton.Joints.Count},measurements=HumanoidMeasurements.Evaluate(h),attachments=h.Attachments.Select(x=>x.Id),morphs=h.MorphChannels,validation.Diagnostics},HumanoidArtifactIO.JsonOptions));return validation.IsValid?0:2;
}
static int Morph(string[] args)
{
    var input=Required(args,"--input");var output=Required(args,"--output");var h=HumanoidArtifactIO.Load(input);var values=new Dictionary<HumanoidMorphId,double>();foreach(var id in Enum.GetValues<HumanoidMorphId>()){var raw=Value(args,"--"+Kebab(id.ToString()));if(raw is not null)values[id]=double.Parse(raw,System.Globalization.CultureInfo.InvariantCulture);}var shaped=HumanoidMorphing.Apply(h,values);HumanoidArtifactIO.Save(shaped,output);HumanoidArtifactIO.SaveObj(shaped,Path.ChangeExtension(output,".obj"));var validation=HumanoidValidator.Validate(shaped);Console.WriteLine(JsonSerializer.Serialize(new{success=validation.IsValid,topologyUnchanged=shaped.Surface.ConnectivityHash==h.Surface.ConnectivityHash,shapeRevision=shaped.ShapeRevision,values,measurements=HumanoidMeasurements.Evaluate(shaped),validation.Diagnostics},HumanoidArtifactIO.JsonOptions));return validation.IsValid?0:2;
}
static int Pose(string[] args)
{
    var input=Required(args,"--input");var output=Required(args,"--output");var h=HumanoidArtifactIO.Load(input);var elbow=double.Parse(Value(args,"--left-elbow-deg")??"0",System.Globalization.CultureInfo.InvariantCulture);var shoulder=double.Parse(Value(args,"--left-shoulder-deg")??"0",System.Globalization.CultureInfo.InvariantCulture);var hip=double.Parse(Value(args,"--left-hip-deg")??"0",System.Globalization.CultureInfo.InvariantCulture);var knee=double.Parse(Value(args,"--left-knee-deg")??"0",System.Globalization.CultureInfo.InvariantCulture);var poses=new[]{(HumanoidJointKind.LeftElbow,elbow,Vector3.UnitY),(HumanoidJointKind.LeftShoulder,shoulder,Vector3.UnitY),(HumanoidJointKind.LeftHip,hip,Vector3.UnitX),(HumanoidJointKind.LeftKnee,knee,Vector3.UnitX)}.Where(x=>x.Item2!=0).Select(x=>new JointPose(x.Item1,Quaternion.CreateFromAxisAngle(x.Item3,(float)(x.Item2*Math.PI/180)))).ToArray();var pose=new HumanoidPoseState("x0.user-pose",poses,HumanoidTransform.Identity);var positions=HumanoidPosing.EvaluateVertices(h,pose);HumanoidArtifactIO.SaveObj(h,output,positions);var frame=HumanoidPosing.EvaluateAttachment(h,"site:Wrist.Left",pose);Console.WriteLine(JsonSerializer.Serialize(new{success=positions.All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z)),output,leftWristAttachment=frame,topology=h.Surface.ConnectivityHash},HumanoidArtifactIO.JsonOptions));return 0;
}
static int Sweep(string[] args)
{
    var input=Required(args,"--input");var output=Required(args,"--output");var h=HumanoidArtifactIO.Load(input);var cases=new[]{
        ("shoulder-abduction-35",HumanoidJointKind.LeftShoulder,35d,Vector3.UnitY),("shoulder-abduction-70",HumanoidJointKind.LeftShoulder,70d,Vector3.UnitY),
        ("elbow-flexion-45",HumanoidJointKind.LeftElbow,45d,Vector3.UnitY),("elbow-flexion-90",HumanoidJointKind.LeftElbow,90d,Vector3.UnitY),
        ("hip-flexion-35",HumanoidJointKind.LeftHip,35d,Vector3.UnitX),("hip-flexion-70",HumanoidJointKind.LeftHip,70d,Vector3.UnitX),
        ("knee-flexion-45",HumanoidJointKind.LeftKnee,45d,Vector3.UnitX),("knee-flexion-90",HumanoidJointKind.LeftKnee,90d,Vector3.UnitX)};
    var evidence=cases.Select(x=>HumanoidPoseQualification.Evaluate(h,new(x.Item1,[new(x.Item2,Quaternion.CreateFromAxisAngle(x.Item4,(float)(x.Item3*Math.PI/180)))],HumanoidTransform.Identity))).ToArray();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(new{schema="aetheris.humanoid.pose-sweep-evidence.v1",topology=h.Surface.TopologyId,connectivityHash=h.Surface.ConnectivityHash,cases=evidence,acceptance=new{finite=evidence.All(x=>x.Finite),noTopologyFailure=evidence.All(x=>x.CollapsedFaces==0),noInversion=evidence.All(x=>x.InvertedFaces==0),stable=evidence.All(x=>x.IsStable)},limitations=new[]{"Self-intersection is not evaluated in X0.","Linear blend skinning may show shoulder and hip volume loss; area and absolute-volume ratios are screening witnesses."}},HumanoidArtifactIO.JsonOptions));Console.WriteLine(JsonSerializer.Serialize(new{success=true,output,stable=evidence.All(x=>x.IsStable),cases=evidence.Length},HumanoidArtifactIO.JsonOptions));return 0;
}
static void WriteSummary(CanonicalHumanoid h,HumanoidValidationResult validation,string path,double ms)
{
    var evidence=new{milestone="HUMANOID-X0",status=validation.IsValid?"AuthoredTemplateValid":"AuthoredTemplateInvalid",generatorMilliseconds=ms,authority="Aetheris-authored implicit semantic primitives and fixed tetrahedral lattice; no external connectivity",topology=new{h.Surface.TopologyId,h.Surface.BindingTriangulationId,h.Surface.ConnectivityHash,vertices=h.Surface.Vertices.Count,faces=h.Surface.Faces.Count,regions=h.Surface.Vertices.Select(x=>x.Region).Distinct().Count(),components=h.Surface.Components.Select(x=>new{x.Id,x.Kind,faces=x.FaceIndices.Count})},skeleton=new{h.Skeleton.SkeletonId,joints=h.Skeleton.Joints.Count,h.Skeleton.RestPoseId,h.Skeleton.SkinningConvention},landmarks=new{total=h.Landmarks.Count,surface=h.Landmarks.OfType<SurfaceLandmark>().Count(),joint=h.Landmarks.OfType<JointLandmark>().Count()},measurements=HumanoidMeasurements.Evaluate(h),morphs=h.MorphChannels,attachments=h.Attachments.Select(x=>x.Id),validation};File.WriteAllText(path,JsonSerializer.Serialize(evidence,HumanoidArtifactIO.JsonOptions));
}
static string? Value(string[] args,string name){var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
static string Required(string[] args,string name)=>Value(args,name)??throw new ArgumentException($"Missing {name}.");
static string Kebab(string s)=>string.Concat(s.Select((c,i)=>char.IsUpper(c)&&i>0?"-"+char.ToLowerInvariant(c):char.ToLowerInvariant(c).ToString()));
static int Fail(string message){Console.Error.WriteLine(message);Usage();return 1;}
static void Usage()=>Console.WriteLine("Aetheris.Humanoid.X0\n  generate [--out-dir <dir>]\n  inspect --input <canonical.json>\n  morph --input <canonical.json> --output <variant.json> [--height <mm>] [--arm-length <delta-mm>] [--leg-length <delta-mm>] [--shoulder-width <delta-mm>]\n  pose --input <canonical.json> --output <posed.obj> [--left-elbow-deg <deg>] [--left-shoulder-deg <deg>] [--left-hip-deg <deg>] [--left-knee-deg <deg>]\n  sweep --input <canonical.json> --output <evidence.json>");
