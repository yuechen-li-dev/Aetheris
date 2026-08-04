using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

public sealed record CubicLatticeNode(string Id, int I, int J, int K, Point3D Position, int Valence);
public sealed record CubicLatticeMember(string Id, string StartNodeId, string EndNodeId, string Axis, Point3D Start, Point3D End);
public sealed record CubicLatticeConstruction(IReadOnlyList<(int X,int Y,int Z)> Cells, IReadOnlyList<CubicLatticeNode> Nodes, IReadOnlyList<CubicLatticeMember> Members, string Signature);
public sealed record LatticeNodeBRepPlan(string NodeId, IReadOnlyList<string> MemberIds, int Valence);
public sealed record LatticeMemberBRepPlan(string MemberId, string StartNodeId, string EndNodeId, string Axis, double ExposedLength);
public sealed record LatticeBodyBRepPlan(CubicLatticeConstruction Construction, IReadOnlyList<LatticeNodeBRepPlan> Nodes, IReadOnlyList<LatticeMemberBRepPlan> Members, int SeamCount, string Signature)
{ public bool IsAuthoritative => true; }
public sealed record CubicLatticeRealization(LatticeBodyBRepPlan Plan, BrepBody Body, double AnalyticVolume);

/// <summary>Bounded exact analytic M9R lattice plan.  It directly creates shared sphere/cylinder seams; no Boolean operation is used.</summary>
public static class CubicLatticeBRepPlanner
{
    public const string NodeRadiusTooSmallForStruts = "NodeRadiusTooSmallForStruts";
    public const string MemberConsumedByNodes = "MemberConsumedByNodes";

