using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

/// <summary>
/// Independently authored X0 topology. Connectivity comes from a fixed tetrahedral lattice over explicit
/// semantic anatomical primitives; no external mesh positions or connectivity participate in generation.
/// </summary>
public static class CanonicalAdultTemplate
{
    private const double Step = 24d;
    private const string GeneratorVersion = "aetheris-humanoid-implicit-tetra-v1";
    private readonly record struct Primitive(string Id, HumanoidRegionKind Region, Func<Point3D, double> SignedDistance);
    private readonly record struct GridSample(Point3D Position, double Distance, HumanoidRegionKind Region, long Id);
    private readonly record struct EdgeKey(long A, long B)
    {
        public static EdgeKey Create(long a, long b) => a < b ? new(a, b) : new(b, a);
    }

    public static CanonicalHumanoid Create()
    {
        var skeleton = BuildSkeleton();
        var primitives = BuildPrimitives(skeleton);
        var (vertices, faces) = Polygonize(primitives);
        faces = RemoveCollapsedFaces(vertices, faces);
        var (withEyesRaw, withEyeFaces, components) = AddEyes(vertices, faces);
        var (withEyes, normalizedSkeleton, sourceMinimumZ, sourceMaximumZ) = NormalizeHeight(withEyesRaw, skeleton);
        skeleton = normalizedSkeleton;
        var symmetricVertices = AssignVertexSymmetry(withEyes);
        var symmetricFaces = AssignFaceSymmetry(withEyeFaces, symmetricVertices);
        var connectivityHash = ConnectivityHash(symmetricFaces);
        var triangles = symmetricFaces.Select((face, index) => new BindingTriangle($"bt:{index:D6}", index, face.A, face.B, face.C)).ToArray();
        var weights = GenerateWeights(symmetricVertices, skeleton);
        var surface = new HumanoidSurface(
            CanonicalAdultStandardV1.TopologyId,
            CanonicalAdultStandardV1.BindingTriangulationId,
            connectivityHash,
            symmetricVertices,
            symmetricFaces,
            triangles,
            null,
            weights,
            components,
            RegionSymmetry());
        var landmarks = BuildLandmarks(surface, skeleton);
        var protocols = BuildMeasurementProtocols();
        var morphs = BuildMorphChannels();
        var materials = Enum.GetValues<HumanoidRegionKind>().ToDictionary(
            region => region,
            region => region is HumanoidRegionKind.LeftEye or HumanoidRegionKind.RightEye ? MaterialRegionKind.Eyes
                : region is HumanoidRegionKind.MouthInterior ? MaterialRegionKind.MouthInterior : MaterialRegionKind.Skin);
        var attachments = BuildAttachments(surface, landmarks);
        var configHash = Hash(string.Join("|", GeneratorVersion, Step.ToString("R", CultureInfo.InvariantCulture), connectivityHash));
        var provenance = new HumanoidProvenance(
            "aetheris.humanoid.provenance.v1",
            [new("canonical-template", "authored-template", "Aetheris CanonicalAdultTemplate", "v1", connectivityHash, "Aetheris repository license", "canonical topology generation", true, "Connectivity is generated solely from repository-authored primitives and a fixed lattice.")],
            ["explicit semantic primitives", $"fixed {Step:R} mm lattice", "six fixed tetrahedra per cell", "zero-isovalue interpolation", "deterministic sub-0.0001 mm2 collapsed-face rejection", $"outer-skin Z normalization [{sourceMinimumZ:R},{sourceMaximumZ:R}] -> [0,1750] mm", "stable coordinate-order IDs", "procedural regional skin weights"],
            GeneratorVersion,
            configHash,
            connectivityHash,
            "AuthoredTemplate",
            ["X0 topology and facial/hand/foot quality require visual review; no third-party connectivity used."]);
        return new CanonicalHumanoid(
            "aetheris.humanoid.canonical.v1",
            new("adult-standard-v1", "Canonical Adult Standard V1", "adult-v1", "Adult-only engineering humanoid foundation; not medical or population-statistical authority."),
            new("mm", "right-handed", new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), Point3D.Origin, CanonicalAdultStandardV1.NeutralHeightMm),
            surface,
            skeleton,
            landmarks,
            protocols,
            morphs,
            materials,
            attachments,
            new(CanonicalAdultStandardV1.RestPoseId, [], HumanoidTransform.Identity),
            provenance);
    }

    private static List<HumanoidFace> RemoveCollapsedFaces(IReadOnlyList<HumanoidVertex> vertices,IEnumerable<HumanoidFace> faces)
        => faces.Where(face=>
        {
            var a=vertices[face.A].Position;var b=vertices[face.B].Position;var c=vertices[face.C].Position;
            return (b-a).Cross(c-a).LengthSquared>=4e-8;
        }).Select((face,index)=>face with{Id=$"f:{index:D6}"}).ToList();

    private static (List<HumanoidVertex> Vertices, HumanoidSkeleton Skeleton, double MinimumZ, double MaximumZ) NormalizeHeight(List<HumanoidVertex> vertices,HumanoidSkeleton skeleton)
    {
        var outer=vertices.Where(v=>v.Region is not HumanoidRegionKind.LeftEye and not HumanoidRegionKind.RightEye).ToArray();var min=outer.Min(v=>v.Position.Z);var max=outer.Max(v=>v.Position.Z);var scale=CanonicalAdultStandardV1.NeutralHeightMm/(max-min);
        Point3D Map(Point3D p)=>new(p.X,p.Y,(p.Z-min)*scale);
        var mapped=vertices.Select(v=>v with{Position=Map(v.Position)}).ToList();var globals=skeleton.Joints.Select(j=>Map(new(j.GlobalBind.M41,j.GlobalBind.M42,j.GlobalBind.M43))).ToArray();var joints=new HumanoidJoint[skeleton.Joints.Count];
        for(var i=0;i<joints.Length;i++){var old=skeleton.Joints[i];var parent=old.ParentIndex is int pi?globals[pi]:Point3D.Origin;var local=globals[i]-parent;var global=Matrix4x4.CreateTranslation((float)globals[i].X,(float)globals[i].Y,(float)globals[i].Z);Matrix4x4.Invert(global,out var inverse);joints[i]=old with{LocalRest=new(new(local.X,local.Y,local.Z),Quaternion.Identity),GlobalBind=global,InverseBind=inverse};}
        return(mapped,skeleton with{Joints=joints},min,max);
    }

    private static IReadOnlyList<Primitive> BuildPrimitives(HumanoidSkeleton skeleton)
    {
        static double Ellipsoid(Point3D p, Point3D c, Vector3D r)
        {
            var q = new Vector3D((p.X-c.X)/r.X, (p.Y-c.Y)/r.Y, (p.Z-c.Z)/r.Z);
            return (q.Length - 1d) * double.Min(r.X, double.Min(r.Y, r.Z));
        }
        static double Capsule(Point3D p, Point3D a, Point3D b, double radius)
        {
            var ab=b-a; var ap=p-a; var t=double.Clamp(ap.Dot(ab)/ab.LengthSquared,0,1); var q=a+ab*t;
            return (p-q).Length-radius;
        }
        var j=skeleton.Joints.ToDictionary(x=>x.Kind,x=>new Point3D(x.GlobalBind.M41,x.GlobalBind.M42,x.GlobalBind.M43));
        var list=new List<Primitive>
        {
            new("head",HumanoidRegionKind.Head,p=>Ellipsoid(p,new(0,0,1585),new(104,92,145))),
            new("face",HumanoidRegionKind.Face,p=>Ellipsoid(p,new(0,53,1565),new(91,58,112))),
            new("nose",HumanoidRegionKind.Face,p=>Ellipsoid(p,new(0,111,1572),new(22,30,38))),
            new("chin",HumanoidRegionKind.Face,p=>Ellipsoid(p,new(0,72,1479),new(55,45,35))),
            new("lip-upper",HumanoidRegionKind.Face,p=>Ellipsoid(p,new(0,112,1528),new(31,13,9))),
            new("lip-lower",HumanoidRegionKind.Face,p=>Ellipsoid(p,new(0,112,1513),new(32,14,10))),
            new("ear-left",HumanoidRegionKind.LeftEar,p=>Ellipsoid(p,new(-108,0,1570),new(21,13,42))),
            new("ear-right",HumanoidRegionKind.RightEar,p=>Ellipsoid(p,new(108,0,1570),new(21,13,42))),
            new("neck",HumanoidRegionKind.Neck,p=>Capsule(p,j[HumanoidJointKind.Chest],j[HumanoidJointKind.Head],57)),
            new("chest",HumanoidRegionKind.Chest,p=>Ellipsoid(p,new(0,0,1280),new(225,125,245))),
            new("abdomen",HumanoidRegionKind.Abdomen,p=>Ellipsoid(p,new(0,3,1090),new(176,106,220))),
            new("pelvis",HumanoidRegionKind.Pelvis,p=>Ellipsoid(p,new(0,0,925),new(190,122,165)))
        };
        AddLimbs(AnatomicalSide.Left,-1); AddLimbs(AnatomicalSide.Right,1);
        return list;

        void AddLimbs(AnatomicalSide side,int sign)
        {
            var shoulder=side==AnatomicalSide.Left?HumanoidJointKind.LeftShoulder:HumanoidJointKind.RightShoulder;
            var elbow=side==AnatomicalSide.Left?HumanoidJointKind.LeftElbow:HumanoidJointKind.RightElbow;
            var wrist=side==AnatomicalSide.Left?HumanoidJointKind.LeftWrist:HumanoidJointKind.RightWrist;
            var hip=side==AnatomicalSide.Left?HumanoidJointKind.LeftHip:HumanoidJointKind.RightHip;
            var knee=side==AnatomicalSide.Left?HumanoidJointKind.LeftKnee:HumanoidJointKind.RightKnee;
            var ankle=side==AnatomicalSide.Left?HumanoidJointKind.LeftAnkle:HumanoidJointKind.RightAnkle;
            var prefix=side==AnatomicalSide.Left?"left":"right";
            var upper=side==AnatomicalSide.Left?HumanoidRegionKind.LeftUpperArm:HumanoidRegionKind.RightUpperArm;
            var elbowRegion=side==AnatomicalSide.Left?HumanoidRegionKind.LeftElbow:HumanoidRegionKind.RightElbow;
            var fore=side==AnatomicalSide.Left?HumanoidRegionKind.LeftForearm:HumanoidRegionKind.RightForearm;
            var hand=side==AnatomicalSide.Left?HumanoidRegionKind.LeftHand:HumanoidRegionKind.RightHand;
            list.Add(new(prefix+"-shoulder",side==AnatomicalSide.Left?HumanoidRegionKind.LeftShoulder:HumanoidRegionKind.RightShoulder,p=>Capsule(p,j[side==AnatomicalSide.Left?HumanoidJointKind.LeftClavicle:HumanoidJointKind.RightClavicle],j[shoulder],82)));
            list.Add(new(prefix+"-upper-arm",upper,p=>Capsule(p,j[shoulder],j[elbow],61)));
            list.Add(new(prefix+"-elbow",elbowRegion,p=>Ellipsoid(p,j[elbow],new(66,62,67))));
            list.Add(new(prefix+"-forearm",fore,p=>Capsule(p,j[elbow],j[wrist],47)));
            var handEnd=j[wrist]+(j[wrist]-j[elbow])*(0.52);
            list.Add(new(prefix+"-hand",hand,p=>Capsule(p,j[wrist],handEnd,49)));
            AddFingers(side,sign,j[wrist],j[elbow]);
            var thigh=side==AnatomicalSide.Left?HumanoidRegionKind.LeftThigh:HumanoidRegionKind.RightThigh;
            var kneeRegion=side==AnatomicalSide.Left?HumanoidRegionKind.LeftKnee:HumanoidRegionKind.RightKnee;
            var shin=side==AnatomicalSide.Left?HumanoidRegionKind.LeftShin:HumanoidRegionKind.RightShin;
            var ankleRegion=side==AnatomicalSide.Left?HumanoidRegionKind.LeftAnkle:HumanoidRegionKind.RightAnkle;
            var foot=side==AnatomicalSide.Left?HumanoidRegionKind.LeftFoot:HumanoidRegionKind.RightFoot;
            var toes=side==AnatomicalSide.Left?HumanoidRegionKind.LeftToes:HumanoidRegionKind.RightToes;
            list.Add(new(prefix+"-thigh",thigh,p=>Capsule(p,j[hip],j[knee],91)));
            list.Add(new(prefix+"-knee",kneeRegion,p=>Ellipsoid(p,j[knee],new(84,76,76))));
            list.Add(new(prefix+"-shin",shin,p=>Capsule(p,j[knee],j[ankle],61)));
            list.Add(new(prefix+"-ankle",ankleRegion,p=>Ellipsoid(p,j[ankle],new(62,57,65))));
            list.Add(new(prefix+"-foot",foot,p=>Ellipsoid(p,new(sign*88,62,48),new(70,150,50))));
            list.Add(new(prefix+"-toes",toes,p=>Ellipsoid(p,new(sign*88,164,39),new(72,70,34))));
        }
        void AddFingers(AnatomicalSide side,int sign,Point3D wrist,Point3D elbow)
        {
            var direction=wrist-elbow; direction=direction/direction.Length;
            var basePoint=wrist+direction*65;
            var defs=new[]{("thumb",-38d,58d,side==AnatomicalSide.Left?HumanoidRegionKind.LeftThumb:HumanoidRegionKind.RightThumb),
                ("index",-21d,103d,side==AnatomicalSide.Left?HumanoidRegionKind.LeftIndex:HumanoidRegionKind.RightIndex),
                ("middle",-7d,112d,side==AnatomicalSide.Left?HumanoidRegionKind.LeftMiddle:HumanoidRegionKind.RightMiddle),
                ("ring",8d,105d,side==AnatomicalSide.Left?HumanoidRegionKind.LeftRing:HumanoidRegionKind.RightRing),
                ("little",22d,88d,side==AnatomicalSide.Left?HumanoidRegionKind.LeftLittle:HumanoidRegionKind.RightLittle)};
            foreach(var (name,y,length,region) in defs)
            {
                var a=basePoint+new Vector3D(0,y,0); var b=a+direction*length;
                if(name=="thumb") b=b+new Vector3D(sign*12,-12,8);
                list.Add(new($"{side.ToString().ToLowerInvariant()}-{name}",region,p=>Capsule(p,a,b,name=="thumb"?16:12)));
            }
        }
    }

    private static (List<HumanoidVertex> Vertices,List<HumanoidFace> Faces) Polygonize(IReadOnlyList<Primitive> primitives)
    {
        const double minX=-720,minY=-190,minZ=-18,maxX=720,maxY=260,maxZ=1778;
        var nx=(int)double.Ceiling((maxX-minX)/Step)+1;var ny=(int)double.Ceiling((maxY-minY)/Step)+1;var nz=(int)double.Ceiling((maxZ-minZ)/Step)+1;
        var samples=new GridSample[nx*ny*nz];
        long Id(int x,int y,int z)=>((long)z*ny+y)*nx+x;
        int Index(int x,int y,int z)=>(z*ny+y)*nx+x;
        for(var z=0;z<nz;z++)for(var y=0;y<ny;y++)for(var x=0;x<nx;x++)
        {
            var p=new Point3D(minX+x*Step,minY+y*Step,minZ+z*Step);var best=double.PositiveInfinity;var region=HumanoidRegionKind.Abdomen;
            foreach(var primitive in primitives){var d=primitive.SignedDistance(p);if(d<best){best=d;region=primitive.Region;}}
            samples[Index(x,y,z)]=new(p,best,region,Id(x,y,z));
        }
        int[][] corners=[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]];
        int[][] tets=[[0,5,1,6],[0,1,2,6],[0,2,3,6],[0,3,7,6],[0,7,4,6],[0,4,5,6]];
        var verts=new List<HumanoidVertex>();var faces=new List<HumanoidFace>();var cache=new Dictionary<EdgeKey,int>();
        for(var z=0;z<nz-1;z++)for(var y=0;y<ny-1;y++)for(var x=0;x<nx-1;x++)
        {
            var cube=corners.Select(c=>samples[Index(x+c[0],y+c[1],z+c[2])]).ToArray();
            foreach(var tet in tets) EmitTet(tet.Select(i=>cube[i]).ToArray());
        }
        return(verts,faces);

        int Edge(GridSample a,GridSample b)
        {
            var key=EdgeKey.Create(a.Id,b.Id);if(cache.TryGetValue(key,out var found))return found;
            var t=a.Distance/(a.Distance-b.Distance);var p=a.Position+(b.Position-a.Position)*t;
            var region=double.Abs(a.Distance)<=double.Abs(b.Distance)?a.Region:b.Region;
            var index=verts.Count;verts.Add(new($"v:{index:D6}",p,region,-1));cache[key]=index;return index;
        }
        void EmitTet(GridSample[] t)
        {
            var inside=Enumerable.Range(0,4).Where(i=>t[i].Distance<=0).ToArray();var outside=Enumerable.Range(0,4).Where(i=>t[i].Distance>0).ToArray();
            if(inside.Length is 0 or 4)return;
            if(inside.Length==1) Add(Edge(t[inside[0]],t[outside[0]]),Edge(t[inside[0]],t[outside[1]]),Edge(t[inside[0]],t[outside[2]]));
            else if(inside.Length==3) Add(Edge(t[outside[0]],t[inside[0]]),Edge(t[outside[0]],t[inside[2]]),Edge(t[outside[0]],t[inside[1]]));
            else
            {
                var a=Edge(t[inside[0]],t[outside[0]]);var b=Edge(t[inside[0]],t[outside[1]]);var c=Edge(t[inside[1]],t[outside[0]]);var d=Edge(t[inside[1]],t[outside[1]]);
                Add(a,b,c);Add(b,d,c);
            }
        }
        void Add(int a,int b,int c)
        {
            if(a==b||b==c||c==a)return;
            var pa=verts[a].Position;var pb=verts[b].Position;var pc=verts[c].Position;var center=new Point3D((pa.X+pb.X+pc.X)/3,(pa.Y+pb.Y+pc.Y)/3,(pa.Z+pb.Z+pc.Z)/3);
            var eps=0.5;double S(Point3D p)=>primitives.Min(q=>q.SignedDistance(p));
            var grad=new Vector3D(S(center+new Vector3D(eps,0,0))-S(center-new Vector3D(eps,0,0)),S(center+new Vector3D(0,eps,0))-S(center-new Vector3D(0,eps,0)),S(center+new Vector3D(0,0,eps))-S(center-new Vector3D(0,0,eps)));
            if((pb-pa).Cross(pc-pa).Dot(grad)<0)(b,c)=(c,b);
            var regions=new[]{verts[a].Region,verts[b].Region,verts[c].Region};var region=regions.GroupBy(r=>r).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).First().Key;
            faces.Add(new($"f:{faces.Count:D6}",a,b,c,region,null));
        }
    }

    private static (List<HumanoidVertex>,List<HumanoidFace>,IReadOnlyList<HumanoidComponent>) AddEyes(List<HumanoidVertex> vertices,List<HumanoidFace> faces)
    {
        var outerFaces=Enumerable.Range(0,faces.Count).ToArray();var left=new List<int>();var right=new List<int>();
        AddEye(new(-35,91,1572),HumanoidRegionKind.LeftEye,left);AddEye(new(35,91,1572),HumanoidRegionKind.RightEye,right);
        return(vertices,faces,[new("component:outer-skin",HumanoidComponentKind.OuterSkin,outerFaces,["mouth seam is surface-only in X0; no oral cavity opening"]),new("component:left-eye",HumanoidComponentKind.LeftEye,left,[]),new("component:right-eye",HumanoidComponentKind.RightEye,right,[])]);
        void AddEye(Point3D center,HumanoidRegionKind region,List<int> target)
        {
            const int rings=8,segments=16;var top=vertices.Count;vertices.Add(new($"v:{vertices.Count:D6}",center+new Vector3D(0,0,12),region,-1));var baseIndex=vertices.Count;
            for(var r=1;r<rings;r++){var phi=Math.PI*r/rings;for(var s=0;s<segments;s++){var theta=2*Math.PI*s/segments;var p=new Point3D(center.X+12*Math.Sin(phi)*Math.Cos(theta),center.Y+12*Math.Sin(phi)*Math.Sin(theta),center.Z+12*Math.Cos(phi));vertices.Add(new($"v:{vertices.Count:D6}",p,region,-1));}}
            var bottom=vertices.Count;vertices.Add(new($"v:{vertices.Count:D6}",center-new Vector3D(0,0,12),region,-1));
            for(var s=0;s<segments;s++){var n=(s+1)%segments;Add(top,baseIndex+s,baseIndex+n);for(var r=0;r<rings-2;r++){var a=baseIndex+r*segments+s;var b=baseIndex+(r+1)*segments+s;var c=baseIndex+(r+1)*segments+n;var d=baseIndex+r*segments+n;Add(a,b,c);Add(a,c,d);}var last=baseIndex+(rings-2)*segments;Add(last+n,last+s,bottom);}
            void Add(int a,int b,int c){target.Add(faces.Count);faces.Add(new($"f:{faces.Count:D6}",a,b,c,region,null));}
        }
    }

    private static IReadOnlyList<HumanoidVertex> AssignVertexSymmetry(IReadOnlyList<HumanoidVertex> input)
    {
        static string Key(Point3D p)=>$"{Math.Round(p.X,6):F6}|{Math.Round(p.Y,6):F6}|{Math.Round(p.Z,6):F6}";
        var groups=input.Select((v,i)=>(Key(v.Position),i)).GroupBy(x=>x.Item1).ToDictionary(g=>g.Key,g=>g.Select(x=>x.i).Order().ToArray());var result=new HumanoidVertex[input.Count];
        for(var i=0;i<input.Count;i++){var v=input[i];var own=groups[Key(v.Position)];var ordinal=Array.IndexOf(own,i);var reflected=groups.GetValueOrDefault(Key(new(-v.Position.X,v.Position.Y,v.Position.Z)));var pair=reflected is null||ordinal>=reflected.Length?i:reflected[ordinal];result[i]=v with{SymmetryPartnerIndex=pair};}
        return result;
    }
    private static IReadOnlyList<HumanoidFace> AssignFaceSymmetry(IReadOnlyList<HumanoidFace> input,IReadOnlyList<HumanoidVertex> vertices)
    {
        static string Key(int a,int b,int c)=>string.Join("|",new[]{a,b,c}.Order());var map=input.Select((f,i)=>(Key(f.A,f.B,f.C),i)).ToDictionary(x=>x.Item1,x=>x.i);var result=new HumanoidFace[input.Count];
        for(var i=0;i<input.Count;i++){var f=input[i];var pair=map.TryGetValue(Key(vertices[f.A].SymmetryPartnerIndex,vertices[f.B].SymmetryPartnerIndex,vertices[f.C].SymmetryPartnerIndex),out var p)?p:(int?)null;result[i]=f with{SymmetryPartnerIndex=pair};}return result;
    }

    private static HumanoidSkeleton BuildSkeleton()
    {
        var specs=new List<(HumanoidJointKind Kind,HumanoidJointKind? Parent,Point3D Global,AnatomicalSide Side)>();
        void Add(HumanoidJointKind k,HumanoidJointKind? p,double x,double y,double z,AnatomicalSide s=AnatomicalSide.Center)=>specs.Add((k,p,new(x,y,z),s));
        Add(HumanoidJointKind.Root,null,0,0,0);Add(HumanoidJointKind.Pelvis,HumanoidJointKind.Root,0,0,930);Add(HumanoidJointKind.SpineLower,HumanoidJointKind.Pelvis,0,0,1050);Add(HumanoidJointKind.SpineMid,HumanoidJointKind.SpineLower,0,0,1190);Add(HumanoidJointKind.Chest,HumanoidJointKind.SpineMid,0,0,1340);Add(HumanoidJointKind.Neck,HumanoidJointKind.Chest,0,0,1465);Add(HumanoidJointKind.Head,HumanoidJointKind.Neck,0,0,1575);
        AddSide(AnatomicalSide.Left,-1);AddSide(AnatomicalSide.Right,1);
        var index=specs.Select((x,i)=>(x.Kind,i)).ToDictionary(x=>x.Kind,x=>x.i);var joints=new List<HumanoidJoint>();
        foreach(var spec in specs){var parent=spec.Parent is null?(int?)null:index[spec.Parent.Value];var parentGlobal=spec.Parent is null?Point3D.Origin:specs[parent!.Value].Global;var local=spec.Global-parentGlobal;var global=Matrix4x4.CreateTranslation((float)spec.Global.X,(float)spec.Global.Y,(float)spec.Global.Z);Matrix4x4.Invert(global,out var inverse);joints.Add(new($"joint:{spec.Kind}",spec.Kind,spec.Side,parent,new(new(local.X,local.Y,local.Z),Quaternion.Identity),global,inverse,spec.Kind is HumanoidJointKind.LeftEye or HumanoidJointKind.RightEye or HumanoidJointKind.Jaw));}
        var symmetry=Enum.GetValues<HumanoidJointKind>().ToDictionary(k=>k,k=>Swap(k));return new(CanonicalAdultStandardV1.SkeletonId,CanonicalAdultStandardV1.RestPoseId,"linear-blend-skinning; System.Numerics row-vector matrices; pWorld=pModel*InverseBind*GlobalPose",joints,symmetry);
        void AddSide(AnatomicalSide side,int sign)
        {
            var left=side==AnatomicalSide.Left;
            HumanoidJointKind K(string stem)=>Enum.Parse<HumanoidJointKind>((left?"Left":"Right")+stem);
            Add(K("Clavicle"),HumanoidJointKind.Chest,sign*85,0,1370,side);Add(K("Shoulder"),K("Clavicle"),sign*185,0,1345,side);Add(K("Elbow"),K("Shoulder"),sign*365,0,1100,side);Add(K("Wrist"),K("Elbow"),sign*525,0,875,side);
            Add(K("Hip"),HumanoidJointKind.Pelvis,sign*88,0,925,side);Add(K("Knee"),K("Hip"),sign*92,8,520,side);Add(K("Ankle"),K("Knee"),sign*88,4,92,side);Add(K("ToeBase"),K("Ankle"),sign*88,125,40,side);
            var fingerOffsets=new[]{("Thumb",-36d,65d),("Index",-20d,80d),("Middle",-7d,86d),("Ring",8d,81d),("Little",22d,69d)};
            foreach(var (name,y,len) in fingerOffsets)
            {
                var first=name=="Thumb"?"Metacarpal":"Proximal";var second=name=="Thumb"?"Proximal":"Intermediate";var third="Distal";
                var baseX=sign*575;Add(K(name+first),K("Wrist"),baseX,y,835,side);Add(K(name+second),K(name+first),baseX+sign*len*.42,y,810,side);Add(K(name+third),K(name+second),baseX+sign*len*.76,y,790,side);
            }
            Add(K("Eye"),HumanoidJointKind.Head,sign*35,91,1572,side);
        }
        static HumanoidJointKind Swap(HumanoidJointKind kind){var text=kind.ToString();if(text.StartsWith("Left"))return Enum.Parse<HumanoidJointKind>("Right"+text[4..]);if(text.StartsWith("Right"))return Enum.Parse<HumanoidJointKind>("Left"+text[5..]);return kind;}
    }

    private static IReadOnlyList<VertexSkinWeights> GenerateWeights(IReadOnlyList<HumanoidVertex> vertices,HumanoidSkeleton skeleton)
    {
        var byKind=skeleton.Joints.Select((j,i)=>(j.Kind,i)).ToDictionary(x=>x.Kind,x=>x.i);var result=new VertexSkinWeights[vertices.Count];
        for(var i=0;i<vertices.Count;i++)
        {
            var kinds=CandidateJoints(vertices[i].Region);var candidates=kinds.Select(k=>(Index:byKind[k],Distance:Distance(vertices[i].Position,skeleton.Joints[byKind[k]].GlobalBind))).OrderBy(x=>x.Distance).ThenBy(x=>x.Index).Take(2).ToArray();
            var raw=candidates.Select(x=>1d/double.Max(1,x.Distance*x.Distance)).ToArray();var sum=raw.Sum();result[i]=new(i,candidates.Select((x,n)=>new JointWeight(x.Index,raw[n]/sum)).ToArray());
        }return result;
        static double Distance(Point3D p,Matrix4x4 m)=>new Vector3D(p.X-m.M41,p.Y-m.M42,p.Z-m.M43).Length;
        static HumanoidJointKind[] CandidateJoints(HumanoidRegionKind r)
        {
            var text=r.ToString();var left=text.StartsWith("Left");var right=text.StartsWith("Right");var prefix=left?"Left":right?"Right":"";
            HumanoidJointKind K(string suffix)=>Enum.Parse<HumanoidJointKind>(prefix+suffix);
            if(text.Contains("Eye"))return[K("Eye"),HumanoidJointKind.Head];if(text.Contains("Ear")||r is HumanoidRegionKind.Head or HumanoidRegionKind.Face)return[HumanoidJointKind.Head,HumanoidJointKind.Neck];
            if(r==HumanoidRegionKind.Neck)return[HumanoidJointKind.Neck,HumanoidJointKind.Head];if(r==HumanoidRegionKind.Chest)return[HumanoidJointKind.Chest,HumanoidJointKind.SpineMid];if(r==HumanoidRegionKind.Abdomen)return[HumanoidJointKind.SpineLower,HumanoidJointKind.SpineMid];if(r==HumanoidRegionKind.Pelvis)return[HumanoidJointKind.Pelvis,HumanoidJointKind.SpineLower];
            if(text.Contains("Shoulder")||text.Contains("UpperArm"))return[K("Shoulder"),K("Elbow")];if(text.Contains("Elbow")||text.Contains("Forearm"))return[K("Elbow"),K("Wrist")];if(text.Contains("Wrist")||text.Contains("Hand"))return[K("Wrist"),K("MiddleProximal")];
            foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"})if(text.Contains(finger))return finger=="Thumb"?[K("ThumbMetacarpal"),K("ThumbProximal"),K("ThumbDistal")]:[K(finger+"Proximal"),K(finger+"Intermediate"),K(finger+"Distal")];
            if(text.Contains("Thigh"))return[K("Hip"),K("Knee")];if(text.Contains("Knee")||text.Contains("Shin"))return[K("Knee"),K("Ankle")];if(text.Contains("Ankle")||text.Contains("Foot")||text.Contains("Toes"))return[K("Ankle"),K("ToeBase")];return[HumanoidJointKind.Pelvis];
        }
    }

    private static IReadOnlyList<HumanoidLandmark> BuildLandmarks(HumanoidSurface surface,HumanoidSkeleton skeleton)
    {
        var desired=new List<(string Id,Point3D P,string? Pair,string Protocol)>
        {
            ("surface.head.crown",new(0,0,1750),null,"Highest reviewed outer-skin point excluding hair in measurement pose."),("surface.face.chin",new(0,105,1475),null,"Most inferior reviewed chin surface point."),("surface.face.nose-tip",new(0,145,1570),null,"Most anterior reviewed nasal tip."),
            ("surface.torso.sternum",new(0,128,1320),null,"Anterior midline sternum proxy."),("surface.torso.navel",new(0,110,1070),null,"Reviewed anterior navel proxy."),
        };
        AddPair("face.eye-inner",new(-17,103,1575),new(17,103,1575),"Reviewed medial eyelid corner proxy.");AddPair("face.eye-outer",new(-54,100,1575),new(54,100,1575),"Reviewed lateral eyelid corner proxy.");AddPair("face.mouth-corner",new(-31,122,1520),new(31,122,1520),"Reviewed lip commissure proxy.");AddPair("head.ear",new(-112,0,1570),new(112,0,1570),"Tragus-like surface proxy; not medical landmark.");
        AddPair("shoulder.acromion",new(-210,20,1340),new(210,20,1340),"Acromion-like outer-skin proxy.");AddPair("pelvis.asis",new(-120,112,955),new(120,112,955),"ASIS-like outer-skin proxy.");AddPair("elbow.tip",new(-365,-45,1100),new(365,-45,1100),"Posterior elbow surface proxy.");AddPair("wrist.ulnar",new(-525,-35,875),new(525,-35,875),"Ulnar wrist surface proxy.");AddPair("hand.knuckle-middle",new(-595,0,820),new(595,0,820),"Middle metacarpal knuckle proxy.");AddPair("hand.middle-tip",new(-680,-7,780),new(680,-7,780),"Middle fingertip proxy.");AddPair("knee.patella",new(-92,82,520),new(92,82,520),"Anterior patella-like surface proxy.");AddPair("ankle.lateral",new(-148,4,92),new(148,4,92),"Lateral ankle surface proxy.");AddPair("foot.heel",new(-88,-82,45),new(88,-82,45),"Posterior heel point on outer envelope.");AddPair("foot.toe-tip",new(-88,225,38),new(88,225,38),"Longest-toe envelope tip.");
        var result=new List<HumanoidLandmark>();foreach(var item in desired){var vi=NearestVertex(surface.Vertices,item.P);var fi=surface.Faces.Select((f,i)=>(f,i)).First(x=>x.f.A==vi||x.f.B==vi||x.f.C==vi).i;var f=surface.Faces[fi];var bary=f.A==vi?(1d,0d,0d):f.B==vi?(0d,1d,0d):(0d,0d,1d);result.Add(new SurfaceLandmark(item.Id,new(surface.TopologyId,f.Id,0,bary.Item1,bary.Item2,bary.Item3),item.Protocol,.85,item.Pair,"Aetheris authored template; nearest deterministic surface binding; requires human review",false));}
        foreach(var joint in skeleton.Joints)result.Add(new JointLandmark("joint."+joint.Kind,new(skeleton.SkeletonId,joint.Kind,Vector3D.Zero),"Canonical semantic joint origin in shaped rest/bind pose.",1,JointPair(joint.Kind),"Aetheris canonical skeleton",true));
        return result;
        void AddPair(string stem,Point3D left,Point3D right,string protocol){var l="surface."+stem+".left";var r="surface."+stem+".right";desired.Add((l,left,r,protocol));desired.Add((r,right,l,protocol));}
        static int NearestVertex(IReadOnlyList<HumanoidVertex> vertices,Point3D p)=>vertices.Select((v,i)=>(D:(v.Position-p).LengthSquared,I:i)).OrderBy(x=>x.D).ThenBy(x=>x.I).First().I;
        static string? JointPair(HumanoidJointKind k){var s=k.ToString();return s.StartsWith("Left")?"joint.Right"+s[4..]:s.StartsWith("Right")?"joint.Left"+s[5..]:null;}
    }

    private static IReadOnlyList<MeasurementProtocol> BuildMeasurementProtocols()=>
    [
        new("measure.height.v1",HumanoidMeasurementId.Height,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.VerticalHeight,["surface.head.crown","sole-plane"],"Crown height above common sole support plane; hair excluded."),
        new("measure.shoulder-width.v1",HumanoidMeasurementId.ShoulderWidth,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.StraightDistance,["surface.shoulder.acromion.left","surface.shoulder.acromion.right"],"Straight 3D distance between reviewed acromion-like proxies."),
        new("measure.hip-width.v1",HumanoidMeasurementId.HipWidth,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.StraightDistance,["surface.pelvis.asis.left","surface.pelvis.asis.right"],"X0 proxy distance; not outer-skin breadth or population anthropometry."),
        new("measure.arm-length.v1",HumanoidMeasurementId.ArmLength,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.JointChainLength,["joint.LeftShoulder","joint.LeftElbow","joint.LeftWrist"],"Left canonical link-chain length; right must agree by symmetry."),
        new("measure.leg-length.v1",HumanoidMeasurementId.LegLength,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.JointChainLength,["joint.LeftHip","joint.LeftKnee","joint.LeftAnkle"],"Left canonical link-chain length; right must agree by symmetry."),
        new("measure.foot-length.v1",HumanoidMeasurementId.FootLength,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.ProjectedExtent,["surface.foot.heel.left","surface.foot.toe-tip.left"],"Heel-to-longest-toe distance projected on foot sagittal axis."),
        new("measure.hand-length.v1",HumanoidMeasurementId.HandLength,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.StraightDistance,["joint.LeftWrist","surface.hand.middle-tip.left"],"Wrist joint proxy to middle fingertip in extended-hand measurement pose."),
        new("measure.chest-depth.v1",HumanoidMeasurementId.ChestDepth,CanonicalAdultStandardV1.MeasurementPoseId,"mm",MeasurementEvaluatorKind.ProjectedExtent,["region:Chest"],"Anterior/posterior extent of declared chest region in measurement pose; only valid when section selection succeeds.")
    ];
    private static IReadOnlyList<HumanoidMorphChannel> BuildMorphChannels()=>
    [
        new(HumanoidMorphId.Height,HumanoidMorphKind.MeasuredDimension,1650,1750,1850,"mm",[HumanoidRegionKind.Pelvis,HumanoidRegionKind.Abdomen,HumanoidRegionKind.Chest,HumanoidRegionKind.Neck,HumanoidRegionKind.Head,HumanoidRegionKind.LeftThigh,HumanoidRegionKind.RightThigh,HumanoidRegionKind.LeftShin,HumanoidRegionKind.RightShin],"piecewise stature distribution across legs and torso; widths unchanged","min/default/max topology and measurement direction"),
        new(HumanoidMorphId.ArmLength,HumanoidMorphKind.MeasuredDimension,-50,0,50,"mm",[HumanoidRegionKind.LeftUpperArm,HumanoidRegionKind.RightUpperArm,HumanoidRegionKind.LeftForearm,HumanoidRegionKind.RightForearm,HumanoidRegionKind.LeftHand,HumanoidRegionKind.RightHand],"bilateral weighted extension along shoulder-elbow-wrist chains","min/default/max topology and arm-length direction"),
        new(HumanoidMorphId.LegLength,HumanoidMorphKind.MeasuredDimension,-60,0,60,"mm",[HumanoidRegionKind.LeftThigh,HumanoidRegionKind.RightThigh,HumanoidRegionKind.LeftShin,HumanoidRegionKind.RightShin,HumanoidRegionKind.LeftFoot,HumanoidRegionKind.RightFoot],"bilateral weighted extension along hip-knee-ankle chains with sole plane fixed","min/default/max topology and leg-length direction"),
        new(HumanoidMorphId.ShoulderWidth,HumanoidMorphKind.MeasuredDimension,-40,0,40,"mm",[HumanoidRegionKind.Chest,HumanoidRegionKind.LeftShoulder,HumanoidRegionKind.RightShoulder],"symmetric transverse shoulder translation with torso blend","min/default/max measurement direction")
    ];

    private static IReadOnlyList<HumanoidAttachmentSite> BuildAttachments(HumanoidSurface surface,IReadOnlyList<HumanoidLandmark> landmarks)
    {
        var definitions=new[]{("Head","surface.head.crown",HumanoidJointKind.Head),("Face","surface.face.nose-tip",HumanoidJointKind.Head),("Chest","surface.torso.sternum",HumanoidJointKind.Chest),("UpperBack","surface.torso.sternum",HumanoidJointKind.Chest),("Waist","surface.torso.navel",HumanoidJointKind.SpineLower),
            ("UpperArm.Left","surface.shoulder.acromion.left",HumanoidJointKind.LeftShoulder),("UpperArm.Right","surface.shoulder.acromion.right",HumanoidJointKind.RightShoulder),("Wrist.Left","surface.wrist.ulnar.left",HumanoidJointKind.LeftWrist),("Wrist.Right","surface.wrist.ulnar.right",HumanoidJointKind.RightWrist),("Hand.Left","surface.hand.knuckle-middle.left",HumanoidJointKind.LeftWrist),("Hand.Right","surface.hand.knuckle-middle.right",HumanoidJointKind.RightWrist),("Thigh.Left","surface.pelvis.asis.left",HumanoidJointKind.LeftHip),("Thigh.Right","surface.pelvis.asis.right",HumanoidJointKind.RightHip),("Shin.Left","surface.knee.patella.left",HumanoidJointKind.LeftKnee),("Shin.Right","surface.knee.patella.right",HumanoidJointKind.RightKnee),("Ankle.Left","surface.ankle.lateral.left",HumanoidJointKind.LeftAnkle),("Ankle.Right","surface.ankle.lateral.right",HumanoidJointKind.RightAnkle),("Foot.Left","surface.foot.toe-tip.left",HumanoidJointKind.LeftToeBase),("Foot.Right","surface.foot.toe-tip.right",HumanoidJointKind.RightToeBase)};
        return definitions.Select(d=>{var l=(SurfaceLandmark)landmarks.Single(x=>x.Id==d.Item2);var p=Resolve(surface,l.Binding);var up=new Vector3D(0,0,1);var normal=d.Item1=="UpperBack"?new Vector3D(0,-1,0):d.Item1 is "Chest" or "Face"?new Vector3D(0,1,0):new Vector3D(p.X<0?-1:p.X>0?1:0,0,p.X==0?1:0);if(!normal.TryNormalize(out normal))normal=new(0,1,0);var tangent=up.Cross(normal);if(!tangent.TryNormalize(out tangent))tangent=new(1,0,0);var binormal=normal.Cross(tangent);return new HumanoidAttachmentSite("site:"+d.Item1,l.Binding,new(p,tangent,normal,binormal),d.Item3,"surface-bound neutral frame follows declared joint; no clearance offset in X0");}).ToArray();
    }
    internal static Point3D Resolve(HumanoidSurface surface,SurfaceBinding binding)
    {
        var f=surface.Faces.Single(x=>x.Id==binding.FaceId);var a=surface.Vertices[f.A].Position;var b=surface.Vertices[f.B].Position;var c=surface.Vertices[f.C].Position;return new(a.X*binding.BarycentricA+b.X*binding.BarycentricB+c.X*binding.BarycentricC,a.Y*binding.BarycentricA+b.Y*binding.BarycentricB+c.Y*binding.BarycentricC,a.Z*binding.BarycentricA+b.Z*binding.BarycentricB+c.Z*binding.BarycentricC);
    }
    private static IReadOnlyDictionary<HumanoidRegionKind,HumanoidRegionKind> RegionSymmetry()=>Enum.GetValues<HumanoidRegionKind>().ToDictionary(r=>r,r=>{var s=r.ToString();return s.StartsWith("Left")?Enum.Parse<HumanoidRegionKind>("Right"+s[4..]):s.StartsWith("Right")?Enum.Parse<HumanoidRegionKind>("Left"+s[5..]):r;});
    private static string ConnectivityHash(IEnumerable<HumanoidFace> faces)=>Hash(string.Join(";",faces.Select(f=>$"{f.A},{f.B},{f.C}")));
    private static string Hash(string text)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
