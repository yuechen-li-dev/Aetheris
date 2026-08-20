using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.Kernel.Firmament.Piping;

/// <summary>Bounded X3 semantic piping front end for explicit and accepted orthogonal routes.</summary>
public static class PipingAuthoring
{
    private const double Tol=1e-7;
    public static bool IsPipingSource(string source)=>Regex.IsMatch(source,@"\bPipingSystem\s+[A-Za-z_]\w*\s*\{",RegexOptions.CultureInvariant);

    public static PipingCompilationResult Compile(string source,string sourceIdentity="memory")
    {
        var diagnostics=new List<string>();var parseClock=System.Diagnostics.Stopwatch.StartNew();
        var parsed=Parse(source,sourceIdentity,diagnostics);parseClock.Stop();
        if(parsed is null||diagnostics.Count>0)return new(false,null,null,diagnostics);
        ValidateNozzleContracts(parsed,diagnostics);
        if(diagnostics.Count>0)return new(false,null,null,diagnostics);
        var obstacleClock=System.Diagnostics.Stopwatch.StartNew();var obstacles=parsed.KeepOuts;obstacleClock.Stop();
        var routeClock=System.Diagnostics.Stopwatch.StartNew();
        var routes=new List<RouteIr>(parsed.Routes);var proposals=new List<RouteProposalIr>();
        foreach(var request in parsed.Requests)
        {
            var proposal=OrthogonalAutoRouter.Propose(request.Request);proposals.Add(proposal);
            if(!proposal.IsSuccess){diagnostics.AddRange(proposal.Diagnostics);continue;}
            if(request.AcceptAs.Length>0)routes.Add(OrthogonalAutoRouter.Accept(proposal,request.Request,request.AcceptAs));
        }
        foreach(var edit in parsed.Reroutes)
        {
            var index=routes.FindIndex(x=>x.Name==edit.Route);
            if(index<0){diagnostics.Add($"piping-local-reroute-unknown-route:{edit.Name}:{edit.Route}");continue;}
            var connection=parsed.Connections.Single(x=>x.Name==routes[index].Connection);
            var environment=RequestFor(connection,parsed,edit.Name,edit.Clearance);
            var extra=edit.Avoid.Select(name=>parsed.KeepOuts.SingleOrDefault(x=>x.Name==name)).Where(x=>x is not null).Cast<KeepOutIr>().ToArray();
            if(extra.Length!=edit.Avoid.Count){diagnostics.Add($"piping-local-reroute-unknown-keepout:{edit.Name}");continue;}
            var rerouted=OrthogonalAutoRouter.Reroute(routes[index],environment,edit.FromAnchor,edit.ToAnchor,extra);
            if(rerouted.Route is null)diagnostics.AddRange(rerouted.Diagnostics);else routes[index]=rerouted.Route;
        }
        routeClock.Stop();
        ValidateRoutes(routes,parsed,diagnostics);
        if(diagnostics.Count>0)return new(false,null,null,diagnostics);

        var simplifyClock=System.Diagnostics.Stopwatch.StartNew();
        routes=routes.Select(Canonicalize).ToList();simplifyClock.Stop();
        var fittingClock=System.Diagnostics.Stopwatch.StartNew();
        var segments=new List<PipeSegmentComponentIr>();var fittings=new List<PipingFittingComponentIr>();
        foreach(var route in routes.OrderBy(x=>x.StableId,StringComparer.Ordinal))
        {
            var connection=parsed.Connections.Single(x=>x.Name==route.Connection);var policy=parsed.Policies.Single(x=>x.Name==connection.PipePolicy);
            var bend=route.Turns.FirstOrDefault()?.BendRadiusMm??Math.Max(50,policy.OuterDiameterMm*2);
            for(var i=0;i<route.Segments.Count;i++)
            {
                var run=PipingGeometry.TrimmedRun(route.Anchors,i,bend);var body=PipingGeometry.Pipe(run.Start,run.End,policy);
                if(!body.IsSuccess||body.Value is null){diagnostics.AddRange(body.Diagnostics.Select(x=>$"piping-segment-geometry:{route.Name}:{i}:{x.Message}"));continue;}
                var length=(run.End-run.Start).Length;segments.Add(new($"pipe-segment:{route.Name}:{i}",route.Name,i,policy.StableId,policy.Material,
                    i==0?$"interface:pipe-segment:{route.Name}:{i}:Start":$"fitting:{route.Name}:{i}:Outlet",i==route.Segments.Count-1?$"interface:pipe-segment:{route.Name}:{i}:End":$"fitting:{route.Name}:{i+1}:Inlet",length,length,body.Value));
            }
            for(var i=1;i<route.Anchors.Count-1;i++)
            {
                var incoming=route.Anchors[i].Point-route.Anchors[i-1].Point;var outgoing=route.Anchors[i+1].Point-route.Anchors[i].Point;
                var body=PipingGeometry.Elbow(route.Anchors[i].Point,incoming,outgoing,policy,bend);
                if(!body.IsSuccess||body.Value is null){diagnostics.AddRange(body.Diagnostics.Select(x=>$"piping-fitting-geometry:{route.Name}:{i}:{x.Message}"));continue;}
                var u=incoming/incoming.Length;var v=outgoing/outgoing.Length;
                fittings.Add(new($"fitting:{route.Name}:{i}",route.Name,i,"Standard.Piping.Elbow90",policy.StableId,
                    [new($"fitting:{route.Name}:{i}:Inlet","Inlet",route.Anchors[i].Point-u*bend,-u),new($"fitting:{route.Name}:{i}:Outlet","Outlet",route.Anchors[i].Point+v*bend,v)],body.Value));
            }
        }
        var nozzles=new List<EquipmentNozzleComponentIr>();
        foreach(var port in parsed.Ports.Where(x=>x.Equipment is not null))
        {
            var policy=parsed.Policies.Single(x=>x.Name==port.PipePolicy);var u=port.OutwardDirection/port.OutwardDirection.Length;
            var nozzleRoot=port.Position-u*port.NozzleLengthMm;var body=PipingGeometry.Pipe(nozzleRoot,port.Position,policy);
            if(!body.IsSuccess||body.Value is null){diagnostics.AddRange(body.Diagnostics.Select(x=>$"piping-nozzle-geometry:{port.Name}:{x.Message}"));continue;}
            nozzles.Add(new($"nozzle:{port.Name}",port.Name,port.Equipment!,port.OwnerKeepOut!,policy.StableId,nozzleRoot,port.Position,
                new($"interface:nozzle:{port.Name}:EquipmentMate","EquipmentMate",nozzleRoot,-u),
                new(port.StableId,"PipeMate",port.Position,u),body.Value));
        }
        var mates=BuildEndpointMates(routes,parsed,segments,nozzles,diagnostics);
        var exemptions=nozzles.Select(x=>new PipingKeepOutExemptionIr($"keepout-exemption:{x.Port}",x.Port,x.StableId,x.OwnerKeepOut,"NozzleEnvelopeOnly")).ToArray();
        fittingClock.Stop();if(diagnostics.Count>0)return new(false,null,null,diagnostics);

        var brepClock=System.Diagnostics.Stopwatch.StartNew();
        var proxies=parsed.KeepOuts.Select(x=>(KeepOut:x,Body:PipingGeometry.Proxy(x))).ToArray();
        foreach(var p in proxies.Where(x=>!x.Body.IsSuccess))diagnostics.AddRange(p.Body.Diagnostics.Select(x=>$"piping-proxy-geometry:{p.KeepOut.Name}:{x.Message}"));
        if(diagnostics.Count>0)return new(false,null,null,diagnostics);
        var definitions=segments.Select(x=>new Step242AssemblyDefinition("def:"+x.StableId,x.StableId,x.Body))
            .Concat(fittings.Select(x=>new Step242AssemblyDefinition("def:"+x.StableId,x.StableId,x.Body)))
            .Concat(nozzles.Select(x=>new Step242AssemblyDefinition("def:"+x.StableId,x.StableId,x.Body)))
            .Concat(proxies.Select(x=>new Step242AssemblyDefinition("def:proxy:"+x.KeepOut.Name,"routing-proxy:"+x.KeepOut.Name,x.Body.Value))).ToArray();
        var identity=new double[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};var root="piping-system:"+parsed.Name;
        var occurrences=new[]{new Step242AssemblyOccurrence(root,parsed.Name,null,null,identity)}.Concat(definitions.Select(x=>new Step242AssemblyOccurrence("occ:"+x.StableId,x.Name,root,x.StableId,identity))).ToArray();
        var step=Step242AssemblyExporter.Export(new(parsed.Name,root,definitions,occurrences));
        if(!step.IsSuccess||step.Value is null){diagnostics.AddRange(step.Diagnostics.Select(x=>"piping-step-export:"+x.Message));return new(false,null,null,diagnostics);}
        var imported=Step242AssemblyImporter.Import(step.Value);if(!imported.IsSuccess){diagnostics.AddRange(imported.Diagnostics.Select(x=>"piping-step-reimport:"+x.Message));return new(false,null,null,diagnostics);}
        brepClock.Stop();
        var bom=BuildBom(segments,fittings,nozzles);var cut=BuildCutList(parsed.Policies,segments);
        var bodies=segments.Select(x=>x.Body).Concat(fittings.Select(x=>x.Body)).Concat(nozzles.Select(x=>x.Body)).Concat(proxies.Select(x=>x.Body.Value)).ToArray();
        var points=bodies.SelectMany(BodyPoints).ToArray();var surfaces=bodies.SelectMany(x=>x.Geometry.Surfaces.Select(s=>s.Value.Kind)).ToArray();
        var maximumRadius=parsed.Policies.Max(x=>x.RadiusMm);var routePoints=routes.SelectMany(x=>x.Anchors.Select(a=>a.Point)).ToArray();
        var bounds=new[]{
            Math.Min(routePoints.Min(x=>x.X)-maximumRadius,parsed.KeepOuts.Select(x=>x.Minimum.X).DefaultIfEmpty(double.PositiveInfinity).Min()),
            Math.Min(routePoints.Min(x=>x.Y)-maximumRadius,parsed.KeepOuts.Select(x=>x.Minimum.Y).DefaultIfEmpty(double.PositiveInfinity).Min()),
            Math.Min(routePoints.Min(x=>x.Z)-maximumRadius,parsed.KeepOuts.Select(x=>x.Minimum.Z).DefaultIfEmpty(double.PositiveInfinity).Min()),
            Math.Max(routePoints.Max(x=>x.X)+maximumRadius,parsed.KeepOuts.Select(x=>x.Maximum.X).DefaultIfEmpty(double.NegativeInfinity).Max()),
            Math.Max(routePoints.Max(x=>x.Y)+maximumRadius,parsed.KeepOuts.Select(x=>x.Maximum.Y).DefaultIfEmpty(double.NegativeInfinity).Max()),
            Math.Max(routePoints.Max(x=>x.Z)+maximumRadius,parsed.KeepOuts.Select(x=>x.Maximum.Z).DefaultIfEmpty(double.NegativeInfinity).Max())};
        var routeClearance=routes.Select(r=>{var p=parsed.Policies.Single(x=>x.Name==parsed.Connections.Single(c=>c.Name==r.Connection).PipePolicy);var bend=r.Turns.FirstOrDefault()?.BendRadiusMm??Math.Max(50,p.OuterDiameterMm*2);return OrthogonalAutoRouter.MinimumMaterializedClearance(r.Anchors.Select(x=>x.Point).ToArray(),parsed.KeepOuts,p.RadiusMm,bend);}).DefaultIfEmpty(double.PositiveInfinity).Min();
        var nozzleClearance=nozzles.Select(n=>{var p=parsed.Policies.Single(x=>x.StableId==n.PipePolicy);var obstacles=parsed.KeepOuts.Where(x=>x.Name!=n.OwnerKeepOut).ToArray();return OrthogonalAutoRouter.MinimumClearance([n.RootPosition,n.TipPosition],obstacles,p.RadiusMm);}).DefaultIfEmpty(double.PositiveInfinity).Min();
        var clearance=Math.Min(routeClearance,nozzleClearance);
        var geometryClear=routes.All(r=>{var p=parsed.Policies.Single(x=>x.Name==parsed.Connections.Single(c=>c.Name==r.Connection).PipePolicy);var bend=r.Turns.FirstOrDefault()?.BendRadiusMm??Math.Max(50,p.OuterDiameterMm*2);var required=parsed.Requests.FirstOrDefault(x=>x.Request.Connection==r.Connection)?.Request.ClearanceMm??parsed.DefaultClearance;return OrthogonalAutoRouter.MinimumMaterializedClearance(r.Anchors.Select(x=>x.Point).ToArray(),parsed.KeepOuts,p.RadiusMm,bend)+Tol>=required;});
        foreach(var nozzle in nozzles)
        {
            var policy=parsed.Policies.Single(x=>x.StableId==nozzle.PipePolicy);var foreignObstacles=parsed.KeepOuts.Where(x=>x.Name!=nozzle.OwnerKeepOut).ToArray();
            var actual=OrthogonalAutoRouter.MinimumClearance([nozzle.RootPosition,nozzle.TipPosition],foreignObstacles,policy.RadiusMm);var required=RequiredClearanceForPort(parsed,nozzle.Port);
            if(actual+Tol<required){geometryClear=false;diagnostics.Add($"piping-nozzle-foreign-keepout-clearance:{nozzle.Port}: verified {actual:G6}mm, required {required:G6}mm");}
        }
        if(!geometryClear){diagnostics.Add("piping-final-geometry-clearance-failed: materialized pipe envelope violates KeepOut clearance");return new(false,null,null,diagnostics);}
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))).ToLowerInvariant();
        var assemblyInterfaces=parsed.Ports.Select(x=>x.StableId).Concat(nozzles.Select(x=>x.EquipmentInterface.StableId)).Concat(segments.SelectMany(x=>new[]{x.StartInterface,x.EndInterface})).Concat(fittings.SelectMany(x=>x.Interfaces.Select(i=>i.StableId))).Distinct().ToArray();
        var assembly=new PipingAssemblyIr(root,parsed.Name,definitions.Select(x=>x.StableId).ToArray(),occurrences.Skip(1).Select(x=>x.StableId).ToArray(),assemblyInterfaces);
        var report=new PipingReport("aetheris:piping:x3a",parsed.Name,assembly,parsed.Policies,parsed.Equipment,parsed.Ports,parsed.Connections,parsed.KeepOuts,proposals,routes,segments,fittings,nozzles,mates,exemptions,bom,cut,
            clearance,geometryClear,bodies.All(IsEnclosed),bodies.Length,surfaces.Count(x=>x==SurfaceGeometryKind.Cylinder),surfaces.Count(x=>x==SurfaceGeometryKind.Torus),surfaces.Count(x=>x==SurfaceGeometryKind.Plane),
            bounds,hash,true,
            "PipingSystem->EquipmentPort/Nozzle->Connection->RouteRequest/ExplicitRoute->RouteProposal->AcceptedRoute->PipeSegments+Fittings->Mates->Assembly->BOM/CutList->AP242",null,null,null,
            new(parseClock.Elapsed.TotalMilliseconds,obstacleClock.Elapsed.TotalMilliseconds,routeClock.Elapsed.TotalMilliseconds,simplifyClock.Elapsed.TotalMilliseconds,fittingClock.Elapsed.TotalMilliseconds,brepClock.Elapsed.TotalMilliseconds));
        return new(true,step.Value,report,[],segments,fittings,nozzles);
    }

    public static string RoutingJson(PipingReport r)=>JsonSerializer.Serialize(new{schema="aetheris:piping-routing:x3a",pipingSystem=r.PipingSystem,equipment=r.Equipment,ports=r.Ports,nozzles=r.Nozzles,mates=r.Mates,keepOutExemptions=r.KeepOutExemptions,routes=r.Routes.Select(x=>new{x.StableId,x.Connection,provenance=x.Provenance.ToString(),anchors=x.Anchors.Select(a=>new{a.Ordinal,kind=a.Kind.ToString(),point=new[]{a.Point.X,a.Point.Y,a.Point.Z},a.Locked}),segments=x.Segments,turns=x.Turns}),r.MinimumVerifiedClearanceMm},JsonOptions);
    public static string CutListJson(PipingReport r)=>JsonSerializer.Serialize(new{schema="aetheris:pipe-cut-list:x3",units=new{length="mm"},pipingSystem=r.PipingSystem,entries=r.CutList},JsonOptions);
    public static string BomJson(PipingReport r)=>JsonSerializer.Serialize(new{schema="aetheris:piping-bom:x3a",pipingSystem=r.PipingSystem,entries=r.Bom},JsonOptions);
    private static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};

    private sealed record Parsed(string Name,IReadOnlyList<PipePolicyIr> Policies,IReadOnlyList<PipingEquipmentIr> Equipment,IReadOnlyList<PipingPortInterfaceIr> Ports,IReadOnlyList<PipingConnectionIr> Connections,IReadOnlyList<KeepOutIr> KeepOuts,IReadOnlyList<RouteIr> Routes,IReadOnlyList<AcceptedRequest> Requests,IReadOnlyList<RerouteSpec> Reroutes,double DefaultClearance);
    private sealed record AcceptedRequest(RouteRequestIr Request,string AcceptAs);
    private sealed record RerouteSpec(string Name,string Route,int FromAnchor,int ToAnchor,IReadOnlyList<string>Avoid,double Clearance);
    private readonly record struct Block(string Name,string Body);

    private static Parsed? Parse(string source,string sourceIdentity,List<string>d)
    {
        var systems=Blocks(source,"PipingSystem");if(systems.Count!=1){d.Add("piping-system-cardinality: exactly one PipingSystem is required");return null;}var system=systems[0];
        var defaultClear=Blocks(source,"RouteSpace").Select(x=>Length(Field(x.Body,"Clearance",false),25,d,"route-space:"+x.Name)).FirstOrDefault();if(defaultClear==0)defaultClear=25;
        var policies=new List<PipePolicyIr>();foreach(var b in Blocks(source,"PipePolicy")){var od=Length(Field(b.Body,"OuterDiameter"),0,d,b.Name);var wall=Length(Field(b.Body,"WallThickness"),0,d,b.Name);var material=Field(b.Body,"Material").Trim('"');if(od<=0||wall<=0)d.Add($"piping-pipe-policy-positive:{b.Name}");if(2*wall>=od)d.Add($"piping-pipe-wall-too-thick:{b.Name}");var resolved=new MaterialResolver().Resolve(material);if(!resolved.IsSuccess)d.Add($"piping-material-unresolved:{b.Name}:{material}");policies.Add(new($"standard:piping:pipe:{Q(od)}:{Q(wall)}:{material}",b.Name,od,wall,material));}Duplicates(policies.Select(x=>x.Name),"pipe-policy",d);
        var keepouts=new List<KeepOutIr>();foreach(var b in Blocks(source,"KeepOut")){var min=Point(Field(b.Body,"Min"),d,"keepout:"+b.Name);var max=Point(Field(b.Body,"Max"),d,"keepout:"+b.Name);if(max.X<=min.X||max.Y<=min.Y||max.Z<=min.Z)d.Add($"piping-keepout-invalid-bounds:{b.Name}");keepouts.Add(new($"concept:keepout:{b.Name}",b.Name,min,max,Field(b.Body,"Concept",false) is var c&&c.Length>0?c:"KeepOut"));}Duplicates(keepouts.Select(x=>x.Name),"keepout",d);
        var equipment=new List<PipingEquipmentIr>();foreach(var b in Blocks(source,"Equipment")){var keepOut=Field(b.Body,"KeepOut");if(!keepouts.Any(x=>x.Name==keepOut))d.Add($"piping-equipment-unknown-keepout:{b.Name}:{keepOut}");equipment.Add(new($"equipment:{b.Name}",b.Name,keepOut));}Duplicates(equipment.Select(x=>x.Name),"equipment",d);
        var ports=new List<PipingPortInterfaceIr>();foreach(var b in Blocks(source,"Port")){var policy=Field(b.Body,"PipePolicy");if(!policies.Any(x=>x.Name==policy))d.Add($"piping-port-unknown-policy:{b.Name}:{policy}");var position=Point(Field(b.Body,"Position"),d,"port:"+b.Name);var direction=Vector(Field(b.Body,"Direction"),d,"port:"+b.Name);if(!Axis(direction))d.Add($"piping-port-direction-nonorthogonal:{b.Name}");if(!Enum.TryParse<PipingConnectionType>(Field(b.Body,"ConnectionType",false) is var ct&&ct.Length>0?ct:"Generic",out var type))d.Add($"piping-port-connection-type-unsupported:{b.Name}");var owner=Field(b.Body,"Equipment",false);var nozzleLength=Length(Field(b.Body,"NozzleLength",false),0,d,"port:"+b.Name);string? ownerKeepOut=null;if(owner.Length>0){var e=equipment.SingleOrDefault(x=>x.Name==owner);if(e is null)d.Add($"piping-port-unknown-equipment:{b.Name}:{owner}");else ownerKeepOut=e.KeepOut;if(nozzleLength<=0)d.Add($"piping-nozzle-length-invalid:{b.Name}");}else if(nozzleLength>0)d.Add($"piping-nozzle-owner-required:{b.Name}");ports.Add(new($"interface:port:{b.Name}",b.Name,position,direction,policy,type,$"datum-frame:port:{b.Name}",owner.Length>0?owner:null,ownerKeepOut,nozzleLength));}Duplicates(ports.Select(x=>x.Name),"port",d);
        var connections=new List<PipingConnectionIr>();foreach(var b in Blocks(source,"Connection")){var from=Field(b.Body,"From");var to=Field(b.Body,"To");var policy=Field(b.Body,"PipePolicy");var fp=ports.SingleOrDefault(x=>x.Name==from);var tp=ports.SingleOrDefault(x=>x.Name==to);if(fp is null||tp is null){d.Add($"piping-connection-unknown-port:{b.Name}");continue;}if(!policies.Any(x=>x.Name==policy)){d.Add($"piping-connection-unknown-policy:{b.Name}:{policy}");continue;}var reducer=fp.PipePolicy!=tp.PipePolicy;var adapter=fp.ConnectionType!=tp.ConnectionType;if(reducer&&!Bool(Field(b.Body,"Reducer",false)))d.Add($"piping-incompatible-port:{b.Name}: different pipe policies require Reducer: true");if(adapter&&!Bool(Field(b.Body,"Adapter",false)))d.Add($"piping-incompatible-port:{b.Name}: connection styles require Adapter: true");connections.Add(new($"connection:{b.Name}",b.Name,from,to,policy,Field(b.Body,"Service",false).Trim('"') is var s&&s.Length>0?s:null,reducer,adapter));}Duplicates(connections.Select(x=>x.Name),"connection",d);
        var routes=new List<RouteIr>();foreach(var b in Blocks(source,"Route")){var connection=connections.SingleOrDefault(x=>x.Name==Field(b.Body,"Connection"));if(connection is null){d.Add($"piping-route-unknown-connection:{b.Name}");continue;}var points=Points(Field(b.Body,"Through"),d,"route:"+b.Name);var from=ports.Single(x=>x.Name==connection.FromPort);var to=ports.Single(x=>x.Name==connection.ToPort);if(points.Count==0||!Near(points[0],from.Position)||!Near(points[^1],to.Position))d.Add($"piping-route-endpoints-mismatch:{b.Name}");var anchors=points.Select((p,i)=>new RouteAnchorIr($"route:{b.Name}:anchor:{i}",i,p,i==0||i==points.Count-1?RouteAnchorKind.Endpoint:RouteAnchorKind.HardWaypoint)).ToArray();var locks=Ints(Field(b.Body,"LockedSegments",false));var provenance=Enum.TryParse<RouteProvenance>(Field(b.Body,"Provenance",false),out var pv)?pv:RouteProvenance.Explicit;var bend=Length(Field(b.Body,"BendRadius",false),Math.Max(50,policies.Single(x=>x.Name==connection.PipePolicy).OuterDiameterMm*2),d,b.Name);routes.Add(OrthogonalAutoRouter.BuildRoute(b.Name,connection.Name,from.StableId,to.StableId,anchors,bend,provenance,sourceIdentity,locks));}
        var provisional=new Parsed(system.Name,policies,equipment,ports,connections,keepouts,routes,[],[],defaultClear);
        var requests=new List<AcceptedRequest>();foreach(var b in Blocks(source,"RouteRequest")){var connection=connections.SingleOrDefault(x=>x.Name==Field(b.Body,"Connection"));if(connection is null){d.Add($"piping-route-request-unknown-connection:{b.Name}");continue;}var clearance=Length(Field(b.Body,"Clearance",false),defaultClear,d,b.Name);var bend=Length(Field(b.Body,"BendRadius",false),Math.Max(50,policies.Single(x=>x.Name==connection.PipePolicy).OuterDiameterMm*2),d,b.Name);var req=RequestFor(connection,provisional,b.Name,clearance) with{HardWaypoints=Points(Field(b.Body,"HardWaypoints",false),d,"request:"+b.Name),BendRadiusMm=bend};requests.Add(new(req,Field(b.Body,"AcceptAs",false)));}
        var reroutes=new List<RerouteSpec>();foreach(var b in Blocks(source,"LocalReroute")){if(!int.TryParse(Field(b.Body,"FromAnchor"),out var from)||!int.TryParse(Field(b.Body,"ToAnchor"),out var to)){d.Add($"piping-local-reroute-anchor-invalid:{b.Name}");continue;}reroutes.Add(new(b.Name,Field(b.Body,"Route"),from,to,Names(Field(b.Body,"Avoid",false)),Length(Field(b.Body,"Clearance",false),defaultClear,d,b.Name)));}
        return provisional with{Requests=requests,Reroutes=reroutes};
    }

    private static RouteRequestIr RequestFor(PipingConnectionIr c,Parsed p,string name,double clearance)=>new($"route-request:{name}",name,c.Name,p.Ports.Single(x=>x.Name==c.FromPort),p.Ports.Single(x=>x.Name==c.ToPort),p.Policies.Single(x=>x.Name==c.PipePolicy),p.KeepOuts,clearance,[]);
    private static void ValidateNozzleContracts(Parsed p,List<string>d)
    {
        foreach(var port in p.Ports.Where(x=>x.Equipment is not null))
        {
            if(port.OwnerKeepOut is null)continue;
            var owner=p.KeepOuts.SingleOrDefault(x=>x.Name==port.OwnerKeepOut);if(owner is null)continue;
            var u=port.OutwardDirection/port.OutwardDirection.Length;var root=port.Position-u*port.NozzleLengthMm;
            if(!OnOutwardFace(root,u,owner))d.Add($"piping-nozzle-root-not-on-owner-face:{port.Name}:{owner.Name}");
            if(InsideOpen(port.Position,owner))d.Add($"piping-nozzle-tip-inside-owner:{port.Name}:{owner.Name}");
        }
    }
    private static IReadOnlyList<PipingMateIr> BuildEndpointMates(IReadOnlyList<RouteIr> routes,Parsed p,IReadOnlyList<PipeSegmentComponentIr> segments,IReadOnlyList<EquipmentNozzleComponentIr> nozzles,List<string>d)
    {
        var result=new List<PipingMateIr>();
        foreach(var route in routes)
        {
            var connection=p.Connections.Single(x=>x.Name==route.Connection);var first=segments.Single(x=>x.Route==route.Name&&x.Ordinal==0);var last=segments.Single(x=>x.Route==route.Name&&x.Ordinal==route.Segments.Count-1);
            Add(p.Ports.Single(x=>x.Name==connection.FromPort),first.StartInterface,route.Anchors[0].Point,-Unit(route.Anchors[1].Point-route.Anchors[0].Point),"From",route.Name);
            Add(p.Ports.Single(x=>x.Name==connection.ToPort),last.EndInterface,route.Anchors[^1].Point,Unit(route.Anchors[^1].Point-route.Anchors[^2].Point),"To",route.Name);
        }
        return result;
        void Add(PipingPortInterfaceIr port,string pipeInterface,Point3D pipePosition,Vector3D pipeOutward,string end,string routeName)
        {
            if(port.Equipment is null)return;var nozzle=nozzles.SingleOrDefault(x=>x.Port==port.Name);
            if(nozzle is null){d.Add($"piping-nozzle-missing:{port.Name}");return;}
            var coincident=Near(nozzle.PipeInterface.Position,pipePosition);var opposed=(nozzle.PipeInterface.OutwardDirection+pipeOutward).Length<=Tol;
            if(!coincident)d.Add($"piping-port-mate-not-coincident:{port.Name}:{end}");if(!opposed)d.Add($"piping-port-mate-direction-mismatch:{port.Name}:{end}");
            result.Add(new($"mate:{port.Name}:{routeName}",port.Name,nozzle.PipeInterface.StableId,pipeInterface,pipePosition,nozzle.PipeInterface.OutwardDirection,pipeOutward,coincident,opposed));
        }
    }
    private static double RequiredClearanceForPort(Parsed p,string port)
    {
        var connections=p.Connections.Where(x=>x.FromPort==port||x.ToPort==port).Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
        return p.Requests.Where(x=>connections.Contains(x.Request.Connection)).Select(x=>x.Request.ClearanceMm).Append(p.DefaultClearance).Max();
    }
    private static void ValidateRoutes(IReadOnlyList<RouteIr> routes,Parsed p,List<string>d)
    {
        Duplicates(routes.Select(x=>x.Name),"route",d);foreach(var r in routes){var c=p.Connections.Single(x=>x.Name==r.Connection);var policy=p.Policies.Single(x=>x.Name==c.PipePolicy);if(r.Anchors.Count<2){d.Add($"piping-route-too-short:{r.Name}");continue;}for(var i=0;i<r.Segments.Count;i++){var a=r.Anchors[i].Point;var b=r.Anchors[i+1].Point;if(!Axis(b-a))d.Add($"piping-route-nonorthogonal:{r.Name}:segment:{i}");}var first=r.Anchors[1].Point-r.Anchors[0].Point;var last=r.Anchors[^1].Point-r.Anchors[^2].Point;var from=p.Ports.Single(x=>x.Name==c.FromPort);var to=p.Ports.Single(x=>x.Name==c.ToPort);if(!Parallel(first,from.OutwardDirection)||Dot(first,from.OutwardDirection)<0)d.Add($"piping-endpoint-direction-impossible:{r.Name}:start");if(!Parallel(last,-to.OutwardDirection)||Dot(last,-to.OutwardDirection)<0)d.Add($"piping-endpoint-direction-impossible:{r.Name}:end");var bend=r.Turns.FirstOrDefault()?.BendRadiusMm??50;foreach(var segment in r.Segments)if(segment.LengthMm<=bend*((segment.Ordinal>0?1:0)+(segment.Ordinal<r.Segments.Count-1?1:0))+Tol)d.Add($"piping-route-bend-spacing-insufficient:{r.Name}:segment:{segment.Ordinal}");var required=p.Requests.FirstOrDefault(x=>x.Request.Connection==r.Connection)?.Request.ClearanceMm??p.DefaultClearance;var actual=OrthogonalAutoRouter.MinimumMaterializedClearance(r.Anchors.Select(x=>x.Point).ToArray(),p.KeepOuts,policy.RadiusMm,bend);if(actual+Tol<required)d.Add(actual<=Tol?$"piping-route-through-keepout:{r.Name}: verified {actual:G6}mm, required {required:G6}mm":$"piping-insufficient-clearance:{r.Name}: verified {actual:G6}mm, required {required:G6}mm");}}
    private static RouteIr Canonicalize(RouteIr r){var points=OrthogonalAutoRouter.Simplify(r.Anchors.Select(x=>x.Point).ToArray());if(points.Count==r.Anchors.Count)return r;var a=points.Select((p,i)=>new RouteAnchorIr($"route:{r.Name}:anchor:{i}",i,p,i==0||i==points.Count-1?RouteAnchorKind.Endpoint:RouteAnchorKind.AutoWaypoint)).ToArray();return OrthogonalAutoRouter.BuildRoute(r.Name,r.Connection,r.StartInterface,r.EndInterface,a,r.Turns.FirstOrDefault()?.BendRadiusMm??50,r.Provenance,r.SourceProvenance,[]);}
    private static IReadOnlyList<PipingBomEntry> BuildBom(IReadOnlyList<PipeSegmentComponentIr>s,IReadOnlyList<PipingFittingComponentIr>f,IReadOnlyList<EquipmentNozzleComponentIr>n)=>s.GroupBy(x=>new{x.PipePolicy,x.Material}).Select(g=>new PipingBomEntry("Standard.Piping.Pipe",g.Key.PipePolicy,g.Key.Material,g.Count(),g.Select(x=>x.StableId).Order().ToArray())).Concat(f.GroupBy(x=>new{x.ProductIdentity,x.PipePolicy}).Select(g=>new PipingBomEntry(g.Key.ProductIdentity,g.Key.PipePolicy,"AsSpecifiedByPipePolicy",g.Count(),g.Select(x=>x.StableId).Order().ToArray()))).Concat(n.GroupBy(x=>x.PipePolicy).Select(g=>new PipingBomEntry("Standard.Piping.NozzleStub",g.Key,"AsSpecifiedByPipePolicy",g.Count(),g.Select(x=>x.StableId).Order().ToArray()))).OrderBy(x=>x.ProductIdentity).ThenBy(x=>x.PipePolicy).ToArray();
    private static IReadOnlyList<PipeCutListEntry> BuildCutList(IReadOnlyList<PipePolicyIr>p,IReadOnlyList<PipeSegmentComponentIr>s)=>s.GroupBy(x=>new{x.PipePolicy,x.Material,Length=Q(x.CutLengthMm)}).Select((g,i)=>{var policy=p.Single(x=>x.StableId==g.Key.PipePolicy);return new PipeCutListEntry($"pipe-cut-group:{i+1}",g.Key.PipePolicy,policy.OuterDiameterMm,policy.WallThicknessMm,g.Key.Material,g.First().CutLengthMm,g.Count(),g.Select(x=>x.StableId).Order().ToArray());}).OrderBy(x=>x.PipePolicy).ThenBy(x=>x.CutLengthMm).ToArray();
    private static IEnumerable<Point3D> BodyPoints(BrepBody b)=>b.Topology.Vertices.Select(v=>b.TryGetVertexPoint(v.Id,out var p)?p:default);
    private static bool IsEnclosed(BrepBody b)=>b.ShellRepresentation is not null&&b.Topology.Edges.All(e=>{var uses=b.Topology.Coedges.Count(c=>c.EdgeId==e.Id);return uses>=2&&uses%2==0;});
    private static IReadOnlyList<Block> Blocks(string source,string keyword){var result=new List<Block>();foreach(Match m in Regex.Matches(source,$@"\b{keyword}\s+(?<n>[A-Za-z_]\w*)\s*\{{",RegexOptions.CultureInvariant)){var open=m.Index+m.Length-1;var close=Matching(source,open);if(close>open)result.Add(new(m.Groups["n"].Value,source[(open+1)..close]));}return result;}
    private static int Matching(string s,int open){var depth=0;for(var i=open;i<s.Length;i++){if(s[i]=='{')depth++;else if(s[i]=='}'&&--depth==0)return i;}return-1;}
    private static string Field(string body,string name,bool required=true){var m=Regex.Match(body,$@"\b{Regex.Escape(name)}\s*:\s*(?<v>[^;]+);",RegexOptions.CultureInvariant);return m.Success?m.Groups["v"].Value.Trim():required?"":"";}
    private static double Length(string value,double fallback,List<string>d,string owner){if(string.IsNullOrWhiteSpace(value))return fallback;var n=value.Replace("mm","",StringComparison.Ordinal).Trim();if(double.TryParse(n,NumberStyles.Float,CultureInfo.InvariantCulture,out var x)&&double.IsFinite(x))return x;d.Add($"piping-length-invalid:{owner}:{value}");return fallback;}
    private static Point3D Point(string value,List<string>d,string owner){var v=Vector(value,d,owner);return new(v.X,v.Y,v.Z);}
    private static Vector3D Vector(string value,List<string>d,string owner){var parts=value.Trim().Trim('[',']').Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);if(parts.Length!=3){d.Add($"piping-vector-invalid:{owner}:{value}");return default;}var values=parts.Select(x=>Length(x,0,d,owner)).ToArray();return new(values[0],values[1],values[2]);}
    private static IReadOnlyList<Point3D> Points(string value,List<string>d,string owner)=>Regex.Matches(value??"",@"\[(?<p>[^\[\]]+)\]").Select(m=>Point(m.Groups["p"].Value,d,owner)).ToArray();
    private static IReadOnlyList<int> Ints(string v)=>v.Trim().Trim('[',']').Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(x=>int.TryParse(x,out var n)?n:-1).Where(x=>x>=0).ToArray();
    private static IReadOnlyList<string> Names(string v)=>v.Trim().Trim('[',']').Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim('"')).ToArray();
    private static bool Bool(string v)=>string.Equals(v,"true",StringComparison.OrdinalIgnoreCase);
    private static bool Axis(Vector3D v)=>new[]{Math.Abs(v.X)>Tol,Math.Abs(v.Y)>Tol,Math.Abs(v.Z)>Tol}.Count(x=>x)==1;
    private static bool Parallel(Vector3D a,Vector3D b)=>a.Cross(b).Length<=Tol;
    private static double Dot(Vector3D a,Vector3D b)=>a.Dot(b);
    private static bool Near(Point3D a,Point3D b)=>(b-a).Length<=Tol;
    private static Vector3D Unit(Vector3D v)=>v/v.Length;
    private static bool InsideOpen(Point3D p,KeepOutIr b)=>p.X>b.Minimum.X+Tol&&p.X<b.Maximum.X-Tol&&p.Y>b.Minimum.Y+Tol&&p.Y<b.Maximum.Y-Tol&&p.Z>b.Minimum.Z+Tol&&p.Z<b.Maximum.Z-Tol;
    private static bool OnOutwardFace(Point3D p,Vector3D u,KeepOutIr b)
    {
        bool Within(double value,double min,double max)=>value>=min-Tol&&value<=max+Tol;
        if(Math.Abs(u.X)>Tol)return Math.Abs(p.X-(u.X>0?b.Maximum.X:b.Minimum.X))<=Tol&&Within(p.Y,b.Minimum.Y,b.Maximum.Y)&&Within(p.Z,b.Minimum.Z,b.Maximum.Z);
        if(Math.Abs(u.Y)>Tol)return Math.Abs(p.Y-(u.Y>0?b.Maximum.Y:b.Minimum.Y))<=Tol&&Within(p.X,b.Minimum.X,b.Maximum.X)&&Within(p.Z,b.Minimum.Z,b.Maximum.Z);
        return Math.Abs(p.Z-(u.Z>0?b.Maximum.Z:b.Minimum.Z))<=Tol&&Within(p.X,b.Minimum.X,b.Maximum.X)&&Within(p.Y,b.Minimum.Y,b.Maximum.Y);
    }
    private static void Duplicates(IEnumerable<string>v,string k,List<string>d){foreach(var g in v.GroupBy(x=>x,StringComparer.Ordinal).Where(x=>x.Count()>1))d.Add($"piping-duplicate-{k}:{g.Key}");}
    private static long Q(double x)=>(long)Math.Round(x*1_000_000,MidpointRounding.AwayFromZero);
}