    public static KernelResult<CubicLatticeRealization> Create(int nx, int ny, int nz, double cellSize, double strutRadius, double nodeRadius, Point3D? center = null)
    {
        if (nx <= 0 || ny <= 0 || nz <= 0 || !double.IsFinite(cellSize) || !double.IsFinite(strutRadius) || !double.IsFinite(nodeRadius) || cellSize <= 0 || strutRadius <= 0 || nodeRadius <= 0)
            return Failure("InvalidCubicLatticeParameters");
        if (nodeRadius <= double.Sqrt(2d) * strutRadius) return Failure(NodeRadiusTooSmallForStruts);
        var seamOffset = double.Sqrt(nodeRadius * nodeRadius - strutRadius * strutRadius);
        if (cellSize - 2d * seamOffset <= 1e-9) return Failure(MemberConsumedByNodes);

        var construction = BuildGraph(nx, ny, nz, cellSize, center ?? new Point3D(0d, 0d, 0d));
        var nodePlans = construction.Nodes.Select(n => new LatticeNodeBRepPlan(n.Id, construction.Members.Where(m => m.StartNodeId == n.Id || m.EndNodeId == n.Id).Select(m => m.Id).Order().ToArray(), n.Valence)).ToArray();
        var memberPlans = construction.Members.Select(m => new LatticeMemberBRepPlan(m.Id, m.StartNodeId, m.EndNodeId, m.Axis, cellSize - 2d * seamOffset)).ToArray();
        var planText = construction.Signature + "|" + string.Join("|", memberPlans.Select(m => m.MemberId));
        var plan = new LatticeBodyBRepPlan(construction, nodePlans, memberPlans, construction.Members.Count * 2, Hash(planText));
        var body = BuildBody(construction, strutRadius, nodeRadius, seamOffset);
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return KernelResult<CubicLatticeRealization>.Failure(preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => new Diagnostics.KernelDiagnostic(Diagnostics.KernelDiagnosticCode.ValidationFailed, Diagnostics.KernelDiagnosticSeverity.Error, d.Code, d.Context)).ToArray());
        var capHeight = nodeRadius - seamOffset;
        var capVolume = double.Pi * capHeight * capHeight * (nodeRadius - capHeight / 3d);
        var analytic = construction.Nodes.Count * (4d / 3d * double.Pi * nodeRadius * nodeRadius * nodeRadius)
            - construction.Members.Count * 2d * capVolume
            + construction.Members.Count * double.Pi * strutRadius * strutRadius * (cellSize - 2d * seamOffset);
        return KernelResult<CubicLatticeRealization>.Success(new(plan, body, analytic));
    }

    public static CubicLatticeConstruction BuildGraph(int nx, int ny, int nz, double cellSize, Point3D? center = null)
    {
        var nodes = new List<CubicLatticeNode>();
        var members = new List<CubicLatticeMember>();
        var domainCenter = center ?? new Point3D(0d, 0d, 0d);
        var origin = new Vector3D(domainCenter.X - cellSize * nx / 2d, domainCenter.Y - cellSize * ny / 2d, domainCenter.Z - cellSize * nz / 2d);
        for (var i=0;i<=nx;i++) for (var j=0;j<=ny;j++) for (var k=0;k<=nz;k++)
        {
            // Boundary dimensions remove one incident direction each.
            var v = 6 - ((i==0?1:0)+(i==nx?1:0)+(j==0?1:0)+(j==ny?1:0)+(k==0?1:0)+(k==nz?1:0));
            nodes.Add(new($"cubic:node:{i}:{j}:{k}",i,j,k,new Point3D(origin.X+i*cellSize,origin.Y+j*cellSize,origin.Z+k*cellSize),v));
        }
        var byId=nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        foreach(var n in nodes)
        {
            Add(n, n.I<nx ? (n.I+1,n.J,n.K) : (-1,-1,-1), "X");
            Add(n, n.J<ny ? (n.I,n.J+1,n.K) : (-1,-1,-1), "Y");
            Add(n, n.K<nz ? (n.I,n.J,n.K+1) : (-1,-1,-1), "Z");
        }
        var cells=(from i in Enumerable.Range(0,nx) from j in Enumerable.Range(0,ny) from k in Enumerable.Range(0,nz) select (i,j,k)).ToArray();
        var text=string.Join("|",nodes.Select(n=>n.Id))+";"+string.Join("|",members.Select(m=>m.Id));
        return new(cells,nodes,members,Hash(text));
        void Add(CubicLatticeNode a,(int I,int J,int K) q,string axis) { if(q.I<0)return; var b=byId[$"cubic:node:{q.I}:{q.J}:{q.K}"]; members.Add(new($"cubic:member:{axis}:{a.Id}:{b.Id}",a.Id,b.Id,axis,a.Position,b.Position)); }
    }

    private static BrepBody BuildBody(CubicLatticeConstruction c, double rs, double rn, double d)
    {
        var b=new TopologyBuilder(); var g=new BrepGeometryStore(); var bind=new BrepBindingModel(); var points=new Dictionary<VertexId,Point3D>();
        var nodeMembers=c.Nodes.ToDictionary(n=>n.Id,_=>new List<(CubicLatticeMember M,bool Start)>(),StringComparer.Ordinal);
        foreach(var m in c.Members){nodeMembers[m.StartNodeId].Add((m,true));nodeMembers[m.EndNodeId].Add((m,false));}
        var seam=new Dictionary<(string,string),EdgeId>();
        foreach(var n in c.Nodes.OrderBy(n=>n.Id,StringComparer.Ordinal))
        {
            var loops=new List<IReadOnlyList<Use>>();
            foreach(var x in nodeMembers[n.Id].OrderBy(x=>x.M.Id,StringComparer.Ordinal))
            { var e=AddSeam(n,x.M,x.Start); seam[(x.M.Id,n.Id)]=e; loops.Add([new Use(e,false)]); }
            var f=AddFace(b,loops); AddSurface(f,SurfaceGeometry.FromSphere(new SphereSurface(n.Position,Z(),rn,X())),g,bind);
        }
        foreach(var m in c.Members.OrderBy(m=>m.Id,StringComparer.Ordinal))
        {
            var axis=Direction3D.Create(m.End-m.Start); var f=AddFace(b,[[new Use(seam[(m.Id,m.StartNodeId)],false)],[new Use(seam[(m.Id,m.EndNodeId)],true)]]);
            AddSurface(f,SurfaceGeometry.FromCylinder(new CylinderSurface(m.Start,axis,rs,Reference(axis))),g,bind);
        }
        var shell=b.AddShell(b.Model.Faces.Select(f=>f.Id).ToArray());b.AddBody([shell]);return new BrepBody(b.Model,g,bind,points);
        EdgeId AddSeam(CubicLatticeNode n,CubicLatticeMember m,bool start){var axis=Direction3D.Create(m.End-m.Start);var dir=start?axis.ToVector():-axis.ToVector();var center=n.Position+dir*d;var refAxis=Reference(axis);var v=b.AddVertex();points[v]=center+refAxis.ToVector()*rs;var e=b.AddEdge(v,v);var cid=new CurveGeometryId(g.Curves.Count()+1);g.AddCurve(cid,CurveGeometry.FromCircle(new Circle3Curve(center,axis,rs,refAxis)));bind.AddEdgeBinding(new EdgeGeometryBinding(e,cid,new ParameterInterval(0,2d*double.Pi)));return e;}
    }
    private static Direction3D X()=>Direction3D.Create(new Vector3D(1,0,0)); private static Direction3D Z()=>Direction3D.Create(new Vector3D(0,0,1));
    private static Direction3D Reference(Direction3D a)=>System.Math.Abs(a.ToVector().Z)<.9?Z():X();
    private static void AddSurface(FaceId f,SurfaceGeometry s,BrepGeometryStore g,BrepBindingModel b){var id=new SurfaceGeometryId(g.Surfaces.Count()+1);g.AddSurface(id,s);b.AddFaceBinding(new FaceGeometryBinding(f,id));}
    private static FaceId AddFace(TopologyBuilder b,IReadOnlyList<IReadOnlyList<Use>> loops){var ids=new List<LoopId>();foreach(var uses in loops){var l=b.AllocateLoopId();var cs=uses.Select(_=>b.AllocateCoedgeId()).ToArray();for(var i=0;i<cs.Length;i++)b.AddCoedge(new Coedge(cs[i],uses[i].Edge,l,cs[(i+1)%cs.Length],cs[(i+cs.Length-1)%cs.Length],uses[i].Reverse));b.AddLoop(new Loop(l,cs));ids.Add(l);}return b.AddFace(ids);}
    private static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    private static KernelResult<CubicLatticeRealization> Failure(string code)=>KernelResult<CubicLatticeRealization>.Failure([new Diagnostics.KernelDiagnostic(Diagnostics.KernelDiagnosticCode.ValidationFailed,Diagnostics.KernelDiagnosticSeverity.Error,code,"CubicLattice")]);
    private readonly record struct Use(EdgeId Edge,bool Reverse);
}
